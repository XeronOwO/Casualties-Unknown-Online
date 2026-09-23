# 协议消息

[文档总览](../README.md) > [参考](README.md) > 协议消息

---

**读完这一页**，你能查到 CUO 在线上到底发了什么：装下所有权威流量的那一帧、帧里的四种[信封](glossary.md)、每种信封共用的头部、恢复路径，以及信封之外每一条直连消息的 id。这样设计的原因在[四种信封](../internals/envelope-protocol.md)；没见过内核的话先读那一页。

## 一帧一信封

所有权威流量都搭在同一个传输帧上（`NetMsg.KernelEnvelope`，id 122），载荷是 `ProtocolFrame`。一帧只装一个信封，而且信封种类是显式的，所以接收方在碰载荷之前就能拒掉自己看不懂的东西。

任何人在动这个帧之前，它先过结构校验。`src/CasualtiesUnknownOnline.Protocol/Wire/ProtocolFrameValidator.cs` 会拒绝：

- 帧里没有信封，或者装了不止一个；
- 头部与信封种类对不上；
- 头部的发送方不是传输层的发送方；
- 载荷判别值不属于这一类信封；
- 未知的**关键**载荷。

表现类载荷是刻意豁免的：未知的表现载荷不作致命错误，这样将来可选的表现效果能直接搭在协议上，不必为此抬一次关键版本号。`KernelProtocolService` 对每一个收到的帧都调用这个校验器（`src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolService.cs`）。

## 四种信封

| 信封 | 方向 | 它代表什么 | 源码 |
|---|---|---|---|
| `CommandEnvelope` | 客机 → 主机；拒绝时也有主机 → 客机 | 一条意图或一条原生观察，外加回答它的那份拒绝 | `src/CasualtiesUnknownOnline.Protocol/Wire/CommandEnvelope.cs` |
| `CommittedBatchEnvelope` | 主机 → 各客机 | 一个原子的已提交批次 —— 权威状态变了的唯一确认 | `src/CasualtiesUnknownOnline.Protocol/Wire/CommittedBatchEnvelope.cs` |
| `CheckpointEnvelope` | 主机 → 客机 | 完整状态副本的一个分片，用于加入、重连或补差 | `src/CasualtiesUnknownOnline.Protocol/Wire/CheckpointEnvelope.cs` |
| `StateStreamEnvelope` | 主机 → 各客机；玩家上报时也有客机 → 主机 | 收敛性的高频字段更新 | `src/CasualtiesUnknownOnline.Protocol/Wire/StateStreamEnvelope.cs` |

帧与它的枚举：`ProtocolFrame.cs`、`EnvelopeKind.cs`、`EnvelopeHeader.cs` 与 `WirePayloadType.cs`，都在 `src/CasualtiesUnknownOnline.Protocol/Wire/` 下。

## 共用的头部

每种信封都带同一份 `EnvelopeHeader` 字段：

```text
ProtocolVersion
RunEpoch
SenderId
MessageId
OperationId (when applicable)
BaseGlobalRevision
PayloadType
```

[运行纪元](glossary.md)的作用是挡住上一局的流量污染新的一局：指令、批次与状态流按各自的纪元比对，不匹配就丢弃；检查点分片则按主机在世界加入指令（`WorldJoinMsg.RunEpoch`）里公布的那份局身份校验收下的分片 —— 在任何一个分片被缓冲之前就拒掉。

## 一个动作的路径

1. 客机把一条玩法意图变成 `CommandEnvelope` 发出去。指令覆盖生成、拾取、丢下、销毁、更新、转移、容器同步、烹饪、玩家状态、背负，以及敌人／流体事实。
2. 主机校验这个帧，把解码后的指令送过应用层的[准入](glossary.md)接缝（`KernelProtocolCommandHandler`）。
3. 内核检查运行纪元与幂等性，路由到正确的领域模块，产出「已接受的 `CommittedBatch`」或「有类型的 `Rejection`」。
4. 被接受的批次以 `CommittedBatchEnvelope` 广播给每个客机，主机同时把它投影进自己的运行时世界表、远端克隆与其他投影。
5. 每个客机把批次应用到自己的重放内核再投影出结果。`Apply` 按 `OperationId` 幂等，所以重复批次会被忽略。

## 加入与追赶

客机从不从头重放整局。它拿到一份检查点，以及检查点之后的那段日志尾：

```text
Host: checkpoint at revision N
Host: checkpoint chunks
Host: batches N+1..M (journal tail)
Guest: restore checkpoint → apply tail → Ready(M)
Host: start normal Batch/Stream
```

