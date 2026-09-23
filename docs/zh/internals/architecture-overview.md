# CUO 的整体结构

[文档总览](../README.md) > [弄懂原理](README.md) > CUO 的整体结构

---

**读完这一页**，你能说清 CUO 的每个机制该放在哪儿，以及为什么权威状态存在[内核](../reference/glossary.md)里，而不是存在游戏渲染出来的那些对象里。还没玩过一次会话的话，先读 [CUO 是什么](../start/what-is-cuo.md)。

## 一件事实只有一个写入方

CUO 把一款单机游戏变成主机权威的[会话](../reference/glossary.md)。难的不是把字节经 Steam 送出去，而是：对每一条共享事实定下哪台机器可以改它，并让其余每台机器从同样的输入得到同样的值。

由此得到一条规则：**每一条要持久保存的玩法事实，只有一个写入方。** 一堆物品的数量、一名玩家的生命值、一扇门开没开 —— 每条都只有唯一一条权威写入路径，其他所有显示它的东西都由这条路径派生：你点得到的 Unity 对象、队友的远端克隆体、网络缓存、存档。能被两处写入的事实，早晚会跟自己都对不上。

## 树的底部

`CasualtiesUnknownOnline.GameState` 就是这枚带类型的确定性内核。它不引用任何其他 CUO 项目，也是持久玩法状态唯一的所在。它的接口刻意很小 —— 完整摘自 `src/CasualtiesUnknownOnline.GameState/IGameStateKernel.cs`：

```csharp
Decision Execute(GameCommand command, CommandContext context);
ApplyResult Apply(CommittedBatch batch);
GameCheckpoint CreateCheckpoint();
RestoreResult Restore(GameCheckpoint checkpoint);
RunEpoch RunEpoch { get; }
IReadOnlyDictionary<ulong, ItemState> QueryItems();
ItemState? FindItem(ulong instanceId);
RunState? QueryRun();
WorldEntityState? QueryWorldEntities();
PlayerStateTable? QueryPlayers();
EnemyStateTable? QueryEnemies();
FluidStateTable? QueryFluids();
```

- `Execute` 是权威那一侧：判定一条带类型的[指令](../reference/glossary.md)，接受的话，就把产生的事件作为一个[批次](../reference/glossary.md)提交。
- `Apply` 是其余所有侧：同一个批次从网络、从存档或从回放到达，直接应用，不再判定一次。
- `CreateCheckpoint` 与 `Restore` 把一整个[世界](../reference/glossary.md)存出去、读回来；`RunEpoch` 报出这个存储当前持有的那一局。
- 旁边那些查询方法 —— `QueryItems`、`FindItem`、`QueryRun`、`QueryWorldEntities`、`QueryPlayers`、`QueryEnemies`、`QueryFluids` —— 是给界面、存档与诊断用的只读视图，不是第二条入口。

类型的注释写着这条规矩：内核表面要小且稳定，领域行为用带类型的指令表达，而不是几十个按领域分的方法。里面每个玩法领域都是内核路由过去的模块，`src/CasualtiesUnknownOnline.GameState/Kernel/IDomainModule.cs` 定下这份契约，它的注释写明边界：某个领域的代码，永远看不到另一个领域的内部。

## 上面的分层

| 项目 | 它负责什么 |
|---|---|
| `CasualtiesUnknownOnline.Abstractions` | 对模组公开的接口；不引用任何项目 |
| `CasualtiesUnknownOnline.GameState` | 带类型的确定性内核；不引用任何项目 |
| `CasualtiesUnknownOnline.Protocol` | 只放线上类型与编解码；不引用任何项目 |
| `CasualtiesUnknownOnline.Application` | 准入关口与内核复制 —— 从内核向上的唯一通路 |
| `CasualtiesUnknownOnline.Runtime` | 会话、Steam、依赖注入、模组加载、运行时投影 |
| `CasualtiesUnknownOnline.GameAdapter` | 唯一引用游戏程序集的项目 |
| `CasualtiesUnknownOnline.Plugin` | BepInEx 入口，一层很薄的生命周期驱动 |

