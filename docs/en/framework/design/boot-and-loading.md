# Boot and loading

**Read this in other languages:** [English](boot-and-loading.md) | [简体中文](../../../zh-CN/framework/design/boot-and-loading.md)

*Design record* — how Grow takes a game from a cold start to a usable kernel.

Starting a game has two parts that used to be implicit: composing the kernel, and waiting for
whatever must be ready before gameplay. Grow makes both explicit. The same paths serve a game with no
asynchronous work — which still reaches `Ready` before `Initialize()` returns — and a game that must
prepare a scene or a configuration table first.

## States

| State | Meaning |
| --- | --- |
| `None` | Nothing has run; the process default. |
| `Reset` | `GrowApp.Initialize()` cleared the previous session. |
| `Installed` | The loop host, lifecycle manager and module graph are composed. |
| `Booted` | The module graph is initialized; asynchronous loading may still be ahead. |
| `Loading` | The only state that intentionally spans frames. |
| `Ready` | Every module started and every asynchronous load finished. |
| `Failed` | A load failed. Published once, then the composition rolls back. |
| `Shutdown` | The cycle ended; `Reset` begins a new one. |

`Ready` is the only "usable" state. It means both halves of the barrier are done: every
`IModule.OnStart` ran, and every asynchronous load returned. Nothing moves backwards after `Ready`;
`Failed` is terminal for the cycle and only `Reset` restarts it.

## Hooks and the barrier

`IModule` stays synchronous. Its hooks run in dependency order: `OnRegister`, `OnInitialize`,
`OnStart`, then `OnStop` and `OnDispose` in reverse. A module that needs more than one frame
implements the separate `IBootLoadable` interface and gets a `Load` pass between `OnInitialize` and
`OnStart`:

```csharp
public interface IBootLoadable {
    IEnumerator Load(IModuleContext context);
    float LoadProgress { get; }
}
```

The split is deliberate. Adding a member to `IModule` would force every module — including ones with
nothing to load — to answer a question it does not have, and would make an empty method the normal
shape. A separate interface keeps the no-load case free and lets the load phase be opted into.

`Load` follows the same dependency order and runs one module at a time. Serial loading trades a
little wall-clock time for a deterministic order: a module can rely on the services an earlier module
registered, and a failure points at exactly one module. Parallel loading, timeouts and retries are
game policy, not kernel policy.

The enumerator is the contract, not a framework coroutine:

| Yielded value | Meaning |
| --- | --- |
| `null` | Wait one frame. |
| `AsyncOperation` | Wait until `isDone`. |
| anything else | Treated as one frame and reported once in the editor. |

Calling `Load` itself and the first `MoveNext` happen in the same frame, so a module that completes
without yielding never enters `Loading`.

## One tick source

The load pump and the scheduler are both driven by the loop host that already exists. `PreUpdate`
drains `GrowScheduler.Post`; `Update` advances the timers and then one load step. No thread, timer
object or second `Update` loop is introduced, and a kernel with nothing scheduled does one branch per
phase.

A time mechanism is a mechanism, not a capability: it owns time, which is the first question in the
layering ladder. `GrowScheduler` therefore lives in the kernel, exposes a small surface
(`NextFrame`, `DelayFrames`, `Delay`, `Repeat`, `RepeatFrames`, `Cancel`, `Post`), and is the only
place a cross-thread hand-off may enter the main thread. Capabilities such as scene flow poll through
it instead of bringing their own tick.

## Composition root

`GrowBuilder` is the assembly point a game uses:

```csharp
var host = new GrowBuilder()
    .AddModule(new GameModule())
    .UseStore()   // capability package, not the kernel
    .UseScenes()
    .Build();
```

`Build` claims a process-wide slot, registers the modules and turns off `GrowApp.AutoInitialize`
while the host lives, so the automatic `BeforeSceneLoad` start cannot race it. `GrowHost` exposes the
state, readiness, progress and the service registry, and releases the slot when disposed. Only one
host may exist per session; a second build throws instead of silently sharing global state.

The builder knows only kernel types. `UseStore()` and `UseScenes()` are extension methods in their
own capability packages, which is what keeps the kernel free of capability knowledge.

## Failure

A load that throws fails the boot. The module failure is recorded, `Failed` is published once with a
message that survives the rollback, and the composition shuts down: modules stop, subscriptions are
released, the host is destroyed. The game reads `GrowBoot.FailureMessage` and decides what to show.
`Failed` does not retry on its own — retry is a product decision — and a module that wants to degrade
instead of failing catches its own exception.

## Non-goals

- Loading screens and transitions: capabilities and games own their presentation.
- Parallel loading, timeouts, retries and priorities.
- A second asynchronous model: `IEnumerator` is the contract, not `Task` or a third-party library.
- Scene knowledge in the kernel: scenes are a capability.
- Automatic discovery of modules: composition is explicit.