客机发现批次有缺口时，会发一个区间请求（`RequestRange` / `WireCommandKind.RangeRequest`）。如果请求的区间已经掉出主机那扇有界日志窗口，主机就直接再发一份检查点，而不是发一段它已经服务不了的区间。两条路都会把客机带到主机点名的那个修订号上，正常流量从那里继续（`KernelProtocolService.SendCheckpoint`、`RequestRange`、`HandleRangeRequest`，都在 `src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolService.cs`）。

## 状态按频率分层

| 层 | 例子 | 复制方式 | 日志 |
|---|---|---|---|
| 权威离散状态 | 归属、死亡、容器内容、陷阱触发 | 可靠批次 | 有 |
| 收敛连续状态 | 位置、速度、瞄准、区域流体体积 | 不可靠状态流 | 无 |
| 表现状态 | 动画相位、本地粒子、非关键音效 | 本地派生 | 无 |
| 检查点 | 完整的局／玩家／物品／世界实体／敌人／流体状态 | 可靠分片 | 另存一份 |

状态流只能更新已存在的收敛字段。它不能创建或销毁聚合体、不能改归属或容器关系、不能推进关键的玩法状态机 —— 一个允许被丢弃的值，永远不能决定谁拥有什么。后续逻辑要依赖的终态必须变成领域事件，搭批次走。

## 版本校验就是兼容边界

双方在加入[握手](glossary.md)时比对协议版本，数字不同，任一方都会结束这次尝试。整个兼容故事就这一句话：线上变更与版本号抬升在同一次改动里一起发布，所以代码永远不用留着旧形状。当前值活在 `src/CasualtiesUnknownOnline.Runtime/Protocol/ProtocolVersion.cs` —— 那个常量自己的注释就是线上变更日志，这一页刻意不复述任何数字。

协议自己的版本纪律：

- 显式的信封版本与检查点结构版本；
- 事件载荷用数字 id（`WireEventKind` 携带逐个事件的判别值）；
- 未知的关键事件拒绝；未知的非关键表现效果忽略；
- 黄金线上契约测试。

## 错误与恢复

| 失败 | 处理 |
|---|---|
| 指令重传 | 返回原来的判定 |
| 重复批次 | 按修订号／操作 id 静默幂等 |
| 批次缺口 | 请求日志区间 |
| 缺口过大 | 重发检查点 |
| 不变量失败 | 不提交；输出完整的事务诊断 |
| 运行纪元不对 | 丢弃 —— 指令、批次与状态流按各自信封／批次的纪元，检查点分片按主机在世界加入指令里公布的局身份 |
| 未知关键载荷 | 丢弃该帧并记日志；不实现自动断连 |
| 投影抛异常 | 该领域被标脏，由主线程泵从内核只读模型重建（`ProjectionHealthCoordinator`）；已提交的批次不回滚 |

## 指令被拒绝

被拒的指令以 `CommandEnvelope` 回来，带 `WirePayloadType.CommandRejected`（以及 `WireCommandKind.CommandRejected`），不再是独立帧类型。这替掉了旧的专用 `NetMsg.ItemReject` 帧；比如破坏方块时的掉落拒绝，现在用 `RejectionReason.BlockAlreadyBroken`。如果某个消息族以前有自己的拒绝消息，那条消息已经没了：答案跟着问题走，搭在同一个信封里（`src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolCommandHandler.cs`、`IKernelProtocolControl.cs`）。

## 信封之外的消息

不是每一帧都是那四种。会话与控制、世界变更与表现、角色表现、敌人快照与攻击、交易与聊天、模组接口，以及玩家交互请求，仍然各有自己的直连 `NetMsg` 帧。它们是现役的单路径协议，不是「权威状态还存了第二份」：需要其他玩家达成一致的玩法事实属于已提交批次，只有单个接收方会据以行动的东西才可以直连。

下面的 id 来自 `src/CasualtiesUnknownOnline.Runtime/Protocol/NetMsg.cs`，那份枚举是真相源；代码里用标识符，线上传的是 id。

**会话控制与成员**

| id | 消息 | 方向 | 它带什么 |
|---|---|---|---|
| 16 | `Handshake` | 客机 → 主机 | 协议版本、身份，以及客机声明的模组列表 |
| 17 | `HandshakeAck` | 主机 → 客机 | 对每一次握手都确认，重复的也确认 |
| 58 | `HandshakeAckAck` | 客机 → 主机 | 端到端确认；主机只在这一步把成员标成已握手 |
| 18 | `SceneState` | 主机 → 客机 | 场景／加载状态 |
| 20 | `WorldJoin` | 主机 → 客机 | 开始加载世界；携带检查点校验所依据的局身份 |
| 21 | `WorldReady` | 主机 → 客机 | 所有人都加载完了 —— 开始玩 |
| 32 | `PlayerJoin` | 主机 → 客机 | 自我激活加花名册公布 |
| 33 | `PlayerLeave` | 主机 → 客机 | 一名已同步成员离开 |
| 110 | `WorldSnapshotComplete` | 主机 → 客机 | 世界加入快照组结束 |
| 111 / 112 | `Kicked` / `Banned` | 主机 → 客机 | 该成员被踢出／被封禁 |
| 1 / 2 | `Ping` / `Pong` | 双向 | 诊断 |

