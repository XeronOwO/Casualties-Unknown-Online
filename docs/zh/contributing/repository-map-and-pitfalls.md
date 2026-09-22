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

卫星模组（mod）住在本仓库里、和框架并排，守同一条线：只有绑游戏的那一半引用游戏程序集，不碰游戏的那一半才是测试工程直接引用的部分。
在决定一个系统该待在插件里还是自成模组之前，先套用
[advanced-modification-policy.md](../../api/advanced-modification-policy.md) §1.2 的四层规则。

## 文档该放哪儿

- `docs/en/` 与 `docs/zh/` —— 成对的人类文档，路径一一对应（[文档编写规范](documentation-standard.md)）。
- `docs/standard/` —— 规则依赖的两份登记表：术语与配对对齐。
- `docs/api/` 与 `docs/evidence/` —— 门禁当前读取的机器基线（`abstractions-api-baseline.txt`，以及那些 JSON 基线）。
  规范另外声明了一个 `docs/contracts/` 分区来收拢它们，目前还是空的。
- `docs/backlog/`、`docs/evidence/`、`docs/decisions/` 等流程记录 —— 只有英文，不进人类导航；
  它们的结论要吸收进上面那些页面。

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
- [扩展与稳定性策略](../../api/advanced-modification-policy.md) —— 四层规则与稳定性分级

---

[文档总览](../README.md) > [贡献者文档](README.md) > 仓库地图与坑
