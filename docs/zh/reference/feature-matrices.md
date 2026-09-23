# 特性矩阵

[文档总览](../README.md) > [参考](README.md) > 特性矩阵

---

**读完这一页**，你能查到某个游戏特性到底是已同步、刻意不同步，还是仍然开放，也知道哪个文件才是这份答案的机器副本。物品行背后的游戏侧细节在[适配器背后的游戏](../internals/game-internals.md)；读数为什么永远不参与仲裁，在[谁来决定玩家身上发生的事](../internals/judgment-ownership.md)。

## 两张矩阵

| 矩阵 | 文件 | 形状 |
|---|---|---|
| 物品 | `docs/features/item-features-matrix.csv` | 一行一个物品（192 行），12 个特性列 |
| 实体 | `docs/features/entity-features-matrix.csv` | 一行一个实体（67 行），10 列 |

用工具读，不要手翻：列数错位的行在任何输出被信任之前就会被查出来。

```text
tools/item-features.ps1   validate | list | get <item> [feature] | set <item> <feature> <value> | add-item | remove-item | add-feature
tools/entity-features.ps1 validate | list | get <entity> [feature] | set <entity> <feature> <value> | add-entity | remove-entity | add-feature
```

- CSV 是 UTF-8 无 BOM；单元格里不能有半角逗号（多个值用 `/` 分隔）。
- 每次读之前先校验，每次写之后也校验 —— 列数错位会以退出码 1 中止，绝不静默。
- 叙事页是 `docs/features/items.md`、`docs/features/entities.md` 与 `docs/features/enemies.md`。实体叙事表与实体 CSV 由 `EntityFeaturesDocConsistencyTests` 交叉核对，所以两边必须同一次改动里一起刷新。

## `sync` 列的三个取值

| 取值 | 含义 |
|---|---|
| `covered` | 已同步；`path` 列写出覆盖它的机制（某个实体事件种类、领域类、消息） |
| `excluded` | 刻意不同步；`path` 列写出原因 |
| `missing` | 还没同步；`path` 列写出优先级 —— 一行 `missing` 就是一张待办 |

实体矩阵今天没有 `missing` 行：48 行 `covered`、19 行属于设计内 `excluded`。物品矩阵不写 `sync` 列，而是在每个特性列上打 `Y` 或留空，逐特性的结论见下表。

## 物品矩阵的列

| 列 | 它覆盖什么 |
|---|---|
| `battery` | 电池仓与电量消耗，包括电池类物品自身的电量 |
| `liquid` | 液体容器（`stack`） |
| `consumable` | 吃／喝／注射：每次使用的 `condition` 或 `stack`，归零即消失 |
| `durability` | 每次命中或每段时间的 `condition` 递减 |
| `modeswitch` | `CustomItemBehaviour.state` 的档位 |
| `payload` | `CustomItemBehaviour.data` —— 一个 `object[]` 载荷 |
| `gun` | `GunScript` 状态机 |
| `ammo` | `AmmoScript.rounds` |
| `geiger` | `GeigerCounterAudio.active` |
| `randomroll` | 生成时掷一次、之后固定的值 |
| `randomaction` | 每次动作的随机 —— 按设计就是各侧不同 |
| `bodycomponent` | 挂到身体或肢体上的组件 |

## 物品逐特性的同步状态

