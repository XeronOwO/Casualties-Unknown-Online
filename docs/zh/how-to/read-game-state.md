# 读取游戏状态

[文档总览](../README.md) > [做一件事](README.md) > 读取游戏状态

---

**读完这一页**，你的模组能读到 CUO 手里已经有的、关于另一名玩家的状态 —— 他在不在世界里、生命体征（vitals）、随身物品 —— 还能按编号跟住一件物品走到哪去了，并且知道每一份读数有多新。先读[你的第一个模组](../start/your-first-mod.md)。

## 权限就是开关

读数据由 `ModPermission.ReadGameState` 把关；这个标志本身在[声明权限与主机命令](declare-permissions-and-commands.md)里讲。

```csharp
if (context.GameState.CanRead && context.GameState.TryGetPlayer(steamId, out var player))
{
	var health = player.Vitals?.BrainHealth;
	var items = player.Inventory?.Items;
}
```

这份模组声明了这个标志，`CanRead` 才是 true。每次 `TryGetPlayer` 都会再校验一遍，不满足时返回 `false` 并写一行日志，而不是抛异常 —— 忘声明标志的模组表现为“读不到数据”，不会表现为崩溃。

## 一名玩家长什么样

| 成员 | 里面是什么 |
|---|---|
| `SteamId` | 这是谁的状态 |
| `InWorld` | 本侧的会话当前是否知道这名玩家在世界里 |
| `Vitals` | `IModPlayerVitals`；第一份健康数据到达之前是 `null` |
| `Inventory` | `IModPlayerInventory`；第一份物品快照到达之前是 `null` |

`Vitals` 带 `BrainHealth`、`Hunger`、`Thirst`、`Stamina`、`Energy`、`Temperature`，以及派生出来的 `Alive`、`Conscious` 两个标志。`Inventory` 带顶层 `Items`、原始的 `HandSlot` 值和 `Count`；每一条 `IModInventoryEntry` 给出 `InstanceId`、`ItemId`、`SlotIndex`、`Condition`、`Favourited` 和可递归的 `Contents`，所以容器的内容物是按树走的。`SlotIndex` 为负表示这件物品穿在身上 —— 那是游戏自己的编码，不是 CUO 的发明。

## 按编号跟住一件物品

一个实例编号只指一件物品，从出生到结束。拿这个编号去读，就能知道这件东西是什么、现在在哪 —— 无论你记下它之后它搬去了哪里：

```csharp
if (context.GameState.TryGetItem(itemId, out var item))
{
	switch (item.Location)
	{
		case ModItemLocation.Applied:
			// 装在玩家 item.OwnerSteamId 的第 item.LimbIndex 条肢体上
			break;
		case ModItemLocation.Carried:
		case ModItemLocation.Contained:
			// 在玩家 item.OwnerSteamId 身上；装在容器里时 item.ParentItemId 是那个容器
			break;
		case ModItemLocation.World:
			// 掉在 item.X、item.Y
			break;
		case ModItemLocation.Terminal:
			// 这个编号已经结束，而且永不复用
			break;
	}
}
```

| `Location` | 这件东西在干什么 | 哪些成员有意义 |
|---|---|---|
| `World` | 掉在世界里 | `X`、`Y`；如果它落在某个世界容器里，还有 `ParentItemId` |
| `Carried` | 在玩家的背包、手上或身上 | `OwnerSteamId` |
| `Contained` | 装在另一件物品里 | `OwnerSteamId`、`ParentItemId` |
| `Applied` | 作为夹板或止血带装在某人某条肢体上 | `OwnerSteamId`、`LimbIndex` |
| `Terminal` | 被消耗、被毁、被烹饪或被制作掉 | 没有 —— 这个编号结束了 |

`InstanceId`、`ItemId`、`Condition` 与 `Favourited` 无论位置是什么都在；其余成员一律是 `0`（肢体那条是 `-1`），所以该读哪些成员由 `Location` 决定。唯一可能让人意外的是 `ParentItemId`：装在容器里的物品确实在它里面，而一件掉在世界里的物品也可能挂在容器上 —— 世界容器的内容物就是记成挂在容器上的世界物品 —— 所以父编号非 0 与「装在容器里」并不是同一件事。

这个读法有三点是刻意讲究的：

