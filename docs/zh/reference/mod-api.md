# 模组接口契约

[文档总览](../README.md) > [参考](README.md) > 模组接口契约

---

**读完这一页**，你能查到模组可以声明什么、CUO 真正强制什么、有哪些接口面，以及每一个接口面承诺到什么程度。这是模组作者写代码所依据、框架也照此履行的契约；背后的道理在[模组的一生](../internals/mod-loading-lifecycle.md)与[权限与安全](../internals/permissions-and-security.md)。还没写过模组的话，先读[你的第一个模组](../start/your-first-mod.md)。

## 模组可以引用什么

`CUO.Abstractions` 是模组唯一引用的程序集，而这句话说的是**契约**，不是给模组划的围栏：按名字给 CUO 自己的实现打[补丁](glossary.md)是允许的，但不带任何承诺；需要用到游戏自己代码的模组，通过声明式的[原生绑定](glossary.md)这一层去绑。哪一层是承诺、哪一层只是实现，模组可以改什么、作者能指望哪些诊断信息，写在 `docs/api/advanced-modification-policy.md`，它的 §1.1 就是那张层级表。

## 模组是怎么被加载的

BepInEx 5 在一个循环里逐个加载插件，先加载、再 `Awake`。由此有两条规则：

1. **发现发生在框架的第一个更新帧**，不在它自己的 `Awake` 里 —— 在 `Awake` 里扫描会漏掉之后加载的每一个插件。`ModService` 只扫一次 `AppDomain.GetAssemblies()`，找出所有用 `[CuoMod]` 声明的 `ICuoMod` 类型，校验它们，把通过的集合按依赖做拓扑排序，然后绑定。发现那一帧依次跑 `Bind` → `Initialize` → `Start` → `Update`；之后 `Update` 每帧都跑，`Stop`／`Dispose` 按加载的逆序执行。
2. **模组的 BepInEx 外壳 `Awake` 必须留空** —— 它可能在 CUO 插件的 `Awake` 之前或之后运行，不能碰任何 CUO 接口。

布局必须避开两个坑：

- **双实例陷阱。** BepInEx 实例化外壳（`BaseUnityPlugin`），CUO 实例化 `[CuoMod]` 类。让同一个类型同时扮演两个角色，会得到两个各自持有状态的实例，所以外壳与模组类永远是两个类型；`src/CasualtiesUnknownOnline.ModExample/` 就是标准布局。
- **与「不引用 BepInEx」的对账。** BepInEx.Core 只作为加载机制被引用（外壳继承 `BaseUnityPlugin`，仅此而已）；每一行业务逻辑只引用 `CUO.Abstractions`。

## 清单

`[CuoMod]` 是唯一的清单元数据来源：id、显示名、版本、`NetworkMode`、`Permissions`、`Dependencies`、描述、`Namespace` 与 `NativeBinding`。

```csharp
[CuoMod("com.example.mymod", "My Mod", "1.0.0", NetworkMode = NetworkMode.Synchronized,
        Permissions = ModPermission.SendNetworkMessage | ModPermission.RegisterCommand,
        Dependencies = new[] { "com.example.dependency" }, Description = "...")]
public sealed class MyMod : ICuoMod   // ICuoService lifecycle + Bind
{
    public void Bind(IModContext context) { ... }   // once, before Initialize
    // ICuoService: Initialize / Start / Update / Stop / Dispose
}
```

**`NetworkMode`** 默认是 `Unspecified`，并在发现阶段被拒。其他拒绝原因：id 重复、类型是抽象或非公开、缺少公开无参构造函数、版本不符合 SemVer、权限位不认识、在 `ClientOnly`／`Cosmetic` 上声明了主机／状态类权限，以及依赖不合法或无法满足（目标缺失、自依赖、重复、成环 —— 传递失败会波及依赖它的模组）。声明了 `Namespace` 还会多三条：语法不合法、占用了保留的内置命名空间 `cu`，或者该命名空间已被另一个已加载模组声明。一个被拒的模组永远不挡扫描：逐模组 fail-closed。

**`Permissions`** 从不隐含 —— 默认是 `ModPermission.None`。八个可声明标志与它们真正的执行点：

| 标志 | 它管住什么 |
|---|---|
| `SendNetworkMessage` | `IModNetwork` 的**发送与接收** |
| `RegisterCommand` | 命令注册 |
| `ExecuteHostAction` | 另外还管住 `ModCommand.IsHostAction` |
| `WriteGameState` | 主机持久化的模组状态写入（`IModState`） |
| `RegisterContent` | 模组内容注册（`IModContent`） |
| `ReadGameState` | 只读的游戏状态投影（`IModGameState`） |
| `SpawnEntity` | 世界实体生成（`IModEntitySpawn`）、世界物品生成（`IModItemSpawn`），以及地块／方块、结构与液体的放置接口面（`IModTilePlacement`、`IModStructurePlacement`、`IModLiquidPlacement`） |
| `AccessNativeApi` | 经过筛选的原生／游戏私有操作[注册表](glossary.md)（`IModNativeApi`） |

