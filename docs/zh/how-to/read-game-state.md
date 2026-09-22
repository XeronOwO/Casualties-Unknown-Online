# 读取游戏状态

[文档总览](../../README.md) > [做一件事](README.md) > 读取游戏状态

**读完这一页**,你的模组能读到 CUO 手里已经有的、关于另一名玩家的状态 —— 他在不在世界里、生命体征(vitals)、随身物品 —— 并且知道这份读数有多新。先读[你的第一个模组](../start/your-first-mod.md)。

## 权限就是开关

读数据由 `ModPermission.ReadGameState` 把关;这个标志本身在[声明权限与主机命令](declare-permissions-and-commands.md)里讲。

```csharp
if (context.GameState.CanRead && context.GameState.TryGetPlayer(steamId, out var player))
{
	var health = player.Vitals?.BrainHealth;
	var items = player.Inventory?.Items;
}
```

这份模组声明了这个标志,`CanRead` 才是 true。每次 `TryGetPlayer` 都会再校验一遍,不满足时返回 `false` 并写一行日志,而不是抛异常 —— 忘声明标志的模组表现为"读不到数据",不会表现为崩溃。

## 一名玩家长什么样

| 成员 | 里面是什么 |
|---|---|
| `SteamId` | 这是谁的状态 |
| `InWorld` | 本侧的会话当前是否知道这名玩家在世界里 |
| `Vitals` | `IModPlayerVitals`;第一份健康数据到达之前是 `null` |
| `Inventory` | `IModPlayerInventory`;第一份物品快照到达之前是 `null` |

`Vitals` 带 `BrainHealth`、`Hunger`、`Thirst`、`Stamina`、`Energy`、`Temperature`,以及派生出来的 `Alive`、`Conscious` 两个标志。`Inventory` 带顶层 `Items`、原始的 `HandSlot` 值和 `Count`;每一条 `IModInventoryEntry` 给出 `InstanceId`、`ItemId`、`SlotIndex`、`Condition`、`Favourited` 和可递归的 `Contents`,所以容器的内容物是按树走的。`SlotIndex` 为负表示这件物品穿在身上 —— 那是游戏自己的编码,不是 CUO 的发明。

## 这是一份副本,而且最多旧一秒

这份投影装的是 1 Hz 角色流上已经在到达的数据 —— 内置联机界面画的就是同一个来源。它不是推给你的:

- **按自己的节奏读。** 每次 `TryGetPlayer` 返回的是那一刻缓存的事实;返回的对象是副本,调用之后(比如跨帧)依然有效。
- **缺一半是正常的。** 生命体征和物品是两份不同的快照,所以可能一半是 `null` 而另一半已经有值。`null` 要当成"还没到",不能当成 0。
- **缓存属于会话。** 玩家离开世界、或者会话结束,缓存就被清掉,`TryGetPlayer` 开始返回 `false`;两半都还没到的时候同样返回 `false`,所以拿到 `true` 不等于它会一直是 `true`。

## 这一片没有覆盖什么

本机玩家自己的角色状态不在 `IModGameState` 里;它走原生接口的只读本机玩家投影。世界、物品、地块、实体这些全局表目前也还没有开放 —— 将来开放时,框架走的是同一套投影模式。

## 常见坑

- **游戏一秒才产出一次的值,不要每帧都去读。** 状态流是 1 Hz;一秒读六十次只是白烧帧。
- **这不是权威。** 它是只读模型(read model):读到的是 CUO 最近听到的值,由它推出的结论没有经过任何仲裁,见[判断一个动作由哪一侧判定](decide-which-side-judges-an-action.md)。
- **`null` 不是"以后再处理"。** 现在就分支处理,因为刚加入的那一小段时间里两半本来就都没有。

## 验证它真的成了

开一局,世界里有两名玩家,按秒记录你读到的对方 Steam id 的状态:数值会在对方第一份角色快照之后出现,在对方受伤、进食、捡东西时变化。让对方离开世界再读一次 —— `TryGetPlayer` 返回 `false`。

## 相关阅读

- [声明权限与主机命令](declare-permissions-and-commands.md) —— 这一页需要的那个标志
- [给其他玩家发消息](send-a-network-message.md) —— 读到的数据需要送出去时怎么办
- [判断一个动作由哪一侧判定](decide-which-side-judges-an-action.md) —— 为什么"读数"不等于"结论"
- [你的第一个模组](../start/your-first-mod.md) —— 读数据所处的生命周期
- [术语表](../reference/glossary.md) —— 投影、快照、状态流

[文档总览](../../README.md) > [做一件事](README.md) > 读取游戏状态
