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
- **[热重载](glossary.md)**：运行时在每次做决定时读一次选项监视器，所以改完文件不用重启游戏就生效。联机界面的管理页与控制台的 `/gamerule` 命令改的是同一批配置项。
- BepInEx 的注解会在 CUO 读到文件值之前先夹一次，而且夹得很硬：`AcceptableValueRange` 把越界数字替换成边界值；`AcceptableValueList` 把任何它不接受的文本——拼写错误，或者只是大小写不同，因为匹配是逐字符的——替换成列表的**第一项**。所以两条三档主机规则各自声明一个回退档，插件把它放在第一位（`RemoteBackpackAccess` 是 `deny`，`NativeBindingParity` 是 `warn`），不认识的取值只会落在这个回退档上，绝不会落在开放的 `allow`；对 `NativeBindingParity` 来说这个回退档就是文档里的默认档，不是最严档，所以把 `require` 写成别的形式会变成 `warn`，而不是继续拒绝。声明在 `HostRuleCensus.ConservativeValue`，顺序由 `HostRuleValueText.ChoicesForClamp` 决定；存档选项自己还会再夹一次，那是给直接构建选项的调用方的第二道防线，而不是文件值之所以安全的原因。
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

主机权威的救活与重生规则，都在做决定的那一刻读取。其中四条——`KeepInventory`、`ReviveFromTrader`、`ReviveOnNextLevel`、`Permadeath`——作为主机规则随公告下发（见下面的 `[HostRules]`），因为成员的管理页会展示这个规则面；`KeepSkills` 只留在主机本地的配置里（它不是主机规则，也不下发）。

| 键 | 默认值 | 作用 |
|---|---|---|
| `Permadeath` | `false` | true = 死亡即终结：既不能在商人处救活，也不会在下一层自动重生。 |
| `ReviveFromTrader` | `true` | true = 活着的玩家可以在友好的商人处救活死亡的队友。 |
| `ReviveOnNextLevel` | `true` | true = 主机完成下一层世界生成时，死亡玩家自动重生。 |
| `KeepInventory` | `true` | true = 自动重生保留角色随身与穿戴的物品。 |
| `KeepSkills` | `true` | true = 自动重生保留技能与经验；false 则清零。 |

## `[HostRules]`

`[Respawn]` 之外的主机规则面，同样在做决定时读取。控制台的 `/gamerule <rule name> [<value>]` 按名字读取或设置一条主机规则——名字是这条规则自带的下划线小写形式（例如 `remote_backpack_access`、`save_inventory`；名字写错会把全部规则列出来）——省略取值就是查询。它属于作弊动词：主机直接用；主机授予了作弊权限的成员（作弊开关加 `/op` 名单）通过一次主机往返改的是**主机的**配置项，成员自己的配置永远不写，因为写了自己也会被公告集覆盖。会随主机规则一起下发的四条 `[Respawn]` 规则也照此办理：`save_inventory`、`revive_from_trader`、`revive_on_next_level`、`permadeath`。

**这些值会送到成员端。** 成员客户端会依据它们行事——成员列表里的取物按钮、自己控制台的物品数据写入、自己背着别人时身体多出的负重、以及管理页的只读摘要——所以主机会在成员握手完成的那一刻，把这套规则整套（绝对集，普查判定为「成员会读」的规则各占一个字段）发给它，之后随世界进入组与 60 秒会话内修复组重发，并在会话进行中每次改动时立即广播。规则的所有权仍归主机：成员只按收到的公告拒绝被禁止的操作，主机在背后检查同一件事，所以过期、丢失或被伪造的认知永远不能成为「允许」的理由。没收到公告的成员读自己的配置项并跳过本地检查，这与没有这条公告的构建行为一致，而不是去猜主机想要什么。下表里的「仅主机」说的是这条规则由主机设定并执行，**不是**说它的值只留在主机上：那几行恰好就是成员客户端会读的。

| 键 | 默认值 | 合法范围 | 作用 |
|---|---|---|---|
| `PvpEnabled` | `false` | | 为将来的 PVP 伤害领域预留的主机规则面；目前没有任何玩法效果。 |
| `AutoContinue` | `false` | | 为自动进入下一层预留的主机规则面；尚未接线。 |
| `AllowLateJoin` | `true` | | true = 全新玩家可以加入主机已经开了的世界。 |
| `RemoteBackpackAccess` | `allow` | `allow`、`unconscious_or_dead`、`deny` | 仅主机：谁能打开并操作其他玩家的背包。`allow` = 任何看得见的玩家（模组一直以来的合作规则）。`unconscious_or_dead` = 清醒玩家的背包以**只读**方式打开——物品看得见，但拿、移、丢、用、给这些操作全部被拒，界面用游戏自己的红色槽位标出来——只有昏迷或死亡的玩家才能被操作。`deny` = 谁都不行，背包根本打不开，任何操作都被拒。规则由主机执行，成员客户端读它来决定成员卡上的背包与取物按钮，每条拒绝都会点名这条规则。 |
| `AllowMemberCheatVerbs` | `false` | | 仅主机：作弊开关——true = 成员可以执行作弊动词（`/data` 的写动词、`/give`、`/summon`、`/heal` 重置与 `/gamerule`）。它是主机两道裁决之一：成员还必须在这份作弊名单上，名单由 `/op` 与 `/deop` 编辑。开关关着时，已经授权的成员照样被拒；只有开关、没人授权也谁都进不来。主机自己的动词两道都不看（它的资格就是联机会话开着）。false = 每个成员的作弊动词都被点名拒绝；已经收到公告的成员会在自己客户端上、**还没写入任何东西之前**就被拒，而没收到公告的成员照旧先应用、先上报，等主机的拒绝到达再把值放回去。 |
| `WidenRunSettings` | `true` | | 仅主机：在联机时放宽游戏自带的自定义开局设置滑块，好让这一局按真实大厅人数调整。数值仍然走既有的世界初始参数。 |
| `PiggybackWeightMultiplier` | `0.8` | 0.0–3.0 | 仅主机：背负关系生效期间，被背一方全部负重加到背负者身上的比例。`0` 关闭这项移动惩罚。读它的是背人的那一方自己的客户端，所以成员背人时用的是主机的值。 |
| `NativeBindingParity` | `warn` | `allow`、`warn`、`require` | 仅主机：当双方都列出的某个模组，客机声明的[原生绑定](glossary.md)与主机自己的不同时怎么判。`allow` = 不检查；`warn` = 准入并把不一致记进主机的日志；`require` = 拒绝该成员。 |
| `RadiationLineStopsAboveTheFloor` | `true` | `true`、`false` | 仅主机：辐射线停在地板上方一段距离、不再继续下探，于是层底仍然可住，一队人可以在那里分补给、歇一歇。`false` = 游戏原本不封顶的下探。 |

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
| `PlayerColor` | （空） | （空）、`#RRGGBB`、`#RRGGBBAA` | 玩家标记颜色。留空 = 按 SteamId 稳定的自动配色；其余情况直接写颜色本身，用游戏自己的十六进制写法（`#RRGGBB`，半透明就再加两位 `#RRGGBBAA`）。它是本地偏好，通过握手与花名册消息共享；保存的配置模板会像其他条目一样带上它。 |

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
