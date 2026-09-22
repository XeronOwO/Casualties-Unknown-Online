# 装起来玩

[文档总览](../../README.md) > [从这里开始](README.md) > 装起来玩

**读完这一页**,你已经装好 CUO、开过或加入过一次会话,并且知道最常见的那三种失败是什么意思。模组作者跑自己构建的版本,步骤也一样。

## 装了什么,装在哪

CUO 是一个 BepInEx 插件,它不改动游戏安装目录:

| 东西 | 位置 |
|---|---|
| 模组本体 | `BepInEx/plugins/CasualtiesUnknownOnline/` |
| 它的设置 | `BepInEx/config/` |
| 它的世界 | CUO 自己的存档目录 |
| 游戏自己的存档文件 | CUO 从不写入 |

## 安装

1. 如果游戏目录里还没有 **BepInEx 5**,先装上。
2. 把 CUO 的插件文件夹复制到 `BepInEx/plugins/CasualtiesUnknownOnline/`。
3. 启动一次游戏,让 BepInEx 生成配置文件,然后关掉。

## 开一次会话

主机(host)照常启动游戏并照常玩 —— 已经打开的那个世界就是共享世界。打开 CUO 的联机面板,创建一个 Steam 大厅(lobby)并开始会话。朋友会通过这个大厅看到你并加入,所以游玩期间保持它开着。

## 加入会话

客机(guest)启动游戏,打开同一个面板,在好友列表里加入主机的大厅。

不使用 Steam 的话,配置文件里有 `IpDirect` 段的直连模式:把 `JoinAddress` 设成主机地址,`JoinPort` 设成主机监听的端口。

## 不工作时怎么办

- **加入被拒绝,或者客机刚连上就退回菜单。** 两边对[协议版本](../reference/glossary.md)的认知不一致,通常发生在版本不同的构建之间。所有人跑同一个构建就好。
- **大厅里什么都没有。** 检查 Steam 是否在运行、游戏是否通过 Steam 启动。
- **游戏更新后某个功能不对劲。** 适配器(Game Adapter)找不到它依赖的东西,于是停用那部分而不是崩溃;日志里会写明它跳过了什么。
- **日志位置:** `BepInEx/LogOutput.log`、`BepInEx/logs/latest.log` 与 `CUO.log`。

## 相关阅读

- [CUO 是什么](what-is-cuo.md) —— 会话是什么、谁决定什么
- [搭好开发环境](set-up-dev-environment.md) —— 自己构建 CUO
- [术语表](../reference/glossary.md) —— 主机、客机、大厅、协议版本

[文档总览](../../README.md) > [从这里开始](README.md) > 装起来玩
