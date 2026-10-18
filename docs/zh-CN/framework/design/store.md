<!-- source: docs/en/framework/design/store.md @ blob:d5799e0563cfa02f071e7c3255491f512847cb42 -->
<!-- 中文译文；以英文 docs/en/framework/design/store.md 为准，译文可能滞后于原文。blob 是英文文件的内容 sha，可用 git hash-object docs/en/framework/design/store.md 核验。 -->

# Store

**其他语言：** [English](../../../en/framework/design/store.md) | [简体中文](store.md)

*设计记录*——基于文件的键值存储能力。

Store 为 Grow 提供一个小型、基于文件的键值存储，用来保存基础类型和 `byte[]`。它不定义 schema、记录模型或序列化格式；这些决定留在调用方自己的代码中，需要保存编码数据时使用 `byte[]`。

## 目标

- 不依赖数据库或第三方后端，存储基础类型和 `byte[]`。
- 每个 store 都能以普通文件读取，常见情况下只有一个可见 bucket。
- 每个 `StoreType` 共享一个目录，而不是每个 store 一个目录。
- 大型 store 在启动时拆分为 bucket，而不是在游戏运行中拆分。
- `Save` 显式提交并 flush；从不运行自动保存定时器。
- 只有物理磁盘和 IO 失败才是硬失败。
- 从不使用 `EditorPrefs`、`PlayerPrefs`、`Microsoft.Win32` 或注册表。

## 非目标

- 基础类型之外的 schema 或记录类型。
- 序列化、查询、索引或文档模型。
- WAL、replay、delta 文件或 compaction。
- 多进程写入、云同步、加密或压缩。
- 内容寻址的 blob 去重。

## 作用域

store 类型决定作用域目录。同一类型的所有 store 共享该目录；只有不同类型才使用不同目录。

| `StoreType` | 编辑器 | 玩家 |
| --- | --- | --- |
| `Editor` | `<project>/UserSettings/Grow/Editor` | 不支持 |
| `Runtime` | `<project>/UserSettings/Grow/Runtime` | `<persistentDataPath>/Grow/Runtime` |
| `User` | `<project>/UserSettings/Grow/User` | `<persistentDataPath>/Grow/User` |

编辑器 `Runtime` 与玩家 `Runtime` 默认分开。`UsePlayerPathInEditor` 可以选择在编辑器中使用玩家路径。`Editor` 在玩家平台不受支持。

## 目录布局

```text
<scope>/
├── manifests
├── manifests.bak
├── settings.0.1.kv
├── settings.0.2.kv
├── settings.1.1.kv
├── settings.blobs/
│   └── <blobId>.bin
├── audio.0.1.kv
├── audio.blobs/
└── quarantine/
```

名为 `settings` 的 store 拥有 `settings.<bucket>.<version>.kv`，并懒创建 `settings.blobs/`。不同 store 共享目录，但不共享文件。一个 `StoreType` 恰好拥有一个目录。

## 名称

store 名先折叠为小写，然后校验：

- 1–64 个字符；
- 只允许 `a-z`、`0-9`、`.`、`_`、`-`；
- 必须以 `a-z` 或 `0-9` 开头和结尾；
- 禁止 `..`、路径分隔符、控制字符、空白、首尾 `.`；
- 禁止 Windows 设备名（`con`、`prn`、`aux`、`nul`、`com1`–`com9`、`lpt1`–`lpt9`）；
- 禁止保留名 `manifests`、`manifests.bak`、`manifests.tmp`、`quarantine`；
- 禁止以 `.blobs` 结尾。

非法名以 `ArgumentException` 拒绝，从不静默改写。

## 文件格式

### Manifests

每个作用域有一个 `manifests` 文件，记录当前 bucket 映射：

```text
// grow-store-manifests 1 hash=fnv1a32-mix1 generation=42 bodyCrc32=9f3a1c2d
store settings bucketCount=2
  bucket 0 version=2 bytes=18342
  bucket 1 version=1 bytes=9210
store audio bucketCount=1
  bucket 0 version=2 bytes=4210
```

