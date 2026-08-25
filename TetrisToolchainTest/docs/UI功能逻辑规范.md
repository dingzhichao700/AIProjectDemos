# UI 功能逻辑规范

> 适用范围：基于本模板开发的 Unity uGUI 窗口绑定类与功能逻辑代码。

## BasePanel 窗口规范

1. UI 作为窗口使用时，对应的绑定类必须继承 `BasePanel`。
2. 每个 `BasePanel` 子类必须声明构造函数，并在构造函数中设置 `layer`。具体窗口层级根据页面替换、覆盖关系和交互需求选择，不固定套用某个层级。
3. 每个 `BasePanel` 子类必须覆写 `OnOpen()` 和 `OnClose()`，即使当前没有额外逻辑也保留生命周期入口。
4. 界面需要接收键盘操作时，必须覆写 `OnPanelOperate(PanelOperateEnum operateCode)`，并根据 `PanelOperateEnum` 将操作分发到对应响应逻辑。
5. 窗口需要注册消息或交互监听时，通常在 `OnOpen()` 中调用私有函数 `AddLis()`，并在 `OnClose()` 中调用私有函数 `RemoveLis()`。注册与注销必须一入一出、严格成对。

`AddLis` 和 `RemoveLis` 是项目约定命名，不替换为其他同义名称。

## C# 换行风格

使用 K&R 风格：类、函数、条件和 `switch` 的左花括号与声明位于同一行。

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

UIBinder 自动生成的成员区域继续由工程既有生成器维护；功能逻辑写在生成标记区域之外。
