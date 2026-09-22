# 搭好开发环境

[文档总览](../README.md) > [从这里开始](README.md) > 搭好开发环境

---

**读完这一页**，你的机器上能构建这个解决方案，也能跑通测试。你需要 .NET SDK 和一台 Windows 机器：所有项目都以 `net48` 为目标，因为游戏跑的是 Mono 上的 BepInEx 5。

## 拿到代码、构建、跑测试

```bash
git clone <仓库地址>
cd CasualtiesUnknownOnline
dotnet build CasualtiesUnknownOnline.slnx
dotnet test CasualtiesUnknownOnline.slnx
```

`dotnet test` 会跑两个测试工程：行为测试套件，以及 `tests/CasualtiesUnknownOnline.NormativeGates.Tests` —— 后者装的是这个仓库自己的规则：协议编号、被评审过的接口面、文档配对、指令预算。只改文档的改动可以跳过它们；改代码或测试必须过。

## 解决方案里有什么

| 工程 | 它是什么 |
|---|---|
| `CasualtiesUnknownOnline.Abstractions` | 模组接口 —— 模组唯一引用的程序集 |
| `CasualtiesUnknownOnline.Runtime` | 稳定层：协议、会话、模组加载、存档 |
| `CasualtiesUnknownOnline.GameAdapter` | 唯一认识游戏私有类型的层 |
| `CasualtiesUnknownOnline.Plugin` | 启动 CUO 的 BepInEx 入口 |
| `CasualtiesUnknownOnline.ModExample` | 一个能跑的示例模组 —— 下一篇就读它 |
| `tests/…` | 行为测试套件与规范门禁 |

## 把你构建出来的东西放进自己的游戏

```powershell
powershell -ExecutionPolicy Bypass -File tools/deploy.ps1 -GameDir "<游戏目录>"
```

必须显式传 `-GameDir`。这个脚本只部署构建产物 DLL 和 Steam 依赖，拒绝沙盒路径，也从不碰 BepInEx 自己的 DLL。部署前先关掉游戏。

## 相关阅读

- [你的第一个模组](your-first-mod.md) —— 从头到尾读一个能跑的模组
- [装起来玩](install-and-play.md) —— 玩家拿同一个文件夹做什么
- [术语表](../reference/glossary.md) —— 运行时、适配器、内核

---

[文档总览](../README.md) > [从这里开始](README.md) > 搭好开发环境
