# 让模组数据跨会话保留

[文档总览](../../README.md) > [做一件事](README.md) > 让模组数据跨会话保留

**读完这一页**,你的模组能把自己那点数据 —— 设置、解锁、计数 —— 存进主机那份 CUO 配置文件(`BepInEx/config/`),跨世界共享,并在下一次开局读回来。先读[声明权限与主机命令](declare-permissions-and-commands.md):写入需要其中那个标志。

## 一张表,一个主人

`context.State` 的作用域就是你的模组 id:你只读写自己那一条,别人的碰不到。值是 `byte[]`,内容不透明,框架既不解释也不序列化,所以格式、版本和迁移都归你。

```csharp
if (context.State.CanWrite)
{
	context.State.TrySet("loadout", Encoding.UTF8.GetBytes(json));
	context.State.TrySetSchemaVersion(2);
}

if (context.State.TryGet("loadout", out var bytes))
{
	// bytes 是你自己的格式;结构版本变了就按 context.State.SchemaVersion 分支
}
```

两个方向都是副本:你传进去的数组、或者你拿到手的数组,之后怎么改都不会动到已存的表,除非再显式调一次 `TrySet`。

## 只有主机能写,而且要有标志

`TrySet`、`TrySetSchemaVersion`、`TryRemove`、`TryClear` 同时要求主机角色**和** `ModPermission.WriteGameState`;只有这一种组合里 `CanWrite` 才是 true。同步模组在客机那一侧拿到的是 `CanWrite == false`,也读不到主机的表。客机需要写入时,去请主机写 —— 用 `IModNetwork` 或者一条主机命令,见[给其他玩家发消息](send-a-network-message.md) —— 真正落笔的是主机那份副本。

## 什么时候落到磁盘

- 主机把它写进 `BepInEx/config/CasualtiesUnknownOnline.mod-state.bin`,每次成功调用做一次"临时文件 + 原子替换"。
- **每次写入都会落盘整张表。** 值变了再写,不要逐帧写。
- 内存里的表属于进程,只在发现模组与 `Bind` 之前加载一次,所以在 `Bind` 里读到的已经是上一次会话的数据。
- 文件不存在就是空状态。文件损坏或者版本不认识,会带着一行警告退化成空 —— 绝不因此启动崩溃,也绝不猜着迁移。
- 文件里记着模组 id、最后写入者的模组版本,以及你自报的结构版本(schema version)。`SchemaVersion` 在你设置之前默认为 1。

## 模组缺席时数据留着

下一次开局如果没装你的模组,它那一条会原样保留;模组回来时数据还在。因为某次会话被从模组列表里移除,也是同样处理。

## 上限

键最长 128 个字符,一个模组最多 1024 个键,单个值最大 64 KiB。越过上限的调用返回 `false` 并写日志;不会静默截断。

## 常见坑

- **要看返回值。** 被拒绝的写入否则完全不可见:`TrySet` 返回的是 `bool`。
- **能重建的东西不要存。** 这一层是给模组自己的数据用的;世界状态归内核,拿这里当镜像迟早会和世界对不上。
- **结构版本别塞进载荷。** `TrySetSchemaVersion` 记的是框架替你携带的元数据;下一次开局,你自己的迁移代码把 `SchemaVersion` 读回来。
- **客机自己去写本地文件,从构造上就是错的。** 表在主机那份副本上;客机要配合它,而不是再长出一张。

## 验证它真的成了

在主机上写一个键并设一个结构版本,完全退出游戏,再启动并把这个键读回来:值和版本都还是你写下的。把文件内容换成乱码再启动 —— 日志里有一行警告,表从空开始,游戏照常启动。在客机上,`CanWrite` 是 false,`TrySet` 返回 false 并写日志。

## 相关阅读

- [给其他玩家发消息](send-a-network-message.md) —— 客机怎么请主机代写
- [注册内容](register-content.md) —— 模组自带东西的另一半
- [声明权限与主机命令](declare-permissions-and-commands.md) —— `WriteGameState` 与主机命令
- [你的第一个模组](../start/your-first-mod.md) —— 读写所处的生命周期
- [术语表](../reference/glossary.md) —— 主机、会话、世界

[文档总览](../../README.md) > [做一件事](README.md) > 让模组数据跨会话保留