- **已装物品不在任何人的 `IModPlayerInventory` 里** —— 玩家并没有随身带着它，放一条背包条目进去就等于说他们带着。它保住了自己的编号，所以 `TryGetItem` 能在肢体上找到它，等它被取下来，又能用同一个编号在操作者手里找到同一件东西。
- **「结束了」「从未存在」与「还没到」是三个不同的答案。** 编号的一生已经结束时返回 `true`，`Location` 是 `Terminal`；你这台客户端手里没有这个编号的事实则返回 `false` —— 刚加入、还没收到加入检查点的客机手里是**所有**编号都没有，`0`（表示「还没分配」）也同样是 `false`。只有 `splint`、`carcasssplint` 与 `tourniquet` 这三件东西能把编号带过「装到肢体上」这道变换；其余肢体工具 —— 冰袋、绷带、缝合线 —— 根本不会把这件东西变成别的什么，而烹饪与制作会结束原来那一行并给产物一个**新**编号，所以生肉的编号不是熟肉的编号。

## 这是一份副本，而且最多旧一秒

这份投影装的是 1 Hz 角色流上已经在到达的数据 —— 内置联机界面画的就是同一个来源。它不是推给你的：

- **按自己的节奏读。** 每次 `TryGetPlayer` 返回的是那一刻缓存的事实；返回的对象是副本，调用之后（比如跨帧）依然有效。
- **缺一半是正常的。** 生命体征和物品是两份不同的快照，所以可能一半是 `null` 而另一半已经有值。`null` 要当成“还没到”，不能当成 0。
- **缓存属于会话。** 玩家离开世界、或者会话结束，缓存就被清掉，`TryGetPlayer` 开始返回 `false`；两半都还没到的时候同样返回 `false`，所以拿到 `true` 不等于它会一直是 `true`。
- **两个读法的来源不同。** `TryGetPlayer` 读的是 1 Hz 角色流；`TryGetItem` 读的是框架自己的物品事实，也就是一次搬迁所提交的那份事实。每台客户端都持有它 —— 主机是它自己的记录，客机是每个已提交批次与加入检查点填出来的镜像 —— 所以队友那一侧看到物品移动的理由与主机相同。

## 这一片没有覆盖什么

本机玩家自己的角色状态不在 `IModGameState` 里；它走原生接口的只读本机玩家投影。方块与实体表同样没有开放，而且这里也没有枚举物品的办法：按编号读只回答你手里已经有的编号，所以它是「跟住一件物品」的读法，不是把世界倒出来。一件世界物品的位置是它唯一携带的世界事实。

## 常见坑

- **游戏一秒才产出一次的值，不要每帧都去读。** 玩家那条流是 1 Hz；一秒读六十次只是白烧帧。按编号读本身很便宜，但一件物品的状况也只有被什么东西推动时才会变。
- **这不是权威。** 它是只读模型（read model）：读到的是 CUO 最近听到的值，由它推出的结论没有经过任何仲裁，见[判断一个动作由哪一侧判定](decide-which-side-judges-an-action.md)。
- **`null` 不是“以后再处理”。** 现在就分支处理，因为刚加入的那一小段时间里两半本来就都没有。
- **`TryGetItem` 返回 `false` 不等于「它被毁掉了」。** 那要看 `Location` 是不是 `ModItemLocation.Terminal`；`false` 要当成「框架手里没有这个编号」。

## 验证它真的成了

开一局，世界里有两名玩家，按秒记录你读到的对方 Steam id 的状态：数值会在对方第一份角色快照之后出现，在对方受伤、进食、捡东西时变化。让对方离开世界再读一次 —— `TryGetPlayer` 返回 `false`。

至于物品读法：把你刚捡起来的某件东西的编号记下来并一直读它 —— 你搬动它时它还是同一件东西，而队友腿上夹着夹板的那段时间，它读出来是 `Applied`，肢体号也对得上。给队友装一块夹板，再从你自己的客户端读那个编号：你能看到它在那条肢体上，而这是任何背包投影都不会告诉你的。

## 相关阅读

- [声明权限与主机命令](declare-permissions-and-commands.md) —— 这一页需要的那个标志
- [给其他玩家发消息](send-a-network-message.md) —— 读到的数据需要送出去时怎么办
- [判断一个动作由哪一侧判定](decide-which-side-judges-an-action.md) —— 为什么“读数”不等于“结论”
- [你的第一个模组](../start/your-first-mod.md) —— 读数据所处的生命周期
- [状态流与快照](../internals/state-and-snapshots.md) —— 读数从哪来，以及为什么它不是结论
- [术语表](../reference/glossary.md) —— 投影、快照、状态流

---

[文档总览](../README.md) > [做一件事](README.md) > 读取游戏状态
