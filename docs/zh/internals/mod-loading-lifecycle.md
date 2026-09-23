# 模组的一生

[文档总览](../README.md) > [弄懂原理](README.md) > 模组的一生

---

**读完这一页**，你能说清框架什么时候发现一个模组、按什么顺序调用它，以及一个模组崩了、或者别人没装它时会怎样。[你的第一个模组](../start/your-first-mod.md) 是写给模组作者的走查；这一页讲它底下的机器。

## 发现发生在第一个 update 帧

模组不是在框架自己启动时被找到的。BepInEx 逐个加载插件：加载一个、调一次它的 `Awake`，所以在 CUO 自己的 `Awake` 里扫描，会漏掉在那之后加载的每一个插件。`src/CasualtiesUnknownOnline.Abstractions/ICuoMod.cs` 写明了这个后果：框架靠 `CuoModAttribute` 发现模组，时机是第一个 update 帧 —— BepInEx 逐个加载插件，若在框架自己的 `Awake` 里扫描，会漏掉之后才加载的插件。

扫描每个进程只做一次：第一个 update 帧置一个标志，之后不再扫。所以发现一定在某次会话开始之前完成，客机加入时也绝不会拿着一份只建了一半的模组列表去比对。

## 什么样的模组会被收下

- `[CuoMod]` 特性**就是**清单的来源 —— 框架按它构造 `ModManifest`，模组不必把元数据声明两次（`src/CasualtiesUnknownOnline.Abstractions/CuoModAttribute.cs`）。
- 候选类型必须实现 `ICuoMod`，并且有一个公开的无参构造；实例由框架自己创建。
- `NetworkMode` 默认是 `Unspecified`，而它在发现阶段就会被**拒绝**：没有声明网络模式（network mode）的模组不会加载。这是失败朝关闭一侧倒，所以一次漏写永远不会悄悄变成最宽松的模式。
- 依赖会被兑现：发现返回的列表本身就按依赖顺序排好（拓扑排序），所以一个模组的依赖先于它加载。目标缺失、id 重复、自依赖、成环、用了保留的或已被占用的内容命名空间、权限组合非法，都会**带着日志**拒掉该候选；依赖了被拒模组的模组，跟着一起跳过。
- 一个坏模组永远不会挡住其他模组。`src/CasualtiesUnknownOnline.Runtime/Session/Mods/ModRegistry.cs` 写明了这条：被拒的候选带着日志跳过，一个坏模组永远不挡后面的。

## 调用的顺序

```text
discovery frame:  Bind  ->  Initialize  ->  Start
every frame:      Update
shutdown:         Stop  ->  Dispose        (reverse load order)
```

`Bind` 是模组唯一一次拿到上下文的机会：一份会话快照、消息通道、它自己的日志器，以及它声明了权限的那些接口面。`Bind`、`Initialize`、`Start` 都在发现那一帧跑完；从那以后，模组的 `Update` 每帧在主线程上被驱动。关闭时按加载的逆序走，所以最后加载的模组最先停。

## 模组拿到的是什么

每个加载成功的模组都有自己的 `ModContext` —— 框架面向单个模组的那张脸。它背后的存储（`ModStateStore`、`ModDataStore`、`ModStatusStore`、建筑运行时、命令服务）每个进程只有一份，context 是模组够到它们的过滤视图。一个模组看不到另一个模组的 context；已加载表、泵和持久化都留在框架手里：`ModService` 只是一个接线点，它自己的注释说它宁可这样，也不做一个上帝对象。

## 崩掉的模组会被隔离

每一次进入模组代码的调用都过同一道保护。`Bind`、`Initialize`、`Start`、`Update` 里抛出的异常都会被捕获并记日志 —— 隔离掉，泵继续走 —— 而且只影响那一个模组：加载失败的模组被跳过，其余照常加载；某一帧抛异常的模组，不会让别的模组这一帧不更新。

同样的隔离也罩着模组的流量。进来的模组消息按发送方限速、按载荷上限检查、本地没有这个 id 的模组就丢弃、本地模组没声明网络权限也丢弃 —— 每一种都有自己的日志行，而不是把异常抛进谁的模组里。

## 别人必须装什么

模组的 `NetworkMode` 就是它与这次会话的契约，主机在加入[握手](../reference/glossary.md)时执行它。摘自 `src/CasualtiesUnknownOnline.Runtime/Session/Handlers/HandshakeHandler.cs`：

```text
RequiresAllPlayers/Synchronized/Authoritative missing on either side, or
version-unequal, → reject (the host cannot arbitrate state the member
lacks or claims with a different version); HostOnly is host-side only (a
guest lacking it passes); ClientOnly/Cosmetic differences pass (local
surfaces).
```

格式不对的列表同样被拒：id 为空或重复、网络模式未知、权限与模式不匹配、承载状态的版本号解析不出来。还有一条：**跑不成的检查不算通过** —— 如果主机这边的发现还没跑完，这次加入会以“尚未检查”被拒，而不是放行；客机一秒后重试时才会被真正检查。

## 生命周期不做什么

- **不热重载。** 没有“现在加载这个模组”这样的调用：发现每个进程只跑一次，运行中途往游戏里放的模组文件不会在这次运行里被捡起来。
- **不单独卸载。** `Stop` 与 `Dispose` 属于框架关闭，而且会走完整个已加载列表。
- **不随会话重启模组。** 会话结束时不会重新 `Bind`；context 只是把会话结束的事件发出去，已加载表保持原样。
- **进程中途不重读状态。** 模组持久化的那张表在发现之前读一次，好让 `Bind` 就能读到它；内存里的那份活到这个进程结束。

## 相关阅读

- [你的第一个模组](../start/your-first-mod.md) —— 一个模组需要的两个文件
- [权限与安全](permissions-and-security.md) —— 声明一项权限换到的是什么
- [声明权限与主机命令](../how-to/declare-permissions-and-commands.md) —— 面向模组的那些标志
- [四种信封](envelope-protocol.md) —— 放在上下文里的加入握手
- [术语表](../reference/glossary.md) —— 模组、适配器、运行时、握手、模组状态

---

[文档总览](../README.md) > [弄懂原理](README.md) > 模组的一生
