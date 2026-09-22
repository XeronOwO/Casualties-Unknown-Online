# 你的第一个模组

[文档总览](../README.md) > [从这里开始](README.md) > 你的第一个模组

---

**读完这一页**，你已经从头到尾读过一只能跑的 CUO 模组：它由哪两个文件组成、框架交给你什么、怎么看着它跑起来。代码在
`src/CasualtiesUnknownOnline.ModExample/`，就是拿来当起点的。

## 两个文件，两种职责

一个 CUO 模组是同一个程序集里的两个类型，这个拆分是有意的。

**外壳**只为了让 BepInEx 加载这个程序集。它继承 `BaseUnityPlugin`、带 `[BepInPlugin(...)]` 特性，并且保持**空的**：

```csharp
[BepInPlugin("CasualtiesUnknownOnline.ModExample", "CUO Mod Example", "0.1.0")]
public sealed class Plugin : BaseUnityPlugin
{
}
```

这个类型由 BepInEx 自己实例化。模组类由 CUO 另外实例化，时间点是它自己的第一帧，在所有插件的 `Awake` 之后。如果让一个类型同时扮演两个角色，你会得到两个各自持有状态的对象 —— 这就是双实例陷阱，所以“两个文件”不是风格问题。

**模组类**才是模组：带 `[CuoMod(...)]` 特性的 `ICuoMod`，由 CUO 创建、永远不由 BepInEx 创建。所有逻辑都写在这里，而且只引用 `CUO.Abstractions`。

## 声明部分

```csharp
[CuoMod("cuo.example", "CUO Example", "0.1.0", NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.SendNetworkMessage | ModPermission.RegisterCommand
		| ModPermission.ExecuteHostAction | ModPermission.RegisterContent)]
public sealed class ExampleMod : ICuoMod
```

- **id**（`cuo.example`）是别的模组依赖它时用的名字，也是主机看到的名字；
- **`NetworkMode.Synchronized`** 表示两边必须跑同一个版本：没有装这个模组的成员会在加入[握手](../reference/glossary.md)时被拒绝，而不是等到后面才出错；
- **`Permissions`** 声明这个模组能做什么。没声明的就不授予，所以这份清单同时也是模组的公开面。

## 框架交给你什么

CUO 会调用一次 `Bind(IModContext context)`，所有东西都从这个 context 拿：

```csharp
context.Content.TryRegister("example.recipe", "recipe", [0x01, 0x02, 0x03]);
context.Network.MessageReceived += (sender, payload) => { /* … */ };
context.Commands.Register(new ModCommand("echo", c => $"echo:{string.Join(" ", c.Arguments)}"));
context.Ui.Register("example", "CUO Example", window => window.Label("hello"));
context.PlayerJoined += id => context.Logger.LogInformation("[Example] player {Id} joined.", id);
```

| 部件 | 它是干什么的 |
|---|---|
| `context.Logger` | 你写的日志和 CUO 自己的日志进同一个地方 |
| `context.Content` | 注册内容，框架之后把它当成世界的一部分 |
| `context.Network` | 收发你自己的消息 |
| `context.Commands` | 控制台命令；`isHostAction: true` 表示只有主机能执行 |
| `context.Ui` | 在 CUO 界面里放一个面板 |
| `context.Session` | 会话是否在进行、你是不是主机 |
| 事件 | `PlayerJoined`、`PlayerLeft`、`SessionEnded` |

## 生命周期

```
发现那一帧:  Bind → Initialize → Start → Update
之后每帧:    Update
离开时:      Stop → Dispose
```

`Bind` 在 CUO 的第一帧执行 —— 不在外壳的 `Awake` 里，这也是为什么比 CUO 晚加载的模组照样能用。从那一帧之后，`Update` 每帧调用一次。

## 看着它跑起来

1. 构建并部署你的构建 —— 见[搭好开发环境](set-up-dev-environment.md)。
2. 启动游戏：日志里会出现 `[Example] bound (session active: …, host: …)`。
3. 打开 CUO 控制台：模组的命令列在里面，是 `/echo` 和 `/whoami`。
4. 执行 `/echo hello` —— 模组回答 `echo:hello`。`/whoami` 是主机动作：只有主机能执行，它回答发起者的 Steam id。
5. 当别的成员的模组发来消息时，收到的一方会记日志，主机把它以 `echo:…` 广播回所有成员。

## 常见坑

- **绝对不要在外壳里碰 CUO 或游戏。** 它的 `Awake` 可能早于也可能晚于 CUO 的，而且只有模组类持有状态。
- **逻辑只引用 `CUO.Abstractions`，外壳只引用 `BepInEx.Core`** —— 不引用游戏程序集，不引用 `CUO.Runtime`。
- **先声明再使用。** 特性里没写的权限，用到时会被拒绝。

## 相关阅读

- [搭好开发环境](set-up-dev-environment.md) —— 构建并部署你刚读到的代码
- [装起来玩](install-and-play.md) —— 以玩家身份跑起来
- [术语表](../reference/glossary.md) —— 模组、补丁、适配器、握手
- [参考](../reference/README.md) —— 完整的模组接口将来放在这里

---

[文档总览](../README.md) > [从这里开始](README.md) > 你的第一个模组