层级方向是数据，不是文字说明。`tests/CasualtiesUnknownOnline.NormativeGates.Tests/ProjectDirectionPolicy.cs` 存着门禁读的那张表：它拒绝被声明的层不允许的引用，拒绝表里没归类的项目，也拒绝一个不再引用 Application 的 Runtime —— 注释的原话是，绕过这一层去够内核，正是门禁要拦下的事。

## 一次操作，一个批次

```text
Command  ->  Decide  ->  CommittedBatch  ->  Reduce  ->  Effects
```

- **指令**提出“让某件事发生”的请求；它带类型，可以被拒绝，并给出原因。
- **判定**在状态的工作副本上进行，产出事件草稿或一次拒绝。它没有副作用：一条指令正在被判定时，内核之外的东西不会改变。
- **已提交批次**是原子结果。`src/CasualtiesUnknownOnline.GameState/CommittedBatch.cs` 里记着操作 id、全局[修订号](../reference/glossary.md)、行动者、权威类别、[运行纪元](../reference/glossary.md)、前置修订号与已接受的事件；注释写明：批次是确认状态发生改变的唯一途径。
- **归约**把那些事件应用到权威状态上。每台机器用同样的方式归约同一个批次，这正是客机、中途加入的人和回放最终落到同一个世界的原因。
- **效果**是外层必须做的事：移动一个 Unity 对象、播一个声音、发一帧。它们由投影派生，不存进批次，所以回放不必把表现层也存一遍。

一次复合操作 —— 扣材料、造出产物、更新玩家、解锁配方 —— 是一条由多条内层指令组成的指令，按顺序在同一份工作副本上执行，最后作为一个批次提交。任何一条内层指令被拒绝，工作副本就被丢弃，什么都不提交。

## 为什么必须确定性

内核从不读时钟、随机数、Unity 变换或文件。这些都以显式输入进来：一份指令上下文、一条具名随机流、一个[检查点](../reference/glossary.md)。于是同样的批次序列永远产出同样的状态，而这一点才让模拟与回放框架成为[证据](../reference/glossary.md)：缺陷不开游戏也能复现，出现分歧时它是关于输入的事实，而不是关于谁家帧率的事实。

## 投影可以丢掉

Unity 场景、界面、远端克隆体、网络缓存与存档，都是已提交状态的[投影](../reference/glossary.md)。它们都不是权威，所以谁都不需要靠手工维持一致：某个投影一旦对不上或坏掉，按内核的只读模型重建它永远是合法的修法，而且投影失败绝不会把内核已经提交的事实退回去。

## 内核不是什么

- 不是万能的实体组件系统，也不是通用的增删改查：每个领域保有自己的类型模型与不变式。
- 不是事件日志：只有一个有界的已提交批次窗口，用于重传与幂等；状态本身是带类型的[快照](../reference/glossary.md)。
- 不是上帝对象：内核只做五件事 —— 路由指令、建工作副本、收集事件草稿、原子提交、发布批次。物品规则、流体公式、冷却时间都属于领域。
- 不承诺向后兼容：客机加入时会校验[协议版本](../reference/glossary.md)，所以任何改动都不必把旧的线上形状留着。

## 靠什么维持

上面这些规则是被强制执行的，不靠记性。`SourceShapeGateTests` 会在这些情况下失败：内核状态用字符串做键、指令没声明[权威](../reference/glossary.md)、GameState 引用了别的项目，或者旧的与双架构的标记重新出现；`ProjectDirectionGateTests` 从解决方案文件读出分层表；适配器自己的门禁把它的接缝收窄。规则到门禁的对应表在 `docs/evidence/normative-gates.md`。

## 相关阅读

- [弄懂原理](README.md) —— 这一分区的其余页面
- [谁来决定玩家身上发生的事](judgment-ownership.md) —— 权威规则实际怎么用
- [四种信封](envelope-protocol.md) —— 已提交批次怎么到达其他机器
- [状态流与快照](state-and-snapshots.md) —— 内核推什么，读的人看到什么
- [仓库地图与坑](../contributing/repository-map-and-pitfalls.md) —— 一个文件该属于哪个项目
- [术语表](../reference/glossary.md) —— 内核、指令、事件、批次、投影、确定性

---

[文档总览](../README.md) > [弄懂原理](README.md) > CUO 的整体结构