| 特性 | 状态 | 哪些东西在走线 |
|---|---|---|
| `battery` | 已覆盖 | 电量（`Item.condition`）在每条路径上都同步。驱动消耗的 `decayMultiplier` 由模式状态派生，所以在状态白名单落地之前，两端的消耗**速率**可能不一致；但电量本身一直跟着主机的镜像。 |
| `liquid` | 已覆盖 | `WaterContainerItem.stack` 在每条路径上以 `Liquids` 往返。恢复时直接重建这个栈：预制体的 `Awake` 已经填过默认内容，叠加式恢复会把它又读成「满」。 |
| `consumable` | 已覆盖 | `condition` 与 `stack` 同步；使用动作本身是客机本地事实 —— 使用上报被主机无条件采纳，因为拿使用证据去比对会把每一次使用都弹回来。 |
| `durability` | 已覆盖 | `condition`，每条路径。 |
| `modeswitch` | 已覆盖 | 状态白名单收下了 `CustomItemBehaviour`，而且只有公开的 `int state` 会走线 —— 私有的 `Item` 引用与 `object[] data` 数组不在其中。恢复按组件简单名匹配，十条携带路径共用同一对抓取／恢复。 |
| `payload` | 部分覆盖 | `object[]` 仍然不是受支持的通用可存盘字段种类，但里面两处持久玩法状态现在各有明确的线上面貌：液体离心机的冷却被当作合成的 `cooldown` 组件字段抓取（还带一个单帧重套用的标记，因为 `CustomItemBehaviour.Start` 会把数组重新初始化），炸药的点火锁存被当作合成的 `fuse` 组件字段抓取，而起爆本身走 `DynamiteExplosion`（`NetMsg` 105）。只有帧级的喷气背包油门仍留在本地。 |
| `gun` | 已覆盖 | `GunScript` 那九个 `[Saveable]` 字段在每条路径上抓取与恢复。持续性转变（开火、上膛／退膛、保险、装弹、卸弹）还会立刻通过既有的物品使用事实路径（`GunStateSync`）上报，所以主机的记录与同伴的克隆体在动作发生的那一刻就更新；1 Hz 角色快照仍是兜底。一次性的开火表现走 `CharacterSoundKind.GunFire` 事件与 `MuzzleFlashReplay`。 |
| `ammo` | 已覆盖 | `AmmoScript.rounds`。 |
| `geiger` | 已覆盖 | `GeigerCounterAudio.active`；使用动作同时镜像 `decayMultiplier`。 |
| `randomroll` | 已覆盖 | 生成时掷一次、之后固定的值（`EPdaScript.savedIndex`、`PlushScript.index`、`BlueprintScript.recipeIndex`、`NonDescriptCan` 的内容）。在隔离的生成流里，这次掷点每侧都是确定的；生成之外各侧各掷一次，`Start` 之后的抓取在套用时用主机的值盖掉。 |
| `randomaction` | 设计如此 | 每次动作消耗随机不是状态分歧：同一个动作在两台机器上落下不同结果，是游戏本身的设计（食物效果、枪械卡壳与散布、喷气背包湿油门与火焰透明度、暴露核心的死亡动画）。测试时「两个人吃同一个真菌块得到不同效果」是正确行为，不是 bug。 |
| `bodycomponent` | 已覆盖 | 挂到肢体或身体上的组件（止血带、夹板、冰袋、各类注射剂）以 `CharacterLimbMsg.Components` 走线 —— 与物品组件同一套组件状态形状 —— 搭在 1 Hz 角色快照、跨玩家使用物品的结果，以及重连恢复上；`LimbComponentStateCodec` 负责抓取与应用真正的游戏组件。 |

**被动效果物品**不进矩阵：它们持续修改身体，但自己不持有状态（水晶碎片那一组、自动变焦护目镜的变焦辅助、潜水服的湿润消耗、眼罩、玫瑰灯亮度）。它们没有东西需要走线。

## 制作：按操作同步，不按条目同步

制作族是按**操作**同步的：一个操作就是一条 `CraftReport`（`NetMsg` 76），携带它完整的终态 —— 被消耗或改变的材料加产物。主机逐条对照自己的世界表与转移表，整份应用、整份转发（排除来源方），绝不拆成逐条目的广播。

