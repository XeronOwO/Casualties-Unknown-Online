# 适配器背后的游戏

[文档总览](../README.md) > [弄懂原理](README.md) > 适配器背后的游戏

---

**读完这一页**，你能说出 CUO 必须了解游戏的哪些部分，以及其中哪些最可能在一次游戏更新里被挪动。先读 [适配器与游戏更新](adapter-and-updates.md)：这一页讲的是游戏，不是 CUO 的边界。

## 这是一款什么样的游戏

《Casualties Unknown》（Demo）是一款 Unity 2022.3 的游戏，逻辑都在 `Assembly-CSharp.dll` 里。要紧的只有两个场景：`PreGen` 是菜单与开局前设置，世界在 `SampleScene` —— 由 `PreRunScript.cs:268` 的 `SceneManager.LoadScene("SampleScene")` 载入。CUO hook 的东西全在第二个场景里：世界生成、玩家、世界界面。

下面这些结论来自 `reversing/`，那棵反编译的工作树。它被 gitignore，而且从不被编辑，这正是它可以按行号引用的原因：行号不会漂移，游戏变了就是换一份新的反编译，而不是去改它。

## 玩家是场景里的对象，不是被生成出来的

这里没有一处“生成玩家”的调用可以拦截。玩家就是场景预置体 —— 每个场景实例一个 `Body` 加一个 `PlayerCamera` —— 摄像机跟着绑定的那具身体走：

- `PlayerCamera.cs:3142` 声明 `public static PlayerCamera main;`，`:3130` 声明 `public Body body;`。摄像机跟着 `PlayerCamera.main.body`。
- 移动是输入驱动的物理。`PlayerCamera.cs:843` 是 `public void HandleInput()`：它读按键绑定和鼠标，写 `body.moveDir` 与 `body.targetLookPos`。物理步进 —— `Body.cs:2297` 的 `private void FixedUpdate()` —— 把它变成力与速度。除了物理步进，没有别的地方读 `moveDir`。
- 按键本身来自 `KeyBinds.GetBind("up"|"down"|"left"|"right"|"jump"|"attack"|…)`，一张可配置键码的缓存表；而 `HandleInput()` 是游戏唯一的输入收集点 —— 控制台或界面面板打开的那一帧它会自己让开。游戏对输入做的一切都挂在这一个调用下面，所以 CUO 自己的界面不必去和它抢。
- 身体自己的状态就是一组普通字段：`Body.cs:203` 的 `public bool alive`、`:213` 的 `public bool conscious`、`:3745` 的 `public Limb[] limbs`（0 号是头）。

这个形状决定了很多 CUO 的设计：因为本地身体由游戏自己的物理模拟，客户端永远不必去预测自己的移动；又因为别人的身体只是一具克隆体，CUO 必须给它状态，而不是给它指令。

## 世界生成本身并不确定

原生生成到处都在调 `Random.Range`，方块生成内部还用自己的 PRNG，所以两台机器生成“同一层”默认并不会一致。CUO 因此把这条随机流隔离出来：`src/CasualtiesUnknownOnline.GameAdapter/WorldGen/WorldGenRandomIsolation.cs` 的存在就是为了让“世界生成的随机流在多端之间确定”，让主机与客机“从同一份捕获到的 `Random.state` 出发”。

这也解释了为什么内核里的运行基线要带上生成输入 —— 种子与两个稀有度乘数 —— 以及为什么换层边界的存档必须在换层提交**之后**才切：检查点要描述的是即将生成的那一层，而不是刚结束的那一层。

决定一局进行到哪儿的那些字段 —— `WorldGeneration.totalTraveled`、`biomeDepth` 与 `biomeOverride` —— 都通过 `HarmonyTraverse` 读写，因为游戏一个 setter 都没公开；其中 `biomeOverride` 是 `None`／`Tutorial`／`Debug` 的枚举，决定走哪条生成分支。`WorldStartParams.LoadedRun` 是那个值得记住的例外：它在 `WorldGeneration` 上**根本没有对应字段**，所以上了线也是 false —— 客机那一层照样对得上，因为驱动生成的是恢复出来的 `Random.state`，不是这个标志。

## 原生存档不是世界存档

