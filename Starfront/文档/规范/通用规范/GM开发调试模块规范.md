# GM 开发调试模块规范

## 定位与归属

- GM 是模板必备的开发调试业务模块，代码位于 `Project/Assets/Scripts/csharp/main/gm`，编译到 Main。Foundation 和 Login 不得引用 GMControl。
- 面板位于 `Assets/Prefab/default/gm/GmPanel.prefab`，图集原图位于 `Assets/Art/atlasSource/default/gm`，图集位于 `Assets/Art/atlas/default/gm.png`。面板与图集进入已有的 `Default Local Group`，标记 `main-content`；组名不代表资源必须本地加载，沿用项目的平台策略。
- GMResource.xlsx 继续放在工程根目录 `文档/配置表`，使用既有 Luban 结构；导出数据属于 configData，代码属于 Login 的生成配置程序集边界。不要为将 GM 界面归入 default 而移动所有配置资源。
- 字体复用模板公共字体，不复制来源工程的系统字体。界面绑定路径和运行时类型标识同时指向 Main。

## 初始化与入口

- 正式内容、Main 程序集和配置就绪后调用 `GMControl.ins.Init()`，重复初始化不重复监听。
- 模板 MainPanel 提供 GM 按钮，鼠标/触摸点击与 Home 快捷键共用 `OpenPanel()`；尚未进入正式内容时不启用 GM。
- 模板 MainPanel 关闭时调用 `Shutdown()`：注销快捷键、关闭 GM 并恢复进入前的计时器倍率。派生项目替换 MainPanel 时，将初始化/退出放到实际正式内容生命周期内，不照搬到每个业务窗口。
- 沿用来源工程的开发入口可用方式，本次不新增编译宏或生产环境开关。正式发布是否开放 GM，由项目另行明确。

## 指令配置与扩展

字段、参数、注册及扩展规则统一见[GM表配置规范](GM表配置规范.md)，本文只维护模块生命周期与运行集成。

## 时间调试

- `-` / `=` 按原 GM 档位减速/加速，`P` 暂停/恢复；输入参数期间不响应这些全局快捷键。
- 只调整 SCENE、PLAYER、ENEMY 三类 Timer，COMMON 保持运行，使 GM 界面在暂停时仍可操作。
- 暂停记录每类 Timer 的原倍率，恢复分别还原；暂停期间选择新倍率则更新恢复目标。倍率必须是大于 0 的有限数值，暂停通过独立指令表达。
- 派生项目若另有场景时间控制器，应将 GM 操作适配到其权威接口，不保留两套互相覆盖的时间状态。

## 本次迁移来源与同步

2026-10-15，经用户明确授权将 b1 的 GM 界面及通用调试能力提升为模板基线。保留 GmPanel、GMItem、GMControl 及面板的原 GUID；调整代码实现以解除来源业务依赖。

图集只提取原 title/title 的 `bg_yellowBlack1`、`progressBar_bg1`、`sliderDot_red` 三张原图，生成独立 default/gm 图集并替换 GM 面板引用；来源工程及模板原 title 图集均保留。清除旧面板序列化的列表预览 Clone，保留权威 Item 模板。

手动同步到已有样例时，应按差异同步以下内容并携带 `.meta`：

1. `Project/Assets/Scripts/csharp/main/gm`。
2. `Project/Assets/Prefab/default/gm`、GM 图集及三张原图。
3. `UIEnum.GM_PANEL` 和正式内容的初始化/退出入口；模板提供的按钮改动位于 MainPanel.cs 与 MainPanel.prefab，已有业务主界面应合并入口，不能整文件覆盖。
4. GMResource.xlsx 与 `Assets/ConfigBin/cfgobj_gmobj.json`；保留样例自己的业务指令并补齐处理器，不盲目覆盖。
5. Default Local Group 中 GM 面板与图集的两条记录；保留样例自己的其他条目。

按用户要求，本次未执行模板验证脚本、Unity 编译、Addressables 构建或运行验收，由用户同步已有样例后验证。已执行的 Luban 导出仅用于同步配置产物，不代表运行验收通过。

## 后续回写与校验

- GM 面板共享背景必须引用模板自身 default/common 的 Sprite，不能保留外部来源图集 GUID。修复引用时按 Sprite 名核对模板的 GUID 与 fileID，不复制样例图集元数据。
- `Tools/Test-GMBaseline.ps1` 检查绑定脚本路径及 Main 类型标识、Sprite GUID/fileID、GM Addressables 地址和 main-content 标签、五条通用指令及注册处理函数。
- 模板总检查调用该脚本，同时校验生成 Tables 与 ALL_CONFIG_LIST 完全一致，避免配置初始化时遗漏预加载。样例业务配置名不进入模板。
- 静态检查和 Luban 导出通过不代表 Unity 运行或微信真机验收通过；分别记录验证结果。