| 接口面 | 怎么上报，什么才算提交 |
|---|---|
| `Recipe.TryMake`（制作菜单） | 材料是背包加上 10 米内的地面物品。材料的去向来自配方数据；液体**产物**并入已有容器、不产生新物品，协调器的液体指纹差分把那个容器算成一条「已改变」。拒绝路径（材料不够）什么都不上报；仍然装着内容的可销毁材料会在原生消耗路径之前被 `CraftingContentsGuard` 拒掉，所以制作不会把那些内容弄丢 —— 玩家得先清空物品。首次制作的加成与失败分支的受伤搭 1 Hz 角色快照（可接受的延迟）。 |
| `Body.CombineItems`（拖拽合并） | 枪／弹匣与弹匣／子弹的装填会销毁被拖动的物品，它的帧末 `OnDestroy` 走销毁声明集合；状况合会改动两件物品；被拒的装填或「已满状况」的空操作什么都不提交。水那一支改为打开交互式的液体转移界面，转移完成之前什么都不上报（取消则无变化）。 |
| 图纸使用 | 图纸自身的销毁走既有的使用摘要；解锁走 `RecipeUnlock`（`NetMsg` 77），每一侧都应用到自己的进程级静态表。原生的「学会配方」弹窗对一次**新**解锁也会在其他侧重放，并在动手那一侧被抑制。一次性上报旁边还有一个绝对集合：`RecipeUnlockSnapshot`（`NetMsg` 139）在世界加入与每 60 秒修复组里发送主机实时的已解锁索引，客机持续重报自己那份集合，直到主机的集合包含它 —— 回填是静默的，因为追赶不是学会。 |
| 枚举型组件字段（编解码种类 6） | `GunScript.roundInChamber` 与弹药／射击模式枚举以底层 int 搭组件摘要走线。枪的实时状态本来就已通过公开的 bool／int 字段覆盖；这个枚举在此前被静默丢掉，现在由 `CraftCodecContractTests` 守住那张种类表。 |

仍然留在原处的缺口：非制作的容器材料倾洒（容器破碎、卸下全部子物品）刻意走容器物品领域，因为那会产生真正的新世界物品；不自动拾取的产物走物品领域的生成路径；思维清除的配方静态重置走实体领域；存档恢复造成的配方分歧不会发生，因为多人没有读档路径。加热炉烹饪已解决：一个 `CookItemCommand` 落在一个内核批次里。开火与上膛也已解决，走 `GunStateSync`。

## 已知的状态缺口

| 物品 | 状态 |
|---|---|
| `CustomItemBehaviour.data` | 只剩帧级的喷气背包油门留在本地；冷却与点火锁存已作为合成组件字段同步，起爆走自己的消息。 |
| 抓钩的 `fired`／`hookLatched`／`pulling` | 已同步：编解码的多人状态表在每条物品状态路径上携带这三个私有 bool，克隆体渲染出已发射的精灵并停用本人那份脚本。绳索与钩爪抛射物仍是本地表现。 |
| `WatchScript` 的计时器 | 设计内排除：它们只驱动本人玩家的界面与身体说话，渲染克隆体上的 `WatchScript` 已停用。 |
| `AutoPump.worn` | 设计内排除：它只驱动本人玩家的血压效果，渲染克隆体上的 `AutoPump` 已停用。 |
| 同伴视角的渲染 | 克隆体渲染器按预制体实例化并套用快照里的组件状态，所以远端玩家手里的手电会按真实档位渲染、发射过的抓钩会渲染出已发射精灵。仅表现路径。 |
| 关键帧上的世界物品组件状态 | 已解决：周期快照（基础 5 秒，自适应调速器在压力下最多拉到 10 秒）会在已有世界物品的顶层状态与主机表不一致时重新对齐。位置仍归位置流，容器内容仍归容器消息族。 |

## 实体矩阵的列

| 列 | 含义 |
|---|---|
| `type` | 所属族：`trap`、`lifepod`、`unlock`、`trade`、`crystal`、`fluid`、`environment`、`building`、`creature` |
| `trigger` | 驱动该实体状态的交互面：`collide`、`trigger`、`click`、`detect`、`field`、`story`、`use` |
| `state` | 两侧之间可能分歧的字段；`none` = 无状态 |
| `one-shot` | `yes` = 实体消耗掉自己、不能再次触发，所以这次消耗必须只共享一次 |
| `damages` | `yes` = 该实体造成伤害（写肢体／身体数值或销毁物品） |
| `random` | `yes` = 该实体消耗随机流：生成期的掷点在隔离流内是确定的，运行期的掷点留在本地、结果作为状态走线 |
| `replay` | 接收侧为该事件播什么 —— 也就是「别人看不看得见、听不听得见」这一列 |
| `sync` | `covered`、`excluded` 或 `missing` |
| `path` | 覆盖它的机制，或排除原因，或 `missing` 行的优先级 |

## 实体按族列出

**陷阱** —— 走实体事件通道：触发侧算完整原始效果、上报、主机应用、转发，接收侧重放。

