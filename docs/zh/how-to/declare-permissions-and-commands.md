# 声明权限与主机命令

[文档总览](../README.md) > [做一件事](README.md) > 声明权限与主机命令

---

**读完这一页**，你的模组会声明自己允许做什么，并注册一条在主机上执行的命令。先读[你的第一个模组](../start/your-first-mod.md)。

## 权限从不默认授予

默认值是 `ModPermission.None`：没声明的面就不授予。八个标志各自把关什么：

| 标志 | 把关的东西 |
|---|---|
| `SendNetworkMessage` | 模组消息通道，**发和收都算** |
| `RegisterCommand` | 注册主机命令 |
| `ExecuteHostAction` | 命令标了 `isHostAction: true` 时额外需要 |
| `ReadGameState` | 只读的游戏状态投影 |
| `WriteGameState` | 主机持久化的模组数据 |
| `RegisterContent` | 注册被框架当作世界一部分的内容 |
| `SpawnEntity` | 实体、物品、地块、结构与液体的生成 |
| `AccessNativeApi` | 精选的原生操作注册表 |

缺标志的后果是“被拒绝 + 一行日志”，不是异常。特性里同时还声明**模组的 id、版本与 `NetworkMode`**；**`NetworkMode` 没有默认值** —— 不设的模组会在发现阶段被拒；id 重复、版本不符合 SemVer、依赖无法满足，同样被拒，而且一个模组被拒不会影响其他模组的扫描。

## 两种命令面，只有一种会走网络

- `context.Commands` —— **主机命令**。处理函数只在主机那份模组上运行，客机的调用会传到主机。本页讲的就是它。
- `context.ConsoleCommands` —— **本地控制台命令**。在调用者自己的游戏里执行，不过网络；适合调试或纯客户端命令。

## 注册一条主机命令

```csharp
context.Commands.Register(new ModCommand(
	"heal",
	c => $"healed {string.Join(" ", c.Arguments)} for {c.RequesterSteamId}",
	description: "Heal a member",
	isHostAction: true));
```

`isHostAction: true` 表示只有主机能执行，它除了 `RegisterCommand` 还需要特性上的 `ExecuteHostAction`。处理函数的返回值就是请求方看到的文本；返回 `null` 表示“没有输出”。

## 客机调用时会发生什么

1. 客机的 `TryExecute` 发出一个命令请求，框架给它分配一个请求 id。
2. 主机在**你的处理函数运行之前**先校验：名字 ≤64 字符、最多 16 个参数、每个 ≤256 字符且合计 ≤4 KiB、发送方已完成[握手](../reference/glossary.md)、模组与命令已注册、权限标志齐全。对每个客机，请求速率限制为每秒 4 次、突发 8 次。
3. 你的处理函数负责语义校验 —— 框架不可能知道 `alice` 是不是真的成员 —— 并可以按 `c.RequesterSteamId` 对具体客机授权。
4. 主机回一个定向结果，按请求 id 结算调用方的回调。

```csharp
context.Commands.TryExecute("heal", new[] { "alice" }, result =>
	context.Logger.LogInformation("[Heal] {Name} ok={Ok} output={Output} error={Error}",
		result.Name, result.Success, result.Output, result.Error));
```

在主机上，回调在 `TryExecute` 返回之前就执行了。在客机上，回调等结果到达时才执行 —— 也可能永远不执行：请求丢失时，框架会在请求期限（10 秒，每帧清扫一次）之后用 `Success == false` 和一个 `timed out` 错误结算它；会话结束时，所有未结算的请求也会同样失败。每个模组最多同时有 32 个未结算请求，超出会在发送端被拒（`false`，没有回调）。

## 常见坑

- **超时不是崩溃，而是一个结果。** 永远读 `Success`：主机丢弃请求（限流、形状超限、不是成员）和结果丢失，到你这里是同一种超时。
- **不要阻塞等命令。** 结果是另外送回来的；客机的处理函数是“请求”，不是“调用”。
- **处理函数抛异常不会致命。** 它会变成 `Success == false`，异常信息进 `Error`。
- **输出有上限：** 输出 32 KiB，错误文本 4 KiB。

## 验证它真的成了

在 CUO 控制台里执行这条命令：主机打印返回的文本，客机看到的是主机执行的结果，而不是自己执行的。在客机请求还在等待时结束会话：客机的回调会以超时失败触发，而不是一直挂住。

## 相关阅读

- [给其他玩家发消息](send-a-network-message.md) —— 上报所走的通道
- [你的第一个模组](../start/your-first-mod.md) —— 特性与生命周期
- [参考](../reference/README.md) —— 完整的模组接口将来放在这里

---

[文档总览](../README.md) > [做一件事](README.md) > 声明权限与主机命令
