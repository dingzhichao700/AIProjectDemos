# 雷电战机 Demo 开发交接

更新日期：2026-09-04。本文按当前状态维护；旧实现与试调参数已合并移除。

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
- 关卡内可预知资源统一在进入关卡前递归收集并预加载，禁止使用时临时加载。
- 2026-09-01 已执行一次 Addressables `Build All (Assign + Player Content)`，构建成功；输出位于 `Library/com.unity.addressables/aa/Windows/settings.json`。

## 3. Luban 配置管理现状

Luban 运行时加载链路已经接入：OpeningPanel 预加载配置 JSON 后调用 `CfgManager.Init()`，业务代码通过 `CfgManager.tables` 读取。

当前业务配置表包括：

- `StageResource.xlsx`
- `StageWaveResource.xlsx`
- `EnemyResource.xlsx`
- `PlayerAircraftResource.xlsx`
- `PlayerAircraftLevelResource.xlsx`
- `BulletResource.xlsx`
- `EffectResource.xlsx`
- `SceneBgResource.xlsx`
- `StageItemResource.xlsx`
- `WingmanResource.xlsx`

配置关系：

- 关卡表管理关卡选择位置、波次引用和星级分数线。
- 波次表管理敌机类型引用、数量、编队、生成位置与运动方式。
- 玩家飞机和敌机统一通过 `Aircraft` 配置飞行物外观、血量、碰撞形状、发射器和死亡表现。
- 子弹表通过 `type + level` 定位唯一子弹配置，管理外观、圆形碰撞半径、速度、伤害、运动方式、发射特效和命中特效。
- 特效表统一管理子弹、飞机爆炸和其他帧动画特效；子弹发射特效资源名使用 `shootFire_` 前缀。
- 场景背景表管理无限滚动视差背景；关卡只引用场景背景 ID。
- 关卡道具表管理道具类型、资源、拾取圆形半径、移动参数、效果值和循环特效。
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

2026-09-07 已按用户要求恢复配置测试，专门验证常量表现有示例项；测试预期值与业务判断分离，不恢复旧的固定表数量或爆炸次数断言。

## 4. 当前战斗、波次与奖励规则

- 720×1280 视窗使用左上原点。玩家机位限于下半屏：x 为 0～720，y 为 -1280～-640。敌机机身活动下沿为 -560，和中线间保留 80 距离；入场、离场允许越过上方或侧边视窗边界。
- 普通敌机只使用三种正式路径：`STATIONARY / 定点攻击`、`HORIZONTAL_PASS / 横向穿越`、`PATROL / 巡航攻击`。旧四种枚举及兼容映射已删除；枚举数值 0、2、3 保持已迁移数据的对应关系。
- 定点和巡航由顶部入场并减速，横穿由侧边入场；完整入场后准备再射击，离场停火，已发射子弹继续运动。巡航完成一次横向往返后斜向上离场。
- 当前普通波次入场倍率 2，准备 400ms，定点攻击 5000ms，横穿最少 5000ms，巡航攻击 7000ms，巡航幅度上限 90。横穿时间还受移速与区域宽度限制；巡航幅度还受空间与移速限制。
- 编队布局预留 15° 倾斜的保守机身范围。所有关卡引用的路径在进关准备阶段验证并计算布局，过大的编队明确报错。运行路径只保存时间，不重复计算布局。
- 第一关波次 101～104 数量为 2、3、3、3；其他关卡数量维持现状。上一波全部解决后等待当前配置的 1500ms 再推进。
- 普通敌机不显示血条。精英使用跟随 HUD 曲线血条，Boss 使用顶部血条；死亡时移除对应血条。
- 精英和 Boss 在上半屏驻留并按配置移速移动。全部普通波次结束后才请求 Boss；击毁 Boss 后才进入胜利流程。
- 完整击毁普通编队才发放该波 `rewardItemId` 指定的奖励，0 表示无奖励；有敌机逃离时不发全灭奖励。
- 自然补给按关卡的 `supplyItemIds` 顺序循环，首次等待 200ms、之后间隔 10000ms。Boss 阶段持续生成；暂停、退出、结算统一由模拟生命周期停止。大时间步不批量追补。
- 道具在上半屏先向下输送，完整进入下半屏后才消耗反弹拾取期；之后沿用视窗反弹与到期离场。最后 8 秒透明度往返提示，道具不吸附，飞机碰撞形状与道具圆形接触后拾取。
- 拾取后隐藏图标、播放特效 12008 和 HUD 飘字，按现有拾取减速表现完成后回收。循环特效比例 1.25，具体特效读取道具配置。
- Boss 死亡后停火并退出战斗碰撞，但配置为保留的机身继续移动，包括全部爆炸完成后的等待期；当前胜利等待为 3500ms，从最后一个爆炸播放完成时开始。玩家死亡后续流程等待仍为 1.5 秒。

