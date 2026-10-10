# Casualties Unknown: Online（CUO）

**中文** | [English](README.md)

一款为 [*Casualties: Unknown*](https://store.steampowered.com/app/4576510)（目前为 Demo）打造的多人联机模组框架，构建在 [BepInEx](https://github.com/BepInEx/BepInEx) 之上。

游戏本体不带联机。CUO 注入一套新的多人运行时，把原本只在本机成立的游戏状态重组为**主机权威模拟 + 客机输入与状态同步**，从而加上基于 Steam 的**主机 + 客机**合作（局域网／好友之间）—— 思路接近 Minecraft Forge，但先做扎实的联机内核，而不是一整套模组生态。

## 状态

**积极开发中 —— 架构演进已完成。** 阶段 0–4（可行性、单人实体同步、实体生命周期、游戏主循环、公开 Mod API）已完成并通过运行时验证；类型化确定性游戏状态内核的迁移（阶段 A–E）也已完成。完整文档地图见 [`docs/README.md`](docs/README.md)，当前架构见 [`docs/architecture/README.md`](docs/architecture/README.md)，已落地的决策见 [`docs/decisions/active.md`](docs/decisions/active.md)，具有约束力的 Mod API 契约见 [`docs/en/reference/mod-api.md`](docs/en/reference/mod-api.md)。

## 架构速览

```
模组 → 模组框架 API → 多人运行时 → 游戏适配层 → BepInEx / Unity / Steam
```

- **稳定的 CUO 运行时**：网络协议、主机/客机状态机、模组加载、序列化、tick/快照、日志、版本协商。
- **可替换的游戏适配层**：唯一认识游戏私有类型的层；每个游戏构建对应一个适配层，启动时做能力探测，游戏更新破坏兼容时安全降级。
- **先接受再仲裁的同步策略**：主机先信任每个客机的上报（先采纳并转发，绝不阻塞玩家的操作），只在明显冲突（例如竞态）时才纠正；严格校验与反作弊在功能补齐之前刻意保持低优先级。

## 构建

需要 .NET SDK（见 [`AGENTS.md`](AGENTS.md)）。

```bash
dotnet build CasualtiesUnknownOnline.slnx
```

所有项目都面向 `net48`（BepInEx 5 加游戏的 Mono 运行时）。把插件部署进游戏的 `BepInEx/plugins/CasualtiesUnknownOnline/` 目录由 `deploy.ps1` 完成。

## 文档

- [`docs/README.md`](docs/README.md) —— 语义化文档地图与阅读路径
- [`AGENTS.md`](AGENTS.md) —— 项目约定与面向 AI 辅助开发的说明
- [`docs/architecture/README.md`](docs/architecture/README.md) —— 当前架构与已完成的演进史
- [`docs/architecture/current.md`](docs/architecture/current.md) —— 当前的类型化确定性内核设计
- [`docs/architecture/domains.md`](docs/architecture/domains.md) —— 领域归属与投影
- [`docs/architecture/protocol.md`](docs/architecture/protocol.md) —— 四信封协议与数据流
- [`docs/en/contributing/build-and-test.md`](docs/en/contributing/build-and-test.md) —— 构建、测试与部署插件
- [`docs/evidence/verification.md`](docs/evidence/verification.md) —— 证据链、门禁、重放与模拟
- [`docs/decisions/active.md`](docs/decisions/active.md) —— 已落地的约束性决策
- [`docs/decisions/index.md`](docs/decisions/index.md) —— 决策编号索引
- [`docs/en/reference/mod-api.md`](docs/en/reference/mod-api.md) —— Mod API 契约
- [`docs/backlog/README.md`](docs/backlog/README.md) —— 待办缺陷、工作项、决策与未来项

## 开发

本项目借助 AI 开发：

- 项目代码主要由 **DeepSeek V4 Flash/Pro** 写就；**DeepSeek V4.1 Flash** 是目前主要使用的开发模型；少量架构设计由 **GPT 5.6 Sol** 贡献。
- 在 **DeepSeek Harness** 出现之前，开发使用 **Claude Code**；DeepSeek Harness 可用之后，本项目完全使用 **DeepSeek Harness** 开发。

## 致谢

- [KrokMP](https://github.com/Krokosha666/cas-unk-krokosha-multiplayer-coop) —— 本作更早的一次多人尝试。它的做法给了我们很有价值的启发，也帮助确定了 CUO 的整体方向。
- [CUCoreLib](https://github.com/jimmyking9999999/CUCoreLib) —— 本作的开源库，在游戏相关的接入细节上是有用的参考。
- [DeepSeek Harness](https://github.com/deepseek-ai/deepseek-harness) —— CUO 开发所用的开发装置。
- [BepInEx](https://github.com/BepInEx/BepInEx) —— CUO 所依托的模组框架。
- [Claude Code](https://github.com/anthropics/claude-code) —— 用于 CUO 的早期开发；因交付前出现可疑迹象，致谢已撤回。

## 许可

见 [LICENSE](LICENSE)。BepInEx 及其依赖各有自己的许可 —— 分发前请自行确认。

## 免责声明

*Casualties: Unknown* 是第三方游戏，官方不支持模组。CUO 是非官方社区项目；不保证与未来的游戏更新兼容，兼容性由游戏适配层维护。