`bucketCount` 是 2 的幂。`bytes` 是逻辑 bucket 大小，也是启动拆分的提示。`bodyCrc32` 覆盖 header 之后的所有行。`manifests.bak` 保存上一份完整索引。

### Bucket 文件

```text
// grow-store-bucket 1 hash=fnv1a32-mix1 store=settings bucket=0 version=2 bucketCount=2 bytes=18342 dataCrc32=9f3a1c2d
a<TAB>string<TAB>hello
b<TAB>int<TAB>2
avatar<TAB>blob<TAB>7f3a1c2d9e8b4a6c8d0e1f2a3b4c5d6e:1048576:9f3a1c2d
```

- header 必须与文件名一致。
- 正文按原始 key 的 Ordinal 顺序排序。
- 行格式为 `escapedKey<TAB>kind<TAB>escapedValue`。
- `dataCrc32` 覆盖所有正文字节；行尾为 LF，文件为无 BOM 的 UTF-8。
- kind 为 `int`、`long`、`float`、`double`、`bool`、`string`、`blob`。
- `bool` 为 `0` 或 `1`；数字使用 invariant culture；字符串转义 `\`、TAB、LF、CR。
- `blob` 存储 `<blobId>:<length>:<crc32>`；字节存放在 bucket 之外。

`version` 是每个 bucket 的提交号，不是语义版本。它从 1 开始，不复用，使用 `long`。

## 保存与提交

`Save` 提交当前缓存并 flush。干净的 store 不产生 IO。

1. 按 bucket 分组变化的 key。
2. 对每个受影响的 bucket，写 `<store>.<bucket>.<next>.kv.tmp`，flush、校验，然后重命名为最终版本名。
3. 用新的 bucket version 更新 `manifests` 中的记录。
4. 安全替换 `manifests`。
5. 删除不再被引用的 bucket 版本。

多 bucket 保存是原子的，因为 manifest 是切换点。切换前崩溃时旧 manifest 仍可读，最多留下孤儿 bucket 版本；切换后崩溃会暴露新版本。manifest 写入失败会保留旧索引。

`TrySaveAll` 先写入所有健康且 dirty 的 store，然后只替换一次 manifest。损坏的 store 被跳过并报告失败。bucket 写入失败会阻止 manifest 提交；已写入的版本成为孤儿，所有健康 store 保持 dirty。

### 安全的 manifest 替换

主路径写入并校验 `manifests.tmp`，然后调用 `File.Replace(manifests.tmp, manifests, manifests.bak)`。备份由 replace 自身生成。`File.Replace` 不可用时，fallback 先让经过校验的 `.bak` 持久化，再删除并移动临时 manifest；`.bak` 覆盖崩溃窗口。两条路径都要在每个支持的平台上测试。

## 恢复

打开时：

1. 读取并校验 `manifests`。
2. 回退到 `manifests.bak`。
3. 两者都无效时，扫描 bucket header 并重建 manifest。
4. 执行启动 bucket 拆分和垃圾回收。
5. 按访问懒加载 bucket。

header 或 CRC 错误的 bucket 会被移动到 `quarantine/`，store 变为 `Corrupted`。读取只返回可校验的数据；写入抛 `InvalidOperationException`；`TrySave` 返回 `false`。`Reset` 清空并重建 store。已提交的 blob 指针如果文件缺失或长度/CRC 校验失败，就是损坏，不是 miss。

## Bucket 拆分

bucket hash 是固定算法：`FNV-1a 32` 后接一个 32 位混合步骤。id 为 `fnv1a32-mix1`，记录在 manifest 和 bucket header 中。从不使用 `string.GetHashCode()`，因为它在不同进程或运行时之间不稳定。

bucket 为 `hash & (bucketCount - 1)`。启动拆分为：

- 检查 manifest 中的每个 store；
- 当任意 bucket 超过 `TargetBucketBytes` 时，翻倍 `bucketCount` 并重写该 store；
- 为空 bucket 写 header-only 文件；
- 只有所有新 bucket 校验通过后才切换 manifest；
- 达到 `BucketCountLimit`（默认 4096）时停止；如果单个 key 仍超过目标，记录一次日志。

运行期保存从不拆分。长会话可能让 bucket 超过目标；下次启动会收敛，或在安全点显式调用 `IStoreDiagnostics.TrySplit`。

## Blob

`byte[]` 值写入 `<store>.blobs/<blobId>.bin`；bucket 只存指针。blob 文件在指针提交前写入。blob 目录懒创建，GC 后为空时移除。blob id 是 GUID 生成的 32 个小写十六进制字符。

Blob GC 只扫描拥有 `.blobs/` 目录的 store。它从当前 manifest 和 `manifests.bak` 收集指针，然后删除未引用的 `.bin` 和 `.tmp` 文件。不做内容去重：两个 key 即使字节相同也使用两个文件。

## 容量与失败

| 设置 | 默认 | 行为 |
| --- | --- | --- |
| `TargetBucketBytes` | 2 MiB | 启动拆分目标；从不是硬限制 |
| `BlobWarningBytes` | 64 MiB | 软日志阈值；从不是硬限制 |
| `BucketCountLimit` | 4096 | 启动拆分上限 |

没有单值、key 数、store 或作用域限制。逻辑大小阈值只记录日志。物理磁盘和 IO 失败通过 `IOException` 或 `TrySave` 报告。没有 `StoreCapacityException`，也没有 `SplitRecommended` 属性。

## 契约

公开契约刻意保持很小：

```csharp
public interface IStore
{
    T Get<T>(string key, T defaultValue = default);
    bool TryGet<T>(string key, out T value);
    IStore Set<T>(string key, T value);
    bool ContainsKey(string key);
    IStore Delete(string key);
    IStore Clear();
    IStore Save();
    bool TrySave(out string error);
    StoreSize Size { get; }
    bool IsDirty { get; }
    bool IsCorrupted { get; }
    bool Reset();
}

