# UI 功能逻辑规范

> 适用范围：基于本模板开发的 Unity uGUI 窗口绑定类与功能逻辑代码。

UIBinder 可见绑定与运行时解析、跨平台输入和舞台尺寸响应，统一遵循 [运行时框架与加载规范.md](运行时框架与加载规范.md)，不得以旧类名回退掩盖空绑定。

### UIBinder Inspector 搜索绑定

- 绑定列表上方提供横向搜索输入框和“清空”按钮，按绑定 GameObject 的节点名称实时进行连续子串匹配，忽略大小写及搜索文本首尾空格；空文本显示全部。
- 显示匹配数与总数，无匹配时提示“没有匹配的绑定节点”。搜索文本只保存在 Inspector 实例中，不写入组件或 Prefab。
- 点击清空或切换选择时重置搜索；普通 Inspector 重绘不清空。选择事件订阅随 Inspector 启用、停用成对管理。
- 搜索只过滤显示，保留原始列表索引和绑定编号；新增编号仍根据完整列表计算，删除定位原始条目。一键生成绑定及相关同步始终使用完整列表。

## BasePanel 窗口规范

1. UI 作为窗口使用时，对应的绑定类必须继承 `BasePanel`。
2. 每个 `BasePanel` 子类必须声明构造函数，并在构造函数中设置 `layer`。具体窗口层级根据页面替换、覆盖关系和交互需求选择，不固定套用某个层级。
3. 每个 `BasePanel` 子类必须覆写 `OnOpen()` 和 `OnClose()`，即使当前没有额外逻辑也保留生命周期入口。
4. 界面需要接收键盘操作时，必须覆写 `OnPanelOperate(PanelOperateEnum operateCode)`，并根据 `PanelOperateEnum` 将操作分发到对应响应逻辑。
5. 窗口需要注册消息或交互监听时，通常在 `OnOpen()` 中调用私有函数 `AddLis()`，并在 `OnClose()` 中调用私有函数 `RemoveLis()`。注册与注销必须一入一出、严格成对。

`AddLis` 和 `RemoveLis` 是项目约定命名，不替换为其他同义名称。

## C# 换行风格

使用 K&R 风格：类、函数、条件和 `switch` 的左花括号与声明位于同一行。

人工编写的 `if`、`else`、`for`、`foreach`、`while`、`do` 等控制语句必须使用花括号，执行体独立换行；即使执行体只有 `return`、`continue` 或 `break`，也不得压缩成单行。

人为维护的 C# 代码中，函数、构造函数和委托调用的完整参数列表必须写在同一物理行；函数或构造函数的声明参数列表也必须写在同一物理行。UIBinder 自动生成区域、第三方代码和其他机器生成文件保持各自生成格式。

```csharp
public class ExamplePanel : BasePanel {
    public ExamplePanel() {
        layer = PanelLayer.SCALE_PANEL_SECOND;
    }

    public override void OnOpen() {
        AddLis();
    }

    public override void OnClose() {
        RemoveLis();
    }

    private void AddLis() {
    }

    private void RemoveLis() {
    }

    public override void OnPanelOperate(PanelOperateEnum operateCode) {
        switch (operateCode) {
            case PanelOperateEnum.ESC:
                Close();
                break;
        }
    }
}
```

UIBinder 自动生成的成员区域继续由工程既有生成器维护，不为其中的成员声明添加人工注释；功能逻辑写在生成标记区域之外。

## 图片对齐与列表预览

1. uGUI 图片默认使用矩形区域对齐。资源集合明确声明为锚点对齐时，使用方在图片加载完成后同步 Sprite Pivot，并保持业务定义的语义锚点坐标固定；切换资源时不得对坐标进行 Pivot 补偿。
2. 对可能需要锚点对齐的可替换系列资源，应按需要提示使用者确认对齐模式；不得由实现者逐张猜测。
3. ScrollList 中保留的编辑器预览项不得命名为 `{Template}(Clone)`，该命名只用于运行时实例，避免预览项被对象池误收。
4. 货币、评级、资源消耗等正式语义图标优先使用独立 `Image`，不依赖特殊字体字符模拟。
5. UI 交付前检查实际文本的 TMP 字形覆盖。静态字库缺字时补齐字形或配置合适的回退字体，不接受缺字方框作为完成状态。

## 通用组件状态与异步生命周期

