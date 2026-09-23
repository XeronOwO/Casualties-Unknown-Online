# 世界怎么存档

[文档总览](../README.md) > [弄懂原理](README.md) > 世界怎么存档

---

**读完这一页**，你能说清 CUO 的一次存档到底写了什么、为什么崩溃也留不下半个世界，以及一份被拒绝的快照会怎样。[让模组数据跨会话保留](../how-to/save-mod-data-across-sessions.md) 是面向模组的那一半；这一页讲的是档案本身。

## 一个世界一个文件夹

CUO 保有自己的[世界归档](../reference/glossary.md)，从不读写游戏自己的 `save.sv`：原生格式表达不了“一局打到一半”的存档，让两者保持一致只会造出两个真相源。一个世界就是游戏持久化数据根目录下 `cuo/saves/` 里的一个文件夹，装着当前状态和一组压缩备份。

- `index.json` 列出所有世界；每个世界一个文件夹，里面有 `world.json` 和一个不打包的 `live/` 目录。
- `live/` 故意不打包：这样存档就是一次快速的文件写入，人也可以不解包就查看或修一个世界。
- `backups/` 里是 `.cuoz` 归档 —— 同一套文件，一份快照一个归档。
- `worldId` 不可变，就是文件夹名；显示名随便改。
- **只有主机写。** 客机只存自己的本地设置，别的什么都不存，所以磁盘上永远不会出现第二份会分叉的世界。

## 一次切片里有什么

`live/` 里有一份 `manifest.json`，加上每个领域一个文件 —— `run.json`、`players.json`、`items.json`、`world-entities.json`、`enemies.json`、`fluids.json`、`world-blocks.json`、`world-transients.json` —— 再按玩家各有一个 `characters/<playerKey>.json`。玩家键按传输方式区分：`steam-<steamId64>` 或 `name-<显示名>`，所以 Steam 世界不会被直连模式里同名的人悄悄认领。

这些 DTO 与中途加入的客机收到的形状一致，所以恢复出来的主机手里握的，正好是当初加入会得到的那些。新领域往归档里加一个文件，绝不改写已有的那个。每个载荷文件的根都是一个 JSON 数组 —— 解码器一次只拿一条，这也正是下面“逐条打捞”所依赖的接缝。

## 什么时候可以切

存档只有**一致**才有用：它不能和一次指令批次或一次帧末冲刷交错。所以抓取只在主机主线程泵的两个接缝之一发生，并由 `manifest.json` 记下用的是哪一个，好让恢复能证明自己手里是什么：

| `cutPhase` | 何时切 | 为什么在这里 |
|---|---|---|
| `layer-boundary` | 内核提交一次换层时 | 切片拿到的正是要进入的那一层的运行基线 —— 早一步切就会存下上一层的基线，恢复时生成出另一个世界 |
| `frame-end` | CUO 泵的最后一步 | 唯一一个既没有指令批次提交到一半、也没有帧冲刷发到一半的时刻 |

`/save` 命令和主动返回主菜单并不在自己发生的地方切，而是**武装**一次切片，由帧末接缝来切 —— 命令跑在游戏的输入处理里，在那里抓快照可能读到半应用的一帧。切片会等在途状态先落地；如果过了 `WorldCutDeferral.MaxFrames`（八帧）仍然切不了，就把挡住它的那类状态写进切片报告里，而不是让请求一直饿着。

## 一次存档就是一笔事务

```text
stage:   write the world's .staging/ folder (manifest last)
verify:  re-read every file and compare against the manifest's checksums
backup:  zip .staging/ to backups/<kind>-<stamp>.cuoz.tmp, then rename to .cuoz
commit:  rename live/ aside, rename .staging/ into place, then refresh index.json
```

只有归档安全写完之后才会替换 `live/`，所以任意两步之间崩溃，留下的要么是旧快照、要么是新快照 —— 绝不会是半个世界。中断留下的 `.staging/` 在加载时被丢弃，留下的 `.previous/` 会被恢复，两者都会记一条警告。

文件夹只有一个写入方，这条规则保护的是文件夹而不是进程：`world.lease` 记着正在写它的进程，每次写入都刷新。若发现另一个进程在 `WorldLease.StaleAfter`（30 分钟）内刷新过租约，这次写入会被拒绝并点名持有者；超过这个时长没人刷新的租约会被接管，并记一条警告。

## 被拒绝的快照是证据

清单是整个格式里唯一一道硬门禁。清单读不出或解析不了，这份归档就算损坏，绝不会被静默加载：加载器退回到清单能读的最新备份，并且大声说出来。被拒的那份快照留在 `damaged-<stamp>/` 里，而不是删掉 —— 没人读它，之后的切片、清理或加载也永远不会删它。

打捞是**逐条，而不是逐领域**。有一行恢复时造不出来 —— 比如某次模组更新删掉的物品定义、映射不上的 id —— 就单独跳过它并写进报告，而不是把整个世界一起拖下水。

版本不一致也不会被静默加载。`protocolVersion` 或 `gameBuild` 不同，世界以修复模式打开并给出警告；`schemaVersion` 比读取方新，则跳过那份载荷而不是瞎猜。

## 备份与保留

每次成功存档都会留一份备份；换层、玩家显式请求、定时自动存档，以及即将替换掉现有快照的那次恢复，也都各留一份。定时默认十分钟（`SaveOptions.DefaultAutosaveIntervalMinutes`），而且只在世界真的开着的时候武装，所以回了主菜单的主机不会去折腾一个没人玩的世界。玩家自己要的那次切片永远优先占用接缝。

保留策略留下最新的 N 份归档（默认 10，`SaveOptions.DefaultBackupRetentionCount`），永不删除最新的那一份，并在每次提交成功的切片之后从最旧的开始清理。删不掉的文件会被报告；切片保持已提交，世界保持可加载。

## 它不是什么

没有云存档，没有跨机器共享存档，备份不另用一种归档格式，也不与原生 `save.sv` 共存。这是本地磁盘面，不是线上协议：这一页里的任何东西都不改变对端收到什么。

## 相关阅读

- [CUO 的整体结构](architecture-overview.md) —— 切片所依据的检查点
- [让模组数据跨会话保留](../how-to/save-mod-data-across-sessions.md) —— 模组自己的那张表存在主机存档里
- [状态流与快照](state-and-snapshots.md) —— 快照在别处的含义
- [构建、测试与部署](../contributing/build-and-test.md) —— 存档失败会出现在哪些日志里
- [术语表](../reference/glossary.md) —— 世界归档、切片、备份、租约、检查点

---

[文档总览](../README.md) > [弄懂原理](README.md) > 世界怎么存档
