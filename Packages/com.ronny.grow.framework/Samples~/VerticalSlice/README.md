# Vertical Slice

A minimal game on top of `com.ronny.grow.framework`: the kernel boots, one module loads
"configuration" over two frames, a session module keeps coins in the store, and the store restores
them after a Play Mode restart.

## Run it

1. Import this sample (Package Manager → Grow Framework → Samples → Import).
2. Add `Assets/Samples/Grow Framework/<version>/Vertical Slice/Scenes/Boot.unity`,
   `Menu.unity` and `Gameplay.unity` to **File > Build Settings**, with `Boot` first.
3. Press Play from `Boot`.

> The sample starts its own composition from `SampleEntry`. If your project already starts Grow,
> remove or adapt that class before importing; only one `GrowHost` may exist per session.

What happens:

- `SampleEntry` composes `GrowFramework.Create()` with the two sample modules and starts it.
- `GameModule` implements `IBootLoadable`; the kernel stays in `Loading` for two frames and reaches
  `Ready` only after the load and the start pass.
- `SessionModule` reads `coins` from its store when it initializes and saves on stop.
- `BootController` shows the boot state and progress, then loads `Menu` through `ISceneFlow`.
- `MenuController` shows the coins restored from the store and loads `Gameplay`.
- `GameplayController` adds coins, saves them on demand and goes back to the menu.

The script the sample is built around:

1. Cold start: `Boot` shows `Loading`, then `Ready`, then the menu appears.
2. Start the game, add coins, press **Save**, go back to the menu.
3. Stop Play Mode and start it again: the coins come back from the store.
4. Add a coin and stop **without** saving: the module's `OnStop` saves it anyway.

The scenes are part of the sample so the acceptance path runs without writing any scene setup. They
contain only a camera and one controller each; there is no prefab, no uGUI canvas and no text asset,
because the sample draws its buttons with `OnGUI`. A real game replaces the controllers and the
scenes — the modules under `Scripts/` are what carries the framework usage.

## Scenes in your own game

`ISceneFlow` is a capability, not a requirement: a game may load scenes itself. To drive the flow
from a game, resolve it from the host and check the request:

```csharp
var scenes = SampleEntry.Host.GetRequiredService<ISceneFlow>();
var request = scenes.Load("Menu");

if (request.IsFaulted) {
    Debug.LogError(request.Error);
}
```

`SceneRequest` reports progress and completion. The flow allows one foreground operation at a time,
reports failures on the request instead of throwing, and never fakes a cancellation.

## Files

| File | Purpose |
| --- | --- |
| `Scripts/SampleEntry.cs` | Composition and startup; the one place that chooses capabilities. |
| `Scripts/GameModule.cs` | Two-frame asynchronous load (`IBootLoadable`). |
| `Scripts/SessionModule.cs` | Coins persisted through the store capability. |
| `Scripts/BootController.cs` | Watches the boot state and hands over to the menu scene. |
| `Scripts/MenuController.cs` | Shows the restored coins and starts the game scene. |
| `Scripts/GameplayController.cs` | Adds coins, saves, returns to the menu. |
| `Scenes/Boot.unity`, `Menu.unity`, `Gameplay.unity` | One camera plus one controller each. |

## Where the tests live

The executable version of this slice is a Play Mode test in the package
(`Packages/com.ronny.grow.framework/Tests/PlayMode/VerticalSlice`). It drives the same modules
through the same public API; the tests keep their own entry point because a test assembly cannot
rely on the sample being imported.
