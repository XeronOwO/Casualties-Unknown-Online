# 注册内容

[文档总览](../README.md) > [做一件事](README.md) > 注册内容

---

**读完这一页**，你的模组能注册自己的内容 —— 一件物品、一个配方、一种地块、一条状态定义 —— 让框架把它当作世界的一部分。先读[你的第一个模组](../start/your-first-mod.md)和[声明权限与主机命令](declare-permissions-and-commands.md)。

## 内容和模组一起走，不走网络

你注册的字节就是你模组程序集的一部分；注册表只在进程内，里面的东西谁都不会收到。一致性边界是模组握手 —— 模组 id、版本、权限、网络模式 —— 所以可能收到你内容实例的每一名玩家都必须跑同一版本的模组。正因如此，运行时绑定器只接受能保证“每个接收方都有一份”的网络模式：`Synchronized`、`Authoritative`、`RequiresAllPlayers`；`HostOnly`、`ClientOnly`、`Cosmetic` 的内容永远不会被绑进共享世界状态。

## 先声明命名空间

内容 id（content id）是规范的 `namespace:path`，其中命名空间（namespace）来自模组声明：

```csharp
[CuoMod("cuo.example", "CUO Example", "0.1.0", NetworkMode = NetworkMode.Synchronized,
	Namespace = "example",
	Permissions = ModPermission.RegisterContent)]
public sealed class ExampleMod : ICuoMod
```

声明了 `Namespace = "example"`，你的物品地址就是 `example:wooden.sword`；游戏自带内容的地址保持 `cu:<物品 id>`，比如 `cu:fentanyl`。命名空间的语法是 `[a-z][a-z0-9_]{0,31}`，路径段（bare id）的语法是 `[a-z0-9][a-z0-9_.-]{0,94}` —— 首字符加最多 94 个后续字符，合计最长 95。`ContentId` 负责解析与格式化，并在解析外部输入时规范大小写；注册这一步要求你给的已经是规范小写形式。没有声明命名空间的模组继续用裸 id，作用域仍限自己；但裸 id 同时也是游戏表里的键，所以两个模组为同一类别注册同一个裸 id 就是冲突 —— 两个规范地址都能解析，游戏表里却只能存在一条；控制台的资源 id 补全会把这类歧义裸 id 整条略过，不给出游戏兑现不了的地址。

## 在 `Bind` 里注册

```csharp
if (context.Content.CanRegister)
{
	var sword = new ModItemDefinition
	{
		DisplayName = "Wooden Sword",
		Description = "A simple wooden sword.",
		Weight = 1f,
		Value = 5,
		Usable = true,
		Tags = "weapon",
		TemplateId = "stone"
	}.ToPayload();

	context.Content.TryRegister("wooden.sword", ModContentKind.Item, sword);
	context.Content.TryRegister("healing.recipe", ModContentKind.Recipe, recipeBytes, schemaVersion: 2);
}
```

`TryRegister` 收的是裸 id、内容类别（content kind）和一个不透明的载荷。`Definitions` 返回一份快照，读出来的载荷都是副本；`IsRegistered` 回答单个 id，`TryUnregister` 撤掉一条定义。注册要放在 `Bind` 里：注册表在发现与 `Bind` 之前就已经加载过一次，之后再注册就是你自己的竞态。

## 有类型化载荷的类别

注册本身只认识 `id`、类别和 `bytes` 三样。对框架今天会绑进游戏的类别，`CUO.Abstractions` 另外提供了 DTO，你填好之后用 `ToPayload()` 变成载荷：

| 类别 | DTO | 适配器拿它做什么 |
|---|---|---|
| `item` | `ModItemDefinition` | 登记物品，并按 `TemplateId` 组装一份运行时模板 |
| `recipe` | `ModRecipeDefinition` | 把配方注入配方表 |
| `liquid`、`liquidtile` | `ModLiquidDefinition`、`ModLiquidTileDefinition` | 填液体注册表与世界流体网格 |
| `building` | `ModBuildingDefinition` | 组装建筑模板，并可喂给世界生成（world generation） |
| `tile`、`structure` | `ModTileDefinition`、`ModStructureDefinition` | 分配地块索引，并可喂给世界生成 |
| `status`、`moodle` | `ModStatusDefinition`、`ModMoodleDefinition` | 存下投影要读的静态描述 |

这些 DTO 大多另带一个 `CustomData` 字典，用来放已知类别还没命名的字段（`ModRecipeDefinition` 和 `ModLiquidDefinition` 没有）；而框架存下来的始终是你注册的那串不透明字节。

## 查一条定义归谁

```csharp
if (context.ContentOwners.TryGetOwner(ModContentKind.Item, "example:wooden.sword", out var owner))
{
	context.Logger.LogInformation("[Example] {Id} belongs to {Owner}", "example:wooden.sword", owner);
}
```

这个查询是只读的，不需要权限，冲突策略和运行时目录一致：同一类别加同一个 id 有多条时返回 `false`，不猜。

## 会被拒绝的情况

- 没声明 `RegisterContent`：`CanRegister` 是 false，每次调用都返回 false 并写日志。
- 同一个模组里 id 重复，或者 id、类别为空。
- 载荷超过 64 KiB、结构版本（schema version）不是正数、一个模组的定义超过 1024 条。
- id 不是规范的小写路径段：含大写、含空白、路径里带 `:`，或者路径超过 95 个字符。
- 两个模组注册了同一类别、同一个裸 id。

拒绝的表现是 `false` 加一行日志；不会静默截断，也不会抛异常。

## 常见坑

- **不要在游戏跑起来之后再注册。** 注册是内容，不是运行时状态；注册表属于 `Bind`，没有任何逐帧路径通向它。
- **结构版本和它的迁移都归你。** 框架原样存下你的字节和你自报的版本，版本之间绝不替你转换。
- **游戏表还没就绪不是你的问题。** 适配器的各个 provider 会等自己那张表；表好了自然会来读你的定义。
- **注册内容不等于同步内容。** 注册好的物品不会自己出现在任何地方；把它放进世界是一次生成，那是另一个权限、另一页的事。

## 验证它真的成了

载入模组，把 `context.Content.IsRegistered("wooden.sword")` 记进日志 —— true 就说明注册表收下了这条定义，日志里也没有针对它的拒绝。控制台的资源 id 补全（即 `ResourceLocation` 那套词表）会用规范 id 提示你的内容 —— 没声明命名空间的旧式注册没有规范 id，会被有意略过；带 `TemplateId` 的物品类别可以通过物品生成接口放出来。网络模式声明错了的模组，表现是内容只在本地存在，永远绑不进共享世界。

## 相关阅读

- [声明权限与主机命令](declare-permissions-and-commands.md) —— 标志与模组声明
- [让模组数据跨会话保留](save-mod-data-across-sessions.md) —— 模组要持久化的另一半
- [读取游戏状态](read-game-state.md) —— 关于玩家你能读回什么
- [你的第一个模组](../start/your-first-mod.md) —— `Bind` 的生命周期
- [模组的一生](../internals/mod-loading-lifecycle.md) —— 发现过程，以及别人必须装什么
- [术语表](../reference/glossary.md) —— 模组、适配器、握手

---

[文档总览](../README.md) > [做一件事](README.md) > 注册内容
