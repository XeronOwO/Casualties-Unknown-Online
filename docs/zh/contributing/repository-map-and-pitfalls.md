# 仓库地图与坑

[文档总览](../README.md) > [贡献者文档](README.md) > 仓库地图与坑

---

**读完这一页**，你能判断一个新文件该放进哪个工程、它能引用什么，以及这棵树里已知的陷阱有哪些。
还没构建过解决方案的话，先看[构建、测试与部署](build-and-test.md)。

## 有哪些工程

```text
src/CasualtiesUnknownOnline.Abstractions/      # 公开接口;模组唯一可以引用的包
src/CasualtiesUnknownOnline.GameState/         # 确定性内核;不引用任何其它工程
src/CasualtiesUnknownOnline.Protocol/          # 只有线路 DTO 与编解码;不引用内核/运行时
src/CasualtiesUnknownOnline.Application/       # 准入接缝 + 内核复制
src/CasualtiesUnknownOnline.Runtime/           # DI、日志、BepInEx、Steam、会话;绝不碰游戏程序集
src/CasualtiesUnknownOnline.GameAdapter/       # 唯一引用游戏程序集的工程;HarmonyX
src/CasualtiesUnknownOnline.Plugin/            # BepInEx 5 入口;很薄的生命周期驱动
src/CasualtiesUnknownOnline.ModExample/        # 示例模组;只引用 Abstractions
src/CasualtiesUnknownOnline.PinyinSearch/      # 卫星模组:绑游戏的那一半
src/CasualtiesUnknownOnline.PinyinSearch.Core/ # 同一个模组:不碰游戏的那一半
tools/CasualtiesUnknownOnline.ContractTool/    # 契约行生成器;只读元数据,不引用任何 src/ 工程
CasualtiesUnknownOnline.slnx                   # 解决方案
references/                                    # 游戏程序集,不进 git,按需放入
reversing/                                     # 逆向工作区,不进 git,从不编辑
artifacts/                                     # 门禁与工具产物,不进 git
```

## 依赖方向是声明出来的，而且有门禁（gate）

`GameState`、`Protocol`、`Abstractions` 是底层，不引用任何其它工程。`Application` 是从内核（kernel）向上的唯一通路，
它只能引用 `GameState` 与 `Protocol`；`Runtime` 经 `Application` 到达内核（Runtime → Application → GameState），
自己不声明对 `GameState` 的引用，这样“某条指令算不算合法”这类会话级判断只有一个归属，而不是每个入口各判一次。
`GameAdapter` 与 `Plugin` 在 `Runtime` 之上，永远不直接够到 `GameState`。测试与工具属于消费者，不受这条线约束：
`tools/CasualtiesUnknownOnline.ContractTool` 就是其中一个——它把游戏构建当元数据读进来，生成契约行，供门禁和适配器自己声明的行比对；
没有任何 `src/` 工程引用它，它也不引用任何 `src/` 工程。

声明表是 `tests/CasualtiesUnknownOnline.NormativeGates.Tests/ProjectDirectionPolicy.cs` 里的
`ProjectDirectionPolicy.AllowedReferences`，消费者清单是同一文件里的 `ConsumerProjects`。
新增一条引用——或者新增一个工程——就要在同一次改动里在那里声明，否则 `ProjectDirectionGateTests` 会失败；
它的构造用例钉住了四种拒绝：向上引用、运行时（Runtime）绕过那一层、未登记的新工程、消费者向下伸手。

卫星模组（satellite mod）住在本仓库里、和框架并排，守同一条线：只有绑游戏的那一半引用游戏程序集，不碰游戏的那一半才是测试工程直接引用的部分。

## 一个新系统该待在哪里

[修改策略](../reference/modification-policy.md)讲的是模组可以怎样绑定 CUO；这一节讲的是**一个特性**该待在哪里 —— 也就是贡献者真正会问的那个问题：「这东西该自成模组吗？」。四层，按离框架的远近排列：

