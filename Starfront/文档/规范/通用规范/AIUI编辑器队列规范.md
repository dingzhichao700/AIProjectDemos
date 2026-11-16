# AIUI 编辑器状态与请求协议

- 外部流程只能依据 `Library/AIUI/editor-status.json` 中由 Unity API 上报的 Play、编译、刷新和执行状态，不得根据窗口标题、日志停顿或进程表现推断。
- 不得通过模拟键盘快捷键切换 Play。需要退出 Play 时提交显式 `stopPlay` 控制请求，由 Unity 内部调用编辑器 API。
- 请求必须先完整写入临时文件，再原子改名为 `pending-*.json`；不得直接向监听文件执行覆盖复制。
- 请求、状态和结果必须保留同一非空 `requestId`。解析或执行失败时也不得丢失已能提取的请求 ID。
- 提交前必须校验工具版本、Unity PID 和忙碌状态。
- 用户启动的 Unity 默认不得关闭或重启；自动启动的实例必须记录 PID，并在交付前正常关闭、确认工程锁释放。
- 普通内容修改不得顺带修改常驻 Editor 工具。工具能力升级应独立完成一次编译与验证，再进入内容请求阶段。
- 同一批业务代码应集中修改后只触发一次编译；普通预览不执行图集、Addressables 或全工程扫描。

## 测试相机、截图与现场恢复

适用：自建Camera/Canvas的编辑器预览、自动截图及隔离运行验收；不限于AIUI队列。不得把项目专用角色夹具、场景节点名、资源或请求数据复制为模板基线。本节基于已验收的屏幕输出/截图恢复修复提炼，供现有及后续测试工具遵守。

### 先明确测试的输出用途

- 留给用户查看或仍需正常Game视图的Play现场，必须保持目标Display上至少一台有效、启用且activeInHierarchy的相机向屏幕输出，不能把唯一测试相机长期绑定到离屏RenderTexture。Canvas按自身渲染模式核实相机、目标显示与实际可见结果；仅相机数量非零不算通过。
- 纯离屏截图允许在明确标识的无人交互采样阶段不输出屏幕，但不得把这种Play现场当作可用用户入口交付。采样结束必须恢复屏幕相机或正常结束并恢复原现场。需要用户查看的阶段不得以纯离屏模式解释黑屏。
- 同时检查普通Scene与DontDestroyOnLoad中的相机/画布，避免漏隔离导致重复输出，或漏恢复导致没有屏幕输出。只操作本测试登记拥有或已明确获准临时更改的对象。

### 截图只临时占用渲染目标

截图入口必须在同一同步作用域内保存原camera.targetTexture与RenderTexture.active，短暂绑定离屏目标、Render、读回和编码/写文件，并用finally恢复两者；原target可能非空，不能无条件置null。写文件失败、Render/读回失败和调用方取消均不得跳过恢复。截图过程不跨await持有全局RenderTexture.active，不与其他截图并发争用同一相机/全局渲染目标。

通用示意（离屏target由调用方持有，方法仅拥有临时读回Texture2D）：

```csharp
static void CaptureToFile(Camera camera, RenderTexture target, string path) {
    var previousTarget = camera.targetTexture;
    var previousActive = RenderTexture.active;
    Texture2D readback = null;
    try {
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        readback.Apply();
        System.IO.File.WriteAllBytes(path, readback.EncodeToPNG());
    } finally {
        if (camera != null) camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        if (readback != null) {
            if (Application.isPlaying) UnityEngine.Object.Destroy(readback);
            else UnityEngine.Object.DestroyImmediate(readback);
        }
    }
}
```

调用前检查相机、target有效且target已创建，调用期间由本测试持有其生命周期。不得销毁、Release借用的相机/纹理；本测试创建的RT在所有使用者解绑后Release并销毁。正常运行图与离屏读回图标明来源，不把后者单独当成Game视图已恢复的证据。

### 结束、异常及编辑器重载的清理

- 进入前记录原Scene设置、相机enabled/targetTexture/targetDisplay、被改根节点active值、Canvas绑定、资源Play模式及本测试的监听/计时/异步请求/原生资源所有权。按实际改动记录，不为了测试无条件保存正式Scene或覆盖原有未保存修改。优先在Play内隔离；必须在Edit改现场时先保存可恢复的精确快照，不只用场景索引/同名搜索猜对象。
- 正常完成、失败、取消、提前停止Play以及脚本重载均进入同一幂等清理流程。finally负责截图作用域；测试生命周期还须在相应Play退出、Edit恢复及重载边界注销监听和计时，失效旧异步回调，再解绑相机目标、释放本测试创建的RT/Texture、销毁本测试的Camera/Canvas/EventSystem及临时对象，按快照恢复被改状态。不得清理别人的对象、真实存档、当前构建产物或按名称全局删除。
- 根节点逐项还原原active值，不能全部设true；原本停用的池保持停用。恢复相机/Canvas输出、Addressables测试模式及原场景设置，检查Scene脏状态，不能通过保存场景或清掉脏标记掩盖未恢复改动。
- 对象已销毁、场景已卸载或恢复依据不匹配时记录明确失败和剩余恢复项，不在一次恢复异常后静默放弃其他可执行清理。未清理或仍无正常屏幕输出时不得标记测试完成/时段释放。
- 进程崩溃/显卡重置无法保证finally或Play退出回调执行。需要在Edit停用真实场景的工具须保留可核对的恢复记录；接续先保护日志、备份与未保存Scene，核实进程及待执行请求。SessionState不作为跨进程唯一恢复凭据；不盲目重放旧请求、保存临时场景或强制开启全部根节点。没有本轮现场许可不停止他人的Play，遵守项目共享队列。

### 自主复核与交付证据

至少覆盖：仅测试相机输出时的Game视图；截图成功及故意写入失败后camera.targetTexture和RenderTexture.active均恢复；正常完成/失败或取消后的清理；已正常回Edit再进入正常入口，核实相机/Canvas、可见页面及适用往返。重复开始/结束，检查临时相机/Canvas/RT、监听和旧回调没有积累，原场景/资源模式和存档受保护。

证据区分实际执行与未覆盖分支：真实Game运行图、状态/异常链及原值对照共同复核，不以编译、离屏截图或相机计数替代。新建或改动通用C#工具需做实际编译与相关生命周期验证；仅规范回写则核对已验收实现和运行证据，并明确没有新增运行实现。技术自检不代替项目约定的用户阶段验收。