public interface IStoreProvider : IDisposable
{
    IStore GetStore(string storeName, StoreType storeType);
    bool TryGetStore(string storeName, StoreType storeType, out IStore store);
    bool Close(string storeName, StoreType storeType);
    bool TrySaveAll(out string error);
    bool DeleteStore(string storeName, StoreType storeType);
    bool IsSupported(StoreType storeType);
}
```

`IStore` 不实现 `IDisposable`；store 的生命周期由 provider 独占。一个 provider 拥有一个作用域。第二个 provider 打开同一作用域时会在启动阶段失败，而不是覆盖共享 manifest。

`Close` 保存健康且 dirty 的 store，并在成功时释放。保存失败时 store 保持打开且 dirty。损坏的 store 不写入，直接释放。`DeleteStore` 先删除 manifest 记录，再删除 store 文件；两步之间崩溃会留下孤儿文件，由 GC 清理。`Reset` 重建空 store，并尽力删除其 blob 目录。

`IStoreDiagnostics` 由 `StoreModule.Diagnostics` 暴露，提供安全点的 `TrySplit` 和 `TryGc`。它不属于日常契约。

## 生命周期

`StoreProvider` 订阅 pause、focus loss、quit 和 `StoreModule.OnStop`。生命周期保存使用预算：pending 字节、store 数量和 fsync 次数。pending blob 超过配置阈值的 store 会被跳过并保持 dirty。`Dispose` 从不隐式保存。

默认的 `StoreModule` store 名为 `settings`。`StoreOptions` 位于实现包，配置 `TargetBucketBytes`、`BlobWarningBytes`、`BucketCountLimit`、`UsePlayerPathInEditor` 和生命周期预算。

## 取舍

- 单 bucket store 在每次保存时仍会写 manifest。为了一条代码路径、一种文件格式和一套恢复模型，接受这次小写入。
- manifest 是作用域全局的，因此其大小随 store 数量增长。等到真实工作负载需要时再考虑 per-store manifest。
- 启动拆分和 blob GC 可能阻塞加载。运行期拆分不会自动进行。
- store 是单进程、主线程的。此能力不计划后台写入、加密、压缩或云同步。

## 另见

- [架构](../architecture/README.md)——能力在 Grow 中的位置。
- [术语表](../reference/glossary.md)——这些页面共享的术语。