| 层 | 它是什么 | 随插件发布吗 | 例子 |
|---|---|---|---|
| 框架核心 | 框架自身运转需要的能力：它的控制面、它的管理与安全面、它自己的结果送到玩家那里、以及共享的模拟 | 是 | 控制台、存档层、会话／世界／实体各领域、模组加载器 |
| 卫星模组 | 面向游戏、本身没有会话词汇的特性；CUO 没装它照样成立 | 否 —— 它自成模组 | 拼音搜索（`src/CasualtiesUnknownOnline.PinyinSearch*`） |
| 仓库工具 | 运行期不需要游戏，也不进插件的依赖图；它服务于开发与验证 | 否 —— 而且它不是模组 | 契约工具链（`tools/CasualtiesUnknownOnline.ContractTool`） |
| 可复用组件 | 几处消费者能共用的机器部件，本身没有会话词汇 | 取决于它的消费者 | 控制台的输入／补全引擎；拼音匹配核心 |

**那六个问题** —— 按顺序问，前两个自己就能定案：

1. 它的词汇里有没有会话、权威、世界、存档或模组状态？有 → 框架核心。
2. 没有它，框架还转得动吗 —— 它的管理功能、它的安全面、它自己的结果送到玩家那里？转不动 → 框架核心。
3. CUO 没装的时候它还成立吗？不成立 → 框架核心；成立 → 继续往下问。
4. 它是面向游戏的体验，还是面向会话的能力？面向会话 → 框架核心。
5. 把它抽出去会不会造出双向依赖，或者逼框架公开一大片新契约？会 → 框架核心，或者做成组件而不是卫星。
6. 它需要游戏自己的代码吗？需要 → 这个卫星按声明出来的层级去绑。

两个现成的例子，免得下一位重新推导：

- **拼音搜索** —— 1 否、2 否、3 是、4 面向游戏 → 卫星。它最后落成自己的一对工程，住在 CUO 里的那份实现被删掉了。
- **控制台** —— 1 是（它的动词和它的输出缓冲区承载着这次会话自己的结果），2 是（没有它的主机就少了管理与会话存档动词，也看不到存档／恢复／起始补给那几笔账），3 否，5 是 → 框架核心。它的输入／补全引擎才是组件的候选，而[提升漏斗](../reference/modification-policy.md)说组件要等到第二个消费者出现。

拆出去从来不是免费的：每多一个交付物，就多一份构建、部署、验证与验收面；而一个把控制面做成可选附加件的框架，等于把治理也做成了可选项。

## 文档该放哪儿

- `docs/en/` 与 `docs/zh/` —— 成对的人类文档，路径一一对应（[文档编写规范](documentation-standard.md)）。
- `docs/standard/` —— 规则依赖的两份登记表：术语与配对对齐。
- `docs/contracts/` —— 门禁与工具读取的机器基线和表格（[`abstractions-api-baseline.txt`、特性矩阵与事件重放矩阵](../../contracts/README.md)）；`docs/evidence/` 下的那些 JSON 基线仍留在被描述对象的旁边。
- `docs/architecture/` —— 英文的架构规格：现役设计（[`current.md`](../../architecture/current.md)、[`domains.md`](../../architecture/domains.md)、[`protocol.md`](../../architecture/protocol.md)、`guards.md`、`projection-framework.md`、`mod-status-domain.md`、`save-archive-format.md`、`glossary.md`）以及 [`evolution/`](../../architecture/README.md) 下已完成的演进历史。属于贡献者材料，不进人类导航。
- `docs/development/` —— 面向代理的参考页：仓库布局与坑、复核提示词，以及游戏更新手册。
- `docs/backlog/`、`docs/evidence/`、`docs/decisions/` 等流程记录 —— 只有英文，不进人类导航；它们的结论要吸收进上面那些页面。

## 已知的坑

- 提交进文档里的数字（计数、行数、套件总数）必须能从它所描述的那棵树复现，或者写明来源；
  从没提交的中间状态量出来的数字，不是关于这个仓库的事实。
