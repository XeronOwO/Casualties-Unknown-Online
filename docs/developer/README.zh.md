# 开发者指南

[English](README.md) | 中文

本指南面向要构建、给 CUO 打补丁或扩展它、但还没有读过源码的人。它说明模组的形状、各部分如何互相
对话,以及从哪里开始;精确的契约留在参考层,本指南只链接过去,而不复制一份。

第一次请按顺序阅读。之后,末尾的参考地图就是找到你真正需要那份文档的最快路径。

## 页面

- [总览](overview.zh.md) —— 模组的形状:稳定的运行时、可替换的适配器,以及确定性的内核。
- [协议](protocol.zh.md) —— 四种信封、加入过程、状态流与版本校验。
- [同步模型](sync-model.zh.md) —— 主机拥有什么、每台客机自行判定什么,以及冲突如何被解决。
- [分层](layers.zh.md) —— Runtime、Game Adapter 与 Abstractions:你可以对什么打补丁,什么不是承诺。
- [工具](tooling.zh.md) —— 日志、模拟框架,以及如何运行与筛选测试套件。
- [存档](saves.zh.md) —— 世界归档的布局与恢复路径。
- [已知问题](known-issues.zh.md) —— 已声明的缺口,以及每一项记录在哪里。
- [参考地图](reference-map.zh.md) —— 按阅读顺序排列的英文参考层。

## 如何使用本指南

本指南解释机制时使用代码自己的术语,所以你读到的标识符就是你在代码树里能找到的标识符。当某一页
需要超出它承载能力的深度时,它会链接进参考层,而不是把它重述一遍。

## 构建模组

构建就是一个普通的 .NET 解决方案:先 `dotnet build CasualtiesUnknownOnline.slnx`,再用
`dotnet test CasualtiesUnknownOnline.slnx` 跑门禁与测试套件。仓库根目录的
[AGENTS.md](../../AGENTS.md) 保存具有约束力的命令,[运维文档](../operations/README.md)覆盖部署与
本机工具链。