**角色状态与表现**

| id | 消息 | 方向 | 它带什么 |
|---|---|---|---|
| 37 | `CharacterData` | 客机 → 主机上报，主机 → 客机恢复 | 1 Hz 角色快照 |
| 53 | `HostCharacterData` | 主机 → 客机 | 主机自己的 1 Hz 角色快照 |
| 93 | `LimbStateEvent` | 上报加转发 | 某个肢体锁存变了（断裂／接上／截断）；带事后完整的肢体与生命状态 |
| 94 | `CharacterSound` | 上报加转发 | 一次性动作音效（枪声还带后坐力） |
| 113 | `CharacterAttackAnim` | 上报加转发 | 本人攻击动画的重放 |
| 114 | `CharacterLandingVisual` | 上报加转发 | 落地动画与尘土的重放 |
| 120 | `CharacterRagdoll` | 上报加转发 | 本人的布娃娃姿态 |
| 121 | `WorldBloodSpawn` | 上报加转发 | 世界坐标上的一次性血迹贴花 |
| 123 | `PlayerColorUpdate` | 上报加转发 | 纯外观的标记颜色（不带权威） |
| 124 | `LocationPing` | 上报加转发 | 一次性的界面位置标记 |

**世界变更、方块与世界事件**

| id | 消息 | 方向 | 它带什么 |
|---|---|---|---|
| 40 | `BlockDamaged` | 上报加转发 | 方块的局部损伤 |
| 136 | `BlockDamageReport` | 客机 → 主机 | 该发送方对「主机还没记上的格子」累计的损伤量；主机按格回自己的值 |
| 41 | `WorldBlockState` | 主机 → 客机 | 世界加入时的完整方块状态（损伤表）快照 |
| 89 | `BlockDamageSnapshot` | 主机 → 客机 | 当前的局部方块损伤（世界加入与每 60 秒重发） |
| 42 | `BlockPlaced` | 上报加转发 | 一次方块写入；上报方收到自己的回声即为确认 |
| 51 | `BuildingEntityDamaged` | 上报加转发 | 建筑实体受损 |
| 52 | `BuildingEntityOpened` | 上报加转发 | 某个箱子／锁被打开 |
| 55 | `EarthquakeStart` | 主机 → 客机 | 地震开始（带时长）；客机抑制自己那份独立地震 |
| 57 | `KeypadCode` | 主机 → 客机 | 键盘锁密码，由主机在世界加入时生成 |
| 66 | `EntityEvent` | 上报加转发 | 一次被触发的陷阱／机关事件 |
| 68 | `EntitySpawned` | 上报加转发 | 一个运行时世界实体的创建 |
| 69 | `GeyserStateSnapshot` | 主机 → 客机 | 间歇泉的液体类型（世界加入与每 60 秒重发） |
| 79 | `TrapLayoutSnapshot` | 主机 → 客机 | 陷阱实体的权威位置 |
| 106 | `RadiationLineState` | 主机 → 客机 | 辐射线的 active／timeGone 状态 |
| 134 / 135 | `RuntimeEntitySnapshot` / `RuntimeEntityRejected` | 主机 → 客机／主机 → 上报方 | 运行时创建实体的绝对表／让挂起的重报停下来的拒绝 |
| 140 | `RunFacts` | 主机 → 客机 | 绝对的局计时基数与本层辐射计时，盖上局基线那代的戳 |

**流体、世界时间与教学**

| id | 消息 | 方向 | 它带什么 |
|---|---|---|---|
| 70 | `FluidRegion` | 主机 → 客机（不可靠） | 某片网格区域的绝对 RLE 快照：10 Hz 变化盒差分加 1 Hz 整视口兜底 |
| 71 | `FluidInteraction` | 上报加转发 | 一格被消耗的流体 |
| 96 | `FluidPresentation` | 主机 → 客机 | 某个网格格子上的一次推水或水流声 |
| 90 | `WorldTimeRequest` | 客机 → 主机 | 客机已在本地套用的时间速度 |
| 91 | `WorldTime` | 主机 → 客机 | 权威的世界时间速度 |
| 104 | `TutorialClawState` | 主机 → 客机（不可靠） | 教学爪子的表现快照（20 Hz，按序号门控） |

**物品与背包**

