# Rebirth Pub Trainer

《Rebirth Pub》单机游戏的修改工具，免费、开源。

## 游戏介绍
《转生居酒屋 / 转生酒馆》（Rebirth Pub）游戏以异世界转生为背景，融合酒馆/旅店经营、多角色养成、沙盒冒险与动态演出内容，由 Seikou Soft. 开发、HimitsuCP 发行的互动模拟经营SLG（Simulation Life Game）游戏，目前处于持续更新状态，如果该修改器，无法使用，请提交issues（介绍是百科说的）

## 建议先自行游玩，卡住了，再用修改 ##


功能

- 货币 / 资源：金币、魂石、行动点、体力、技能点
- 角色好感度：3 位女主角 + 13 位 NPC
- 道具数量：全部道具   全部探索技能
- 图鉴 / 收集：一键解锁图鉴

## 使用

把 `RebirthPubTrainer.exe` 放到游戏根目录（与 `Rebirth Pub.exe` 同一目录）。
需要系统已安装 Microsoft Edge WebView2 运行时（Windows 10/11 一般自带）。

## 版本

- 修改器版本：1.0
- 对应游戏版本：0.65

## 构建

需要：

- Windows 与 .NET Framework 4.x（系统自带）
- 游戏本体（编译插件时需要引用游戏的 Managed 程序集）
- BepInEx 5.x
- WebView2 SDK（NuGet 包 `Microsoft.Web.WebView2`，解压后填到脚本里的 `$webView2Dir`）

编译脚本见 `build/` 目录，使用前请把脚本中的路径改成你的实际路径。

