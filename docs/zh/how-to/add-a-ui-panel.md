# 在界面里加一个面板

[文档总览](../README.md) > [做一件事](README.md) > 在界面里加一个面板

---

**读完这一页**，你的模组在 CUO 界面里有了自己的窗口，你也知道它能碰什么、不能碰什么。先读[你的第一个模组](../start/your-first-mod.md)。

## 模组窗口是什么

`IModUi` 是每个模组各自的、纯本地的即时模式窗口注册表。你在 `Bind` 里注册一个 id、一个标题和一个绘制回调；界面打开时 CUO 每帧调用这个回调，所有 Unity 与 IMGUI 细节都由插件负责，你的代码里不会出现任何 Unity 类型。

- **不需要权限。** 窗口本身碰不到网络、会话或权威状态，所以任何网络模式都能用。
- **窗口只负责显示。** 共享状态仍然走模组网络或主机命令；窗口只是把模组已经知道的东西画出来。
- **控件集合故意很小**：`Label`、`Button`（点击那一帧返回 true）、`TextField`（返回编辑后的文本）、`Separator`。

## 注册一个窗口

```csharp
context.Ui.Register("status", "My Mod Status", window =>
{
	window.Label($"session active: {context.Session.SessionActive}");

	if (window.Button("ping"))
	{
		context.Network.Broadcast(Encoding.UTF8.GetBytes("ping"));
	}

	_text = window.TextField(_text);
});
```

回调每帧都会执行，所以要便宜、不要有多余分配。跨帧要保留的值由模组自己存 —— 示例把文本框的值放进自己的字段。

## 规则与故障隔离

- id 或标题为空、回调为 `null` 会被拒绝；同一个模组里用重复 id 注册第二个窗口也会被拒绝。
- `context.Ui.Unregister("status")` 移除窗口。
- 回调抛异常会在窗口里显示一行错误并记日志；它不会破坏其他人的界面帧。

## 验证它真的成了

打开 CUO 界面：窗口在、标题正确，状态与会话一致 —— 没开会话也没加入时是 `session active: False`，之后是 `True`。点按钮，再看消息处理函数留下的日志。

## 常见坑

- **不要把状态放在回调的闭包里指望它活下来。** 存进字段；下一帧回调会再被调用一次。
- **不要在窗口里干活。** 每帧都发消息的窗口会撞上发送速率限制；在点击或状态变化时发。

## 相关阅读

- [给其他玩家发消息](send-a-network-message.md) —— 按钮调用的那个方法
- [声明权限与主机命令](declare-permissions-and-commands.md) —— 窗口能驱动的另一种面
- [你的第一个模组](../start/your-first-mod.md) —— `Bind` 与 context 从哪来
- [权限与安全](../internals/permissions-and-security.md) —— 为什么本地窗口不需要任何标志

---

[文档总览](../README.md) > [做一件事](README.md) > 在界面里加一个面板
