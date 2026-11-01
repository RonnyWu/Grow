<!-- source: docs/en/framework/design/boot-and-loading.md @ blob:bd88a1c9654b266fbdf8acc8bd42f8b5db213c80 -->
<!-- 中文译文；以英文 docs/en/framework/design/boot-and-loading.md 为准，译文可能滞后于原文。blob 是英文文件的内容 sha，可用 git hash-object docs/en/framework/design/boot-and-loading.md 核验。 -->

# 启动与加载

**其他语言：** [English](../../../en/framework/design/boot-and-loading.md) | [简体中文](boot-and-loading.md)

*设计记录*——Grow 如何把一个游戏从冷启动带到「内核可用」。

启动游戏有两件过去隐式完成的事：一是装配内核，二是等待玩法开始前必须就绪的内容。Grow
把这两件事都显式化。同一套路径既服务「没有异步工作」的游戏（此时 `Initialize()` 返回前就已到达
`Ready`），也服务「必须先准备好一个场景或一张配置表」的游戏。

## 状态

| 状态 | 含义 |
| --- | --- |
| `None` | 什么都没跑；进程默认值。 |
| `Reset` | `GrowApp.Initialize()` 清掉了上一次会话。 |
| `Installed` | 循环宿主、生命周期管理器和模块图已装配。 |
| `Booted` | 模块图已初始化；异步加载可能还没结束。 |
| `Loading` | 唯一允许跨帧停留的状态。 |
| `Ready` | 所有模块已启动，且所有异步加载已结束。 |
| `Failed` | 某个加载失败。只发布一次，随后组合根回滚。 |
| `Shutdown` | 本周期结束；`Reset` 开始新周期。 |

`Ready` 是唯一「可用」的状态，含义是屏障的两半都完成：每个 `IModule.OnStart` 都跑过，且每个
异步加载都返回了。`Ready` 之后不回退；`Failed` 是本周期的终态，只有 `Reset` 能重新开始。

## 钩子与屏障

`IModule` 保持同步。它的钩子按依赖顺序执行：`OnRegister`、`OnInitialize`、`OnStart`，之后按相反顺序
`OnStop`、`OnDispose`。需要超过一帧的模块实现独立的 `IBootLoadable` 接口，从而在 `OnInitialize` 与
`OnStart` 之间得到一个 `Load` 阶段：

```csharp
public interface IBootLoadable {
    IEnumerator Load(IModuleContext context);
    float LoadProgress { get; }
}
```

拆分是刻意的。给 `IModule` 加成员会逼每个模块（包括根本没东西可加载的）回答一个它并不存在的问题，
并使「空实现」成为常态。独立接口让「无需加载」保持零成本，也让加载阶段成为显式选择。

`Load` 遵循同样的依赖顺序，逐个模块串行执行。串行用一点实际耗时换确定性：模块可以依赖前一个模块注册的
服务，失败时也能精确指向某一个模块。并行加载、超时和重试属于游戏侧策略，不属于内核策略。

枚举器就是契约本身，不是框架协程：

| yield 的值 | 含义 |
| --- | --- |
| `null` | 等一帧。 |
| `AsyncOperation` | 等它 `isDone`。 |
| 其它值 | 当作等一帧，并在编辑器里报告一次。 |

调用 `Load` 与第一次 `MoveNext` 发生在同一帧，因此不需要等待就完成的模块根本不会进入 `Loading`。

## 唯一的 tick 源

加载泵和调度器都由已存在的循环宿主驱动。`PreUpdate` 排空 `GrowScheduler.Post`；`Update` 先推进定时器，
再推进一次加载。这里不新开线程、不建定时器对象、也不引入第二个 `Update` 循环；没有待办事项时，每个阶段
只多一次分支判断。

时间机制属于**机制**而不是能力：它「掌时」，正好是分层判据的第一问。因此 `GrowScheduler` 落在内核，
只暴露一小片接口（`NextFrame`、`DelayFrames`、`Delay`、`Repeat`、`RepeatFrames`、`Cancel`、`Post`），
也是跨线程交接进入主线程的唯一入口。场景流程这类能力通过它轮询，而不是自带一套 tick。

## 组合根

`GrowBuilder` 是游戏使用的装配点：

```csharp
var host = new GrowBuilder()
    .AddModule(new GameModule())
    .UseStore()   // 能力包提供，不在内核
    .UseScenes()
    .Build();
```

`Build` 占用进程级唯一槽位、注册模块，并在宿主存活期间关掉 `GrowApp.AutoInitialize`，避免自动的
`BeforeSceneLoad` 启动与它抢时序。`GrowHost` 暴露状态、就绪、进度和服务注册表，`Dispose` 时释放槽位。
一个会话只允许一个宿主；第二次 `Build` 直接抛，而不是悄悄共享全局状态。

宿主在 `AfterAssembliesLoaded` 阶段构建。内核用自己的 `SubsystemRegistration` 钩子复位静态状态，而
Unity 不保证这个钩子与游戏钩子的顺序；在 `SubsystemRegistration` 里构建，刚建好的宿主会被复位释放。
`AfterAssembliesLoaded` 晚于所有 `SubsystemRegistration` 钩子、早于 `BeforeSceneLoad`。

Builder 只认识内核类型。`UseStore()` 与 `UseScenes()` 是各自能力包里的扩展方法——这正是内核不需要知道
任何能力的原因。

## 失败

加载抛异常即启动失败。模块失败被记录，`Failed` 带着一条在回滚后仍然保留的消息发布一次，随后组合根关闭：
模块停止、订阅释放、宿主销毁。游戏读 `GrowBoot.FailureMessage` 决定展示什么。`Failed` 不会自行重试——
重试是产品决策；希望降级而不是失败的模块自己捕获异常。

## 非目标

- 加载界面与转场：由能力或游戏自己负责呈现。
- 并行加载、超时、重试与优先级。
- 第二套异步模型：契约是 `IEnumerator`，不是 `Task` 或第三方库。
- 内核认识场景：场景是能力。
- 模块自动发现：组合是显式的。
