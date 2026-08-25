# 雷电战机 Demo 开发交接

更新日期：2026-08-20

## 1. 工程入口与权威依据

- 工程根目录：`E:\UnityTemplateTest\RaidenDemo`
- Unity 工程：`E:\UnityTemplateTest\RaidenDemo\Project`
- 配置表目录：`E:\UnityTemplateTest\RaidenDemo\文档\配置表`
- Luban 脚本目录：`E:\UnityTemplateTest\RaidenDemo\Project\ConfigResource`
- 目标分辨率：`720×1280`
- 当前玩法模块：`raiden`

新任务必须先完整读取本文档、工程根目录 `AGENTS.md` 及本次实际使用的 Skill。以本文档和当前工程现场为权威依据，不要重新生成或改动已经人工验收通过的内容。

当前可用职责划分：

- `$game-framework-toolchain`：可复用 Unity 框架、工具链与基础配置管理框架。
- `$aiui-to-ugui`：UI 设计产物到 Unity uGUI 表现层集成。
- `$game-feature-logic`：玩法规则、业务数据、业务配置及功能验证。
- `$game-art-asset-pipeline`：非 UI 游戏美术资产的生成、透明化、切图、命名、尺寸与锚点处理。

图像生成如后续确有需要，必须遵守对应 Skill 的外部模型约束；不得擅自调用 Codex 内置 imagegen。

## 2. 已打通并验收的主流程

当前流程：

`新游戏 → HomePanel → 关卡选择 → LoadingPanel → BattlePanel → BattleResultPanel`

已实现：

- BattlePanel 暂停、继续、重开和返回关卡选择。
- 胜利后按分数计算 1～3 星，并更新当前运行期关卡进度。
- Demo 暂不接入任何本地持久化；关卡进度、星数消费、飞机解锁和出战选择只存在于当前运行周期。
- 测试阶段使用 Addressables `Use Asset Database`；除非用户明确进入真正打包或发布节点，否则不要主动提出 Player Content Build。

## 3. Luban 配置管理现状

Luban 运行时加载链路已经接入：OpeningPanel 预加载配置 JSON 后调用 `CfgManager.Init()`，业务代码通过 `CfgManager.tables` 读取。

当前业务配置表包括：

- `StageResource.xlsx`
- `StageWaveResource.xlsx`
- `EnemyResource.xlsx`
- `EnemyBulletResource.xlsx`
- `PlayerAircraftResource.xlsx`
- `PlayerAircraftLevelResource.xlsx`

配置关系：

- 关卡表管理关卡选择位置、波次引用和星级分数线。
- 波次表管理敌机类型引用、数量、编队、生成位置与运动方式。
- 敌机表管理敌机自身外观、基础血量、移动、射击和子弹引用。
- 敌机子弹表管理子弹自身外观、速度、伤害和碰撞尺寸。
- 玩家飞机类型表管理机型身份、名称、最高等级、默认解锁及解锁星数。
- 玩家飞机等级表管理每个机型各等级的外观、血量、子弹、发射数量、战力和显示尺寸。

硬约束：

- 只能运行 `Project\ConfigResource` 中用户提供的导表脚本；禁止自行拼装 Luban 命令行参数。
- 当前生成 JSON 使用 `gen_json.bat`。
- 枚举必须在 `__enums__.xlsx` 注册，业务表中优先填写直观 alias，不使用魔术数字。
- 资源字段只填写资源名；统一前缀 `Assets/Art/unpack/default/raiden/` 和 `.png` 后缀由代码补齐。
- 表格增量编辑保持现有字体、字号和格式；列宽匹配常规内容，但不为极端长内容无限扩宽。
- `com/game/config/mgr` 只放确有二次索引、分类或聚合必要的 Luban 原始映射管理类。
- 业务配置转换后的 VO 由对应玩法模块 Model 管理，不因数据来自配置表就放进 `config/mgr`。

`TestCaseConfigs` 已覆盖配置加载、表数量、主要字段、枚举解析、引用关系及玩家飞机解锁/切换规则。测试会在自身流程中完成星数场景验证，不应为此长期保留额外初始星数。

## 4. 敌机、波次与奖励现状

