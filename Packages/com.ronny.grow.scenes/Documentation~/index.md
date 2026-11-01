# Grow Scenes

Grow Scenes is the scene flow capability of the Grow framework. It loads, activates and unloads
scenes through one controlled path, and reports progress on a request instead of throwing.

**Status:** pre-release. The API may still change before a stable release.

## Install

Add `com.ronny.grow.scenes` through the Unity Package Manager. The package depends on
`com.ronny.grow`; install both. The implementation assembly is not auto-referenced, so reference
`Grow.Scenes` from the assemblies that compose or use it.

## Why no contracts package

The capability ships as one package on purpose. There is exactly one scene backend — Unity's
`SceneManager` — so the "two real, interchangeable backends" rule does not hold and no abstraction
is introduced. A second backend would be the signal to split a contract package out.

## Quick start

```csharp
var host = new GrowBuilder()
    .UseScenes("Boot")
    .Build();

var scenes = host.GetRequiredService<ISceneFlow>();
```

`SceneModule` registers `ISceneFlow`. `Load` / `LoadAdditive` start an operation and return a
`SceneRequest`; `SetActive` activates a finished additive scene; `Unload` releases one.

## Rules

- **One foreground operation.** A second `Load` while `IsBusy` throws. Queuing and priority are not
  provided.
- **Failures are data.** A scene that cannot load produces a faulted request with an `Error`, not an
  exception, so the game can show it.
- **No fake cancellation.** Unity cannot abort a scene operation, so there is no `Cancel`. Calling
  `Unload` on a request that is still loading activates the scene and unloads it when the load
  finishes.
- **No transitions or UI.** Fades, loading screens and addressable lookup belong to the game or to
  another capability.

## Related

- `GrowScheduler` (kernel) drives the poll; the capability owns no frame source of its own.
