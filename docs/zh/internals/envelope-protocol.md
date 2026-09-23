# 四种信封

[文档总览](../README.md) > [弄懂原理](README.md) > 四种信封

---

**读完这一页**，你能说清什么走哪种信封（envelope）、为什么高频的状态流允许丢数据而已提交批次不允许，以及客机怎么在会话中途追上进度。先读 [CUO 的整体结构](architecture-overview.md)：这一页默认你已经知道内核与已提交批次是什么。

## 一帧只装一个信封

所有权威流量都走同一种传输帧，一帧只装一个信封。信封种类是显式写出来的，所以接收方在解包体之前就能拒掉自己看不懂的东西 —— 摘自 `src/CasualtiesUnknownOnline.Protocol/Wire/EnvelopeKind.cs`：

```text
A frame carries exactly one envelope; the kind is explicit so receivers can reject
unknown/unsupported envelopes before touching the payload.
```

帧在任何人动它之前先过结构校验，具体拒绝哪些形状是[协议消息](../reference/protocol-messages.md)里那份契约。表现类载荷是刻意豁免的：`src/CasualtiesUnknownOnline.Protocol/Wire/ProtocolFrameValidator.cs` 的注释说，未知的表现载荷故意不作致命错误，这样将来可选的表现效果能直接搭在协议上，不必为此抬一次关键版本号。

## 四种信封

| 信封 | 方向 | 装的是什么 |
|---|---|---|
| `CommandEnvelope` | 客机 → 主机；被拒绝时主机 → 客机 | 一条意图或一条原生观察，以及回答它的拒绝 |
| `CommittedBatchEnvelope` | 主机 → 各客机 | 一个原子的已提交批次 —— 权威状态发生改变的唯一确认 |
| `CheckpointEnvelope` | 主机 → 客机 | 完整状态副本的一块，用于加入、重连或补缺口 |
| `StateStreamEnvelope` | 主机 → 各客机；玩家报告则客机 → 主机 | 会收敛的高频字段：位置、朝向、流体量 |

每种信封都带同一份头部：协议版本、运行纪元、发送方、消息 id、适用的操作 id、它所基于的基础修订号，以及载荷类型。[运行纪元](../reference/glossary.md)的作用是挡住上一局流量污染新一局 —— 来自旧局的帧会被丢弃，而不是被应用。

## 一个动作的路径

1. 客机把一条玩法意图变成 `CommandEnvelope` 发出去。
2. 主机先校验帧，再把解出来的指令交给 Application 层的准入关口。
3. 内核判定它：接受的指令产生一个已提交批次，拒绝的指令产生一条带原因的带类型拒绝（`src/CasualtiesUnknownOnline.GameState/RejectionReason.cs` 里的 `RejectionReason`）。
4. 被接受的批次以 `CommittedBatchEnvelope` 广播给每一台客机，主机自己也把它投影进本地的世界表与克隆体。
5. 每台客机把批次应用到自己的内核上，再投影结果。`Apply` 按操作 id 幂等，所以重复到达的批次直接忽略。

拒绝是通过 `WirePayloadType.CommandRejected` 装在 `CommandEnvelope` 里回来的，不是另有一种帧。如果你在改的这条家族以前有一条专门的拒绝消息，那条消息已经不存在了：答案跟着问题走，用同一种信封。

## 加入与追赶

客机从不从头重放整场会话。它拿到修订号 N 处的[检查点](../reference/glossary.md)，恢复它，再应用这之后提交的批次 —— 也就是批次尾巴：

```text
Host: checkpoint at revision N     ->  chunks
Host: batches N+1..M               ->  the journal tail
Guest: restore checkpoint, apply tail, answer Ready(M)
Host: normal batches and streams from M on
```

有两条恢复路径，走哪条由客机落后多少决定。客机发现自己漏了一个批次，就请求一段区间；如果这段区间已经掉出主机那个有界的批次窗口，主机就干脆重发一份检查点，而不是去应答一段它已经拿不出来的区间。无论走哪条，客机最后都停在主机报出的某个修订号上，正常流量从那里继续。

## 为什么状态流可以丢，批次不行

状态流按设计就不可靠。它装的是会收敛的值：下一个 tick 会覆盖它们，所以丢一帧只损失新鲜度，不损失正确性。作为交换，状态流不许做只有事实才能做的事 —— 它只能更新既有的收敛字段，不能创建或销毁聚合，不能改归属或容器关系，也不能推进关键状态机。后续逻辑要依赖的终局状态必须变成领域事件、走批次，因为一个允许被丢弃的值，绝不能用来决定某样东西归谁。

## 版本校验就是兼容边界

双方在加入[握手](../reference/glossary.md)时比对协议版本，数字不一致，任何一侧都会结束这次尝试。整个兼容策略就这么一条：`src/CasualtiesUnknownOnline.Runtime/Protocol/ProtocolVersion.cs` 写明了政策 —— 每一次有行为变化的线上扩展都抬高这个号，版本不一致的会话由握手拒绝。所以线上改动和版本号抬升在同一次改动里一起落地，代码永远不必把旧形状留着。

## 什么不走信封

并非每一帧都是这四种之一。会话与控制、世界表现、角色表现、敌人快照与攻击、交易与聊天、模组接口、玩家交互请求，仍然各用自己的帧。它们是单路径协议，不是权威状态的第二个存放处：其他玩家必须一致认同的玩法事实走已提交批次，只有单个接收方会处理的东西才可以直发。

## 相关阅读

- [CUO 的整体结构](architecture-overview.md) —— 批次是从哪个内核出来的
- [状态流与快照](state-and-snapshots.md) —— 状态流怎么变成你能读的东西
- [谁来决定玩家身上发生的事](judgment-ownership.md) —— 在发出去之前，由哪一侧判定
- [给其他玩家发消息](../how-to/send-a-network-message.md) —— 面向模组的那张网络
- [术语表](../reference/glossary.md) —— 批次、检查点、状态流、运行纪元、握手、协议版本

---

[文档总览](../README.md) > [弄懂原理](README.md) > 四种信封
