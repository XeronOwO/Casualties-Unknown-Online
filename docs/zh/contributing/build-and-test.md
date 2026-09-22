# 构建、测试与部署插件

[文档](../../README.md) > [贡献者文档](README.md) > 构建、测试与部署插件

**读完这一页**,你能构建整个解决方案、只跑能抓到这次改动的最小测试范围、把插件部署进自己的游戏,并在出错时找到该看的日志。
前置条件是有一份开发用的检出([搭好开发环境](../start/set-up-dev-environment.md)),以及能编译 `net48` 的 .NET SDK。

## 要跑的命令

```bash
dotnet build CasualtiesUnknownOnline.slnx      # 构建全部工程
dotnet test CasualtiesUnknownOnline.slnx       # 门禁 + 行为测试
dotnet format CasualtiesUnknownOnline.slnx     # 格式化,提交前必跑
```

每次提交前 `dotnet build` 与 `dotnet test` 都必须通过。`dotnet format` 背后有两道构建期机制:
`.editorconfig` 把样式规则提到错误级别,各工程文件里的 `EnforceCodeStyleInBuild` 让编译器真的执行它们,
所以样式不达标就构建不过。迭代期用两个过滤条件把循环压短:

- `dotnet test CasualtiesUnknownOnline.slnx --filter "Category!=Integration"` —— 快循环。
- `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~<类名>"` —— 只跑一个类或一个族。

改动只要碰了 `src/`、`tests/` 或 `tools/`,就走完整阶梯。纯文档改动(这三个目录一个都没动)可以跳过构建、测试与格式化:
复核(review)一遍 diff 直接提交。如果文档描述的是代码改动,那就和代码放在同一次提交里,门禁(gate)在同一次提交里跑。

## `dotnet test` 到底跑了什么

它跑两个工程:

- 行为测试套件 `tests/CasualtiesUnknownOnline.Tests`;
- 规范性门禁 `tests/CasualtiesUnknownOnline.NormativeGates.Tests` —— 它是原先那批 `tools/check-*.ps1` 脚本的 C# 替代品,那些脚本已经不存在了。

迭代时单独跑门禁工程:它几秒就跑完,而且失败信息会点名自己盯的是哪条规则,通常看一眼就知道要改什么。
哪条规则由哪个门禁盯着,看[规则到门禁映射](../../evidence/normative-gates.md);规则本身见[门禁与绑定规则](gates-and-rules.md)。

## 测试套件给自己定的契约

这里的测试要能任意顺序、并行地跑,所以套件自带一批规则。它们是绑定要求,不是偏好:

- 运行器配置是 `tests/CasualtiesUnknownOnline.Tests/xunit.runner.json`。
- 会写进程级静态字段的测试类必须加入 `GameAssembly` 集合
  (`tests/CasualtiesUnknownOnline.Tests/Patching/GameAssemblyCollection.cs`),由 `TestIsolationGateTests` 盯着:
  xUnit v2 会让不同集合并行,两个这样的类会争同一个 Unity/游戏程序集静态量。
- 测试组合不能写按节点滚动的日志文件(`tests/CasualtiesUnknownOnline.Tests/Fakes/TestLogging.cs`)。
- 单个测试类不得超过 40 个真实用例或数据行(含 `MemberData` 展开),由
  `TestClassSizeGateTests.NoTestClass_ExceedsTheCaseLimit` 在测试执行时强制它。超了就按行为族拆开;
  要提高上限,得拿出跟改阶段同等规格的实测证据。
- 让每个类都小到没有任何一个类主导整轮运行;运行时相关的改动要用三次运行取中位数的办法记录,
  办法见[测试并行记录](../../evidence/test-parallelization.md)。
- 按行为族拆分时共用无状态的辅助代码,并且**绝不能重复 `MemberData` 行**:各分片必须保持是一个划分,
  这由一条守卫测试盯着。一个类可以把只读查询门面当作 `IClassFixture` 共享(例如 `DirectionProbe`,
  它只暴露纯查询、把节点保持在私有),但绝不能共享可变的会话或世界夹具。