游戏自己的 `SaveSystem.SaveGame` 往 `save.sv` 写一份 gzip 的 JSON（`SaveSystem.cs:186`），里面只有角色与运行状态 —— 身体与肢体字段、随身与穿着的物品、配方、运行设置、运行时钟。世界一点都不在里面：没有方块、没有世界物品、没有实体、没有敌人、没有流体。所以原生“继续”会把当前这一层从头重新生成，而 `SaveSystem.TryLoadGame` 应用完就把文件删掉（`SaveSystem.cs:453`）。

两个后果塑造了 CUO：

- **CUO 保有自己的归档** —— 见 [世界怎么存档](save-archive.md) —— 因为原生格式根本表达不了一局进行到一半的世界。
- **CUO 挡住原生加载。** `src/CasualtiesUnknownOnline.GameAdapter/Patches/SaveSystemTryLoadGamePatch.cs` 只为一个理由存在：“决策 165：CUO 从不读原生 `save.sv`……游戏自己的 `SaveSystem.TryLoadGame`……因此绝不能跑。”这个前缀不去碰 `SaveSystem.loadedRun`，好让游戏仍然以为自己在继续；而每一个事实都来自 CUO 的恢复。

## 远端玩家是场景角色的克隆

CUO 不是用零件拼一个角色出来。`RemoteBodyFactory` 找到场景里的 `"Experiment"` 玩家对象并实例化它 —— “和 KrokMP 用的是同一个模板”—— 再把副本变成一具冻结的渲染替身：物理停下，但 `Body.Update` 继续跑，好让肢体与姿势有动画；那些会和冻结状态打架的东西（IK 瞄准手柄、肢体铰链关节）在克隆体上被关掉。克隆体必须在世界已经存在之后才创建，因为身体自己的 `Awake` 会去世界生成里取它的声音混音组。

这套替身配方是逐组件定的，都在 `reversing/` 的反编译源码里核对过：

- **`Body.Update` 在替身上必须跑。** 它的 `HandleVisuals` 分支（`Body.cs:3123+`）用身体与手臂的动画器驱动肢体姿势，还要为 `grounded` 做一次投射；跳过它的替身会一直显示没初始化过的肢体贴图，也什么姿势都不动。拥有者活跃时，克隆体会压掉“动画器到可见体”的那份拷贝，所以状态流带过来的姿势不会被覆盖。
- **`Limb.Update`（`Limb.cs:498+`）可以照常跑。** 它写肢体的着色器参数、跑治疗与感染计时器，从不移动肢体。
- **`IKHandle.Update`（`IKHandle.cs:40-57`）被关掉。** 它把目标朝 `Camera.main.ScreenToWorldPoint(Input.mousePosition)` 插值，并在那儿画一条线段渲染器；放在克隆体上，瞄准线就会指向**本地**玩家的鼠标 —— 也就是那个“头一直看着鼠标”的症状。
- **克隆肢体的 `HingeJoint2D` 和其余物理一起关掉**（跳过 `FixedUpdate`、`Rigidbody2D.simulated` 置 false），根节点变换则每帧用对端报上来的状态写一次。

它对 CUO 的意义和其他地方同一条规则 —— 身体由一侧拥有，另一侧只负责渲染被告知的东西。

## KrokMP 是参考，不是范本

`reversing/` 里反编译出来的 KrokMP 模组，是用来查游戏事实的，不是拿来照着设计的：它把身体状态广播给物理照常运行的克隆体，还直接替换了生成协程。CUO 对同样问题的回答 —— 一具身体一个拥有者、确定性内核、物理冻结的渲染替身 —— 在关键处正好相反，这份对照就是这一页存在的原因。

## 相关阅读

- [适配器与游戏更新](adapter-and-updates.md) —— 吸收这些变动的边界
- [CUO 的整体结构](architecture-overview.md) —— 为什么克隆是投影，而不是第二套模拟
- [世界怎么存档](save-archive.md) —— 取代原生存档的那套归档
- [仓库地图与坑](../contributing/repository-map-and-pitfalls.md) —— `reversing/` 在这棵树里的位置
- [术语表](../reference/glossary.md) —— 原生、适配器、投影、远端克隆体、世界生成

---

[文档总览](../README.md) > [弄懂原理](README.md) > 适配器背后的游戏