| 实体 | 同步 | 路径 |
|---|---|---|
| MineScript | 已覆盖 | `MinePressed` + `MineExploded` |
| SpikeStabberScript | 已覆盖 | `SpikeStabbed` |
| BearTrap | 已覆盖 | `BearTrapClamped`／`BearTrapReleased` |
| BarbedFence | 已覆盖 | `BarbedFenceHit` |
| CoilScript | 已覆盖 | `CoilShocked` |
| CactusScript | 已覆盖 | `CactusHit` 加一次静默的 `BuildingEntityDamaged` 转发 |
| JumpPadScript | 已覆盖 | `JumpPadLaunched` |
| StalactiteDropper | 已覆盖 | `StalactiteDropped` |
| GeyserScript | 已覆盖 | `GeyserActivated`；液体类型走 `GeyserStateSnapshot` |
| SoundCannon | 已覆盖 | `SoundCannonFired` |
| TurretScript | 已覆盖 | `TurretFired`／`TurretSelfDestructed` |
| CrystalElectric | 已覆盖 | `CrystalElectricShocked` |
| CrystalFragile | 已覆盖 | `CrystalFragileBroken` |
| CaveTickSpawner | 已覆盖 | `CaveTicksSpawned`；蜘蛛走 `EntitySpawned` 加运行时生成绑定 |
| BananaPlantSlip | 已覆盖 | `BananaPlantSlip` |
| GrabberPlant | 已覆盖 | 布局键加 `EnemyEffectMsg` 终态 |

**救生舱内部**

| 实体 | 同步 | 路径 |
|---|---|---|
| ShuttleStartOpen | 已覆盖 | `ShuttleDoorOpened` |
| LifepodController（加热按钮） | 已覆盖 | `LifepodHeatChanged`，附加数据里带 `heatState` |
| LifepodShower | 已覆盖 | `LifepodShowerActivated`（一次消耗只共享一次） |
| Heater（烹饪分支） | 已覆盖 | `CookItemCommand` 内核批次；温度字段作为本地身体效果被排除 |

**解锁** —— 一次性进度，一旦分歧就是硬性玩法差异。

| 实体 | 同步 | 路径 |
|---|---|---|
| BioTerminalScript（血液解锁） | 已覆盖 | `BioTerminalUnlocked` |
| ScrapEaterScript | 已覆盖 | `ScrapEaterProgress`，附加数据里带进度 |
| MedStationScript | 已覆盖 | `MedStationHealed` |
| BatteryRecharger | 已覆盖 | `BatteryInserted`；电量本身走物品领域的状况 |

**交易**

| 实体 | 同步 | 路径 |
|---|---|---|
| TraderScript | 已覆盖 | 交易领域（`TradeStateSync`／`TradeExecutor`）加 `TraderSwing`；主机算出的状态在每次交互以及 5 秒兜底时整体覆盖，动手一侧完整跑一遍游戏方法并上报 `TraderAction` |
| Talker | 已覆盖 | `SpeechMsg`（`NetMsg` 74）—— 实体键加文本 id，在克隆体一侧重放气泡 |
| LampScript | 已覆盖 | 交易领域 —— 固定数值的声望损失按广播的基数在两侧各跑一遍 |

**水晶** —— 效果指派发生在隔离的生成流里，所以两侧拿到同一种水晶，只有运行期行为需要同步。

| 实体 | 同步 | 路径 |
|---|---|---|
| CrystalUnstable | 已覆盖 | `CrystalUnstableExploded` + `CrystalUnstableTicked`（爆炸前的滴答是瞬态，爆炸才是持久的消耗） |
| CrystalMetamorphic | 已覆盖 | `CrystalMetamorphicTriggered` —— 死亡与掉落搭该事件 |
| CrystalMimic | 已覆盖 | `CrystalMimicTriggered`；敌人走 `EntitySpawned` 加运行时生成绑定 |
| CrystalShy | 已覆盖 | `CrystalShySwapped` |
| CrystalTeleport | 已覆盖 | `CrystalTeleportTriggered` 加 20 Hz 玩家流负责身体传送 |
| CrystalEMP | 已覆盖 | `CrystalEMPActivated` |
| CrystalDripping | 已覆盖 | 滴落写入走流体领域 |
| CrystalBurning、CrystalTemperature、CrystalSeptic、CrystalHealing、CrystalBlinding、CrystalIrradiated | 排除 | 本地身体效果 |
| CrystalGravity、CrystalKinetic | 排除 | 本地物理 |

