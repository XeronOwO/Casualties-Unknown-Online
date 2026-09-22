# 工具

[English](tooling.md) | 中文

本仓库自带自己的验证工具,因为真正有意思的故障发生在两台机器之间,靠手工无法复现。所有工具都在
开发机上从命令行运行。

一个好习惯是先读失败的那个测试,再读它打印的日志:本仓库的门禁会写明自己执行的是哪条规则,所以
失败信息通常已经说了该改什么。

## 命令

- `dotnet build CasualtiesUnknownOnline.slnx` —— 构建所有项目。
- `dotnet test CasualtiesUnknownOnline.slnx` —— 运行门禁与完整测试套件。
- `dotnet test CasualtiesUnknownOnline.slnx --filter "Category!=Integration"` —— 快速内环。
- `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~<名称>"` —— 单个类或一族测试。
- `dotnet format CasualtiesUnknownOnline.slnx` —— 格式化,提交前必须执行。

## 日志

- `BepInEx/LogOutput.log` —— 链加载与启动异常。
- `BepInEx/logs/latest.log` —— Unity 侧抛出的运行时异常。
- `CUO.log` —— CUO 自己的日志,级别由配置的 `Logging` 段控制。

## 模拟框架

内核是确定性的,所以测试可以在不启动游戏的情况下回放一段录下来的场景:框架把指令喂给内核,比较
产生出来的提交,并在第一处分歧处失败。行为就是这样在没有第二个客机的情况下被验证的。

## 更新日的工具链

`tools/CasualtiesUnknownOnline.ContractTool` 把一个游戏构建快照成元数据,并对两个构建之间的差异
分类,从而把一次游戏更新变成一份可评审的差异,而不是一场调试。流程写在
`docs/development/game-update-runbook.md`。
