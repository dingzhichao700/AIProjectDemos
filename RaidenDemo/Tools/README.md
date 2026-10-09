# 雷电战机开发工具

本目录统一存放雷电战机项目的开发检查脚本及其测试源码，不包含游戏运行时内容。

## 目录结构

- `Test-RaidenBattle.ps1`：运行完整战斗回归检查。
- `Test-BulletLauncher.ps1`：单独运行子弹发射器回归检查。
- `Tests/`：供上述脚本编译使用的 C# 检查源码。
- `Start-WeChatResourceServer.bat`：双击启动微信资源 HTTP 服务。
- `WeChatResourceServer.cjs`：资源服务实现，使用 PATH 中的 Node.js，无需安装 npm 包。

测试产物写入 Unity 工程的 `Library` 或系统临时目录，不应在项目根目录新增测试输出目录。

## 微信资源服务

双击 `Start-WeChatResourceServer.bat`，优先以热更新发布目录 `build/local-cdn` 为资源根目录；尚未发布热更新时，使用 `build/build_wechat_时间戳` 中最新完整导出的 `webgl`。监听 `0.0.0.0:8081`。窗口显示实际目录及局域网地址；保持窗口打开，关闭窗口或按 Ctrl+C 停止。首次切换到热更新发布目录需要重启旧服务，之后发布业务更新无需重启。

电脑开发工具可使用 `http://127.0.0.1:8081`；手机使用窗口列出的电脑局域网地址，例如 `http://192.168.31.132:8081`。将地址填入微信转换面板的游戏资源 CDN，当前产物对应 `minigame/game.js` 的 `DATA_CDN`。手机应能访问该地址下的 `/StreamingAssets/aa/settings.json`；本工具不自动修改 CDN 或防火墙设置。

指定其他目录或端口时，在终端执行：

```bat
Start-WeChatResourceServer.bat --root "E:\UnityTemplateTest\RaidenDemo\build\build_wechat_20261005203453\webgl" --port 8081
```

端口已占用时会显示提示，不会终止已有进程。应先关闭之前启动的服务，或指定其他端口并相应修改 CDN 地址。

## HybridCLR 构建

保持本项目 Unity 编辑器打开、目标为 WebGL，退出 Play 模式。使用 `Build-WeChatHotUpdate.ps1 -Action <动作>` 提交请求，Unity 控制台与 `Project/Library/AIUI/wechat-build-result.json` 显示执行结果。每一步成功后再执行下一步。

- `prepare`：安装项目内 HybridCLR 运行时，设置 Main、Login 为热更新程序集。
- `generate`：生成桥接代码、裁剪后的 AOT 元数据和新底座标识；必须随后重新导出底座。
- `export`：构建 Addressables、微信底座并发布初始资源。
- `content`：只编译业务 DLL、构建 Addressables 并切换资源清单；不重建微信底座。

Foundation、AOT 依赖、裁剪保留范围、桥接需求或平台编译设置发生改变时，重新执行 `generate` 与 `export`。`content` 不保证任意新功能均兼容旧底座。底座版本写入独立资源，不修改 PlayerSettings 的版本号。当前 Login 依赖 Main，因此实际加载顺序为 AOT 元数据、Main、Login、LoginEntry。

发布目录：`build/local-cdn/hotupdate/<底座版本>/<业务版本>/`，其中 `aa` 保存初始化配置，`WebGL` 保存远程 Catalog 和 Bundle。同一底座的 `current.json` 指向完整发布版本；保留旧目录便于回退。正式 CDN 接入时，由构建链路替换 `HotUpdateBootstrap.ServerRoot`，资源布局保持一致。

### 2026-10-07 链路验收记录

- 微信底座：`build/build_wechat_20261007223336/minigame`；独立底座标识 `20261007143318`。
- 初始业务：`initial`；第二次业务发布：`update20261007143649`，当前清单已指向第二次发布。
- 第二次发布仅为 Login 增加入口诊断日志 `[Login] 业务程序集入口已启动。`。Login DLL 的 SHA256 已变化，Main DLL 未变化，微信包体所有文件的 SHA256 均未变化。
- 正式底座的 7 份 AOT 程序集与待下载元数据逐文件哈希一致；裁剪后的底座目录不含 Main.dll、Login.dll。
- 局域网清单、settings 与本次发布全部 21 个文件 HTTP 校验通过。
- 尚待用户执行：开发者工具及 Android/iOS 真机启动，确认出现业务发布版本 `update20261007143649` 和上述 Login 日志，并进入战斗验证。构建及 HTTP 校验不代表真机验收通过。

Jenkins 后续复用准备、生成、导出、内容发布四个阶段；当前脚本通过已打开的 Unity 编辑器队列执行，尚不是无人值守批处理入口。接入 Jenkins 时需要补充批处理入口、构建状态归档和正式 CDN 发布，并将底座与对应 AOT 元数据作为同一套不可变产物保存。

### 当前 Catalog 与缓存策略（2026-10-09）

当前远端根目录为 `E:\testcdn\raiden`，由 GitHub Pages 提供。`bootstrap.json` 按底座选择 `current.json`，后者选择 `<底座版本>/<内容版本>/`。Foundation 将 Addressables RuntimePath 和 Bundle 虚拟根地址映射到此版本目录；settings 和 catalog 都从该目录读取。

构建关闭 `Build Remote Catalog` 并启用 `Disable Catalog Update on Startup`，仅发布 `catalog.json`，不生成 hash 更新依赖，也不复制 ServerData 遗留的 catalog/hash。关闭 Remote Catalog 不改变各 Group 的 Remote 路径。旧发布目录保持原样；新内容必须使用新版本目录。

`WeChatResourceCache.js` 在微信导出时自动写入：真机允许缓存版本化 Bundle 与 Catalog，入口清单、settings、hash 不缓存；开发者工具仍保留缓存兼容禁用。允许缓存不代表实际命中，验收应在真机不清缓存连续启动两次，结合请求地址、报告缓存结果核对。源码规则修改不会自动更新已安装的微信包。
### 2026-10-09 真机缓存与内容更新验收

- 底座保持 `20261008T190447577Z`，微信包体文件哈希未变；内容从 `20261009T034531015Z` 更新至 `20261009T035347653Z`。
- iOS 真机保留缓存，退出微信后重进，日志确认 Login、Main 均加载新内容版本，并出现验证标记 `CACHE-UPDATE-01`。
- 本次确认目录版本可隔离 Catalog 缓存；插件“Catalog 无 hash/版本信息”提示不表示该发布方式无法更新。
- 验收后源码已移除临时标记；已发布的不可变版本保留原样，后续正常构建会生成不带标记的新 DLL。