**流体世界网格**

| 实体 | 同步 | 路径 |
|---|---|---|
| FluidManager | 已覆盖 | `FluidRegion`／`FluidWorldSync` 加 `FluidPresentation`（`NetMsg` 96）；主机模拟每个成员的视口 |
| OilPipeScript | 已覆盖 | 产油走主机的流体流 |
| LifepodPump | 已覆盖 | 泵的写入走主机的流体流 |

**环境**

| 实体 | 同步 | 路径 |
|---|---|---|
| CorpseScript | 已覆盖 | 建筑实体／生成物品权威 —— 尸体生命与战利品 |
| TutorialHandler | 已覆盖 | `TutorialClawState` —— 主机 20 Hz 的爪子流；各侧自己的课程与道具按设计保留 |
| GrapplingHook | 已覆盖 | 物品组件状态加 `RemoteItemPresentation` |
| WaterPusher | 已覆盖 | `FluidPresentation` |
| XalorisScript | 已覆盖 | `EnemyEffectMsg` 的败血滴答，0.5 秒的边沿终态 |
| SurvivorNote | 排除 | 本地界面加本地时间尺度（已接受的差异） |
| Climbable、GeigeFruitScript、LeadbushScript、RadioactiveObject | 排除 | 本地身体 |
| BounceShroom | 排除 | 本地物理 |
| CampfireAnimation | 排除 | 纯表现 |
| ItemLock | 排除 | 只是标记 |

**建筑**

| 实体 | 同步 | 路径 |
|---|---|---|
| Openable（锁与箱子） | 已覆盖 | `BuildingEntityOpened` |
| BuildingEntity（受击） | 已覆盖 | `BuildingEntityDamaged`，非攻击方视角重放受击闪红 |
| DrillPod | 排除 | 世界加入粒度：修理与世界重置 |
| GunmineScript、SawbladeScript | 排除 | 手工放置，在生成流之外 |

序列化下来的 `Openable` 组件都在 `resources.assets` 里（17 个实例、11 个根预制体）：`isKeypad` 只在 `dropcapsule` 预制体以及 `Structures/BrickLoot` 里那两个嵌套的 `dropcapsule` 道具上为 true，`instantOpen` 只在 `foodbox`（根预制体，以及 `BioContainer` 里那份嵌套副本）上为 true。其余 `Openable` 都是撬锁。

**生物** —— 主机权威的敌人领域，它自己的页面是 `docs/features/enemies.md`。

| 实体 | 同步 | 路径 |
|---|---|---|
| SpiderHandler | 已覆盖 | 敌人状态流、由本地判定的 `EnemyAttack` 公布、撕咬事件、爪击动画重放 |
| CaveTicks | 已覆盖 | 与 SpiderHandler 同一族 |
| ElderThornbackBehaviour | 已覆盖 | 敌人状态流加恐怖事件 |
| CrystalEnemy | 已覆盖 | 敌人状态流（蓄力预警）、由本地判定的 `EnemyAttack` 公布，以及内核的扑击结果事件；运行期色调走 `EntitySpawned`／`EnemySnapshot` |

敌人的移动用的是 Unity 物理与本地随机数，两侧各自模拟必然发散：所以客机根本不模拟敌人物理，只渲染由主机状态驱动的冻结副本。`xaloris` 上的 `Heater` 温度字段按设计排除 —— 它只写本机玩家的体温。

## 相关阅读

- [适配器背后的游戏](../internals/game-internals.md) —— 这些行所依赖的游戏机制
- [谁来决定玩家身上发生的事](../internals/judgment-ownership.md) —— 为什么读数永远不会变成结论
- [适配器与游戏更新](../internals/adapter-and-updates.md) —— 一次游戏更新会挪动这些行底下的什么
- [协议消息](protocol-messages.md) —— `path` 列里点到的 id 与通道
- [配置项](configuration.md) —— 塑造这些矩阵所描述那一局的设置
- [术语表](glossary.md) —— 特性矩阵、投影、远端克隆体、确定性、种子

---

[文档总览](../README.md) > [参考](README.md) > 特性矩阵