- 敌机、敌机子弹和普通敌机波次已经结构化配置。
- 敌机血量来自敌机自身配置，不再由关卡表写普通/精英血量。
- 普通敌机支持直行、斜行、蛇形和编队转向等运动原型。
- 敌机射击支持单发、双发、散射和瞄准玩家等原型。
- 击败 Boss 才判定关卡胜利。
- 完整消灭普通敌机编队后掉落奖励；有敌机逃离则该编队不掉落。
- 已验收回血、玩家飞机升级、僚机升级、生命四类独立掉落图标及基本逻辑入口。
- 奖励靠近玩家时会自动吸附，接触后触发获得效果。

## 5. 玩家飞机资产与配置

当前已制作并接入 5 套玩家飞机：

1. 突击型：3 个等级。
2. 重装型：3 个等级。
3. 迅捷型：4 个等级。
4. 散射型：4 个等级。
5. 聚能型：4 个等级。

共 18 张等级外观，位于：

`Project/Assets/Art/unpack/default/raiden/player_aircraft_*_lv*.png`

命名规则采用：`阵营_资产类别_类型_序号_等级` 的语义结构；当前玩家飞机实例为 `player_aircraft_{type}_001_lvNN`。

系列资产采用“整组灰度轮廓设计稿 → 审核 → 整组上色彩图 → 审核 → 自动切图/透明化/尺寸与锚点处理 → 入工程”的流程。现有资产已经人工验收，不要重复生成。

所有玩家飞机 Sprite 已按统一“机位”语义设置 Pivot。HomePanel、HangarPanel 和机型列表显示时：

- 显式同步 Image RectTransform 的 pivot 为当前 Sprite pivot。
- 显示坐标使用固定机位坐标，不根据 pivot 变化进行坐标补偿。
- 切换机型时只改变 Sprite、pivot 和配置尺寸，机位坐标保持固定。

## 6. HomePanel 与 HangarPanel 完成状态

### HomePanel

已验收：

- 中央显示当前出战玩家飞机。
- 显示当前飞机等级配置的基础战力。
- 原“仓库”入口已改为“机库”。
- 僚机显示已移除，等待僚机业务完成后再接入。
- 玩家飞机按固定机位和 Sprite Pivot 正确显示。

### HangarPanel

英文模块名确定为 `Hangar`，Prefab 为：

`Project/Assets/Prefab/default/raiden/HangarPanel.prefab`

已验收：

- 浏览全部 5 种玩家飞机。
- 显示机型名称、默认等级、最高等级、基础战力及星数余额。
- 默认机型自动解锁并出战。
- 消耗当前运行期累计星数解锁其他机型。
- 已解锁机型可设为当前出战机型。
- 浏览预览与实际出战选择分离，必须点击主操作按钮才改变状态。
- 星数语义使用星星图片，不用“X 星”文字代替。
- 星数不足时按钮显示“星数不足”，隐藏按钮内星星；按钮保持禁用灰化，但不降低透明度，灰化颜色已经人工调浅。
- 飞机大预览和列表缩略图均按固定机位与 Sprite Pivot 正确显示。
- 僚机页签目前只显示未开放占位，不接入任何僚机数据。

相关代码：

- `RaidenModel.cs`：运行期飞机解锁集合、星数消费、当前出战机型、默认等级和配置转 VO。
- `PlayerAircraftVO.cs`：玩家飞机展示数据。
- `HomePanel.cs`：当前出战飞机展示。
- `HangarPanel.cs`：机库预览、解锁、出战切换。
- `HangarAircraftItem.cs`：机型列表项状态。

## 7. 已确认但尚未实现的等级生命周期

玩家飞机有关卡外默认等级与关卡内临时等级：

- 默认等级默认为 1，未来由永久强化天赋提升。
- 关卡开始时，当前临时等级初始化为默认等级，并限制在该机型最高等级内。
- 拾取玩家飞机升级道具后，当前临时等级提升 1 级，不能超过最高等级。
- 等级变化后，应使用对应 `PlayerAircraftLevelResource` 更新外观、基础血量、子弹类型、基础发射数量等关卡内属性。
- 玩家飞机死亡并准备复活时，当前临时等级恢复为默认等级。
- 重新开始关卡或进入新关卡时重新计算起始等级。

当前 HomePanel/HangarPanel 已读取默认等级对应的外观和基础战力，但 BattlePanel 尚未完整使用“当前出战机型 + 等级配置”驱动玩家战斗实体，这是下一阶段的起点。

## 8. 僚机设定（已对齐，当前隔离）

当前阶段不要提前实现僚机；后续再单独接入配置、资产、机库页和战斗逻辑。

已确认规则：

