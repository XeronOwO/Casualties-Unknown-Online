# CUO 文档 —— 中文

文档总览

---

玩家和模组作者要用的内容，按阅读顺序排好。这里是中文版块 `docs/zh/`，英文版块 `docs/en/` 是这棵树的完整镜像：
[English documentation](../en/README.md) —— 同样的页面，同样的路径。

## 从哪里开始

按你想做的事选一条线，每条线都由浅入深。

1. **先跑起来** —— [从这里开始](start/README.md)：CUO 是什么、怎么玩、怎么搭开发环境，以及一个能亲眼看到效果的第一个模组。
2. **[做一件事](how-to/README.md)** —— 一件事一页，有步骤、有能跑的例子，还有常见坑。
3. **弄懂原理** —— `internals/`：谁决定什么、确定性内核、协议、存档、适配器边界，以及每种选择的代价。
4. **查东西** —— [参考](reference/README.md)：模组接口、协议消息、配置项、特性矩阵，还有[术语表](reference/glossary.md)。
5. **参与 CUO 自身的开发** —— [贡献者文档](contributing/README.md)：构建与测试、门禁、仓库地图、文档规范，以及复核与交付流程。

## 这些页面怎么写

每篇形状一致：头部面包屑、一句话说清这篇解决什么、前置条件、步骤、能跑的例子、为什么这样设计、常见坑、怎么验证成功、相关阅读。项目特有词第一次出现时链到术语表。

## 仍然是英文的部分

架构、决策、证据与 backlog 是贡献者向的资料，正在被重写进这两棵文档树。重写完成之前，可用的入口是：

- 架构：[当前架构](../architecture/current.md)、[协议](../architecture/protocol.md)、[领域与投影](../architecture/domains.md)、[存档归档格式](../architecture/save-archive-format.md)
- 接口与策略：[模组接口契约](../api/mod-api.md)、[扩展与稳定性策略](../api/advanced-modification-policy.md)
- 机制与矩阵：[物品](../features/items.md)、[实体](../features/entities.md)、[敌人同步](../features/enemies.md)、[游戏内部](../features/game-internals.md)
- 记录：[活跃决策台账](../decisions/active.md)、[证据链](../evidence/verification.md)、[规则到门禁映射](../evidence/normative-gates.md)、[backlog](../backlog/README.md)
- 开发：[仓库布局与坑](../development/agent-reference.md)、[运维与部署](../operations/README.md)

---

文档总览
