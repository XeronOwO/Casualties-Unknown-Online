# 分层

[English](layers.md) | 中文

解决方案这样划分,是为了让游戏更新只有一个地方会被破坏。Runtime 是稳定的 CUO 代码,Game Adapter
是唯一接触游戏私有类型的层,而 Abstractions 项目是模组被允许依赖的那一小块表面。

依赖方向是被强制执行的,而不只是写在文档里:架构门禁会读取解决方案文件,拒绝指向错误方向的项目引用。

## 项目

- **Abstractions** —— 面向模组的公开契约,也是唯一带有稳定性承诺的表面。
- **GameState** —— 带类型的确定性内核;它不引用任何其他 CUO 项目。
- **Protocol** —— 线路 DTO、编解码器与版本管理。
- **Application** —— 指令准入接缝与内核复制面。
- **Runtime** —— 依赖注入组装、会话状态机、网络、投影、模组 API。
- **GameAdapter** —— 面向游戏的实现:钩子、捕获、原生写入。
- **Plugin** —— 很薄的 BepInEx 入口。

## 你可以对什么打补丁

`Runtime` 与 `GameAdapter` 属于实现:模组可以对它们打补丁,而补丁在模组更新后失效是模组自己的问题。
只有 `Abstractions` 是承诺,而且即便在那里,公开表面也是一份被记录的基线 —— 增加或删除成员是有意
且经过评审的动作,而不是重构的副作用。

## 适配器边界

运行时声明契约,适配器实现契约。这正是适配器能够吸收游戏更新 —— 游戏类型改名、方法搬家 —— 而协议、
会话逻辑与模组 API 保持原样的原因。

## 细节在哪里

`docs/api/abstractions-api-baseline.txt` 是记录在案的公开表面,
`docs/api/advanced-modification-policy.md` 定义稳定性等级,
[当前架构](../architecture/current.md)保存完整的依赖图。
