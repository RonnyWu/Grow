<!-- source: README.md @ blob:9f6aefddfb035c0e4c46b216b8cd0aa63bbd5aa2 -->
<!-- 中文译文；以英文 README.md 为准，译文可能滞后于原文。blob 是 README.md 的内容 sha，可用 git hash-object README.md 核验。 -->

# Grow

**其他语言：** [English](../README.md) | [简体中文](README.zh-CN.md)

一个内核很薄的模块化 Unity 游戏框架。

- 状态：**早期规划**——API 与结构尚不稳定。
- 包：[`Packages/com.ronny.grow`](../Packages/com.ronny.grow)
- Unity：**2022.3 LTS**

## 架构

Grow 采用**薄内核 + 能力层**的结构：内核只负责机制，游戏功能一律做成可替换的能力，挂在内核之外。

- **内核**：只负责机制——让游戏启动、逐帧跑起来的那套底层支撑。它不碰、也不包装任何游戏功能。
- **能力**：资源、存档、UI、音频这类游戏功能。每个能力单独发包，对外只暴露一个稳定的**契约**；契约里不出现任何第三方类型，所以实现可以整体替换，调用方的代码不受影响。

分层如下，每层只依赖它下面的层：

| 层 | 内容 | 状态 |
|---|---|---|
| Game | 你自己的游戏 / 应用代码 | — |
| Framework | 元包：把一组推荐能力装配到一起 | 规划中 |
| Capabilities | 契约 + 实现（实现封装第三方库） | 规划中 |
| Kernel | 机制——入口、模块、服务注册表、生命周期、循环 | 进行中 |
| Core | 被动原语（如日志），不依赖任何第三方 | 进行中 |

## 贡献

项目还在早期规划阶段；动手写 PR 之前，请先[开一个 issue](https://github.com/RonnyWu/Grow/issues) 沟通。

- 第一方代码一律放在 UPM 包 `Packages/com.ronny.grow` 内；`Assets/` 按设计保持为空，请不要在其中放代码。

## 许可证

MIT——见 [LICENSE](../LICENSE)。