上述玩法与数值已由用户验收。本轮整理保持这些参数，新增的代码与表结构已通过自动检查；完整关卡手感仍可在 Unity 中继续验收。

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

## 7. 已实现并验收的玩家飞机等级生命周期

玩家飞机有关卡外默认等级与关卡内临时等级：

- 默认等级默认为 1，未来由永久强化天赋提升。
- 关卡开始时，当前临时等级初始化为默认等级，并限制在该机型最高等级内。
- 拾取玩家飞机升级道具后，当前临时等级提升 1 级，不能超过最高等级。
- 等级变化后，应使用对应 `PlayerAircraftLevelResource` 更新外观、基础血量、子弹类型、基础发射数量等关卡内属性。
- 玩家飞机死亡并准备复活时，当前临时等级恢复为默认等级。
- 重新开始关卡或进入新关卡时重新计算起始等级。

BattlePanel 已使用机库当前选择的出战机型，并由等级配置驱动外观、血量、碰撞形状和多套子弹发射器。升级采用多阶段表现：升级期间控制发射器生效状态、播放固定升级特效、切换等级属性，并用 50ms 显隐闪烁表现短暂无敌。

## 8. 僚机当前状态与后续范围

当前代码已接入僚机配置、预加载、伴飞、发射器及战斗编队基础链路。HomePanel 预览与 Hangar 僚机页仍未正式开放；不要把基础战斗实现误记为完整僚机系统已完成。后续扩展沿用下述已对齐规则。

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
- 全局 TMP 字体已迁移为允许随项目分发的思源黑体简体中文版，原始字体及 SIL OFL 许可证位于 `Project/Assets/Art/font/source/`。
- TMP 字体体系采用“常用字符静态主字库 `fontBodyCommon` → 小型符号字库 `fontSymbolCommon` → 动态补字字库 `fontDynamicCommon`”的 Fallback 顺序；动态字库启用 Multi Atlas，避免生成单个超大全量中文 SDF。
- 普通文本使用主字体内置基础材质；统一维护细描边、中描边、粗描边和描边加投影四种 Material Preset。战斗飘字当前使用 `fontBodyCommon_OutlineShadow`。
- TMP 出现方框通常是字体资产缺少对应 Glyph；应补充授权明确且覆盖范围合适的 Fallback 字体，不靠替换业务文字规避，也不要为少量缺字盲目扩成超大全量中文 SDF。
- 字体资产原则上原位维护。删除重建 TMP Font Asset 可能改变内置材质 `fileID`；字体或材质重建后必须检查字体 GUID、材质 `fileID`、Fallback 链、Prefab/场景引用及 Addressables 条目。
- 当前思源黑体资源不包含 `✔✕✖✦✧❤` 六个装饰字符；业务使用这些字符前应先补充合规的符号 Fallback。
- ScrollList 预览元素使用明确的 Preview/Template 命名，避免被误认为真实数据项。
- 禁用按钮保留原 alpha，只调整 RGB 灰化；具体灰化深浅按界面验收结果确定。
- 异步资源回调必须防止视图销毁后的陈旧回调访问；`FrameAnimationView.loadVersion` 用于使旧加载请求失效。
- 对具有“语义对齐点”的锚点对齐资产，应按需要提示使用者确认其语义锚点；不能把所有普通图片都默认归为此类资产。

## 11. 架构与固定归属