- 伴飞类型包括左右排列、环绕、固定阵型和下半屏自由飞行。
- 每种僚机具有独立数量上限与等级上限。
- 关卡外默认数量初始为 0，默认等级初始为 1，未来由永久强化天赋提升。
- 关卡开始时，起始数量/等级取默认值并受所选僚机上限限制。
- 升级道具优先增加当前数量；数量满后再提升全部现有僚机的当前等级。
- 玩家飞机死亡时，僚机当前数量和当前等级均恢复为默认数量和默认等级。
- 僚机无血量、无碰撞、不会被击落。
- HomePanel 未来根据配置的预览数量、伴飞类型和默认等级显示僚机预览。
- 数量与等级均满后再获得升级道具的反馈规则暂不实现。

## 9. 强化系统职责调整

- “机体解锁”和“僚机解锁”由机库界面负责。
- “战机强化”界面只负责消费星数的永久天赋树强化。
- 暂定弹药、僚机、机体三个强化方向。
- 天赋节点有等级上限，每次激活或升级消耗 1 颗星，可有前置节点或页面累计投入星数条件。
- 当前仍不接入存档与本地持久化。

## 10. UI 与运行时已回写规则

- 语义图标优先使用图片，不用文本字符冒充图标。
- TMP 出现方框通常是字体资产缺少对应中文 Glyph；必须检查字体覆盖与 fallback，不靠替换业务文字规避。
- ScrollList 预览元素使用明确的 Preview/Template 命名，避免被误认为真实数据项。
- 禁用按钮保留原 alpha，只调整 RGB 灰化；具体灰化深浅按界面验收结果确定。
- 异步资源回调必须防止视图销毁后的陈旧回调访问；`FrameAnimationView.loadVersion` 用于使旧加载请求失效。
- 对具有“语义对齐点”的锚点对齐资产，应按需要提示使用者确认其语义锚点；不能把所有普通图片都默认归为此类资产。

## 11. 下一任务明确起点

下一阶段先继续玩家飞机，不实现僚机、不接存档：

1. 让 BattlePanel 使用机库当前选择的 `selectedAircraftId` 创建玩家飞机。
2. 从 `PlayerAircraftLevelResource` 读取默认等级对应的外观、血量、子弹类型、发射数量等战斗属性，清理相应硬编码。
3. 建立关卡内 `currentAircraftLevel`，进入关卡时从 `defaultAircraftLevel` 初始化。
4. 玩家飞机升级掉落只提升当前临时等级，并实时刷新等级相关战斗属性。
5. 玩家死亡后、复活前，将当前临时等级恢复为默认等级，并刷新对应战斗属性。
6. 验证切换不同机型后进入关卡，外观与基础属性确实来自所选机型配置。

修改前先检查 BattlePanel 当前玩家实体创建、升级掉落和复活流程，给出小步实施方案；不要直接大规模重构。

## 12. 新任务建议首条消息

见本交接文档更新后的第 11 节；新任务中应先完成现场对齐，再开始改动。

## 13. 场景与战斗模块重构基线

已建立独立的 `com/game/battle` 战斗模块和 `com/game/scene` 场景逻辑模块，后续战斗代码不得重新放回 `raiden/view/BattlePanel.cs`。

- 模块常量统一使用“模块名 + Const”命名；战斗常量类为 `BattleConst`。
- `Timer` 支持逐帧更新时间监听，回调参数为经过该 Timer `scale` 处理后的秒数。
- `SceneModel` 分别监听 `sceneTimer`、`playerTimer` 和 `enemyTimer`，按 Timer 类型分发逻辑时间。
- `SceneElementVO` 是场景逻辑元素基类，只保存逻辑状态，不持有 `GameObject`、`RectTransform` 等视觉对象。
- 玩家及其衍生物使用 `playerTimer`，敌方及其衍生物使用 `enemyTimer`，其他场景元素使用 `sceneTimer`；非场景内容默认使用通用 Timer。
- 现有 BattlePanel 已取消直接读取 `Time.deltaTime`，玩家、敌方和场景更新分别由对应 Timer 消息驱动。
- 关卡预加载依赖收集由 `BattlePreloadCollector` 管理，碰撞和接触点计算由 `BattleCollisionSystem` 管理。
- 当前运行对象类已从 BattlePanel 文件拆到 `battle/model/vo` 独立文件；下一轮继续把其中残留的视觉引用迁往表现层，使其正式接入 `SceneElementVO`。
- 场景单位原则上只能根据自身移速连续改变坐标，不允许通过直接改写坐标产生瞬移；只有明确设计为“瞬移”的技能或机制可以例外。
