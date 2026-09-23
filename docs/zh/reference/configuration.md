# 配置项

[文档总览](../README.md) > [参考](README.md) > 配置项

---

**读完这一页**，你能找到 CUO 的设置文件、查到每一个配置项的默认值与合法范围，并分清哪些开关是[主机规则](glossary.md)、哪些值是写死在代码里的策略。存档相关的键在[世界怎么存档](../internals/save-archive.md)里讲，日志相关的键在[日志速查](logs.md)里讲。

## 设置在哪里

| 什么 | 在哪 |
|---|---|
| 设置文件 | `BepInEx/config/CasualtiesUnknownOnline.cfg` —— BepInEx 按插件 GUID 命名，也就是 `CasualtiesUnknownOnline` |
| 主机持久化的模组状态 | `BepInEx/config/CasualtiesUnknownOnline.mod-state.bin` |
| 主机的封禁名单 | `BepInEx/config/CasualtiesUnknownOnline.host-bans.bin` |
| [配置档案](glossary.md) | `BepInEx/config/CasualtiesUnknownOnline.Profiles/<name>.profile` |

- 插件在启动时绑定全部配置项并保存文件，所以某个更新新加的段（比如 `[UI]`）会出现在老安装里，而不是悄悄藏着。
- 下表的默认值、取值范围与合法取值都是精确的；文字说明是本页对 BepInEx 写在配置文件里那一行旁白的转述。声明本身在 `src/CasualtiesUnknownOnline.Plugin/PluginDependencyRegistrar.cs`（`[Session] InteractionPanelKey` 绑在 `Plugin.cs` 里）。
- **[热重载](glossary.md)**：运行时在每次做决定时读一次选项监视器，所以改完文件不用重启游戏就生效。联机界面的管理页与控制台的主机规则命令改的是同一批配置项。
- BepInEx 的范围校验是第一道夹子，选项对象是第二道：手改文件能绕过第一道，而「每 0 分钟自动存档」和「保留 0 份备份」都不是这套系统可以执行的策略，所以存档选项会再夹一次。
- **配置档案**是全部已绑定配置项的一份带名字的快照。抓取时写一个 `.profile` 文件（结构版本 1）；套用时逐条写入，统计成功与跳过的条数，并报告写不进去的那些。档案名 1–64 个字符，不能包含路径分隔符。

## `[Sync]`

| 键 | 默认值 | 合法范围 | 作用 |
|---|---|---|---|
| `StateStreamHz` | 20 | 1–60 | 玩家／敌人状态快照的频率（Hz）—— 越高越顺滑，也越吃带宽。 |

## `[Logging]`

| 键 | 默认值 | 合法范围 | 作用 |
|---|---|---|---|
| `MinimumLevel` | `Information` | `Information`、`Trace`、`Debug`、`Warning`、`Error`、`Critical`、`None` | 写进 BepInEx 与 `latest.log` 的 CUO 日志最低级别。`Information` 让正常游玩保持安静；`Debug` 打开高频的逐帧／逐事件跟踪（克隆背包、角色转发、方块与音效事件）。 |

## `[Respawn]`

主机权威的救活与重生规则，都在做决定的那一刻读取。

| 键 | 默认值 | 作用 |
|---|---|---|
| `Permadeath` | `false` | true = 死亡即终结：既不能在商人处救活，也不会在下一层自动重生。 |
| `ReviveFromTrader` | `true` | true = 活着的玩家可以在友好的商人处救活死亡的队友。 |
| `ReviveOnNextLevel` | `true` | true = 主机完成下一层世界生成时，死亡玩家自动重生。 |
| `KeepInventory` | `true` | true = 自动重生保留角色随身与穿戴的物品。 |
| `KeepSkills` | `true` | true = 自动重生保留技能与经验；false 则清零。 |

## `[HostRules]`

`[Respawn]` 之外的主机规则面，同样在做决定时读取。

| 键 | 默认值 | 合法范围 | 作用 |
|---|---|---|---|
| `PvpEnabled` | `false` | | 为将来的 PVP 伤害领域预留的主机规则面；目前没有任何玩法效果。 |
| `AutoContinue` | `false` | | 为自动进入下一层预留的主机规则面；尚未接线。 |
| `AllowLateJoin` | `true` | | true = 全新玩家可以加入主机已经开了的世界。 |
| `AllowRemoteInventoryTake` | `true` | | true = 其他玩家可以从远端玩家的背包里拿随身物品（昏迷／死亡后的拾取仍是默认规则；false 彻底关掉跨玩家背包取物）。 |
| `WidenRunSettings` | `true` | | 仅主机：在联机时放宽游戏自带的自定义开局设置滑块，好让这一局按真实大厅人数调整。数值仍然走既有的世界初始参数。 |
| `PiggybackWeightMultiplier` | `0.8` | 0.0–3.0 | 仅主机：背负关系生效期间，被背一方全部负重加到背负者身上的比例。`0` 关闭这项移动惩罚。 |
| `NativeBindingParity` | `warn` | `allow`、`warn`、`require` | 仅主机：当双方都列出的某个模组，客机声明的[原生绑定](glossary.md)与主机自己的不同时怎么判。`allow` = 不检查；`warn` = 准入并把不一致记进主机的日志；`require` = 拒绝该成员。 |

