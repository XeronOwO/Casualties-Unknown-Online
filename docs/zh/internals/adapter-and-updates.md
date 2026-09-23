# 适配器与游戏更新

[文档总览](../README.md) > [弄懂原理](README.md) > 适配器与游戏更新

---

**读完这一页**，你能说清一次游戏更新被允许弄坏什么、[适配器](../reference/glossary.md)向上面的层承诺了什么，以及为什么一个没装上的补丁永远不会悄悄跑起来。先读 [CUO 的整体结构](architecture-overview.md) —— 这一页讲的是那套分层里面向游戏的一半。

## 只有一个地方会被弄坏

游戏是会动的靶子：一次更新会改名一个类型、挪走一个方法、改掉一个字段。CUO 的设计让这种变动只落在一个项目里。`src/CasualtiesUnknownOnline.Runtime/GameAdapter/IGameAdapter.cs` 写明了这条边界：它是唯一认识游戏私有类型的层；每个游戏版本一份实现，运行时定契约、`CUO.GameAdapter` 项目实现它们。

适配器之上的所有东西 —— 协议、会话、内核、模组接口 —— 不需要引用任何一个游戏程序集就能编译。这不是约定：`GameAssemblyReferenceGateTests` 会读解决方案，只要有适配器之外的项目引用了游戏程序集，构建就失败。

## 边界是一组能力端口

适配器接口自己不声明任何成员。它是十二个能力端口（外加只管生命周期的 `IDisposable`）的组合，使用方只解析自己真正用到的那一个：

```text
IGameAdapter : IGameIntegrationLifecycle, IAdapterCapabilityQuery, IWorldPresenceQuery,
               IStartGateState, ILocalHealItemQuery, ITraderRecruitRequest, INativeInputBlocker,
               IRemoteInventoryPresentation, IRemoteMedicalPresentation, IPlayerAnchorQuery,
               IJoinFlowPresentation, ICarryPresentationPump, IDisposable
```

类型注释写明了为什么长这样、以及为什么它被冻结：面向某个游戏版本的适配器因此按能力各自实现，测试替身只实现它要顶替的那一项；往这里加成员会让 `AdapterCapabilityPortShapeTests` 失败。新能力是新增一个端口、再在列表里登记一条，绝不是把这个聚合接口撑大。

同一条规则也管到了补丁：它们通过一个桥读取运行时，而某个领域的接缝可以从桥上拆出去，而不是把公共的那道墙拓宽 —— `IFluidPatchPort` 就是现成的例子：`src/CasualtiesUnknownOnline.GameAdapter/IPatchBridge.cs` 里的聚合既没声明它、也没组合它，所以照着聚合写出来的调用根本够不到那些成员。往聚合上加成员是构建失败，不是一条复核意见。

## 补丁集是全装或全不装

启动时适配器应用自己的 Harmony 补丁、装上动态补丁，然后**逐个核对每个声明的目标是否真的落上了**。摘自 `src/CasualtiesUnknownOnline.GameAdapter/Patches/PatchInstallLifecycle.cs`：

```text
Never let a failed patch silently run: verify every patch class
actually landed on its target (a game update that breaks a target
must fail loud — a silently missing hook is how sync bugs hide).
```

出现阻断性失败时，整次安装被拒绝，并在返回之前卸载补丁，免得一个只装了一半的补丁集跑起来。这次尝试的结果会发布成一份能力报告 —— 探测了哪些游戏类型、多少个补丁目标落上、哪些行缺失 —— 由插件在启动时记进日志。所以一次挪走了方法的游戏更新，给出的是一条响亮、具体的拒绝，而不是一场莫名其妙不同步某样东西的会话。

## 吸收一次游戏更新

工作量被边界圈住了，顺序是：

1. **先搞清楚变了什么。** 探测读的是声明过的游戏类型；类型缺失、补丁目标不存在，都会被点名报告出来，而不是过后变成一条同步缺陷。
2. **只改适配器。** 改名、挪方法、改字段，都在 `CasualtiesUnknownOnline.GameAdapter` 里吸收掉。线上格式、内核与模组接口都不动。
3. **优先运行时探测，而不是写死偏移。** 常规规则是在运行时查游戏的 API，而不是把偏移量或私有字段写死，因为写死的偏移每次更新都会断 —— 反编译树是研究材料，不是稳定地址的来源。
4. **让失败保持响亮。** 某个能力在这个版本上确实做不了，就写进报告；不会悄悄退化成一个半好用的会话。

## 承诺了什么，没承诺什么

- **`Abstractions` 才是承诺。** 它是唯一带稳定性契约的接口面，而且即便在那里，公开表面也是一份登记在册的[基线](../reference/glossary.md)：增删一个成员是经过复核的动作（`ApiSurfaceGateTests` 拿当前表面与 `docs/api/abstractions-api-baseline.txt` 比对）。
- **`Runtime` 与 `GameAdapter` 是实现。** 模组可以给它们打[补丁](../reference/glossary.md)，而一个在 CUO 更新之后失效的补丁，是那个模组自己的问题，不是被违背的承诺。
- **游戏自己的代码什么都不是承诺。** 适配器绑的东西属于游戏；一次游戏更新可以在 CUO 毫不知情的情况下弄坏它 —— 这正是这道边界存在的理由。

## 靠什么守住边界

`GameAssemblyReferenceGateTests`（只有适配器碰游戏程序集）、`ProjectDirectionGateTests`（分层表）、`AdapterCapabilityPortShapeTests`（在行为套件 `tests/CasualtiesUnknownOnline.Tests` 里）与 `PatchBridgePortShapeGateTests`（在门禁工程里）—— 它们盯着接缝不会变宽 —— 以及 `SourceShapeGateTests`（内核里不许出现游戏与 Unity 引用）。规则到门禁的对应表在 `docs/evidence/normative-gates.md`。

## 相关阅读

- [适配器背后的游戏](game-internals.md) —— 适配器到底绑在什么上面
- [CUO 的整体结构](architecture-overview.md) —— 适配器之上那些稳定的层
- [模组的一生](mod-loading-lifecycle.md) —— 模组能声明什么、能打什么补丁
- [仓库地图与坑](../contributing/repository-map-and-pitfalls.md) —— 一个文件该属于哪个项目
- [术语表](../reference/glossary.md) —— 适配器、运行时、原生、补丁、内核

---

[文档总览](../README.md) > [弄懂原理](README.md) > 适配器与游戏更新
