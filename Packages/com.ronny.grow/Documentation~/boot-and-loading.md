# Boot and loading

This page shows how a game starts Grow and how a module can take more than one frame to get ready.

## Quick start

```csharp
using Grow.Kernel.Entry;
using Grow.Kernel.Modules;

[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
private static void Main() {
    new GrowBuilder()
        .AddModule(new GameModule())
        .Build();                       // builds and starts; dispose the host to stop
}
```

Build the host in `AfterAssembliesLoaded`, not `SubsystemRegistration`: the kernel resets its own
static state from a `SubsystemRegistration` hook, Unity does not order that hook against yours, and a
host built too early would be released by the reset.

`Build` registers the modules, turns off `GrowApp.AutoInitialize` while the host lives and starts
the kernel. With `AutoStart = false` you start it yourself:

```csharp
var host = new GrowBuilder()
    .ConfigureBoot(options => options.AutoStart = false)
    .AddModule(new GameModule())
    .Build();

host.Start();
```

Resolve services through the host:

```csharp
var session = host.GetRequiredService<ISession>();
```

## States

`GrowBoot.State` moves forward only:

```text
None → Reset → Installed → Booted → Loading → Ready
                                      ↘ Failed → Shutdown
```

- `Booted` means the module graph is initialized; loading may still be ahead.
- `Loading` is the only state that can stay for several frames.
- `Ready` means every module started **and** every asynchronous load finished.
- `Failed` is published once, then the composition rolls back; read
  `GrowBoot.FailureMessage` and restart with a new initialization.

`GrowApp.IsInitialized` is true only in `Ready`, so it is safe to gate gameplay on it.

## Loading a module over several frames

Implement `IBootLoadable` next to `IModule`:

```csharp
public sealed class ConfigModule : IModule, IBootLoadable {
    public string Name => "Config";
    public int Priority => 0;
    public IReadOnlyList<Type> Dependencies => Array.Empty<Type>();

    public float LoadProgress { get; private set; }

    public void OnRegister(IModuleContext context) { }
    public void OnInitialize(IModuleContext context) { }
    public void OnStart(IModuleContext context) { }   // runs after the load
    public void OnStop() { }
    public void OnDispose() { }

    public IEnumerator Load(IModuleContext context) {
        for (var i = 0; i < 4; i++) {
            LoadProgress = (i + 1) / 4f;
            yield return null;                        // wait one frame
        }
    }
}
```

Rules:

- Modules load one at a time, in dependency order. A later module can resolve services an earlier
  one registered.
- `yield return null` waits one frame; `yield return asyncOperation` waits until `isDone`; any other
  value is treated as one frame and reported once in the editor.
- `null` from `Load` means "nothing to wait for".
- A load that throws fails the boot. Catch inside the module if it may degrade instead.

Progress is available while loading:

```csharp
GrowBoot.Progress;        // 0..1, monotonic
GrowBoot.LoadingModule;   // module name, or null between loads
```

## Timing

`GrowScheduler` is the kernel's only time mechanism and is already driven by the loop:

```csharp
GrowScheduler.NextFrame(() => Debug.Log("next frame"));
GrowScheduler.Delay(0.5f, () => Debug.Log("half a second later"));
var handle = GrowScheduler.Repeat(1f, Tick);
GrowScheduler.Cancel(handle);
GrowScheduler.Post(() => { });     // thread-safe; drained before the next update
```

Timers are not a replacement for coroutines: use a coroutine for a multi-step script and the
scheduler for "do this at that point in time".

## Related

- Capabilities: `com.ronny.grow.store`, `com.ronny.grow.scenes`.
- Contributor design record: `docs/en/framework/design/boot-and-loading.md`.
