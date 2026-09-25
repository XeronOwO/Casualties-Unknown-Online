# 门禁与一个改动必须满足的规则

[文档总览](../README.md) > [贡献者文档](README.md) > 门禁与一个改动必须满足的规则

---

**读完这一页**，你能说出一个改动要满足哪条规则、不满足时哪个[门禁（gate）](../reference/glossary.md)会拦下它，以及一个新门禁要长什么样才会被接受。
命令本身在[构建、测试与部署](build-and-test.md)。

## 规则靠什么执行

每条绑定规则都落在三种机制之一，[规则到门禁映射](../../evidence/normative-gates.md)登记了哪条规则走哪一种：

1. **构建与格式化** —— `.editorconfig` 把样式规则提到错误级别，各工程文件里的 `EnforceCodeStyleInBuild`
   让编译器真的执行它们，再加上“警告即错误”。
2. **门禁工程** —— `tests/CasualtiesUnknownOnline.NormativeGates.Tests`，作为 `dotnet test` 的一部分运行。
   它们是 C# xUnit 测试；原先那批 `tools/check-*.ps1` 脚本已经移植进来并删除。
3. **复核（review）** —— 机器判不了、一判就大量误报的规则：这一页到底有没有把人教会、论证有没有证据撑、自查是不是独立的。

门禁证明的是“存在且形状正确”，它证明不了质量；全绿不等于复核过。

## 门禁清单

| 它会拦下什么 | 门禁 |
|---|---|
| 单类型聚合超过 600 行、一个类型里超过五个布尔状态字段，或一个文件里有两个顶层类型 | `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` |
| 内核（kernel）伸手去碰运行时（Runtime）/游戏/网络类型；投影表被非属主改写；已删除的双架构标记复活；某个 `GameCommand` 没有权威策略；内核状态用字符串做键 | `SourceShapeGateTests.GameStateIsolation_…`、`…ItemAuthority_NoDirectProjectionMutation`、`…NoLegacy_…`、`…CommandAuthority_…`、`…KernelShape_…` |
| 向上引用、运行时绕过 Application 层、未登记的新工程、消费者向下伸手 | `ProjectDirectionGateTests.Tree_FollowsTheDeclaredProjectDirection`（声明表：`ProjectDirectionPolicy.AllowedReferences`） |
| 明明可以用 `using` 或别名，却写全限定类型名 | `FullyQualifiedNameGateTests`（Roslyn） |
| `Abstractions` 的公开成员被新增、改动或悄悄删掉，而受复核的基线（baseline）还写着旧样子 | `ApiSurfaceGateTests.AbstractionsPublicSurface_MatchesTheReviewedBaseline`（附普查下限与匹配器契约用例） |
| git 会带走的任何文件里出现本机绝对路径 | `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths` |
| 交付清单里有必勾项没勾，或勾了 FORBIDDEN 那一行 | `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` |
| 某个 wire 判别值没进同步覆盖矩阵、某行没有结论或证据、证据引文和源文件已经不一致 | `SyncCoverageGateTests.SyncCoverageMatrix_…`、`…SyncCoverageEvidence_EveryQuoteMatchesItsSourceLine` |
| 测试类在 `GameAssembly` 集合之外改动进程级静态量 | `TestIsolationGateTests.StaticGameStateMutations_JoinTheGameAssemblyCollection` |
| 单个测试类超过 40 个真实用例或数据行 | `TestClassSizeGateTests.NoTestClass_ExceedsTheCaseLimit` |
| 现役治理文档复述协议版本号，而不是指向 `ProtocolVersion.Current` | `ProtocolNumberGateTests.LiveGovernanceDocuments_DoNotRestateTheProtocolVersionNumber` |
| [补丁（patch）](../reference/glossary.md)接缝的普查数、组合方式或端口可达性和冻结声明漂移 | `PatchBridgePortShapeGateTests.…` |
| 运行时的会话服务没有通过 `ISessionReset` 响应会话结束，或某个生命周期订阅少了退订的另一半 | `SessionLifecycleGateTests.…` |
| backlog 索引行不再是“指针”、票面状态和所在目录不符、文档按行号引用 `AGENTS.md` | `BacklogIntegrityGateTests.…` |
| 契约工具恢复出来的补丁目标行和适配器自己声明的行不一致 | `PatchContractRowParityTests.ToolRows_EqualTheAdaptersOwnContractRows` |