| id | 消息 | 方向 | 它带什么 |
|---|---|---|---|
| 64 | `ItemIdWatermark` | 双向 | 客机 → 主机：它已分配到的计数；主机 → 客机：它必须从此续上的授权 |
| 65 | `CarriedInventory` | 客机 → 主机 | 客机随身背包与它自己分配的 id |
| 105 | `DynamiteExplosion` | 上报加转发 | 一次炸药起爆（地形／建筑／物品事实各走自己的通道） |
| 97 | `PlayerInventoryTakeRequest` | 客机 → 主机 | 从另一名世界内玩家身上拿走一件随身物品 |
| 125 / 126 | `RemoteInventoryOperationRequest` / `RemoteInventoryApply` | 客机 → 主机／主机 → 本人 | 远端背包的一次手势（丢下／移进容器／倒出／合并／使用／穿戴／电池／槽位／收藏），以及在本人身体上的执行 |

**交易、说话与聊天**

| id | 消息 | 方向 | 它带什么 |
|---|---|---|---|
| 72 | `TraderState` | 主机 → 客机 | 商人的完整权威状态与库存（每次交互，外加可靠的 5 秒兜底） |
| 73 | `TraderAction` | 客机 → 主机 | 一次在本地执行的商人交互 |
| 115 | `TraderSwing` | 上报加转发 | 商人挥击的动画 |
| 74 | `SpeechMsg` | 上报加转发 | 一句对话气泡（文本是数据：说话方已经做过本地化、随机与失真） |
| 109 | `Chat` | 客机 → 主机上报，主机 → 客机转发 | 一行文字聊天 |

**敌人与制作**

| id | 消息 | 方向 | 它带什么 |
|---|---|---|---|
| 81 | `EnemySnapshot` | 主机 → 客机 | 完整的敌人快照（id、生成位置、运行时生成项） |
| 83 | `EnemyAttack` | 主机 → 客机 | 一次被公布的攻击（哪个敌人、哪种攻击、逐敌序号）；每个客机按自己的画面判定，并通过内核战斗事件上报终态 |
| 76 | `CraftReport` | 上报加转发 | 一整个制作操作：消耗与变化的材料加产物 |
| 77 | `RecipeUnlock` | 上报加转发 | 一次图纸解锁 |
| 139 | `RecipeUnlockSnapshot` | 双向 | 已解锁配方索引集合（世界加入、每 60 秒修复，以及结束客机重报的那次回答） |

**玩家交互与医疗操作**

| id | 消息 | 方向 | 它带什么 |
|---|---|---|---|
| 99 / 100 | `PlayerCarryStartRequest` / `PlayerCarryStopRequest` | 客机 → 主机 | 开始／停止背负一名昏迷或死亡的世界内玩家 |
| 102 | `PlayerHealRequest` | 客机 → 主机 | 用随身医疗物品治另一名世界内玩家 |
| 116 | `PlayerItemUseRequest` | 客机 → 主机 | 对另一名世界内玩家使用随身的饮品／食物 |
| 118 / 119 | `PlayerPushRequest` / `PlayerPushResult` | 客机 → 主机／主机 → 所有人 | 一次推搡请求与权威的推力结果 |
| 107 / 108 | `TraderRecruitRequest` / `TraderRecruitResult` | 客机 → 主机／主机 → 目标 | 在商人处招募一名死亡玩家，以及权威的事后身体状态 |
| 127–133 | `MedicalOperationStartRequest`、`StartAck`、`Update`、`State`、`EndRequest`、`EndCommitted`、`Cancel` | 操作方 ↔ 主机 ↔ 各客户端 | 医疗操作会话：主机持有登记表与预留、增量进度，以及唯一一次终态提交 |
| 137 / 138 | `MedicalOperationTargetCheckRequest` / `Answer` | 主机 → 目标／目标 → 主机 | 目标自己的客户端回答它这具活体身体允不允许开始这次操作 |

**模组与内核**

| id | 消息 | 方向 | 它带什么 |
|---|---|---|---|
| 75 | `ModMessage` | 上报加转发 | 共用的模组消息帧：发送模组的 id 加一份不透明[载荷](glossary.md) |
| 86 / 87 | `ModCommandRequest` / `ModCommandResult` | 客机 → 主机／主机 → 请求方 | 主机权威的模组命令执行与结果 |
| 122 | `KernelEnvelope` | 双向 | 四信封内核协议 |

## 相关阅读

- [四种信封](../internals/envelope-protocol.md) —— 内核为什么长成这个形状
- [状态流与快照](../internals/state-and-snapshots.md) —— 状态流怎么变成能读的状态
- [世界怎么存档](../internals/save-archive.md) —— 检查点落到磁盘上是什么
- [谁来决定玩家身上发生的事](../internals/judgment-ownership.md) —— 发出去之前由哪一侧判定
- [模组接口契约](mod-api.md) —— 面向模组的那些消息在契约里的位置
- [术语表](glossary.md) —— 信封、批次、检查点、状态流、运行纪元、修订号、握手

---

[文档总览](../README.md) > [参考](README.md) > 协议消息