## `[Save]`

世界归档策略，归主机所有，运行时每次做决定都读一次。

| 键 | 默认值 | 合法范围 | 作用 |
|---|---|---|---|
| `AutosaveEnabled` | `true` | | 仅主机：世界正在游玩期间写定时自动存档。关掉之后，玩家的 `/save`、层末切片与返回菜单时的切片照旧。 |
| `AutosaveIntervalMinutes` | 10 | 1–1440 | 仅主机：两次定时自动存档之间的分钟数（即 `auto-*.cuoz` 归档）。每次已提交的切片都会重新开始计时，不论切片是谁触发的。 |
| `BackupRetentionCount` | 10 | 1–1000 | 仅主机：一个世界保留多少份备份归档。每次已提交切片之后裁剪最旧的；最新那份归档永远不裁。 |

## `[UI]`

| 键 | 默认值 | 合法范围 | 作用 |
|---|---|---|---|
| `Language` | `en` | `en`、`zh` | CUO 界面语言。本地化服务把以 `zh` 开头的值归一到中文，其余一律归到英文。 |
| `PlayerColorIndex` | -1 | -1–7 | 玩家标记颜色。`-1` = 按 SteamId 自动配色；`0`–`7` = 共享玩家调色板中的一种。它是本地偏好，通过握手与花名册消息共享。 |

## `[IpDirect]`

不走 Steam 的连接方式。端口由 BepInEx 的范围校验把关。

| 键 | 默认值 | 合法范围 | 作用 |
|---|---|---|---|
| `ListenPort` | 7777 | 1–65535 | 直连主机监听的 TCP 端口。 |
| `JoinAddress` | `127.0.0.1` | | 要加入的直连主机地址或主机名。 |
| `JoinPort` | 7777 | 1–65535 | 要加入的直连主机 TCP 端口。 |
| `DisplayName` | （空） | | 直连会话里自定义的游戏内显示名；留空表示 `player-<id>`。Steam 会话仍用 Steam 昵称。 |

## `[Diagnostics]`

需要主动打开的线上路径仪表。默认关闭：它不能影响正常游玩；打开之后也只是给被测的领域调用加一个秒表，并按日志间隔每个名字输出一行汇总。

| 键 | 默认值 | 合法范围 | 作用 |
|---|---|---|---|
| `LatencyInstrumentation` | `false` | | true = 采集并记录逐领域的 CUO 更新泵耗时。 |
| `LatencyLogIntervalSeconds` | 1.0 | 0.1–60.0 | 相邻两行聚合延迟日志之间的秒数。 |
| `SlowFrameThresholdMs` | 25.0 | 0.0–1000.0 | 整帧的 GameAdapter 更新总耗时达到这个毫秒数，就算一次慢帧／掉帧样本。 |

## `[Session]`

| 键 | 默认值 | 作用 |
|---|---|---|
| `InteractionPanelKey` | `F6` | 开关独立玩家交互快捷面板的热键。接受任何 `UnityEngine.KeyCode` 名字。 |

## 哪些不是配置项

下面这些值看着像可以调，其实不是：它们是代码里的策略常量，改一个就是改代码（其中载荷上限还与协议相邻）。

| 常量 | 值 | 来源 |
|---|---|---|
| `ModChannel.MaxPayloadBytes` | 64 KiB | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModChannel.cs` |
| `ModRateLimitPolicy` 模组消息速率 | 持续 20 条每秒，突发 40 条 | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModRateLimitPolicy.cs` |
| `ModCommandPolicy` | 名字 ≤64、参数 ≤16 个、每个 ≤256 字符、合计 ≤4 KiB、输出 ≤32 KiB、错误 ≤4 KiB、请求超时 10 秒、挂起请求 ≤32 | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModCommandPolicy.cs` |
| `ModStatePolicy` / `ModDataPolicy` | 键 ≤128 字符、≤1024 条、单值 ≤64 KiB | `src/CasualtiesUnknownOnline.Runtime/Session/Mods/` |
| `ContentId` 路径长度 | ≤95 字符 | `src/CasualtiesUnknownOnline.Abstractions/ContentId.cs` |
| `WorldLease.StaleAfter` | 30 分钟 | `src/CasualtiesUnknownOnline.Runtime/Persistence/WorldLease.cs` |
| `ResourceLocationCatalog.MaxSuggestions` | 20 | `src/CasualtiesUnknownOnline.Runtime/Session/Content/ResourceLocationCatalog.cs` |

## 相关阅读

- [日志速查](logs.md) —— `[Logging]` 与 `[Diagnostics]` 两段在实际排查里的用法
- [模组接口契约](mod-api.md) —— 上面那些策略常量在模组作者眼里的样子
- [世界怎么存档](../internals/save-archive.md) —— 切片、备份，以及存档那几个键到底管什么
- [构建、测试与部署插件](../contributing/build-and-test.md) —— 部署那个会读这份配置文件的插件
- [术语表](glossary.md) —— 主机规则、热重载、配置档案、原生绑定

---

[文档总览](../README.md) > [参考](README.md) > 配置项
