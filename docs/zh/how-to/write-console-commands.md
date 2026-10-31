# 写一条控制台命令

[文档总览](../README.md) > [做一件事](README.md) > 写一条控制台命令

---

**读完这一页**，你的模组会拥有一条玩家能找到、能补全、能读懂的控制台命令：一条在他自己游戏里跑，一条在主机上跑。请先读[你的第一个模组](../start/your-first-mod.md)。

## 两条配方，怎么选

控制台命令就是一个通过 `context.ConsoleCommands` 注册的 `ICommandDefinition`。**作用域**决定处理器在哪跑：

| 作用域 | 处理器在哪执行 | 适合做什么 |
|---|---|---|
| `CommandScope.Local`（默认） | 打这行命令的客户端 | 只跟这个客户端有关的事：面板、本地诊断、客户端开关 |
| `CommandScope.HostRelayed` | 主机，在主机那份模组上 | 任何会改变共享世界、或需要主机权威的事 |

`HostRelayed` 要求**两端都装了**这个模组——主机得知道这条定义才能执行；握手本来就会拒绝版本不一致的会话，所以"两端都有"在进场时就查过了。

## 配方一：本地命令，带固定选项的补全器

```csharp
context.ConsoleCommands.Register(new ConsoleCommandDefinition(
	"exampleweight",
	"设置示例敷料显示的重量",
	invocation => SetWeight(invocation),
	parameters:
	[
		new ConsoleCommandParameter(
			"weight",
			new FixedChoiceCompleter("0.5", "1"),
			hintKey: "cuo.example.weight.hint"),
	],
	aliases: ["eweight"],
	usage: "/exampleweight <weight>"));
```

- **参数回答的优先级**：参数自己的 `IArgumentCompleter` → 出厂补全器（`FixedChoiceCompleter`、`DelegateCompleter`）→ 它声明的 `Kind` 对应的框架来源（`Selector`、`ResourceLocation`、`PlayerOrSteamId`……）。只想要"一个成员名"的模组，声明种类即可，不用写补全代码。
- **补全器看得到前面已经输入的内容**：`ICompletionContext.Arguments` 是已接受的参数，所以第 N 个参数可以依赖第 N-1 个。
- **`hintKey`** 是候选旁边那句灰字的键，必须归**你的模组**所有（见下一节）。

## 配方二：在主机上执行、用读者语言回答

```csharp
context.Localisation.RegisterCatalog("en", """{ "cuo.example.hostecho.answer": "the host answered {0}: {1}" }""");
context.Localisation.RegisterCatalog("zh", """{ "cuo.example.hostecho.answer": "主机回答 {0}：{1}" }""");

context.ConsoleCommands.Register(new ConsoleCommandDefinition(
	"examplehost",
	"请主机复述一句话",
	invocation => ConsoleAnswer.Ok(
		$"host echo for {invocation.RequesterSteamId}: {string.Join(" ", invocation.Arguments)}",
		"cuo.example.hostecho.answer",
		invocation.RequesterSteamId.ToString(),
		string.Join(" ", invocation.Arguments)),
	scope: CommandScope.HostRelayed,
	parameters: [new ConsoleCommandParameter("text")],
	usage: "/examplehost <text>"));
```

- **答案由"键 + 参数"组成，文本只是兜底。** 成员端会从模组自己的目录渲染那个键，所以作者写一次，中文玩家读到的就是中文；文本留给日志，以及没有这条目录项的客户端。
- **`RequesterSteamId` 是打字的那个人**，尽管处理器是在主机上跑的——一句"你"指的是玩家，不是那台机器。
- **回复是异步的。** 成员端调用会在答案到达之前返回；答案到达时才打印，不要指望同一帧就在 `Lines` 里看到。
- **你的目录必须活在自己的命名空间下。** 每个键都以"模组 id + 点"开头；只要有一个键越界，整张表都会被拒收，而且模组永远覆盖不了框架自己的句子。

## 把句子当数据发出去

示例模组每种语言放一个文件，绑定时注册；翻译只需要改 JSON，不用碰 C#：

```text
Localization/en.json   { "cuo.example.hostecho.answer": "the host answered {0}: {1}" }
Localization/zh.json   { "cuo.example.hostecho.answer": "主机回答 {0}：{1}" }
```

把它们作为嵌入资源加进项目，在 `Bind` 里读出来：

```csharp
using var stream = typeof(MyMod).Assembly.GetManifestResourceStream("My.Mod.Localization.en.json");
using var reader = new StreamReader(stream!);
context.Localisation.RegisterCatalog("en", reader.ReadToEnd());
```

## 坑

- **被拒的注册只进日志，不抛异常。** 表格式错误或键越界时，模组照常运行、句子回落到文本——首次运行后记得看日志。
- **不要在处理器里直接打印玩家可见的文案。** 返回 `ConsoleAnswer.Ok(text, key, args)`；`Refused` 同理，会用错误色显示。
- **参数的 `Kind` 是兜底，不是承诺。** 如果补全对某个参数不合适，就给这个参数自己的补全器，不要覆盖框架的。
- **别名同样占词。** 名字或别名重复时注册会被拒，并给出原因。

## 验证真的成了

打开控制台（`/`），输入命令名和一个空格：列表会显示这个参数能填什么，Tab 能补全。`HostRelayed` 那条要从**成员端**执行：主机日志会记下发起者，成员端控制台会用自己语言显示从模组目录渲染出来的答案。

## 相关阅读

- [声明权限与主机命令](declare-permissions-and-commands.md) —— 八个标志，以及更早的主机命令接口
- [你的第一个模组](../start/your-first-mod.md) —— 这两条配方所依据的代码
- [参考：模组接口](../reference/mod-api.md) —— `ConsoleCommands`、`Localisation` 以及它们的每个成员
- [术语](../reference/glossary.md) —— 主机转发、定义、补全器

---

[文档总览](../README.md) > [做一件事](README.md) > 写一条控制台命令