- `com/game/battle` 管理战斗；`raiden` 管理入口、机库与配置转换。BattlePanel 只组装生命周期和表现，不接回敌机运动规则。
- `BattleModel` 协调整局状态，`SceneElementVO` 保存逻辑数据，不持有 Unity View 对象；元素自身负责移动、发射及生命周期。
- sceneTimer 推进波次、自然补给和结算；playerTimer 推进玩家；enemyTimer 先推进一次共享编队路径，再更新敌方元素。路径不能随 sceneTimer 推进。
- `EnemyWaveVO` 保存已验证配置，`EnemyFormationLayoutVO` 保存只读布局，`EnemyFormationPathVO` 保存每波运行时间。
- `AircraftVO` 决定死亡后的开火与移动策略，`BattleModel` 决定移除逻辑实体和结算时机。`BattleAircraftDeathPresenter` 同步死亡机身并协调爆炸序列，通过通知报告最后一次爆炸开始和全部播放完成；不直接改写 VO。
- `BattleEffectPresenter` 只播放独立特效，不管理飞机逻辑生命周期。退出时先取消死亡序列和播放句柄，再清理视图、对象池和 Model，避免旧回调访问新一局。
- 飞机和道具固定属于 `entityLayer`，子弹固定属于 `projectileLayer`；独立爆炸属于 `effectLayer`，血条与飘字属于 HUD。附着特效跟随所属实体。预热、运行、死亡、回收不改变实体归属。
- `BattleVisualPool` 按父节点实例和资源分池，回收只隐藏，复用重置可变表现。禁止将预热对象临时放到特效层。
- 边界与固定玩法规则集中在 `BattleConst`；可调关卡、波次参数读取 Luban。已清理旧运动常量、原型血量/等级计算、废弃计时字段和特殊敌机固定分数，敌机分数读取自身配置。

## 12. 配置维护入口

波次表新增字段：`entrySpeedMultiplier`、`prepareDurationMs`、`attackDurationMs`、`patrolAmplitude`、`stationHeightRatio`、`exitDirection`、`rewardItemId`。

关卡表新增字段：`supplyFirstDelayMs`、`supplyIntervalMs`、`supplyItemIds`、`waveIntervalMs`、`victoryDelayMs`。

时间字段单位为毫秒；运行时统一转换为秒。`stationHeightRatio` 是合法机身/编队中心活动高度的插值：0 靠下，1 靠上。`exitDirection` 是离场向量，代码归一化；横穿必须沿前进方向从侧边离开，其他路径不得向下撤离。布局可能限制实际巡航幅度。

以后调节节奏直接改表并运行 `Project/ConfigResource/gen_json.bat`，不恢复旧枚举别名，不手改生成 C# 或 JSON。奖励顺序由关卡显式 ID 列表决定，不再依赖道具表行顺序或波次索引取模。

## 13. 回归检查与当前验证结果

1. 在工程根目录运行 `Tools/Test-RaidenBattle.ps1`。使用工程当前 Unity 编译引用和真实导出 JSON，输出到 `Project/Library/RaidenBattleChecks`；不重新打开 Unity、不修改业务配置。
2. 在非播放状态的 Unity 执行 `Tools → Raiden → Check battle view lifecycle`（Ctrl+Shift+F9）。临时节点在检查结束时清理，验证真实 Transform、分层复用、死亡完成通知、保留机身回收及重复 Clear。
3. 手工回归关注：进入关卡、三种路径观感、暂停继续、Boss 阶段补给、Boss 死亡移动与爆炸结束后结算、重开和返回。自动检查不替代完整画面与手感验收。

2026-09-04 本轮执行结果：导表成功；C# 编译通过，仅保留原有 `FrameAnimationView.needStopAtFirstFrame` 未使用字段警告；全部 10 个关卡、51 条普通路径的数据回归通过；当前 Unity 编辑器内的层级与生命周期检查通过。

战斗测试读取真实配置，不断言固定表数量或固定爆炸次数。2026-09-07 已启用 TestCaseConfigs.Run，专门验证常量表 ID 1～8、预期越界、类型检查、缓存复用和集合只读性。

## 14. 后续开发起点

- 已验收的飞机美术、机库界面、字体、手工配置继续保留，后续修改前先检查工作区差异。
- 僚机机库与预览、永久天赋树、弹药/能量碎片和主动技能完整效果仍需继续；子弹附加等级已有战斗传递接口，不能据此宣称科研系统已完成。
- 进度、星数、解锁和出战选择仍仅存在于本次运行期，无本地存档。
- 本轮未提交 Git。配置与资源存在用户已有变更，不能通过整体回滚恢复。

## 15. 通用常量读取（2026-09-07）

ConfigValueHelper 已提供普通值、列表和 Map 的静态类型读取接口，按 ID 缓存成功解析的只读结果，由 CfgManager 初始化/清理时清空。常量表已正式导出，加入 Opening 预加载列表和 title Addressables 组。配置测试及解析边界检查通过，C# 编译通过。具体语法与接口见 [ConfigValueResource填写说明.md](ConfigValueResource填写说明.md)。
