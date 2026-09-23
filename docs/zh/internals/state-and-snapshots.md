# 状态流与快照

[文档总览](../README.md) > [弄懂原理](README.md) > 状态流与快照

---

**读完这一页**，你能说清哪些值客户端才可以直接当成状态、为什么读数永远不是结论，以及一个投影坏掉之后会发生什么。先读 [四种信封](envelope-protocol.md) —— 这一页讲的是那些信封把东西送到之后的事。

## 三种值

客户端能读到的东西只有三类，把它们混起来，正是这一页要拦下的那类缺陷：

| 种类 | 谁能改它 | 它能用来干什么 |
|---|---|---|
| 内核事实 | 只有已提交批次 | 决定任何事情：归属、死亡、消耗、容器内容 |
| 会收敛的状态流值 | 下一个 tick 覆盖它 | 运动与表现：位置、朝向、流体量 |
| 投影或只读模型 | 谁都不能 —— 它是派生的、可重建的 | 显示状态、回答关于状态的问题 |

## 状态流只负责收敛

状态流存在的原因是：每个 tick 都发一个可靠批次荒唐得不现实。它装的是会收敛的值，作为交换，它接受丢包 —— 丢一帧只损失新鲜度，不损失正确性，因为下一个 tick 会覆盖它。让这笔交换保持安全的规则写在线上的类型上 —— `src/CasualtiesUnknownOnline.Protocol/Wire/WireStreamField.cs`：

```text
Streams are convergent-only: they may update existing continuous fields but never
create/destroy aggregates or change ownership.
```

所以一个状态流值永远不会创建物品、不会移除敌人、不会改归属，也不会推进状态机。那些都是事实，事实走已提交批次 —— 一个允许被丢弃的值，绝不能成为决定某样东西归属的那一环。

## 状态流上到底跑什么

- 玩家与敌人的状态流按可配置的节奏送运动与表现，默认 20 Hz。`src/CasualtiesUnknownOnline.Runtime/Configuration/StateStreamOptions.cs` 把这个设置归一到支持的 1–60 Hz 区间。
- 流体域按成员送各自视口的绝对区域快照：10 Hz 的变化区差分，加 1 Hz 的整视口兜底。
- 每秒一次的角色快照是另一套机制、干另一件事：它是专门事件没送到时的兜底与重放通道。在 `src/CasualtiesUnknownOnline.GameAdapter/Character/CharacterDataSync.cs` 里，这个节奏写作 `CharacterReportInterval = 1f; // guest → host character snapshot (1 Hz)`。

## 离散事实走专门的事件，不靠快照

设计偏好是：任何离散的事情都发一条专门的事件，周期快照只在后面当安全网。肢体的锁存、敌人的撕咬、一次消耗：每件都在发生的那一刻走自己的消息，快照负责补上漏掉的那台客户端。适配器的克隆事实表在关键处写明了这条规矩 —— 它处理敌人撕咬的地方注着“the dedicated event — never the 1 Hz snapshot”。

这个先后顺序不是优化。靠缓慢的周期通道送达的离散事实，按定义就是迟到的；而一旦两台机器据此行动，“迟到”就变成“世界有那么一秒对不上”。

## 读数不是结论

只读模型就是字面意思：供读取的状态。`RemoteVitalsService` 与 `RemoteInventoryService`、远端角色表现、世界物品表 —— 它们回答的都是“这台客户端目前认为那名玩家怎样”，而且它们离过期只差一个丢包。

所以任何仲裁、任何判定都不许消费只读模型。晚了一秒的快照绝不能决定一件物品归谁、一具身体能不能接受处置、一次命中算不算数；判定的一方读的是自己这边的实时状态，这条规则写在[谁来决定玩家身上发生的事](judgment-ownership.md)里。只读模型是给界面、诊断与重建路径用的 —— 不是给结论用的。

## 投影坏掉的时候

投影住在内核外面，内核也不允许它们反过来影响自己。契约在 `src/CasualtiesUnknownOnline.Runtime/Session/ProjectionHealth/IProjectionDomain.cs`：每个投影都是“从权威源派生的可重建只读模型”，实现“绝不能改权威”，失败之后这个域必须仍可重建。

其余由协调器兜住 —— 摘自 `src/CasualtiesUnknownOnline.Runtime/Session/ProjectionHealth/ProjectionHealthCoordinator.cs`：

```text
Projection code runs outside the kernel and must never be allowed to make a
committed batch look rejected. This coordinator wraps a projection apply,
records the last successfully applied revision, marks the domain dirty when
the projection throws, and pumps a per-domain rebuild from the kernel read
model on the Unity main thread.
```

反复失败会把该域升级成运维看得见的降级状态，而权威流程照常往前走。修复方向永远只有一条：把投影拉回已提交的状态，绝不把已提交的状态退回给投影。

## 相关阅读

- [四种信封](envelope-protocol.md) —— 这些值装在哪些帧里
- [谁来决定玩家身上发生的事](judgment-ownership.md) —— 为什么读数不能拿去仲裁
- [CUO 的整体结构](architecture-overview.md) —— 这些值所派生的那个内核
- [读取游戏状态](../how-to/read-game-state.md) —— 同一套机制面向模组的那一面
- [术语表](../reference/glossary.md) —— 状态流、快照、只读模型、投影、检查点

---

[文档总览](../README.md) > [弄懂原理](README.md) > 状态流与快照