这张表里有两行——`TestClassSizeGateTests` 与 `PatchContractRowParityTests`——声明在行为测试套件
（`tests/CasualtiesUnknownOnline.Tests`）里，而不是门禁工程里。`dotnet test` 两个工程都会跑，
所以这个区分不影响你怎么跑它们；只有当你去找声明本身时才有意义。

## 工程纪律

这里的标准是“做好，而不只是做出来”。交付前要过三道复核：**架构**（职责单一、依赖干净）、**测试**（行为有运行时验证）、
**可维护性**（下一个人读得懂、改得动）。“能跑”是地板，不是目标。

- 不走捷径：有正路就走正路。手工绕过、写死、复制粘贴、跳过测试，都是在向未来借债。
- 务实不是挡箭牌：一个次优选择要有架构上的理由，不能是“没时间”。对的事一次做对。
- 治根因而不是叠补丁：先问架构上能不能直接消掉这个成因；不要默认堆功能、堆补丁，也不要因为怕改动大就回避该做的重构——
  代价只是转移，不会消失。
- 务实地为未来留余地：给看得见的演进留空间，但不要为想象中的未来预建。“以后再说”不是默认借口。
- 测试要覆盖核心场景，加上边界、异常和失败路径。只有顺路的用例永远证明不了“做好了”。
- 每条关键路径和逻辑分支都必须可观测。日志级别按触发频率选（高频 → Verbose/Debug，低频或异常 → Warn/Error），
  日志要带够定位失败所需的信息：分支、状态、id、输入、结果。不可观测的关键路径算没做完；
  标准是“打一次日志就够”，而不是“改代码 → 部署 → 复现 → 再加日志”。

## 十四条绑定约定

1. **默认英文**：代码、注释和要提交的文档一律英文。成对的中文页面是刻意的例外，见[文档编写规范](documentation-standard.md)。
2. **现代惯用 C#**：`var`、可空、`is null` / `is not null`、集合表达式，命名冲突用 `using` 别名。
   **Unity 对象是例外**：必须用 `== null` / `!= null`，因为它的重载能识别被场景重载销毁的对象。
   能用 switch 表达式就别写 switch 语句和长 `if`/`else` 链。
3. **改动要有证据**：动代码之前先引反编译源码（`reversing/`，写文件加行号——那棵树从不编辑，行号稳定）。
   对我们自己的 `src/` 和 `tests/`，引路径加原文引用，**绝不写行号**：行号会被它上面任何一次编辑顶掉，
   而引用原文才是论断真正立得住的地方。治根因，不治症状。
4. **本机绝对路径红线**：任何本机绝对路径都不许进 git。盘符路径、UNC 路径，以及以 home、user、temp、var、opt
   开头的 Unix 风格绝对路径，都不许出现在被跟踪的文件里；本机路径只写在进不了 git 的 `AGENTS.local.md`，
   或用 `<game-dir>` 这类占位符。历史遗留的已跟踪绝对路径要删掉，不能当历史债留着。
5. **自我学习**：可复用、可推广的知识记进 `AGENTS.md`、`docs/` 或记忆里，要有取舍。
6. **补丁钩子只报告确实发生的写入**：Prefix 吞掉一次写入，就不能让同一个 Postfix 把它报成成功。
   要么重新读回写入后的状态，要么把结论显式传下去。
7. **优先用 `using` 与别名**，别写全限定类型名；只有在实在没法用 `using` 时才写全名（例如 HotRepl 求值）。
8. **游戏已有原生界面就复用**（背包、医疗面板）。不要另做一套平行的 CUO 界面去取代它；优先做薄适配层加补丁。
   原生界面确实复用不了时，把具体阻塞点连同证据记下来，拿到用户方向之后再自建界面。
9. **功能设计要问，实现细节自己做。** 功能设计有歧义、有多种合理做法，或存在用户可见的取舍时，先调研参考实现，
   仍然要向用户确认方向再动手。设计和需求清楚之后，按规范做，不要拿日常实现细节反复问。
   “下一个做哪张票”不属于这类决策——按票面优先级、依赖关系和交接提示词的建议自己定。