- Steam P2P 不是普通局域网 UDP；两种模式不要混。
- 同步 Transform 会在物理、父子关系、动画、寻路、刚体、场景加载上翻车——要同步的是游戏语义状态。
- 依赖写死的偏移量和私有字段，每次游戏更新都会断；应该改成扫描特性。
- Harmony 补丁（patch）的状态泄漏：Prefix 清掉的实例字段，必须由 Postfix 恢复；报告只在写入被确认之后才发。
- 一次 Steam 接收批次是全有或全无：逐条捕获异常，并在 `finally` 里释放。
- 大厅身份必须跟随真实的大厅，而不是进程历史；Steam 延迟初始化之后，要把那些捕获到 `SteamId` 为 0 的下游快照刷新一遍。
- 不许有未定义的失败模式：断线、掉线、版本不匹配各自的行为要明确定义，不能听天由命。
- `System.Memory` 会劫持数组上的 `Reverse()`；用倒序索引循环或 `Enumerable.Reverse`。
- 类型跨工程或命名空间搬家时，每个消费者都要补 `using`：原来同命名空间里的文件是靠简单名解析到它的，
  所以不会带新命名空间的 `using`。当搬走的类型报 `CS0246`/`CS0738`、而同程序集的类型照常解析时，
  原因是漏了那个 `using`——不是产物过期、编译器服务端缓存或可见性问题。
- 在本仓库里，`Abstractions` 中一个普通的公开构造函数会让该工程编译失败并报 `IDE0290`
  （`.editorconfig` 把它设成错误，同时开着 `EnforceCodeStyleInBuild`）；而一个工程编译失败时，
  它的消费者会继续对着上一次成功的 DLL 编译。用主构造函数，别用 `#pragma` 或 `.editorconfig` 覆盖去压掉它。
- `CasualtiesUnknownOnline.Application` 是一个命名空间，而命名空间成员优先于 `using` 别名：
  在任何 `CasualtiesUnknownOnline.*` 命名空间里，裸写 `Application` 会绑到它而不是 `UnityEngine.Application`，
  表现为 `CS0234: … does not contain 'persistentDataPath'`。在那些调用点给 Unity 类型起个别名
  （`using UnityApplication = UnityEngine.Application;`）。
- 每个 `net48` 工程都要有自己的一份 `System.Runtime.CompilerServices.IsExternalInit` 垫片，
  才能用 `init` 访问器和位置记录；没有它的工程用了 `record` 会报 `CS0518`，哪怕隔壁工程用的是同一个语言特性。
- 在测试宿主里，方法体中出现 `Component.transform`、`GetComponent`、`Physics2D` 或 `AddComponent` 的方法必然抛
  `SecurityException`；纯字段读是可测的。对游戏对象的访问要走被测的那道适配器（Game Adapter）接缝，不要写在测试方法里。
- 测试工程不引用 Game Adapter，所以那里的编译问题可能被一次全绿的测试运行盖住：完整跑一遍 `dotnet build`
  才是“整棵树能编译”的证据。

## 怎么确认成了

- 新工程或新引用出现在 `ProjectDirectionPolicy` 里，且 `ProjectDirectionGateTests` 全绿。
- `RepositoryGateTests.NoAbsolutePaths_NoTrackedMachinePaths` 全绿——它同时覆盖还没提交的新文件。
- 你加的文件落在依赖方向表允许的工程里，并且 Game Adapter 之外没有冒出对游戏程序集的引用。

## 相关阅读

- [门禁与绑定规则](gates-and-rules.md) —— 这些门禁背后的规则
- [构建、测试与部署](build-and-test.md) —— 跑这些门禁的命令
- [文档编写规范](documentation-standard.md) —— 一页文档该放哪儿
- [修改策略](../reference/modification-policy.md) —— 稳定性分级，以及模组可以给什么打补丁

---

[文档总览](../README.md) > [贡献者文档](README.md) > 仓库地图与坑
