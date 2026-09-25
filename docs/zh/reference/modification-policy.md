# 修改策略

[文档总览](../README.md) > [参考](README.md) > 修改策略

---

**读完这一页**，你能分清 CUO 的哪些接口面是[契约](glossary.md)、哪些只是实现，知道模组可以给什么打[补丁](glossary.md)、这份自由换来什么，也清楚一个需求要怎样才变成接口的一部分。契约本身在[模组接口契约](mod-api.md)；一个**新**系统该待在哪里，在[仓库地图与坑](../contributing/repository-map-and-pitfalls.md)；这一页点名的[稳定性分级](glossary.md)（stability level）逐条记在 [`../../contracts/abstractions-api-baseline.txt`](../../contracts/abstractions-api-baseline.txt) 里。

## 什么是契约

| 接口面 | 地位 |
|---|---|
| `CasualtiesUnknownOnline.Abstractions` 的公开 API | **契约。** 它是模组唯一可以引用的程序集。 |
| `CasualtiesUnknownOnline.Runtime` | **实现。** 它公开只是因为插件要把自己组合起来；对模组没有任何承诺，形状可以在任何一次提交里改。 |
| `CasualtiesUnknownOnline.GameAdapter` | **实现。** 它是唯一可以引用游戏程序集的项目，所以它暴露出来的任何东西天生就绑在某一个游戏构建上。 |
| 游戏程序集（`Assembly-CSharp`、Unity 各模块） | **通过声明可以够到。** 契约从不引用它们 —— 对 API 提供的一切来说，适配器就是边界 —— 但需要游戏自己代码的模组可以绑定它，并按下面的[层级（tier）](#层级)把这份绑定声明出来。 |

**可见性规则。** 一个类型或成员默认取它实现所需的最小可见性。只有经过设计、写入文档并复核过的能力，才成为对第三方的公开契约 —— 「它编译得过，因为它当时是 public」不算契约；框架要和自己测试共享的一切，正常形态是 `internal` 加 `InternalsVisibleTo`。这条规则、它的理由和强制它的门禁，写在[门禁与绑定规则](../contributing/gates-and-rules.md)第 14 条。

## 层级

兼容性是分层的，不是一刀切；模组取**能表达自己特性的最窄那一层**。这些层级存在的意义，是让「不在契约里」永远不必等于「不许做」：

| 层级 | 模组绑的是什么 | 它得到什么 | 它要承担什么 |
|---|---|---|---|
| 0 | `Abstractions` 的公开 API | 契约：[稳定性分级](#稳定性分级)与受门禁看管的基线 | 除了 API 自身的规则，没有别的 |
| 1 | CUO 自己的实现（`Runtime`、`GameAdapter`），按名字打[补丁](glossary.md) | 被允许，且不被当作敌意行为 | 接受补丁不带任何承诺 |
| 2 | 游戏自己的代码 | 一份**声明出来的**绑定：仍然是 CUO 模组，对主机可见、可做对等检查 | `[CuoMod]` 的 `NativeBinding` 声明，以及游戏更新带来的变动 |
| 3 | 任何东西，作为不受管理的 BepInEx 插件 | 完全不受约束 | 完全没有可见性 —— 没有主机看得见它 |

第 3 层不是要被打倒的敌人；它正是第 2 层存在的理由，因为一个在 CUO 之外绑定游戏的模组，对每一台主机都是隐形的。CUO 不探测未声明的绑定（见[给 CUO 自己打补丁](#给-cuo-自己打补丁)），所以这份声明是自愿的坦白，唯一的作用力来自主机自己的对等规则：声明随[握手](glossary.md)一起走，主机用自己的 `NativeBindingParity` 规则去判 —— 允许、告警（默认）或要求（见[握手一致性](mod-api.md)）。

只要精心维护的原生操作注册表能表达这个特性，它就是更好的答案 —— 注册表是契约的一部分，层级不是。当好几个模组绑同一样东西时，那就是[提升漏斗](#提升漏斗)所说的信号：该做的是往 API 里加一项，而不是把层级放宽。

## 稳定性分级

`Abstractions` 的每一个公开接口面都有分级，写在类型上 —— 当某个成员和它所属类型不同时，写在那个成员上 —— 用 `[ApiStability(ApiStabilityLevel.<级别>)]` 声明。**没有这个特性的接口面就是 `Stable`**，所以分级是有人做过的声明，绝不是碰巧发生的默认值。

| 级别 | 作者可以依赖什么 |
|---|---|
| `Stable` | 形状能扛过一次 CUO 更新。任何增加、删除或改动都是一次需要复核的改动：公开接口面基线会记下来，删除还要写明理由。 |
| `Experimental` | 有文档、可以用，但在定型之前允许变动。新接口面从这里开始；它变成 `Stable` 的路子就是下面的提升漏斗。 |
| `Advanced` | 按文档支持，但形状跟着**游戏**走、而不是跟着 CUO 自己的模型走，所以一次游戏更新可以在没有任何 CUO 接口决定的情况下挪动它。原生逃生口就是典型例子：`IModNativeApi`（精挑的原生操作注册表，可用操作跟着适配器的注册走）与 `IModNativeLocalPlayerState`（游戏自己的身体字段的投影）。 |
| `Obsolete` | 仍然能用，新模组不许再采用，等没人用了就可以删掉。 |

## 公开接口面基线

`docs/contracts/abstractions-api-baseline.txt` 是 `Abstractions` 公开接口面经过复核的记录：每一个公开类型、它的基类列表，以及每个公开成员连同它的签名与分级。`ApiSurfaceGateTests` 从项目源码重新推导这份接口面并逐行比对：

- **增加**一项会红，直到基线被复核并补上那一行；
- **删除**一项会红，除非把那一行删掉**并且**加一条 `*REMOVED* <键> — <理由>` 墓碑，于是删除成为一个写明理由的刻意动作，而不是悄悄抹掉；
- **改动**某个已记录的行 —— 签名、声明修饰符、访问器、默认值、稳定性分级 —— 报 `CHANGED`；
- 格式错误、重复或自相矛盾的行照样报错；再配一条普查下限，免得一次什么都没扫到的检查蒙混过关。

门禁失败时会把候选文件写到 `artifacts/api-surface/abstractions-api-baseline.txt`（已 gitignore）：复核那份文件，把你打算批准的行抄进基线，并和改动 API 的那次提交一起提交。归一化本身也是契约的一部分 —— 类型引用按简单名记录（改一次 `using` 不算 API 变更），空白折叠，隐式枚举值记成 `implicit #<序号>`（于是调整枚举顺序算 API 变更、而换个写法引用不算），C# 14 的 `extension` 块把接收者折进每个成员的参数表，而门禁解析不出级别的 `[ApiStability]` 参数会直接失败，而不是悄悄退回默认级别。

## 给 CUO 自己打补丁

用 Harmony 给 CUO 自己的代码打补丁是**被允许的，而且不被当作敌意行为**：`Runtime` 与 `GameAdapter` 是实现，补丁是扩展它们的正当方式，CUO 里没有任何东西试图探测或对抗补丁。这里没有反作弊立场，也没有混淆。

补丁**买不到**的是承诺。Harmony 补丁绑的是一个方法的身份和它的参数名，所以它可能在任意一次 CUO 提交上失效 —— 包括一次改的是别的东西的提交 —— 这份风险属于补丁，不属于 CUO。具体地说：

- 优先用契约。API 能表达这个特性就用 API；补丁是逃生口，不是首选。
- 尽量绑得精确 —— 补丁类的完整类名与目标签名正是框架自己的补丁清单契约要核对的东西 —— 并且只上报你核实过的写入。
- 别指望被补的方法挪动时会有人通知你。线上协议版本是 CUO 强制的唯一兼容边界（见[线上与存档兼容](#线上与存档兼容)），它管的是网络，不是你的补丁。
- 如果好几个模组最后都需要同一样东西，那就是下面说的提升信号 —— 把它带进 API，而不是各自维护一份补丁。

## 作者可以指望的诊断信息

CUO 的诊断[信息](glossary.md)就是日志行，这是刻意的：没有调试器集成，也没有秘密通道。模组作者可以依赖下面这些，它们缺失就是值得上报的缺陷：

- **你自己的日志器。** `IModContext.Logger` 以 `[Mod:<id>]` 写出，所以你的行在一份共享日志里能归到名下。
- **发现与校验。** 每个被接受的模组都会有一行 `[Mods] discovered <Id> <Version> (<Mode>, permissions <Permissions>, namespace <Namespace>, binds <NativeBinding>) — <DisplayName>.`（模组没有声明原生绑定时是 `binds -`）；每个被拒的模组都有一行 `[Mods] <Id> … — skipped.` 写明原因：id 为空、缺 `NetworkMode`、SemVer 非法、权限非法、命名空间冲突、依赖缺失、依赖成环或 id 重复。
- **生命周期隔离。** 抛异常的模组会被隔离：加载时 `[Mods] <Id> failed to load — skipped, the other mods continue.`，每个帧阶段 `[Mods] <Id> threw in <Stage> — isolated, the pump continues.`。其他模组照常运行。
- **权限与形状拒绝。** 每道关口都用同一种形状说清拒了什么、为什么：`[Mods] <ModId> does not declare <Permission> — the call is refused.`、`… is already declared … the duplicate is refused.`、`… reached the <Cap>… cap — … refused.`
- **指令结果。** 客机发起的主机指令请求会以可观察的方式结清：`[Mods] <ModId>/<Name> result for <Requester> (request <RequestId>, success <True/False>).`；而一个得不到回答的请求会在请求方自己的期限上以具名的失败结清。
- **本机控制台命令。** `[Mods] <ModId> registered local console command /<Name>.`
- **框架自身的健康状况。** 适配器在启动时打印一次 `Game Adapter capability report:` —— 每项能力一行，带它的 Required／Optional 归类、契约数量与失败原因 —— 当报告拒绝这一局时以 `Error` 级别打印。如果框架自己拒绝联机，那一行会点名是哪个玩法系统坏了。

## 提升漏斗

1. **一个补丁。** 有人需要一个 API 没有的接口面；一个 Harmony 补丁，或一个私下的绕行做法，证明了这份需要，而不必先为 API 付账。
2. **好几个模组需要同一样东西。** 一个模组图方便不算 API。两个互不相干的消费者，或者一个消费者的补丁反复失效，才是触发条件。
3. **实验性 API。** 接口面针对真实消费者设计，写进[模组接口契约](mod-api.md)，标上 `[ApiStability(ApiStabilityLevel.Experimental)]`，并记进基线。它允许变动。
4. **稳定 API。** 形状熬过一轮真实使用之后，标记被去掉 —— 或改成 `Stable` —— 作为一次需要复核的改动。从此它按[稳定性分级](#稳定性分级)所说的意义冻结。

提升是一次决定，不是自然漂移：它以一张票或一条决策落盘，并点名消费者，绝不是「它公开了一阵子，所以现在稳定了」。

## 线上与存档兼容

兼容边界就是握手时的协议版本校验：主机丢弃 `Protocol` 不一致的 `HandshakeMsg`，客机在 `HandshakeAckMsg.Protocol` 不匹配时结束会话。于是混合版本的会话根本不存在，这也正是一次线上改动永远不必为兼容性让路的原因。

- 增加线上行为的模组要在同一次改动里 bump `ProtocolVersion.Current`。那个常量自己的文档注释就是线上变更日志，任何活文档都不复述这个数字 —— 记录过去状态的文档保留它当时写下的数字。
- 只动本地或只读接口面、不增加线上变更的模组不用 bump。
- 模组版本是严格的 SemVer；带状态的模式按优先级比较。握手矩阵在[模组接口契约](mod-api.md)里，那才是契约。

## 相关阅读

- [模组接口契约](mod-api.md) —— 模组声明什么、CUO 强制什么
- [仓库地图与坑](../contributing/repository-map-and-pitfalls.md) —— 新系统该待在哪里，以及那六个问题
- [权限与安全](../internals/permissions-and-security.md) —— 一份声明买到什么，CUO 又拒绝防什么
- [门禁与绑定规则](../contributing/gates-and-rules.md) —— 可见性规则与其余绑定约定
- [CUO 的整体结构](../internals/architecture-overview.md) —— 这些层级谈论的那几层

---

[文档总览](../README.md) > [参考](README.md) > 修改策略
