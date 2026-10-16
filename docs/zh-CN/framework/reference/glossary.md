<!-- source: docs/en/framework/reference/glossary.md @ blob:2684e3d128ffe5edb9fb96c1105349c5f57c730c -->
<!-- 中文译文；以英文 docs/en/framework/reference/glossary.md 为准，译文可能滞后于原文。blob 是英文文件的内容 sha，可用 git hash-object docs/en/framework/reference/glossary.md 核验。 -->

# 术语表

**其他语言：** [English](../../../en/framework/reference/glossary.md) | [简体中文](glossary.md)

Grow 文档里用到的术语定义。全程统一用词。

## 视图（View）

描述架构时用的三种结构之一。

| 视图 | 元素 | 描述的对象 |
| --- | --- | --- |
| Module | 模块（Module） | 静态结构：命名空间、程序集与依赖 |
| Component-and-connector（C&C） | 组件、连接件 | 运行时结构：服务与注册表、事件、驱动循环 |
| Allocation | — | 软件单元到包与工程的映射 |

## 层（Layer）

框架依赖顺序中的一级。每层只依赖它下面的层：

`Core` → `Kernel` → `Capabilities` → `Framework` → `Game`。

**层数不等于包数**；打包属于 Allocation 视图。

## Core

被动原语层，例如日志。它不掌时，也不依赖任何东西。

## Kernel

机制层：启动、生命周期、逐帧更新、模块注册与调度。它不拥有任何游戏功能，也不包装任何库。

## 机制（Mechanism）

框架为了让游戏跑起来而做的事。机制归内核，且不设抽象。

## 能力（Capability）

游戏功能，例如资源、存档、UI、音频。能力自成一个包，不被内核拥有。

## 契约（Contract）

能力对外暴露的公开 API。它**不含第三方类型**，因此背后的实现可以被整体替换。

## 实现（Implementation）

契约背后的代码，通常包装某个第三方库。

## 模块（Module）

带编译期边界的代码单元：一个命名空间 + 一个程序集定义。

## 组件（Component）

运行时单元，例如一个模块实例或一个服务。

## 程序集（Assembly）

编译单元，在本工程里由程序集定义（`asmdef`）描述。

## 包（Package）

可分发单元，由 `package.json` 描述，经 Unity Package Manager 安装。

## 元包（Meta package）

把一组推荐能力与默认组合根组装起来的包。