1. 禁用按钮的默认灰化只调整 RGB，保留原 Alpha；灰化强度允许按组件配置。
2. 可销毁、回收或复用的组件发起异步加载时，应使用请求版本或等价机制废弃过期回调，并在回调前确认对象、请求版本和目标资源仍有效。

## C# 注释规范

1. 类注释使用多行 `/// <summary>` 简述角色，并以 `/// <remarks>` 说明职责范围。带参数且逻辑复杂的函数也使用 `/// <summary>`，参数或返回值语义不直观时补充 `param`、`returns`。
2. 变量、属性和简单函数使用单行 `/**注释内容*/`，不使用单行 XML 注释。
3. 生命周期、状态转换、异步回调、资源加载、对象池复用、数值计算和跨界面流程等关键逻辑，必须说明设计意图、前置条件或副作用。注释重点回答“为什么这样处理”，不得逐行复述代码。
4. Demo 中暂时代替正式配置系统的常量或规则，必须在所属类型或配置入口注明未来替换来源，例如 Luban；不要求为每个同类常量重复注释。
5. 本节注释与换行风格只约束人为撰写和维护的代码。UIBinder 自动生成区域、第三方代码及其他机器维护区域保持各自生成格式，不得为了人工代码风格修改生成器或整理其内部成员。
6. 人为代码中显而易见的赋值和监听注册不强制注释，避免制造注释噪音。
7. 人为代码的类左花括号后、类右花括号前至少保留一个空行；变量、属性和函数之间至少保留一个空行。人工代码与自动生成区域交界处保留一个空行即可。
8. 修改功能逻辑后的检查必须包含：人为代码的注释类型是否符合成员复杂度、关键状态和异步边界是否有意图注释、成员间空行是否完整、已有注释是否仍与代码一致。过期注释按缺陷处理。

## C# 类型文件组织

- 每个业务模块使用独立目录 `com/game/{module}/`，同一模块的入口、数据、常量和视图代码均收纳在该目录下，不按单个界面拆成多个代码模块。
- 一个文件只定义一个同名顶层类型，不得在主要 Panel、View、Control 或 Model 文件前后附加包外类、枚举或结构体；仅属于主类型实现细节的嵌套类型可以保留在类内部。
- 拆分或迁移 Unity 源文件时必须连同 `.meta` 一起移动以保持 GUID；整理过程不得顺带改变类型名称、访问级别、序列化关系或运行时行为。
- 模块代码按职责组织：`view/` 只存放 Panel、View、Item 等视图类；`model/` 存放业务数据管理类；`model/vo/` 统一存放各种 `{Semantic}VO.cs` 数据对象；`constant/` 存放静态常量类和静态工具类。
- 作为模块对外入口且采用单例访问的 `{Module}Control` 或 `{Semantic}Control` 直接放在模块根目录，不再增加 `control/` 目录；普通内部控制类不因名称带 `Control` 自动提升为模块入口。
- 模块仅在存在业务数据管理需要时建立与模块同名的 `{Module}Model`，不得为了满足目录形式机械创建 Model；数据管理职责过大时，可以按明确业务职责增加其他 Model。
- 数据对象命名为 `{Semantic}VO`，其中 `VO` 表示 Value Object，不得用 `{Semantic}Model` 表示单条数据对象；模块内的 VO 文件统一放入 `model/vo/`。
- 模块存在 `{Module}Model` 时，模块入口 `{Module}Control` 必须通过只读属性 `model` 暴露其实例，统一使用 `{Module}Control.ins.model` 访问模块数据。`Control` 负责业务流程，`Model` 负责数据集合、查询、初始化与存储边界。
- `View` 负责显示与输入，可以通过 `{Module}Control.ins.model` 读取展示数据；数据修改、状态推进及其他业务动作应调用 `Control` 的业务方法，不得由 `View` 直接修改 `VO`。
- 原型阶段用于代替正式配置系统的静态配置类放入 `constant/`，并在类注释中标明未来配置来源；不得把大型配置类附加在 Panel 或其他 View 文件中。


## 程序集分层的路径适配

本文旧 `com/game/{module}` 路径表示模块内部组织示例。采用程序集分层时，模块根改为 `Assets/Scripts/csharp/{程序集}/{module}`；view、model、model/vo、constant 等内部职责不变。Foundation 不承载设置界面等业务 UI，Login 不依赖 Main，详见运行时与平台专项规范。
