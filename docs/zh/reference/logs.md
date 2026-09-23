# 日志速查

[文档总览](../README.md) > [参考](README.md) > 日志速查

---

**读完这一页**，你知道该先开哪个文件、一行 CUO 日志长什么样、改哪个设置能把细节调出来，以及值得搜哪些字符串。产出部署、跑测试的命令在[构建、测试与部署插件](../contributing/build-and-test.md)；CUO 在每一步记了什么，见[世界怎么存档](../internals/save-archive.md)与[模组的一生](../internals/mod-loading-lifecycle.md)。

## 三条通道

| 文件 | 谁在写 | 它回答什么 |
|---|---|---|
| `BepInEx/LogOutput.log` | BepInEx 自己：链加载器、插件加载，以及每一条经过 BepInEx 日志接收器的 CUO 日志 | 插件到底加载了没有，以及在 CUO 日志存在之前发生了什么 |
| `BepInEx/logs/latest.log` | CUO 自己的滚动文件日志 —— `[Logging]` 设置管的就是它 | 这一局做了什么：启动、会话与世界事件、模组发现与握手、警告与错误 |
| `BepInEx/CUO.log` | 今天没有任何东西在写 | 滚动改造之前的那种单文件日志。如果它存在，下一次启动时会被压缩进 `BepInEx/logs/` 并删除；它是历史，不是现役通道 |

- `latest.log` 在会话期间持续追加（`FileShare.Read`，所以外部 tail 工具能跟），下一次启动时，上一份非空的文件被压缩成 `BepInEx/logs/<yyyy-MM-dd>-<n>.log.gz` —— 归档名取当天第一个没被占用的编号。
- 压缩失败时 `latest.log` 会原地留下，本次会话继续往里追加，所以崩溃现场不会因为轮转失败而丢。
- 记日志永不抛异常：目录被占用或不可写时，只是关掉文件接收器，不会弄坏游戏。

## 一行日志长什么样

`BepInEx/logs/latest.log` 每条记录一行：

```text
[2026-09-23 14:05:31.482] [INF] [CasualtiesUnknownOnline.Plugin] Plugin CasualtiesUnknownOnline is loaded!
```

- 级别码是 `TRC`、`DBG`、`INF`、`WRN`、`ERR` 与 `CRT`。
- 异常跟在消息下面，带堆栈 —— 这里用的格式化器不会把异常塞进消息那一行。
- 第三个字段是日志器的类别，通常就是记这一行的类，所以拿类名去搜能很快缩小范围。
- Unity 自己的错误会被转发进来：`Error`、`Exception` 与 `Assert` 以错误级别到达，并带 `[Unity:<Type>]` 标记；Unity 的警告以警告级别到达，带 `[Unity]`。所以带 `[Unity:Exception]` 的 `[ERR]` 行就是 Unity 侧抛出的运行时异常。
- 模组自己写的行带它的 id：`[Mod:<id>] …`。
- 模组发现阶段一个模组一行 —— 摘自 `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModRegistry.cs`：

```text
[Mods] discovered {Id} {Version} ({Mode}, permissions {Permissions}, namespace {Namespace}, binds {NativeBinding}) — {DisplayName}.
```

## 怎么把细节调出来

`[Logging] MinimumLevel`（默认 `Information`）设的是写进两个日志接收器的最低级别，而且由接收器自己执行，所以改完不用重启就生效。`Debug` 会加上高频的逐帧／逐事件跟踪（克隆背包、角色转发、方块与音效事件）；`None` 则彻底静音 CUO 的两个日志接收器。各个键与取值范围见[配置项](configuration.md)。

节奏类问题由需要主动打开的 `[Diagnostics]` 仪表回答，默认关闭：`LatencyInstrumentation` 采集逐领域的更新泵耗时，`LatencyLogIntervalSeconds` 决定汇总行多久写一次，`SlowFrameThresholdMs` 决定多慢算一次慢帧。

## 值得搜什么

| 症状 | 通道 | 搜什么 |
|---|---|---|
| 插件根本没加载 | `LogOutput.log` | 插件 GUID 那一行，以及链加载器自己的错误 |
| Unity 侧崩溃 | `latest.log` | 带 `[Unity:Exception]` 的 `[ERR]` 行 |
| 某个模组没加载，或版本不对 | `latest.log` | `[Mods] discovered` 与握手判定那一行 |
| 某个模组自己的行为 | `latest.log` | `[Mod:` 加上模组 id |
| 负载下的帧节奏 | `latest.log` | `[Diagnostics]` 的延迟汇总行 |
| 世界打不开 | `latest.log` | 打开世界过程中写下的归档与租约警告 |
| 想和上一局对照 | `BepInEx/logs/*.log.gz` | 解压 `latest.log` 旁边那份带日期的归档 |

## 排查顺序

1. **`LogOutput.log`** —— 插件加载了吗？组合根是不是在 CUO 日志存在之前就失败了？启动阶段的异常同时也会被直接追加进 `latest.log`，所以那种情况下 CUO 日志不会是空的。
2. **`latest.log`** —— 读**第一条** `[ERR]`，不是最后一条：后面的行通常是它的后果。警告行写明 CUO 跳过了什么、为什么继续跑。
3. **带日期的归档** —— 某个症状是在一次更新之后才出现的，最快的定位方式是把上一局的日志与这一次对照。

测试全绿、日志干净都只是开发期证据：它们证明逻辑与走过的路径，从不证明两个客户端的画面是对的。那个检查是用户的实机验收。

## 相关阅读

- [配置项](configuration.md) —— 这些文件背后的 `[Logging]` 与 `[Diagnostics]` 设置
- [构建、测试与部署插件](../contributing/build-and-test.md) —— 命令、分层与部署核对
- [模组的一生](../internals/mod-loading-lifecycle.md) —— 发现那几行日志记录的加载过程
- [复核与交付](../contributing/review-and-delivery.md) —— 一次改动交付前要带哪些证据
- [术语表](glossary.md) —— 热重载、证据、门禁

---

[文档总览](../README.md) > [参考](README.md) > 日志速查