- 会构造生产组合根、完整模拟世界或回放台、共享全栈夹具、游戏程序集反射宿主(`GameAssemblyHost`),
  或使用真实回环套接字(`IpDirectTransport`)的测试类,必须带 `[Trait("Category", "Integration")]`。
  没打标记的类就是快循环。
- 运行器线程上限保持 `1x`(逻辑处理器数)。测过的其它取值,以及"不同线程设置下的测试耗时总和不可比较"这条,
  记在[测试并行记录](../../evidence/test-parallelization.md) §7.5 与 §9。

## 目标框架与包

- 目标框架 `net48`,`LangVersion = preview`,开启可空,警告即错误。
- NuGet 源:nuget.org、nuget.bepinex.dev、nuget.samboy.dev。
- `Microsoft.Extensions` 停留在 3.1.x —— 这是能在 `net48` 上跑的最后一条线
  (`SourceShapeGateTests.MicrosoftExtensionsPinnedToNet48CompatibleLine`)。
- 游戏程序集有版权:只有 Game Adapter 工程可以引用它们,`references/` 不进 git,按需放入。

## 部署进自己的游戏

```powershell
powershell -ExecutionPolicy Bypass -File tools/deploy.ps1 -GameDir "<game-dir>"
powershell -ExecutionPolicy Bypass -File tools/verify-deploy.ps1 -GameDir "<game-dir>"
```

- `-GameDir` 必须显式传。脚本会拒绝沙盒路径、拒绝在游戏运行时执行,并且只部署本树的构建产物加 Steam 依赖,
  从不碰 BepInEx 自己的 DLL。
- `verify-deploy.ps1` 把部署结果和本树构建产物对比,一致时退出码为 `0`;它还会打印部署进去的 `ProductVersion`,
  其中的 `+<sha>` 后缀就是这批 DLL 来自哪次提交。**先提交再构建部署**,否则内嵌的 sha 会落后一个提交。
- 与本机相关的路径只写在进不了 git 的 `AGENTS.local.md` 里,绝不写进任何要被提交的文件。

## 日志

| 位置 | 它能告诉你什么 |
|---|---|
| `BepInEx/LogOutput.log` | 链加载与启动期异常 |
| `BepInEx/logs/latest.log` | Unity 侧抛出的运行期异常(`[ERR][Unity:Exception]`) |
| `CUO.log` | CUO 自己的日志;级别由配置里的 `Logging` 段决定 |

## 常见坑

- 带 `--no-build` 跑,可能在构建产物不完整的情况下报告整套全绿。突然出现大批"数据文件缺失"类失败时,
  先重新构建,不要把失败直接读成代码缺陷;提交前的最后一次全量必须带构建。
- 某个工程编译失败时,它的消费者会继续对着上一次成功的 DLL 编译,看起来就像"新类型不存在"。
  先看构建输出,再怀疑消费者。
- `dotnet format` 会重写文件。复核窗口期工作区是冻结的,别在里面跑它;任何外部工具动过文件后,编辑前都要重新读一遍。
- 这套测试里没有任何东西是一次游戏会话。测试全绿只能证明逻辑;两台真实客户端的画面对比是用户的验收环节
  ([搭好开发环境](../start/set-up-dev-environment.md))。

## 怎么确认成了

- `dotnet build` 报 0 警告 0 错误(这里警告就是错误)。
- `dotnet test` 退出码为 `0`;迭代期单跑门禁工程,几秒内全绿。
- `verify-deploy.ps1` 退出码为 `0`,并且它打印的 `ProductVersion` 以你这次构建的提交 sha 结尾。

## 相关阅读

- [门禁与绑定规则](gates-and-rules.md) —— 门禁盯着哪些规则,以及新门禁怎么写
- [仓库地图与坑](repository-map-and-pitfalls.md) —— 新文件该放进哪个工程
- [复核与交付](review-and-delivery.md) —— 这些命令在流程里的顺序,以及提交信息约定
- [测试并行记录](../../evidence/test-parallelization.md) —— 实测出来的分类与数字
- [游戏更新操作手册](../../development/game-update-runbook.md) —— 游戏更新的那天要做什么

[文档](../../README.md) > [贡献者文档](README.md) > 构建、测试与部署插件
