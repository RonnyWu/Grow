# Vertical Slice

A minimal game on top of `com.ronny.grow.framework`: the kernel boots, one module loads
"configuration" over two frames, a session module keeps coins in the store, and the store restores
them after a Play Mode restart.

## Run it

1. Import this sample (Package Manager → Grow Framework → Samples → Import).
2. Press Play.

> The sample starts its own composition from `SampleEntry`. If your project already starts Grow,
> remove or adapt that class before importing; only one `GrowHost` may exist per session.

What happens:

- `SampleEntry` composes `GrowFramework.Create()` with the two sample modules and starts it.
- `GameModule` implements `IBootLoadable`; the kernel stays in `Loading` for two frames and reaches
  `Ready` only after the load and the start pass.
- `SessionModule` reads `coins` from its store when it initializes and saves on stop.

Try it: add a coin from anywhere (`SampleEntry.Host.GetRequiredService<SessionModule>().AddCoins(1)`),
stop and start Play Mode again — the count comes back from the store.

## Scenes (optional)

The sample ships without scene assets, because a scene file is a project asset, not a package asset.
To drive scene flow, create `Boot`, `Menu` and `Gameplay` scenes, add them to **File > Build
Settings**, and load them through the capability:

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

## Where the tests live

The executable version of this slice is a Play Mode test in the package
(`Tests/PlayMode/VerticalSlice`). Unity does not import `Samples~`, so the tested copy lives in the
test assembly and uses the same public API as this sample.