10. **future 里的条目不是工作项。** `future/` 的含义是“已决定推迟”，不是“待实现”：除非用户把它提升出来，
    否则不要主动去做。
11. **大工程或多阶段工程要拆阶段**，每一阶段产出可验证的结果。不要用一张含糊的大票把多阶段工作藏起来；
    拆开之后，没有独立增量价值的总票可以删掉。
12. **必须留在仓库里的空目录**放一个 0 字节的 `.gitkeep`；不要用占位文档代替，也不要留一个没被跟踪的空目录。
13. **扩展方法一律用 C# 14 的 `extension` 语法。** 它是经典 `this X` 形式的上位替代（方法外加属性、静态成员和运算符，
    接收者只写一次），编译出的调用点完全相同，所以没有理由再写经典形式。看见一处就顺手迁移：
    新写一个经典声明会被 `SourceShapeGateTests.ExtensionMethods_UseTheCsharp14ExtensionSyntax` 判红。
14. **可见性取最小，稳定性要声明。** 类型和成员默认用实现所需的最窄可见性；只有经过设计、写进文档并通过复核的能力，
    才算对第三方公开的契约。`Runtime` 和 `GameAdapter` 是[模组（mod）](../reference/glossary.md)可以打补丁但从未被承诺的实现。
    `Abstractions` 的公开面是受门禁保护的基线（[abstractions-api-baseline.txt](../../contracts/abstractions-api-baseline.txt)）：
    增一项或删一项都会失败，直到基线被复核并更新；删除必须写明理由；不是 `Stable` 的面要用 `[ApiStability]` 声明自己的级别
    （[advanced-modification-policy.md](../../api/advanced-modification-policy.md)）。

## 兼容边界

兼容性从来不是设计输入。边界就是[握手（handshake）](../reference/glossary.md)时的[协议版本](../reference/glossary.md)校验：主机丢弃 `HandshakeMsg.Protocol`
不一致的对端，客机在 `HandshakeAckMsg.Protocol` 不一致时结束会话。所以任何改动都不必为了避开版本号提升而保留旧线路格式、
旧存档形状或遗留字段，也不必因此选一个更弱的机制：该改线路就改，并在同一次改动里提升 `ProtocolVersion.Current`
（编号政策在[活跃决策台账](../../decisions/active.md)）。“没有改 wire”“主机零改动”是可以记录的事实，
但**永远不能**成为设计论证里的优点或约束——发布前发布后都一样。

版本号只存在于一处：`ProtocolVersion.Current`，它的文档注释就是改动记录；现役治理文档指向这个常量而不复述数字，
这正是 `ProtocolNumberGateTests` 在检查的事情。

## 新门禁怎么写

门禁的声明必须等于它真能触达的范围：

- 扫描面从既有的真相源派生（解决方案文件、特性标注、登记表），不要用手工维护、迟早会漂的清单。
- 留一个普查下限，这样“扫描器悄悄看不到树了”会失败，而不是静默通过。
- 用正反样本钉住匹配器：一个必须被抓到的构造样本，一个必须放过的反例。
- 不要扫门禁自己的测试数据；优先用匹配“事实”的正则，而不是匹配周围文本形状的正则。
- 改过匹配器或扫描面之后，重跑反例，然后再看三个状态：上一版提交下应该变红、修好的工作区应该变绿、全仓不该有误报。

## 怎么确认成了

- `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` 退出码为 `0`。
- 有门禁的规则在[规则到门禁映射](../../evidence/normative-gates.md)里有对应行；没有门禁的规则在那里被标成复核/流程类，
  那是对规则的说明，不是遗漏。
- 改动在最后一次提交前已经把[交付清单](../../evidence/delivery-checklist.md)走完。

## 相关阅读

- [构建、测试与部署](build-and-test.md) —— 命令本身，以及测试套件给自己定的契约
- [仓库地图与坑](repository-map-and-pitfalls.md) —— 这些门禁在维护的依赖方向
- [复核与交付](review-and-delivery.md) —— 门禁查不到的东西靠流程兜住
- [规则到门禁映射](../../evidence/normative-gates.md) —— 每条规则对应哪个门禁或哪道流程
- [活跃决策台账](../../decisions/active.md) —— 这些规则背后的已记录决策

---

[文档总览](../README.md) > [贡献者文档](README.md) > 门禁与一个改动必须满足的规则
