# Terminology: English ↔ 中文

The only source of renderings for the paired guide. A Chinese page uses the 中文 column, spells a
listed term as `中文(English)` the first time it appears on that page and uses 中文 alone afterwards,
and never uses a form listed under *Never render as*. A new word is added here before it is used, so
two pages cannot invent two renderings of one mechanism.

| English | 中文 | First use | Never render as | Notes |
|---|---|---|---|---|
| host | 主机 | 主机(host) | 服务器, 服主 | the player whose game owns the world; there is no dedicated server |
| guest | 客机 | 客机(guest) | 客户端, 访客 | the joining player's game; the other half of host |
| session | 会话 | 会话(session) | 房间, 对局 | one host plus its connected guests |
| lobby | 大厅 | 大厅(lobby) | 房间, 组队界面 | the Steam lobby that carries membership |
| world | 世界 | 世界(world) | 地图, 存档 | the saved unit; a world holds many layers |
| save | 存档 | 存档 | 世界, 世界归档 | one stored progress file; the CUO unit of saving is the 世界归档, not a file |
| view (of one player) | 画面 | 画面 | 视图, 视角 | what one machine renders; the derived state itself is 投影 |
| client (a machine in a session) | 客户端 | 客户端 | — | a machine, never a role: the two roles are 主机 and 客机 |
| layer | 层 | 层(layer) | 关卡, 楼层 | one generated level of a run |
| run | 局 | 局(run) | 轮次, 运行 | one playthrough, from world entry to death or end |
| kernel | 内核 | 内核(kernel) | 核心 | the typed deterministic state engine |
| authoritative | 权威 | 权威(authoritative) | 主导, 官方 | 主机权威 = host-authoritative |
| arbitration | 仲裁 | 仲裁 | 裁决 | how a conflict between two claims is settled |
| command | 指令 | 指令(command) | 命令 | a typed request to the kernel |
| console command | 控制台命令 | 控制台命令 | 指令 | typed in the CUO console, for example `/save` |
| event | 事件 | 事件 | 消息 | a fact the kernel accepted |
| batch | 批次 | 批次(batch) | 数据包, 包 | one atomic committed set of facts |
| checkpoint | 检查点 | 检查点(checkpoint) | 存档点 | full authoritative state, used by join and reconnect |
| state stream | 状态流 | 状态流(state stream) | 数据流, 同步流 | unreliable high-frequency field updates |
| projection | 投影 | 投影(projection) | 映射, 视图 | a derived consumer: Unity objects, clones, wire, saves |
| revision | 修订号 | 修订号(revision) | 版本号 | the kernel's monotonic order, not the mod version |
| epoch | 运行纪元 | 运行纪元(epoch) | 时代, 周期 | run identity; stale traffic from an old run is rejected |
| native | 原生 | 原生(native) | 本地, 官方 | the game's own code and behaviour |
| patch | 补丁 | 补丁(patch) | 插件 | one Harmony hook; a plugin is 插件 |
| mod | 模组 | 模组(mod) | 模块, MOD | third-party content loaded through the mod API |
| adapter | 适配器 | 适配器(Game Adapter) | 转接头, 适配层 | the only layer that knows the game's private types |
| runtime | 运行时 | 运行时(Runtime) | 运行环境 | the stable CUO layer: protocol, session, mod loading |
| snapshot | 快照 | 快照(snapshot) | 截图 | a point-in-time copy of state |
| carry | 背负 | 背负(carry) | 携带 | one player carrying another; carried items are 随身物品 |
| revive | 救活 | 救活(revive) | 复活 | bringing a downed teammate back at a trader |
| respawn | 重生 | 重生(respawn) | 复活 | the game placing a dead player back into the world |
| permadeath | 永久死亡 | 永久死亡(permadeath) | 一命模式 | death is terminal |
| item | 物品 | 物品 | 道具 | anything the kernel tracks as an item |
| container | 容器 | 容器 | 箱子 | an item that holds other items |
| capability | 能力 | 能力(capability) | 功能 | per-type composed behaviour such as battery or liquid |
| protocol version | 协议版本 | 协议版本 | 游戏版本 | checked when a guest joins |
| handshake | 握手 | 握手(handshake) | 连接协商 | the join-time version and identity exchange |
| lease | 租约 | 租约(lease) | 锁 | `world.lease` names the process writing a world folder |
| cut | 切片 | 切片(cut) | 裁剪, 分段 | one save write of the live world |
| backup | 备份 | 备份 | 副本 | a compressed copy of an earlier cut |
| world archive | 世界归档 | 世界归档(world archive) | 存档文件, 存档包 | the CUO save layout: one folder per world |
| remote clone | 远端克隆体 | 远端克隆体(remote clone) | 替身, 影子 | the local stand-in showing another player's character |
| nameplate | 名牌 | 名牌(nameplate) | 姓名牌 | the label above a character |
| off-screen arrow | 屏幕外箭头 | 屏幕外箭头(off-screen arrow) | 边缘指示器 | points at a teammate outside the view |
| pinyin search | 拼音搜索 | 拼音搜索(pinyin search) | 拼音输入 | lets native search boxes match Chinese by pinyin |
| world generation | 世界生成 | 世界生成(world generation) | 地图生成 | deterministic generation from the run's seed and settings |
| host rules | 主机规则 | 主机规则(host rules) | 房主设置 | host-owned gameplay switches |
| interaction panel | 交互面板 | 交互面板(interaction panel) | 操作面板 | the CUO panel opened with the configured key, `F6` by default |
| online panel | 联机面板 | 联机面板(online panel) | 联机界面, 网络面板 | the CUO overlay that creates or joins a session |
| unconscious | 昏迷 | 昏迷(unconscious) | 倒地 | alive but unable to act |
| deterministic | 确定性 | 确定性(deterministic) | 固定 | same inputs produce the same result |
| seed | 种子 | 种子(seed) | 随机数 | the world-generation input carried in the run baseline |
