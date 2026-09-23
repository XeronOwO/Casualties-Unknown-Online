# 给其他玩家发消息

[文档总览](../README.md) > [做一件事](README.md) > 给其他玩家发消息

---

**读完这一页**，你的模组就能和会话里的其他机器对话，并且你知道消息没送到时框架会怎么处理。先读[你的第一个模组](../start/your-first-mod.md)。

## 先看清网络的形状

模组消息走的是星形，客机之间不直连：

```
客机 ──上报──▶ 主机 ──广播──▶ 所有成员(含主机自己)
```

这里没有自动转发：你发给某一个成员的消息不会被转给其他人，客机也不能广播。由此得到的写法是：**客机往上上报，主机做决定，主机把结果往下广播。**

## 四个调用

| 调用 | 在主机上 | 在客机上 |
|---|---|---|
| `SendToHost(payload)` | 什么都不做 —— 主机本身就是目的地 | 上报给主机那份模组 |
| `SendToPeer(steamId, payload)` | 发给某一个成员的副本 | 什么都不做 |
| `Broadcast(payload)` | 发给所有成员，包括主机自己的副本 | 什么都不做 |
| `MessageReceived += (sender, payload)` | 收到客机的上报，或主机自己的广播 | 收到定向或广播帧 |

会话之外所有发送都是空操作，所以模组不必先检查有没有会话。

## 先声明权限

发送需要在 `[CuoMod]` 特性上声明 `ModPermission.SendNetworkMessage`。没声明的发送会在发送端被拒并留一行日志；没声明的接收会被丢弃。如果你的消息无声无息地消失了，先看这个特性。

## 一个能跑的例子

```csharp
[CuoMod("com.example.ping", "Ping", "1.0.0", NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.SendNetworkMessage)]
public sealed class PingMod : ICuoMod
{
	private IModContext? _context;

	public void Bind(IModContext context)
	{
		_context = context;

		context.Network.MessageReceived += (sender, payload) =>
			context.Logger.LogInformation("[Ping] from {Sender}: {Text}",
				sender, Encoding.UTF8.GetString(payload));

		context.Ui.Register("ping", "Ping", window =>
		{
			if (!window.Button("ping"))
			{
				return;
			}

			var payload = Encoding.UTF8.GetBytes("ping");
			if (context.Session.IsHost)
			{
				context.Network.Broadcast(payload);  // 主机 → 所有人
			}
			else
			{
				context.Network.SendToHost(payload); // 客机 → 主机那份
			}
		});
	}
	…
}
```

载荷是**不透明的字节**：框架从不解析里面的内容，所以格式、版本和迁移都由你决定。片段省略的 `using` 指令可以在示例模组里看到。

## 消息没送到时

策略是**接受丢包**，而不是保证送达：

- 传输本身可靠，但超出单发送方突发额度的帧会被**直接丢弃并留日志**，不排队也不重发 —— 持续速率每秒 20 条，突发额度 40 条；
- 所以不要建立在重传上：让**下一条**消息带上完整状态，这样丢一帧损失的是新鲜度，而不是正确性；
- 载荷上限是 **64 KiB**，发送端拒绝、接收端再检查一次；
- 接收方不认识这条消息所属模组时，会丢弃并留日志。用 `NetworkMode.Synchronized` 时这种情况会更早被拦住 —— 没装这个模组的成员在加入[握手](../reference/glossary.md)时就被拒绝。

## 验证它真的成了

1. 构建、部署并启动游戏 —— 见[搭好开发环境](../start/set-up-dev-environment.md)。
2. 打开 CUO 界面，再打开模组的 `Ping` 窗口。
3. 在主机上点 **ping**：所有成员都会记下 `[Ping] from <主机的 Steam id>: ping`。
4. 在客机上点：只有主机记下这条上报，其他客机什么都看不到 —— 因为客机的上报只送给主机那一份。要让所有人都看到，就由主机再广播回去。

## 常见坑

- **客机调用 `Broadcast` 什么都不会发生。** 在那台机器上它是空操作，消息根本没出去。
- **主机调用 `SendToHost` 什么都不会发生。** 主机自己就是目的地 —— 主机想让所有人行动时用 `Broadcast`。
- **缺权限的后果是一行日志，不是异常。** 发送被拒绝，但不会有什么异常抛进你的模组。
- **模组版本不一致是会话级问题，不是消息级问题。** 用 `Synchronized` 时，版本不同的成员在加入时就被拒绝。

## 相关阅读

- [你的第一个模组](../start/your-first-mod.md) —— 这里用到的生命周期与 context
- [做一件事](README.md) —— 其他任务
- [参考](../reference/README.md) —— 完整的模组接口将来放在这里
- [谁来决定玩家身上发生的事](../internals/judgment-ownership.md) —— 为什么客机向上报、由主机回答
- [术语表](../reference/glossary.md) —— 主机、客机、会话、握手

---

[文档总览](../README.md) > [做一件事](README.md) > 给其他玩家发消息