**`Dependencies`** 列出的模组 id 会先于依赖方加载。**`NativeBinding`** 声明模组绑定的是游戏自己的哪些代码 —— 即 `docs/api/advanced-modification-policy.md` §1.1 里的 Tier 2。它是声明出来的**事实**，不是权限，也不是拒绝原因：`ModPermission` 才是 CUO 强制的部分，而这里 CUO 什么都不强制，所以未声明也无从探测，这条声明是「自愿坦白」，唯一的作用力来自同伴的对等规则。发现阶段会把空值（空串或纯空白）归一成「没有声明」，并写进 `[Mods] discovered …` 那行日志，于是主机的日志不用读任何模组源码就能回答「哪个模组绑了游戏自己的代码」。它买到的是可见性，绝不是稳定性：它点名的是游戏的东西，一次游戏更新就可能弄坏它，而 CUO 无从置喙。这条声明还会随[握手](glossary.md)一起走（每个 `ModInfoMsg` 都带着它），由主机的 `NativeBindingParity` 规则按模组 id 判定 —— 见[握手一致性](#握手一致性)。

## 生命周期与上下文

`ICuoMod : ICuoService` —— 标准生命周期由框架的泵在 Unity 主线程上驱动，每个阶段都被异常隔离。`Bind` 拿到的是 `IModContext`：

| 成员 | 语义 |
|---|---|
| `Session` | **绑定时刻的快照**，不是实时视图 —— 主机永远不会触发 `SessionActivated`（它在大厅创建时就激活了），发现之前触发的事件也已经丢了。这份快照是唯一可靠的「当前状态」；`MemberSteamIds` 是成员集合，本机自己是 `LocalSteamId`。 |
| `Logger` | 模组自己的日志器，行里带模组 id（`[Mod:<id>]`）。 |
| `Network` | 模组消息通道。 |
| `Commands` | 主机权威命令。 |
| `ConsoleCommands` | 本地游戏内控制台命令，不过网络；主机命令接口面是 `Commands`。 |
| `State` | 主机持久化的模组状态。 |
| `Data` | 运行时按作用域声明的模组数据。 |
| `StatusRuntime` | 运行时的逐玩家／逐肢体模组状态值。 |
| `MoodleRuntime` | 本地逐状态的心情图标（moodle）表现解析器。 |
| `BuildingRuntime` | 逐模组的建筑预制体／实例钩子。 |
| `Ui` | 本地即时模式（immediate-mode）模组窗口。 |
| `Content` | 模组内容注册。 |
| `ContentOwners` | 框架范围内的内容归属查询。 |
| `ResourceCompletion` | 逐模组的资源补全阶段。 |
| `GameState` | 只读的玩家状态投影。 |
| `EntitySpawn` / `ItemSpawn` | 世界实体生成／世界物品生成。 |
| `TilePlacement` / `StructurePlacement` / `LiquidPlacement` | 地块方块／多格结构／液体地块放置。 |
| `NativeApi` | 经过筛选的原生／游戏私有操作注册表。 |
| `SessionActivated` | 第一个成员的握手完成。**主机侧：永不触发** —— 读快照。 |
| `PlayerJoined` / `PlayerLeft` | 某个成员的握手完成／某个成员被移除（主机侧）。这不是世界内实体的加入。每个成员各触发一次，包括你自己。 |
| `SessionEnded` | 会话拆掉了。主机退出时客机不会收到针对主机的 `PlayerLeft`，只有 `SessionEnded`。 |

## 模组消息

`IModNetwork` —— 上报／定向语义、星形拓扑、**不自动转发**：

| 调用 | 主机 | 客机 | 说明 |
|---|---|---|---|
| `SendToHost(payload)` | 空操作 | 上报给主机那份模组 | 不在会话里：空操作 |
| `SendToPeer(steamId, payload)` | 发给某一个成员的那份 | 空操作 | |
| `Broadcast(payload)` | 发给每一个成员，**包括**主机自己那份（以本机 SteamId 本地触发） | 空操作 | 「所有一侧都跑这段」用这个 |
| `MessageReceived` | `(senderSteamId, payload)` —— 客机的上报，或主机自己的广播 | 一条定向或广播帧 | |

模组必须声明 `SendNetworkMessage`：没声明的发送在发送端被拒（空操作加一行日志），没声明的接收被丢弃。[载荷](glossary.md)是**不透明的**；不认识的模组 id 会被丢弃并留日志。传输本身可靠；单发送方的速率上限是持续 20 条每秒、突发 40 条（`ModRateLimitPolicy`）。

**丢帧是被接受的损失。** 超出突发额度的帧直接丢弃并留日志，从不排队，也不会重发：`ModMessage` 与模组状态传输（`ModStatusTransport`）按设计就容忍丢失 —— 它们携带不透明载荷、没有回填，所以恢复手段是**下一条**消息，而不是重试。只有命令的请求／结果这一对是有结算的，结算依据是请求方自己的截止时间。

**64 KiB 载荷上限** —— 这是框架策略（`ModChannel.MaxPayloadBytes`），不是行长度限制：发送端拒绝，接收端再查一次。

## 主机命令

```csharp
context.Commands.Register(new ModCommand("heal", ctx =>
    $"healed {string.Join(" ", ctx.Arguments)} for {ctx.RequesterSteamId}",
    description: "Heal a member", isHostAction: true));
context.Commands.TryExecute("heal", new[] { "alice" }, result => { /* ... */ });
```

- **主机权威执行**：处理函数只在主机那份模组上跑。主机自己调用是同步完成的（回调先于返回）；客机调用会发出 `ModCommandRequest`（`NetMsg` 86），主机校验并执行自己那份，再用定向的 `ModCommandResult`（`NetMsg` 87）回答，按请求 id 结算客机挂起的回调。
- 执行前的框架检查：请求形状上限（名字 ≤64、参数 ≤16 个、每个 ≤256 字符、合计 ≤4 KiB）、发送方是已握手的成员、模组 id 与命令已注册、权限标志、逐客机请求速率上限（每秒 4 条、突发 8）。模组的处理函数仍是语义校验者，可以按 `ctx.RequesterSteamId` 做逐客机授权。
- 处理函数抛出的异常变成 `Success=false` 的结果，带上异常消息；输出上限 32 KiB（错误 4 KiB）。会话结束或框架停机时，挂起的客机回调以失败结算；下面的截止时间会更早结算其余的。
- **请求截止时间与有界的挂起表**：客机请求由结果帧、请求方自己的截止时间（`ModCommandPolicy.CommandRequestTimeoutMs`，10 秒，每帧扫一次），或上面的会话结束／停机失败来结算。截止时间是所有静默丢失的统一答案：主机丢掉的请求（限流、形状超限、不是已握手成员）与结果丢失的请求，都会让调用方拿到 `Success=false` 和 `timed out` 错误，而不是一直挂到会话结束。主机也从不回答超突发额度的帧 —— 回答会让一个灌流的同伴把主机的出站流量推过限制单个成员的令牌桶 —— 所以丢帧与结果丢失在调用方看到的是同一个可观测原因。每个模组最多同时有 `ModCommandPolicy.MaxPendingRequests`（32）个未结算请求；超过上限的调用在发送端被拒（`false`、无回调，与其他发送端拒绝同一个契约）。
- 客机侧的结果回调只发给请求方。请求 id 不认识的结果（已经超时的请求，或这份拷贝从没发过的请求）会被丢弃并留日志；失败结果里带着它的请求 id 与命令名。

## 模组状态

```csharp
context.State.TrySet("loadout", bytes);       // host-only + WriteGameState
context.State.TryGet("loadout", out var bytes);
context.State.TrySetSchemaVersion(2);
```

- **作用域**：`IModState` 按模组 id 划分 —— 一个模组只能读写自己那一条。值是不透明的 `byte[]`；框架从不解释、也不序列化模组载荷，所以结构与迁移都归模组自己。
- **存档权只属主机**：`TrySet`／`TrySetSchemaVersion`／`TryRemove`／`TryClear` 需要主机角色**并且**有 `ModPermission.WriteGameState`。客机那份 `CanWrite` 是 false，也读不到主机的表；需要主机状态的同步类模组要通过 `IModNetwork`／`IModCommands` 协调。
- **持久化**：主机在 `BepInEx/config/CasualtiesUnknownOnline.mod-state.bin` 写一个带版本的 protobuf 文件（临时文件加替换，原子写）。每次写入落盘整张表；内存中的表是进程级的，在发现／`Bind` 之前加载一次。文件不存在视为空表；文件损坏或版本不认识则带一行警告退化成空表 —— 绝不启动崩溃，也绝不猜着迁移。
- **元数据**：文件里带模组 id、模组版本（最后写入方）与模组自己声明的[结构版本](glossary.md)。`SchemaVersion` 默认 1；框架原样存下、不替你迁移。
- **模组缺失时的策略**：当前未加载的模组，它那条记录原样保留，模组回来时数据还在。
- **安全栏**：键 ≤128 字符、每个模组 ≤1024 个键、单值 ≤64 KiB。越界是拒绝加一行日志，绝不静默截断。

## 模组界面

```csharp
context.Ui.Register("status", "My Mod Status", window =>
{
    window.Label($"session active: {context.Session.SessionActive}");
    if (window.Button("ping"))
    {
        context.Network.Broadcast(Encoding.UTF8.GetBytes("ping"));
    }
    var text = window.TextField(_lastText);
    _lastText = text; // the mod owns persistent UI state
});
context.Ui.Unregister("status");
```

- **作用域**：`IModUi` 是逐模组、只在本地生效的即时模式窗口登记表。模组在 `Bind` 里登记一个 id、一个标题和一个绘制回调；CUO 每帧调用回调，所有 IMGUI／Unity 细节由 Unity 插件负责。
- **不需要权限**：一个界面窗口本身碰不到网络、会话或游戏权威状态，所以任何网络模式都能用。要共享的界面状态仍然走 `IModNetwork`／`IModCommands`；窗口只是在本地把那份状态显示出来。
- **控件清单**：`Label`、`Button`（点击时返回 true）、`TextField`（返回编辑后的值，持久化归模组）与 `Separator`。这个集合刻意很小 —— 不让任何 Unity 类型渗进 `CUO.Abstractions`。
- **规则**：id 或标题为空、绘制回调为 null 会被拒；同一个模组内 id 重复会被拒；`Unregister` 撤掉窗口。
- **失败隔离**：绘制回调抛异常，会在窗口里显示一行错误并由插件记日志；它永远不会破坏界面帧。
- **不改线上格式**：纯粹是本地表现。

## 内容注册

```csharp
if (context.Content.CanRegister)
{
    var itemBytes = new ModItemDefinition
    {
        DisplayName = "Wooden Sword",
        Description = "A simple wooden sword.",
        Weight = 1f,
        Value = 5,
        Usable = true,
        Tags = "weapon",
        TemplateId = "stone",
        SpawnComponents = ["Example.WoodenSwordBehaviour, ExampleMod"]
    }.ToPayload();

    context.Content.TryRegister("wooden.sword", ModContentKind.Item, itemBytes);
    context.Content.TryRegister("healing.recipe", ModContentKind.Recipe, myRecipeDefinitionBytes, schemaVersion: 2);
}
var itemDefs = context.Content.Definitions; // snapshot; payloads are copied on read
context.Content.TryUnregister("wooden.sword");
```

- **作用域**：`IModContent` 是逐模组的不透明内容定义登记表（物品定义、武器数值、NPC 类型、配方、技能、地图条目）。模组在 `Bind` 里登记 id、[内容类别](glossary.md)与不透明载荷，也可以声明一个正数的 `schemaVersion`（默认 1）。框架从不解释、序列化或迁移载荷，所以内容结构与版本归模组自己；存下的版本在每次读取定义时原样带出。
- **带命名空间的内容 id（content id）**：每个 CUO 内容 id 都是规范的 `namespace:path`。在清单里声明命名空间（`[CuoMod(..., Namespace = "mymod")]`），内容就可以用 `mymod:wooden.sword` 寻址，而游戏自带内容是 `cu:<item id>`（例如 `cu:fentanyl`）。[命名空间](glossary.md)的语法是 `[a-z][a-z0-9_]{0,31}`，路径是 `[a-z0-9][a-z0-9_.-]{0,94}`；Abstractions 里的 `ContentId` 负责解析、格式化，并把输入统一成小写。没声明命名空间的模组保留裸 id（按模组划分；跨模组重复仍会被报成冲突）。裸 id 同时也是内容提供者落到游戏表里的键，所以两个不同命名空间的模组为同一类别注册同一个裸 id，仍然是冲突 —— 规范地址能解析开，但游戏里只能存在一个条目。控制台的 `ResourceLocation` 补全接受规范 id 前缀、裸 id 或本地化显示名，并且总是插入规范 id。
- **权限**：注册需要 `ModPermission.RegisterContent`。`CanRegister` 反映这份模组拷贝有没有声明该标志；每次 `TryRegister` 还会再强制一次。权限策略本来就拒绝在 `ClientOnly`／`Cosmetic` 上出现这个标志，所以只有带状态的模组才能注册内容。
- **进程本地**：内容字节不过网络。内容是模组自身的一部分，所以一致性边界就是握手（模组 id／SemVer／权限／网络模式）；需要按客户端动态生成内容的模组要改用 `IModNetwork`／`IModCommands` 协调。
- **规则**：id 或类别为空、载荷为 null 或超上限、结构版本不是正数、同一个模组内 id 重复，都会被拒；`TryUnregister` 撤掉一条定义。内容 id 必须已经是规范的小写路径段（`ContentId.IsValidPath`）：大写、空白、`:` 分隔符，以及超过 95 字符的 id 都被拒。安全栏：类别 ≤64 字符、结构版本必须为正、载荷 ≤64 KiB、每个模组 ≤1024 条定义。越界是拒绝加一行日志，绝不静默截断。
- **框架读取视图**：`IModContentControl.Entries` 把每个模组登记的定义以只读快照的形式暴露给 CUO 的其他层（插件，以及将来的原生内容消费者）。运行时内容目录（`IModContentCatalog`）在此之上加了类别筛选、按类别加 id 的唯一解析，以及跨模组重复／结构版本冲突的诊断，而不解释载荷；它是将来原生内容绑定器的既定基础。
- **内容归属查询**：`context.ContentOwners.TryGetOwner(kind, id, out owner)` 能查出任一框架范围内注册内容的归属模组 id。它是 CUCoreLib 那套逐类别 `TryGetOwnerModGuid` 的迁移替代；查询只读、不需要权限，匹配与歧义策略与运行时目录一致 —— 类别加 id 重复就返回 false。
- **共享内容的绑定边界**：运行时内容绑定器只接收那些网络模式能保证「每个可能收到这些内容实例的玩家都有同一份」的模组内容（`Synchronized`、`Authoritative`、`RequiresAllPlayers`）。`HostOnly`、`ClientOnly` 与 `Cosmetic` 的内容永远不绑进共享世界状态：没装同一个模组的客机无法安全地把主机专属物品实体化出来。这是「本地模组数据」与「公开／共享模组数据」之分在静态内容上的第一次具体实现。

**有类型的内容定义。** Abstractions 为每个众所周知的类别提供一个 DTO；模组填好、调用 `ToPayload()`，框架依旧不透明地存字节，真正解码的是 Game Adapter。

| DTO（类别） | 它携带什么，适配器怎么绑 |
|---|---|
| `ModItemDefinition`（`Item`，第一个） | 显示名、描述、分类、重量、价值、可用／可穿／可毁标志、标签、生成频率、可选的游戏自带 `TemplateId`（运行时预制体基底）、可选的 `SpawnComponents`（构建模板时挂上的组件类型名）、可选的 `WorldSpawnPerChunk`（散落式世界生成分布）、可选的 `DropSources`（显式固定的尸体／箱子／商人掉落池）、可选的 `DecayMinutes`，以及可选的 `Container`／`Battery`／`Light`／`Tool`／`Gun` 行为 DTO、可选的 `Visual`（穿戴精灵、液体遮罩资源路径，以及基础、穿戴与注液三种精灵的逐帧动画）和一个可扩展的 `CustomData` 字典。提供者把物品注册进 `Item.GlobalItems`，并在设置了 `TemplateId` 时用那个预制体构建一个未激活的运行时模板、改名为自定义 id、挂上请求的组件，然后通过 CUO 的物品预制体解析接缝供出去 —— 于是 CUO 自己的恢复／生成路径、一个窄的 `Utils.Create` 前缀，以及针对 `BuildingEntity.Update` 与 `SaveSystem.TryLoadGame` 资源加载的定点 transpiler，都能把自定义物品实体化出来。`DropSources` 让自定义物品进入显式的游戏自带掉落容器，而不是（或同时外加）通用分类池：它按稳定的合成 `ItemLootPool` 类别注册到尸体、内置的医疗／食物／容器箱、空投舱、舱体容器与商人 1-3 库存之下，再由窄补丁 `CorpseScript.Start`、`BuildingEntity.Start` 与主机侧 `TraderScript.GenerateSingleItemList` 把这些类别接进原版掉落流程；物品同时从通用池里移除，所以「固定来源」意味着作者指定的来源就是权威。`Container`／`Battery`／`Light`／`Tool`／`Gun`／`Visual` 负责作者能写的那一小片原版行为与表现：数值会被校验，工具／枪械的静态使用默认值与电池衰减标志写进 `ItemInfo`，组件配到运行时模板上，`Light` 通过 GameAdapter 既有的反射约定拿到 URP 的 `Light2D` 类型（URP 不在引用图里），`Visual` 把精灵解析成逐实例的表现状态组件 —— 穿戴精灵钩子在穿戴／丢下以及远端克隆／恢复路径上应用与还原精灵，液体遮罩钩子在 `WaterContainerItem.Start` 之后重新套用注液精灵。`Visual.MultiWornSprites` 通过游戏的 `Wearable.CreateSprites` 路径把叠加精灵写到具名的原版肢体上，穿戴时过滤掉缺失的肢体，创建后再按肢体设偏移与排序；`Visual.BaseSpriteAnimation`、`WornSpriteAnimation` 与 `LiquidMaskAnimation` 接受有序帧路径加每秒帧数与循环设置，落成本地的精灵动画器。 |
| `ModRecipeDefinition`（`Recipe`，第二个） | 产物物品／液体、产物数量与状况、智力要求、配方分类、修理标志，以及有序的 `ModRecipeIngredient` 列表（指定物品 id 或制作品质、是否液体、最低状况、是否销毁）。提供者等 `Recipes.recipes` 就绪，与已有配方表去重，把接受的配方连同它的游戏分类映射注入。 |
| `ModLiquidDefinition`（`Liquid`，第三个） | 显示与描述文本、RGBA 色调、每升价值、health／injection 标志、注射致病、按物品取本地化、制作品质。提供者等 `Liquids.Registry` 就绪，拒绝覆盖已知液体，写入静态字段与本地本地化条目。 |
| `ModLiquidTileDefinition`（`LiquidTile`，液体族里静态世界液体那一半） | 逻辑 `LiquidId`、填充液体、浮力与阻力、每秒接触身体的速率、视觉基础字节与色调、生成量与层数、漫灌上限、饮用即消耗。提供者按稳定 id 顺序从 7 开始分配确定性的自定义世界流体字节，通过 `FluidManager.WorldFluidToLiquidID` 映射，并提供本地的投影面（水体信息、显示颜色与名称、身体接触、饮用、渲染），以及主机权威的运行时放置与漫灌（`IModLiquidPlacement`）。`LiquidTileWorldGenDistribution` 与地块矿石走同一个原版 `GenerateOres` 后置钩子，在 CUO 隔离的生成流里跑；网格变化沿用既有的 `FluidRegion`／`FluidInteraction` 同步。 |
| `ModBuildingDefinition`（`Building`，第四个） | 显示与描述文本、游戏自带 `TemplateId` 基础预制体、可选的 `BuildingEntity` 字段覆盖、可选的 `SpawnComponents`、作者的 `DropOnDestroy`／`AlwaysDrop`／`ItemCategoriesToAdd` 掉落规则、可选的世界生成密度（`SpawnMinPerChunk`、`SpawnMaxPerChunk`、`SpawnLayers`、`GenerationStyle`、`Placement`、`SpawnInGround`、`SurfaceOffset`、`RandomFlip`）和一个可扩展的 `CustomData` 字典。提供者用基础预制体构建未激活的运行时模板，把掉落表写进 `BuildingEntity`，再通过既有的 `Utils.Create`／`EntitySpawned` 实体化路径供出去；开启世界生成的定义稍后由 `PlaceCrystals` 生成流确定性地分布，生成期的 `Start` 被抑制，所以不需要任何线上消息。 |
| `ModTileDefinition`（`Tile`，第五个） | 显示与描述文本、可选的精灵资源路径、可选的游戏自带地块索引（作为视觉基础）、`BlockInfo` 风格的静态字段（生命、击中／脚步音效、睡眠质量、金属／毒性／湿滑标志、变化标志、RGBA 色调、碰撞体类型）和一个可扩展的 `CustomData` 字典。提供者从 36 开始分配确定性的自定义方块索引，把构建好的 Unity `Tile` 注入每一份新的 `WorldGeneration.tiles` 调色板，并通过窄的 `GetBlockInfo` 前缀回答自定义索引的查询。`SpawnAmount`、`SpawnLayers` 与 `GenerationStyle` 由 `TileWorldGenDistribution` 在隔离生成流里从原版 `GenerateOres` 后置钩子消费，于是两端生成出同样的自定义矿藏；本地自定义地块被挖开时的 `Drops` 由适配器生成，走既有的方块破坏／掉落上报。静态地块出现在哪里由模组决定 —— 不涉及随机世界生成，也没有线上消息。 |
| `ModStructureDefinition`（`Structure`，第六个） | 显示与描述文本、一个按行给出的宽×高网格、从单字符标记到游戏自带方块索引或自定义地块内容 id 的映射、逐深度的生成数量和一个可扩展的 `CustomData` 字典。提供者校验并编译网格；逐深度生成数量非空时，由 `StructureWorldGenDistribution` 在世界生成期放置 —— 它跑在 `WorldGenRandomIsolation` 里，在 `generatingWorld` 为 true 时通过原版 `SetBlock` 路径写入（既有的方块转发因此把它当作基线），并拒绝教学世界。多格运行时放置由 `IModStructurePlacement` 负责。 |
| `ModStatusDefinition`（`Status`，第七个） | 显示与描述文本、身体／肢体作用域、可存盘元数据、可选的心情图标 id、可选的逐肢体心情图标路由（`ShowPerLimbMoodles` 加 `LimbMoodles`）和一个可扩展的 `CustomData` 字典。提供者校验作用域／id／存盘字段，把静态描述符存下作为迁移基底；它不创建逐玩家或逐肢体的状态袋 —— 动态运行值属于状态运行时接缝。 |
| `ModMoodleDefinition`（`Moodle`，第八个） | 显示与描述文本、游戏自带的心情图标强度、稳定的图标／资源 id 键、critical／chipped／important 表现标志、持续秒数、可选的 `ModMoodleAnimation` 逐帧路径图标动画、可选的逐肢体显示／描述模板（`LimbDisplayNameFormat`／`LimbDescriptionFormat`）和一个可扩展的 `CustomData` 字典。提供者存下静态描述符；`ModStatusMoodleProjection` 把活跃的状态关联心情图标喂给原版心情图标管理器，`Moodle.Start` 补丁按作者的帧驱动原版界面图像。心情图标内容依旧不是线上特性。 |

**建筑运行时钩子。** `context.BuildingRuntime` 让模组为每个自定义建筑 id 注册一个预制体钩子和／或一个实例钩子。预制体钩子收到一个朴素的 `ModBuildingPrefabRequest`（建筑／模板 id），返回组件类型名；Game Adapter 在运行时模板被缓存之前把它们挂上去。实例钩子收到一个 `ModBuildingInstanceRequest`（建筑／模板 id 加世界 X／Y／旋转），返回组件类型名；Game Adapter 在每一个自定义建筑克隆体变成活跃之前把它们挂上去。只会咨询归属模组自己的钩子。没有活的 `GameObject`、游戏类型或 Unity 类型穿过 Abstractions，也不新增线上消息；模组自己写的组件负责自己的初始化。

这些内容既不让内容字节过网络，也不新增 `NetMsg`：Game Adapter 在每个装了该模组的机器上本地绑定。

## 读取游戏状态

```csharp
if (context.GameState.CanRead)
{
    if (context.GameState.TryGetPlayer(steamId, out var player))
    {
        var hp = player.Vitals?.BrainHealth;
        var items = player.Inventory?.Items;
    }
}
```

- **作用域**：框架手里最新那份**玩家角色状态**的只读投影，数据来自本来就在跑的 1 Hz 角色流 —— 与内置联机界面同源。模组不会看到 Unity 对象或游戏程序集的类型。
- **权限**：读取需要 `ModPermission.ReadGameState`。`CanRead` 反映这份模组拷贝有没有声明该标志，每次 `TryGetPlayer` 也会再强制一次（否则返回 false 并留日志）。
- **暴露的形状**：`IModPlayerState` 带 `SteamId`、`InWorld`、`Vitals`（`BrainHealth`、`Hunger`、`Thirst`、`Stamina`、`Energy`、`Temperature`、`Alive`、`Conscious`）与 `Inventory`（递归的 `IModInventoryEntry` 树：实例 id、物品 id、槽位／穿戴索引、状况、收藏标志、容器内容）。缺的那一半在对应快照到达之前是 null。
- **实时读取、不可变快照**：每次调用返回那一刻最新的缓存事实；返回的对象是副本，可以安全持有。远端离开世界或会话结束会清空缓存。
- **不在这一片里**：本机玩家自己的角色状态不从这里暴露，它在原生操作的只读本机玩家投影里。世界／物品／方块／实体的全局状态尚未暴露。
- **不改线上格式**：这个接口面只投影本来就会到达的数据。

## 生成与放置

| 接口面 | 权限 | 前提与复制方式 |
|---|---|---|
| `IModEntitySpawn`（`context.EntitySpawn`） | `SpawnEntity` | 按游戏预制体 id（`BuildingEntity.id`，也就是 `Utils.Create` 接受的那个 id）创建运行时世界实体 —— `CanSpawn` 与 `TrySpawn(prefabId, worldX, worldY, rotation)` 就是这一片公开接口的全部。需要活跃会话且本机玩家在世界内。适配器通过 `Utils.Create` 创建本地 `BuildingEntity`，随后正常的 `BuildingEntity.Start` 上报路径发出既有的 `EntitySpawned` 消息，于是每一侧都在同一位置、同一旋转创建同一个预制体，并按同样的创建期数据处理（间歇泉液体类型、键盘锁密码、水晶色调）。支持游戏自带预制体与注册成静态内容的自定义建筑定义；它是生成／复制接口面，不是通用的组件或状态注入机制。 |
| `IModItemSpawn`（`context.ItemSpawn`） | `SpawnEntity` | 按游戏物品 id 或经 `ModContentKind.Item` 注册的自定义 id 创建一个世界物品预制体 —— `CanSpawn` 与 `TrySpawn(itemId, worldX, worldY, rotation)`。适配器通过 `Utils.Create` 创建本地 `Item`，随后正常的 `Item.Start` 上报路径发出既有的 `ItemSpawned` 通道。支持游戏自带与自定义物品预制体；模组自己的状态仍然属于 `IModState` 或显式的 `IModNetwork`／`IModCommands` 协调。 |
| `IModTilePlacement`（`context.TilePlacement`） | `SpawnEntity` | 在整数方块坐标上放置一格自定义地形地块，用注册为 `ModContentKind.Tile` 的稳定内容 id 寻址 —— `CanPlace` 与 `TryPlaceBlock(tileId, blockX, blockY)` 就是它的接口；适配器把它解析成确定性的自定义方块索引，调用原版 `WorldGeneration.SetBlock` 路径 —— 该路径早已被 CUO 的 `BlockPlaced` 转发监视（客机上报加主机仲裁／广播）。需要活跃的世界内会话，且目标格当前是空气；它只写一格方块 —— 不写结构，也不做世界生成分布。游戏自带方块索引不从这里暴露。 |
| `IModStructurePlacement`（`context.StructurePlacement`） | `SpawnEntity` | 在整数方块坐标上放置一座静态结构（`originX`、`originY` 是结构左下角方块），用 `ModContentKind.Structure` 的 id 寻址 —— `CanPlace` 与 `TryPlaceStructure(structureId, originX, originY)` 就是它的接口；每个非空气格解析成游戏自带方块索引或自定义地块内容 id，走同一条 `SetBlock` 路径。适配器在第一次写入之前预检整座结构 —— 每个非空气格都必须在当前世界内、且落在空气上 —— 所以失败的请求绝不会留下半座结构。这条运行时接口面不套用世界生成分布，也不消费生成数量：自动放置是另一条生成期接缝。 |
| `IModLiquidPlacement`（`context.LiquidPlacement`） | `SpawnEntity` | 用 `ModContentKind.LiquidTile` 的 id 放置一格自定义世界液体（`TryPlaceLiquid`）或发起一次漫灌（`TryFloodFill`），走原版 `FluidManager.SetLiquid`／`StartFill` 路径。`CanPlace` 反映声明的标志，每次调用也会再强制一次。需要活跃的世界内会话：`TryPlaceLiquid` 要求世界内的空气格，`TryFloodFill` 要求世界内的种子格（`maxFill` 非正数时用定义里作者写的 `MaxFloodFill` 上限），而且未知或映射失败的液体地块会在任何写入之前被适配器拒掉。CUO 的流体网格是主机权威的，所以这个接口面只在主机／单人那份上写入 —— 客机调用会被拒绝并留日志，客机发起的放置应放进主机权威的 `IModCommands` 调用里。游戏自带的流体字节与资源支持的视觉模式不从这里暴露；自动的世界生成分布仍是另一条生成期接缝。 |

这里的每个接口面都复用表里点名的那个标志：`CanSpawn` 与 `CanPlace` 反映这份模组拷贝有没有声明它，每次调用也会再强制一次（否则返回 false 并留日志）。策略本来就拒绝在 `ClientOnly`／`Cosmetic` 上出现这些标志，所以只有带状态的模组能生成或放置。

预制体 id 非法、坐标不是有限值、会话不在世界内、缺权限，或适配器拒绝（预制体未知或不是 `BuildingEntity`），都会返回 false 并留日志；适配器创建出来的非实体预制体会被销毁，绝不留成只在本地存在的幽灵。这些接口面都不新增 `NetMsg`。

## 原生操作

```csharp
if (context.NativeApi.CanAccess)
{
    // The generic operation registry (Game Adapter-curated, not arbitrary reflection)
    if (context.NativeApi.TryInvoke("local.player.state", [], out var raw))
    {
        var state = (IModNativeLocalPlayerState)raw;
        var hp = state.BrainHealth;
    }

    // Typed convenience for the same registered operation
    if (context.NativeApi.TryGetLocalPlayerState(out var local))
    {
        var x = local.X;
        var y = local.Y;
    }
}
```

- **作用域**：`IModNativeApi` 是按权限开放的原生操作登记表。运行时从不暴露任意反射或对游戏程序集的直接访问；只有 Game Adapter 能注册操作，也只有那些操作 id 可被调用。
- **权限**：调用需要 `ModPermission.AccessNativeApi`。`CanAccess` 反映是否声明了该标志；每个调用方法也会再强制一次（否则返回 false 并留日志）。
- **安全的取值范围**：参数与结果只允许 `null`、字符串、数值基元、有上限的 `byte[]` 与基元数组，以及框架 DTO 类型（目前是 `IModNativeLocalPlayerState`）。Unity 对象、游戏程序集对象与任意对象图在 Game Adapter 接缝前后都会被拒 —— 它们永远不会到达模组。
- **这一片注册的操作**：`local.player.state`（`ModNativeApiOperations.LocalPlayerState`）以 `IModNativeLocalPlayerState` 返回本机玩家身体的位置、生命体征、意识，以及派生出的存活／清醒标志。它只读、只在本地：没有线上消息，也不改变权威归属。
- **策略边界**：第一片刻意只读。写入／原生变更类操作要等到有具体消费者、且它的同步与权威边界设计出来之后才注册 —— 这就是那道逃生口策略的明示决定：只开精选白名单，永不开放反射。

## 运行时模组数据

```csharp
// Local-only presentation/config/debug state: never leaves this process.
if (context.Data.TryDeclare("settings", ModDataScope.LocalOnly))
{
    context.Data.TrySet("settings", myBytes);
    context.Data.TryGet("settings", out var current);
}

// Shared state: the host owns the value; a guest keeps a mirror only after
// applying a host-originated value received over context.Network.
if (context.Data.TryDeclare("score", ModDataScope.Shared))
{
    context.Data.TrySet("score", scoreBytes);                     // host only
    context.Network.Broadcast(scoreBytes);                        // mod-owned payload/serialization
    context.Data.TryApplyShared("score", payload, senderSteamId); // guest, in MessageReceived
}

// Host-authoritative state: the framework keeps no guest mirror.
if (context.Data.TryDeclare("hostSecret", ModDataScope.HostAuthoritative))
{
    if (context.Session.IsHost)
    {
        context.Data.TrySet("hostSecret", secretBytes);
    }
}
```

- **作用域**：`IModData` 是逐模组、进程本地、**临时**的运行时存储。它不是 `IModState`，也不是通用快照服务。模组为每个槽位声明一次作用域，然后以不透明 `byte[]` 读写，上限与持久状态存储一致（键 ≤128、单值 ≤64 KiB、每模组 ≤1024 个槽位）。
- **不持久化，也不自动同步**：值只活在当前进程里。要持久化的值放 `IModState`；协作玩法事实放 CUO 有类型的内核领域。框架从不发送运行时数据值 —— 共享镜像由模组自己、用从 `IModNetwork` 收到的值显式套用，所以不存在隐藏的 JToken／JObject 快照协议。
- **作用域取值**：`LocalOnly` —— 任何网络模式都行，任何角色都能读写与移除。`Shared` —— 只有带状态的模式（`Synchronized`、`Authoritative`、`RequiresAllPlayers`），且模组必须声明 `SendNetworkMessage`（镜像要有意义就得有传输）；主机是唯一写入方，客机用会话主机的 SteamId 调 `TryApplyShared` 存一份本地镜像。`HostAuthoritative` —— 带状态的模式外加 `HostOnly`；框架存储里只有主机能读写，客机没有镜像，需要值就用 `IModCommands`／`IModNetwork` 协调。
- **角色门**：`Shared`／`HostAuthoritative` 槽位上的 `TrySet` 与 `TryRemove` 需要主机角色。`TryApplyShared` 需要客机那份拷贝、`Shared` 槽位，且发送方等于会话主机。这些检查都会留日志并返回 false，绝不静默忽略。
- **迁移对照**：CUCoreLib 那些零散的自定义数据／快照模块，迁移方式是声明一个作用域，再用既有的有类型 `IModNetwork`／`IModCommands` 接口面做传输。不要把通用的 JObject 快照登记表照搬过来。

## 运行时模组状态

```csharp
// Declare a typed body-formula status the GameAdapter knows how to project:
if (context.StatusRuntime.TryDeclare(
        "strength.potion",
        ModStatusScope.Body,
        ModDataScope.Shared,
        projectionKind: ModStatusProjectionKind.BodyFormula))
{
    var projection = new ModBodyFormulaProjection { MaxEncumbrance = 2f, Immunity = 5f, HeartRateOffset = 12f };
    context.StatusTransport.TryBroadcastBodyStatus(
        "strength.potion", playerSteamId, projection.ToPayload());
}

if (context.StatusRuntime.TryDeclare("bleeding", ModStatusScope.Limb, ModDataScope.Shared))
{
    context.StatusTransport.TryBroadcastLimbStatus("bleeding", playerSteamId, limbSlot, payload); // host only
    context.Network.MessageReceived += (sender, payload) =>
    {
        if (context.StatusTransport.TryHandleStatusPayload(sender, payload))
        {
            return; // other mod-message traffic continues here
        }
    };
}
```

- **作用域**：`IModStatusRuntime` 是静态内容 `ModStatusDefinition` 在运行时的对应物。值临时、进程本地，以「状态 id、玩家 SteamId、可选肢体槽位」为键。字节载荷的结构与版本归模组自己。
- **作用域规则**：与 `IModData` 同一套 `ModDataScope` —— `LocalOnly` 任意角色；`Shared` 主机写、客机显式套用；`HostAuthoritative` 仅主机且客机无镜像。
- **有类型传输**：`IModStatusTransport` 把已提交的共享值以带版本的 `ModStatusUpdate` 帧发布到既有的 `IModNetwork` 通道上。主机调 `TryBroadcastBodyStatus`／`TryBroadcastLimbStatus`（以及对应的移除重载）；每一侧都在自己的模组消息处理函数里调 `TryHandleStatusPayload`，于是来自主机的帧会自动套用或移除客机镜像。主机消费自己的广播回响，但不会重复套用。
- **客机请求路径**：这条接缝不新增框架命令。需要主机修改共享或主机权威状态的客机仍然用 `IModCommands`；主机命令处理函数是语义校验者，校验通过后调用一个 `TryBroadcast*` 辅助方法发布已提交结果。
- **有类型投影**：`TryDeclare` 可以带一个可选的 `ModStatusProjectionKind`（`BodyFormula` 或 `LimbPhysiology`）。带了这个值，模组的不透明状态值就应该是配套的 DTO（`ModBodyFormulaProjection`／`ModLimbProjection`）；GameAdapter 只解码这些众所周知的载荷，并在本地原版 `Body`／`Limb` 完成自身更新之后套用叠加值。载荷字节与序列化仍然归模组，且没有游戏或 Unity 类型穿过 Abstractions。
- **投影覆盖的字段**：身体侧有 `MaxEncumbrance`、`TotalEncumbrance`、`Immunity`、`JumpSpeed`、`AveragePain`，以及 `HeartRateOffset`、`RespiratoryRateOffset`、`BloodPressureOffset`；肢体侧有 `BleedAmount`、`SkinHealth`、`MuscleHealth`、`InfectionAmount`。循环系统的偏移通过专门的 `Body.HandleCirculation` 前缀／后置接缝套用：原生公式之前先去掉上一次的偏移，公式之后再套上当前偏移，于是这些每帧重算的值稳定在「原生基数加模组偏移」上，而不是每帧被抹掉。原版心情图标行由 `ModStatusMoodleProjection` 通过 `MoodleManager.AddAllMoodles` 的前缀／后置补丁喂给，不走身体叠加。肢体作用域的状态可以用 `ModStatusDefinition.ShowPerLimbMoodles` 选择「每个受影响肢体一行」，用 `LimbMoodles` 把不同肢体路由到不同心情图标描述符，并用心情图标层的 `LimbDisplayNameFormat`／`LimbDescriptionFormat` 模板生成带肢体名的提示文本。
- **本地心情图标解析器**：`IModMoodleRuntime` 让模组为每个运行时状态 id 注册一个解析器。解析器收到一个朴素的 `ModStatusMoodleRequest`（状态／玩家／肢体身份加模组自己的载荷），返回一个静态心情图标 id。GameAdapter 的本地心情图标行投影会对每个活跃的身体／肢体存在调用它，解析器缺失或返回 null 时退回静态的状态／心情图标路由。这是 CUCoreLib `RegisterBody`／`RegisterLimb` 回调的 CUO 安全替代：没有 `Body`／`Limb`／游戏委托穿过 Abstractions，而且它是只在本地生效的表现，没有线上消息。
- **边界**：不透明的 `None` 状态永远不被 GameAdapter 解释 —— 只有身体／肢体投影状态会到达原版层，存储变更事件是内部的。不新增 `NetMsg`，也不抬协议号：有类型的帧搭在既有的 `NetMsg.ModMessage` 通道上，也不引入任何通用 JObject 快照。

## 资源补全

`IModResourceCompletion`（`context.ResourceCompletion`）让模组为控制台的资源位置参数（`CommandArgumentKind.ResourceLocation`）注册自己的补全阶段。它是控制台补全扩展点在模组接口面上的化身：一个阶段收到 `ResourceLocationEntry`（规范 id、内容类别，以及归属来源的显示名），只回答一个问题 —— 这条条目匹配这个前缀吗？

```csharp
[CuoMod("cuo.pinyinsearch", "Pinyin Search", "0.1.0", NetworkMode = NetworkMode.ClientOnly,
    NativeBinding = "PlayerCamera.RefreshRecipeList + Recipe.simpleName getter")]
public sealed class PinyinSearchMod : ICuoMod
{
    public void Bind(IModContext context) =>
        context.ResourceCompletion.TryRegisterMatchStage("pinyin", new PinyinSearchStage());

    public void Initialize() { } public void Start() { } public void Update() { }
    public void Stop() { } public void Dispose() { }
}

internal sealed class PinyinSearchStage : IResourceLocationMatchStage
{
    public bool Matches(ResourceLocationEntry entry, string prefix) =>
        entry.DisplayName.Length > 0 && PinyinMatcher.Contains(entry.DisplayName, prefix);
}
```

- **天然只做加法**：目录只在自己的四级内置排序（精确规范 id、id 前缀、裸路径前缀、显示名前缀）之后才咨询注册的阶段，阶段之间按注册顺序排在后面，所以阶段只能增加候选，绝不会挤掉框架本来就能补出来的东西。`ResourceLocationCatalog.MaxSuggestions`（20）仍是目录自己的上限，被接受的建议始终是规范 id。
- **逐模组且本地**：阶段表属于注册它的模组（同一个 id 在两个模组里就是两条注册），只活在本地进程里，从不发送到任何地方 —— 控制台补全发生在玩家正在打字的那个客户端上，所以主机上从来不需要有阶段。
- **不需要权限标志**：阶段能做的只是拓宽本机玩家自己控制台的候选，所以任何模组都能注册。`ModPermission` 管的是会改变共享状态或他人状态的那些接口面，不是这一个。
- **生命周期与查询快照**：这张表与进程同寿（模组每个进程只发现一次，和它的内容注册一样），`TryUnregisterMatchStage` 是阶段离开它的唯一途径。一次补全查询按它开始时已注册的阶段排序，所以查询进行中又有阶段注册或注销，改变的是下一次查询，不是正在跑的那次。（阶段在 `Matches` 里回调目录自己的查询，那是模组自己的递归 —— 目录在那里不做任何承诺。）
- **安全栏**：一个模组最多持有 8 个阶段。阶段为 null、id 为空白、id 超过 128 字符、id 重复以及超过上限，都会返回 `false` 并留日志（`[ContentId]`）；`TryUnregisterMatchStage` 只会移除调用方模组自己的阶段。
- **失败隔离**：目录里有一个阶段抛异常时，这条条目按「不匹配」算，其余阶段照常运行，失败按 debug 级别记录 —— 因为这条路径是逐按键、逐条目在跑。模组的异常永远不会弄坏控制台。
- **稳定性**：`IModResourceCompletion`、`IResourceLocationMatchStage` 与 `ResourceLocationEntry` 属于 `Experimental`（`docs/api/advanced-modification-policy.md`）；已随仓库发布的拼音搜索模组是这条接缝的第一个消费者，也是注册方式的完整示例（`src/CasualtiesUnknownOnline.PinyinSearch.Core/PinyinSearchMod.cs`）。

## 握手一致性

客机声明的那份模组列表随握手一起走（`HandshakeMsg.Mods`）。主机在创建成员之前完成校验：

| 主机有 | 客机有 | 结论 |
|---|---|---|
| `RequiresAllPlayers`／`Synchronized`／`Authoritative` | 缺失、SemVer 优先级不相等，或权限不相等 | **拒绝** |
| 任意模式 | id 相同但 `NetworkMode` 不同，且任一侧带状态 | **拒绝** |
| `HostOnly` | 缺失 | 通过（主机侧逻辑） |
| `ClientOnly`／`Cosmetic` | 缺失或版本不同 | 通过（本地接口面） |
| — | 声称有 `RequiresAllPlayers`／`Synchronized`／`Authoritative`，而主机没有 | **拒绝** |
| — | 列表不合法（id 为空或重复、模式／权限非法、带状态模式的版本无法解析） | **拒绝** |
| 发现还没跑 | 任何情况 | **「pending」拒绝** —— 客机 1 秒一次的重试会重新检查 |
| 任意模式 | 同一个模组 id，但**声明的原生绑定不同**（包括一方声明、另一方没有） | 按主机的 `NativeBindingParity` 规则 **allow／warn／reject**：allow 静默通过；warn（默认）准入并记下不一致；require 拒绝，并点名该模组与双方的声明 |

**原生绑定对等**单独判定，因为它声明的是事实，不是网络契约。每个 `ModInfoMsg` 都带该模组的 `NativeBinding`；主机**按模组 id**与自己的声明比较，而且只比较两边都列出的模组 —— 只有一侧列出的模组没有对照物，上表也已经决定了谁能缺什么。空白声明在两侧都算「没有」（发现阶段用的是同一套归一），所以空白与缺失是同一个答案，而「声明了、对面没有」算不同。规则是主机 `HostRules` 里的 `NativeBindingParity` 项（`allow`／`warn`／`require`，默认 `warn`），可以在联机界面的管理页上改，也可以用控制台的主机规则命令改。默认取 warn 是因为这条声明刚出现：默认拒绝的主机会把「主机先更新」的每一个会话都锁在门外，而未声明的绑定本来就看不见。比较在去除首尾空白后逐字符进行（区分大小写）：只有周围空白不同的两种写法算相同，大小写不同则不算 —— 第三方作者要通过一台 `require` 的主机，就得把绑定名写得一模一样。

对等规则诚实证明的是：对两边都列出的模组，两边的声明一致，所以开启 require 的主机知道每个被准入的成员要么声明了同样的绑定、要么被拒。它**不能**证明的是：未声明的绑定无从探测（CUO 不站反作弊立场，见 `docs/api/advanced-modification-policy.md` §4），所以一个绑了游戏却不声明的模组能通过所有检查；而声明相同也不证明行为相同 —— 同一个名字可能盖着不同的补丁。选择 `allow` 的主机是明知风险仍然承担，`warn` 下的不一致则留下那行日志作为记录。

版本是严格的 SemVer；带状态的模式按**优先级相等**比较（忽略构建元数据）。兼容范围不会被推断；它们本该对照的那个面已经存在：`docs/api/advanced-modification-policy.md` 定下了稳定性层级，`docs/api/abstractions-api-baseline.txt` 是经过复核的公开接口面记录，由 `ApiSurfaceGateTests` 强制。

## 一个模组的布局

```text
BepInEx/plugins/MyMod/MyMod.dll        <- BepInEx loads this (the shell)
```

外壳（`[BepInPlugin]` 加一个空的 `BaseUnityPlugin`）、`[CuoMod]` 类和清单元数据装在**同一个**程序集里。照抄示例：`src/CasualtiesUnknownOnline.ModExample/` 注册了 `echo`／`whoami` 命令，并且仍是双进程验证的目标。

## 版本纪律

- 当前的线上版本就是 `ProtocolVersion.Current` 声明的值（`src/CasualtiesUnknownOnline.Runtime/Protocol/ProtocolVersion.cs`）。那个常量自己的注释就是线上变更日志 —— 这一页刻意不写任何数字，而且有门禁会让复述该值的现行治理文档变红。
- 改变线上行为要抬 `ProtocolVersion.Current`；只在本地生效、只读、不引入线上变更的模组接口面不用抬。
- 模组版本是严格的 SemVer 字符串，在发现阶段校验，带状态的模式按优先级比较。
- 64 KiB 上限是策略常量（`ModChannel.MaxPayloadBytes`）；调高它是一个与协议相邻的决定，不是线上格式的变更。

## 怎么知道它真的管用

上面每一条模组行为都有覆盖生产栈的纯托管测试（`tests/.../Mods/`）：发现与依赖排序、权限策略、SemVer、生命周期、消息路由与权限／限流门、主机命令、模组状态存盘、本地模组界面、内容注册与内容目录、运行时模组数据与状态、建筑运行时钩子、读取游戏状态、实体／物品生成、地块放置、原生操作及其 GameAdapter 契约、握手矩阵、限流器、方向约定与线上往返。

测试全绿不等于开了一局游戏。示例模组同时是**双进程运行时验证目标**：把它部署到两台机器上并加入 —— 日志里会出现 `[Mods] discovered …`、准入这一对的握手、客机到主机的命令结果，以及 echo 往返。两台真实客户端是用户的实机验收。

## 相关阅读

- [模组的一生](../internals/mod-loading-lifecycle.md) —— 加载顺序、泵与失败隔离
- [权限与安全](../internals/permissions-and-security.md) —— 一条声明买到了什么，以及 CUO 拒绝防什么
- [你的第一个模组](../start/your-first-mod.md) —— 最小的可用模组，一步步搭起来
- [给其他玩家发消息](../how-to/send-a-network-message.md) —— 网络接口面的实际用法
- [注册内容](../how-to/register-content.md) —— 内容接缝的实际用法
- [术语表](glossary.md) —— 权限、网络模式、原生绑定、内容 id、结构版本

---

[文档总览](../README.md) > [参考](README.md) > 模组接口契约
