# 谁来决定玩家身上发生的事

[文档总览](../README.md) > [弄懂原理](README.md) > 谁来决定玩家身上发生的事

---

**读完这一页**，你能说清为什么一个结论留在某台机器上、而不是交给主机，以及这条规则的两半各自落在代码的哪里。[判断一个动作由哪一侧判定](../how-to/decide-which-side-judges-an-action.md) 是写新功能时照着做的清单版；这一页讲的是它背后的道理。

## 规则，以及它的代价

关于一名玩家身上会发生什么的判定，属于**那名玩家自己的客户端**，依据它自己的画面与自己的时间线：受害者判定自己被打中；交互中的两名玩家各自判定自己这一侧的视线；客户端发现自己的身体做不了某项处置，就由它自己说出来。主机留着它模拟的世界，以及冲突主张之间的[仲裁](../reference/glossary.md)。

理由是判错的代价。主机对别人画面的结论，每一次命中都要付账：输入必须来回一趟才允许有任何事发生，于是没人争抢的时候，游戏也像在网络上跑。而一次被仲裁拒绝后的[回滚](../reference/glossary.md)，只在两名玩家真的抢起来时才付账，那很罕见。所以这份延迟预算花在罕见的一侧，而不是花在每一个动作上。

## 这道关卡在哪一侧

规则落实成一条接缝规则：**由提出请求的那一侧客户端来判定。** 每一次跨玩家请求都在发送侧过触及范围这道关，主机端没有任何处理器再去替远端行动者判一次触及范围或距离。运行时只调用一个关口 —— 摘自 `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/IPlayerInteractionVisibility.cs`：

```csharp
bool HasLineOfSight(ulong observerSteamId, ulong targetSteamId);
```

这个接口的类型注释写明了判定方向：交互策略归运行时，它调用这道窄关口；真正的世界查询（两名玩家之间的 `Physics2D`／地面射线）归适配器。主机自己发起的动作也走同一个方法，所以主机和自己客户端上的其他人一样，由自己这一侧判定。

## 当依据在对方身体上时

有些前置条件描述的是目标的身体，而不是行动者的触及范围 —— 那条肢体是不是断了、有没有止血带、还有几块弹片活着。这些没法用主机那份晚了一秒的报告来回答，于是主机去问目标自己的客户端：把请求挂在票据下等着，拿到答复再继续。摘自 `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/MedicalTargetBodyGate.cs`：

```text
The target-body verdict seam of the medical operation family. The host
validates what it owns (participants, the operator's item facts, the claims),
PARKS the start request here and asks the TARGET's client; the target runs the
target half of the preconditions (<see cref="MedicalTargetBodyValidator"/>)
against its OWN live body and answers, and the parked request continues on that
answer — commit on an accept, the target's own reason on a reject.
```

答复到达时，主机仍会重新核对自己那一半事实，因为答复已经隔了一个来回；它不可以做的是替目标回答。

## 主机留着什么

- **它模拟的那个世界。** 本局的种子、世界生成、共享实体的创建与存档都是主机的事实。
- **主张之间的仲裁。** 两名玩家抢同一件物品，先写者得，输的一方回滚。内核为此准备了带类型的词汇 —— `src/CasualtiesUnknownOnline.GameState/RejectionReason.cs` 里的 `RejectionReason.Conflict` —— 所以一次拒绝是客户端能据以行动的原因，而不是无声的丢弃。
- **准入，而不是结果。** 从网络提交上来的指令，行动者会被绑定到传输层的发送方，由准入关口判断这名成员到底允不允许提交它。这是会话层面的决定，而且止步于准入：每一个领域结论仍然归内核。
- **多人协作必须照常可能。** 排他的一人一把锁是例外而不是常态；两名施术者同时处置一名受害者很正常，他们的主张按单位仲裁，而不是加锁。

## 报告先被采纳

客机报上来的状态，主机先采纳、先转发，只在明显冲突时才纠正，而且纠正绝不阻塞玩家。这条默认有一个前提，也是容易被忘掉的那一半：**主机必须真的能持有报告里的那份状态。** 主机持不了的内容（自己的内容集里没有这条定义、id 映射不上、领域对象无法归属）要被拒绝，绝不能“采纳了但没有归属”。一条没有归属的已采纳记录，永远等不到它主人的死亡来收回，于是它会渗进之后的快照，把同伴早已销毁的状态又复活回来。拒绝必须是看得见的：回答上报方，让它的重报兜底停下来，并把具体对不上的地方写进日志。

## 延迟永远不是输入

任何判定、仲裁或容差，都不许建立在一个无视对端实测往返时间的固定窗口上。要么判定所需的事实是已知的 —— 一次创建在任何人动它之前就被判定，一次被拒绝的创建留下[墓碑](../reference/glossary.md) —— 要么这项判定属于看得见它的那个客户端。

这里的界线在于：用来决定的界限与用来放弃的界限不是一回事。问目标客户端要答复，而沉默的对端不能让施术者永远等下去，所以这道关有一个等待上限。它自己的注释定死了这个上限能做什么：原文那句 `The liveness bound is not a judgment parameter` 说的就是 —— 这个上限只放弃目标从未答复的请求，好让沉默的对端给施术者留下一条明确的拒绝，而不是一场永远等不到头的启动。注释最后一句把该记住的规则又说了一遍：`No measured latency, window guess or tolerance enters any verdict.` 被放弃的请求会变成一条施术者可以重试的明确拒绝，而不是一条关于目标的结论。

## 这一页刻意不做什么

- **它不是反作弊。** 客户端在自己的触及范围或自己身体上撒谎，属于功能集稳定之前明确不管的部分；这道接缝以后再收紧即可，不需要把结论搬回主机。
- **本地不等于私密。** 本地判定过的动作仍然要上报它的终局事实 —— 死亡、昏迷、归属、消耗 —— 因为那些是每台机器都必须收敛到的内核事实。
- **它没有把主机降成路由器。** 主机仍然拥有自己的世界，仍然做仲裁；它不拥有的是对别人身体、触及范围或时机的结论。

## 相关阅读

- [CUO 的整体结构](architecture-overview.md) —— 这条规则所在的内核与分层
- [状态流与快照](state-and-snapshots.md) —— 为什么读数不是结论
- [判断一个动作由哪一侧判定](../how-to/decide-which-side-judges-an-action.md) —— 写新功能时的清单
- [给其他玩家发消息](../how-to/send-a-network-message.md) —— 一份报告怎么到达主机
- [术语表](../reference/glossary.md) —— 判定归属、仲裁、回滚、只读模型

---

[文档总览](../README.md) > [弄懂原理](README.md) > 谁来决定玩家身上发生的事
