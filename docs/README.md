# CUO 文档 / CUO documentation

两棵文档树,页面路径一一对应:中文 [`zh/`](zh/README.md),英文 [`en/`](en/README.md)。同一个主题只有一篇正文,
另一种语言是同一篇的对照版本,内容各自成文 —— 不是逐句翻译。

Two mirrored trees, one page set: 中文 [`zh/`](zh/README.md) and English [`en/`](en/README.md).

## 中文文档

- **[从这里开始](zh/start/README.md)** —— CUO 是什么、怎么玩、怎么搭开发环境、写第一个模组(由浅入深)
- **做一件事** —— `zh/how-to/`,一件事一页 *(正在编写)*
- **弄懂原理** —— `zh/internals/`:谁决定什么、协议、存档、适配器边界 *(正在编写)*
- **查东西** —— [参考](zh/reference/README.md)与[术语表](zh/reference/glossary.md)

## English documentation

- **[Start here](en/start/README.md)** — what CUO is, how to play it, how to set up a development checkout, and a first mod you can watch working
- **Do one thing** — `en/how-to/`, one task per page *(being written)*
- **Understand why** — `en/internals/`: who decides what, the protocol, saves, the adapter boundary *(being written)*
- **Look something up** — [Reference](en/reference/README.md) and the [glossary](en/reference/glossary.md)

## 写文档的规则 / Rules for these pages

- 绑定规则在 [`AGENTS.md`](AGENTS.md),agent 进入 `docs/` 时自动加载;术语与中文说法登记在
  [`standard/terminology.txt`](standard/terminology.txt),说明见 [`standard/`](standard/README.md)。
- 每页形状固定:头部面包屑 → 一句话目的 → 前置条件 → 步骤 → 能跑的例子 → 为什么这样设计 → 常见坑 →
  怎么验证成功 → 相关阅读 → 尾部面包屑。
- **语言切换链接只在本页**;子页面靠面包屑回到本页,不各写一份切换链接。
- 中英两侧的对应关系登记在 [`standard/alignment.txt`](standard/alignment.txt);改一侧必须在同一改动里
  同步另一侧。

## 仍在迁移的英文资料 / Contributor material (English only)

架构、决策、证据与 backlog 是贡献者向的资料,正在被重写进上面两棵树。重写完成前,可用的入口:

- 架构:[当前架构](architecture/current.md)、[协议](architecture/protocol.md)、[领域与投影](architecture/domains.md)、[存档归档格式](architecture/save-archive-format.md)
- 接口与策略:[模组接口契约](api/mod-api.md)、[扩展与稳定性策略](api/advanced-modification-policy.md)
- 机制与矩阵:[物品](features/items.md)、[实体](features/entities.md)、[敌人同步](features/enemies.md)、[游戏内部](features/game-internals.md)
- 记录:[活跃决策台账](decisions/active.md)、[证据链](evidence/verification.md)、[规则到门禁映射](evidence/normative-gates.md)、[backlog](backlog/README.md)
- 开发:[仓库布局与坑](development/agent-reference.md)、[运维与部署](operations/README.md)
