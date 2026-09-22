# 参考地图

[English](reference-map.md) | 中文

参考层是写给在代码里工作的人看的,所以它只有英文,而且刻意写得密集。本页是进入它的阅读路径:
每份文档回答什么问题,以及按什么顺序读最省力。

第一次动手之前,请先读前两行。其余的可以等到工作真的需要时再读。

## 路径

- `docs/architecture/README.md` —— 当前架构与已完成的演进历史。
- `docs/architecture/current.md` —— 当前设计:内核、事务、投影、非目标。
- `docs/architecture/domains.md` —— 什么属于哪个域,什么只是投影。
- `docs/architecture/protocol.md` —— 四种信封、加入、状态流、存档与恢复。
- `docs/architecture/guards.md` —— 生效中的架构门禁,以及每一道保护什么。
- `docs/api/mod-api.md` —— 模组 API 契约:生命周期、权限、主机命令、内容。
- `docs/api/advanced-modification-policy.md` —— 契约与实现之别、稳定性等级、补丁策略。
- `docs/decisions/active.md` —— 仍然适用的决策,连同它们的理由。
- `docs/features/items.md`、`docs/features/entities.md`、`docs/features/enemies.md` —— 机制矩阵。
- `docs/evidence/verification.md` —— 支撑本仓库各项声明的证据链。
- `docs/backlog/README.md` —— 按状态分组的待办工作。

## 第一次改动的阅读顺序

先读你要改的那部分对应的架构页,再读提到它的决策记录,最后读声明它同步状态的功能矩阵行。与记录在案
的决策相冲突的改动属于决策变更,而不是实现细节。

## 一个声明如何被证明

本仓库里的每条规则都指向执行它的门禁或证据:`docs/evidence/normative-gates.md` 是规则到门禁的映射,
`docs/evidence/delivery-checklist.md` 是一次交付必须满足的清单。优先相信门禁而不是散文:文档会过时,
而门禁在它所执行的规则被破坏时不可能通过。
