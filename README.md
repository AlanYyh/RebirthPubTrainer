# Rebirth Pub Trainer

《Rebirth Pub》游戏修改工具
## 游戏介绍
《转生居酒屋 / 转生酒馆》（Rebirth Pub）游戏以异世界转生为背景，融合酒馆/旅店经营、多角色养成、沙盒冒险与动态演出内容，由 Seikou Soft. 开发、HimitsuCP 发行的互动模拟经营SLG（Simulation Life Game）游戏，目前处于持续更新状态，如果该修改器，无法使用，请提交issues（介绍是我复制百科的）

## 游戏很好玩，建议遇到卡进度再使用 ##


## 功能

- 货币 / 资源：金币、魂石、行动点、体力、技能点
- 角色好感度：3 位女主角 + 13 位 NPC
- 道具数量 技能等级
- 图鉴 / 收集：一键解锁图鉴、服装、遗物

## 使用

1. 把 `RebirthPubTrainer.exe` 放到游戏根目录（与 `Rebirth Pub.exe` 同一目录）。
2. 首次启动修改器会自动安装注入组件。
3. 启动游戏并载入存档。

## 版本
修改器版本：1.0 对应游戏版本：0.65
注入组件为标准 BepInEx 框架，卸载时删除对应文件即可

## 构建

需要：

- Windows 与 .NET Framework 4.x（系统自带）
- 游戏本体（编译插件时需要引用游戏的 Managed 程序集）
- BepInEx 5.x

编译脚本见 `build/` 目录，使用前请把脚本中的游戏路径改成你的实际路径。

## 许可

[MIT License](LICENSE)
