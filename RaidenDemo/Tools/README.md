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

双击 `Start-WeChatResourceServer.bat`，默认以共享测试 CDN 根目录 `E:\testcdn` 提供 HTTP 服务，监听 `0.0.0.0:8081`。窗口显示实际目录及局域网地址；保持窗口打开，关闭窗口或按 Ctrl+C 停止。后续资源发布无需重启服务。

电脑开发工具可使用 `http://127.0.0.1:8081`；手机使用窗口列出的电脑局域网地址，例如 `http://192.168.31.132:8081`。将地址填入微信转换面板的游戏资源 CDN，当前产物对应 `minigame/game.js` 的 `DATA_CDN`。手机应能访问该地址下的 `/raiden/bootstrap.json`；本工具不自动修改 CDN 或防火墙设置。

指定其他目录或端口时，在终端执行：

```bat
Start-WeChatResourceServer.bat --root "E:\testcdn" --port 8081
```

端口已占用时会显示提示，不会终止已有进程。应先关闭之前启动的服务，或指定其他端口并相应修改 CDN 地址。

## HybridCLR 构建

保持本项目 Unity 编辑器打开、目标为 WebGL，退出 Play 模式。使用 `Build-WeChatHotUpdate.ps1 -Action <动作>` 提交请求，Unity 控制台与 `Project/Library/AIUI/wechat-build-result.json` 显示执行结果。每一步成功后再执行下一步。

- `prepare`：安装项目内 HybridCLR 运行时，设置 Main、Login 为热更新程序集。
- `generate`：生成桥接代码、裁剪后的 AOT 元数据和新底座标识；必须随后重新导出底座。
- `export`：构建 Addressables、微信底座和初始候选资源，不切换发布清单。
- `content`：只编译业务 DLL、构建 Addressables 和候选资源；不重建微信底座。
- `publish`：切换本地 current 与 bootstrap；检查后推送 CDN 仓库，等待 Pages 部署完成。

Foundation、AOT 依赖、裁剪保留范围、桥接需求或平台编译设置发生改变时，重新执行 `generate` 与 `export`。`content` 不保证任意新功能均兼容旧底座。底座版本写入独立资源，不修改 PlayerSettings 的版本号。实际加载顺序为 AOT 元数据、Login、LoginEntry；进入正式内容时再装载 Main。

发布目录为 `E:\testcdn\raiden`，结构及缓存策略见下方当前说明。

### 2026-10-07 链路验收记录

- 微信底座：`build/build_wechat_20261007223336/minigame`；独立底座标识 `20261007143318`。
- 初始业务：`initial`；第二次业务发布：`update20261007143649`，当前清单已指向第二次发布。
- 第二次发布仅为 Login 增加入口诊断日志 `[Login] 业务程序集入口已启动。`。Login DLL 的 SHA256 已变化，Main DLL 未变化，微信包体所有文件的 SHA256 均未变化。
- 正式底座的 7 份 AOT 程序集与待下载元数据逐文件哈希一致；裁剪后的底座目录不含 Main.dll、Login.dll。
- 局域网清单、settings 与本次发布全部 21 个文件 HTTP 校验通过。
- 尚待用户执行：开发者工具及 Android/iOS 真机启动，确认出现业务发布版本 `update20261007143649` 和上述 Login 日志，并进入战斗验证。构建及 HTTP 校验不代表真机验收通过。

Jenkins 后续复用准备、生成、导出、内容发布四个阶段；当前脚本通过已打开的 Unity 编辑器队列执行，尚不是无人值守批处理入口。接入 Jenkins 时需要补充批处理入口、构建状态归档和正式 CDN 发布，并将底座与对应 AOT 元数据作为同一套不可变产物保存。

### 当前 Catalog 与缓存策略（2026-10-09）

当前远端根目录为 `E:\testcdn\raiden`，由 GitHub Pages 提供。`bootstrap.json` 按底座选择 `current.json`，后者通过 resourceRoot 选择 `<底座版本>/releases/<内容版本>/`，通过 bundleRoot 选择 `<底座版本>/bundles/`。Foundation 将 Addressables RuntimePath 映射到前者，将 Bundle 虚拟根地址映射到后者。settings 和 catalog 从内容版本目录读取，Bundle URL 不随内容版本变化。

构建关闭 `Build Remote Catalog` 并启用 `Disable Catalog Update on Startup`，仅发布 `catalog.json`，不生成 hash 更新依赖，也不复制 ServerData 遗留的 catalog/hash。关闭 Remote Catalog 不改变各 Group 的 Remote 路径。旧发布目录保持原样；新内容必须使用新版本目录。

`WeChatResourceCache.js` 在微信导出时自动写入：真机允许缓存版本化 Bundle 与 Catalog，入口清单、settings、hash 不缓存；开发者工具仍保留缓存兼容禁用。允许缓存不代表实际命中，验收应在真机不清缓存连续启动两次，结合请求地址、报告缓存结果核对。源码规则修改不会自动更新已安装的微信包。
### 2026-10-09 真机缓存与内容更新验收

- 底座保持 `20261008T190447577Z`，微信包体文件哈希未变；内容从 `20261009T034531015Z` 更新至 `20261009T035347653Z`。
- iOS 真机保留缓存，退出微信后重进，日志确认 Login、Main 均加载新内容版本，并出现验证标记 `CACHE-UPDATE-01`。
- 本次确认目录版本可隔离 Catalog 缓存；插件“Catalog 无 hash/版本信息”提示不表示该发布方式无法更新。
- 验收后源码已移除临时标记；已发布的不可变版本保留原样，后续正常构建会生成不带标记的新 DLL。
### 共享 Bundle 目录（2026-10-09）

```text
raiden/
├── bootstrap.json
└── <底座版本>/
    ├── current.json
    ├── bundles/                 同一底座各内容版本共享的哈希命名 Bundle
    └── releases/
        └── <内容版本>/
            ├── manifest.json
            ├── settings.json
            └── catalog.json
```

Catalog 仍记录虚拟根地址加 Bundle 完整相对路径，运行时只替换根地址，不扫描目录或挑选“最新”Bundle。每个 Catalog 精确引用该次构建需要的文件；未变化文件继续使用原 URL，变化文件使用新哈希名称。嵌套路径保留，避免扁平化导致重名。

构建只复制当前 Catalog 引用的文件；共享目录已有同名文件时校验 SHA256，一致则复用，不一致则失败，绝不覆盖。旧内容目录及旧 Bundle 默认保留，便于旧客户端会话继续运行与版本回退。清理必须同时考虑所有保留 Catalog，不按文件时间直接删除。

真机缓存白名单为 `<底座>/bundles/*.bundle`（支持子目录、要求哈希名称）及 `<底座>/releases/<内容>/catalog.json`；bootstrap、current、settings、manifest 与 hash 不缓存。此次迁移改变 Foundation 地址映射和微信包内缓存规则，需要新底座。旧目录仅在本次明确授权清理；正常 content 发布不会删除历史版本。
### 共享目录构建验证（2026-10-09）

- 新底座 `20261009T080300738Z`，微信工程 `build/build_wechat_20261009160344/minigame`。
- 初始内容 `20261009T080300738Z`，重复构建内容 `20261009T080510617Z`；current 选择后者。
- 两次构建引用相同的 28 个 Bundle（合计约 42.74 MiB），第二次新增 0 个；URL、SHA256 和文件修改时间全部不变。settings 无 Catalog hash 依赖。
- 按本次用户授权，旧 CDN 的 4 个底座目录已移出，备份于 `build/build_wechat_20261009160344/obsolete-cdn-backup`；入口仅保留新底座。由于直接递归删除被工具安全策略拦截，采用了可恢复归档。
- CDN 发布提交 `69acaef`；新底座真机启动与缓存命中仍需用户验收，构建和文件校验不替代真机验证。
- 发布后在线校验通过：bootstrap/current 指向新内容；settings、Catalog 和 28 个 Bundle 可访问，Login/Main Bundle 下载后的 SHA256 与本地一致。

### 帧动画流畅度验收（2026-10-09）

- 播放改为所属 Timer 的逐帧监听，循环保留超出时长，仅画面变化时刷新；一次性动画完成后退出调度。
- 裁帧任务共享每渲染帧约 2ms 软预算，通过协程等待下一帧，避免在同一 Update 内持续处理整段动画；单次纹理读取、上传无法中断。
- Unity 编译与 7 项帧边界检查通过。新底座 `20261009T083322709Z`，微信工程 `build/build_wechat_20261009163340/minigame`，CDN 提交 `7b4bd4f`。
- 发布后 28 个 Bundle 在线可访问，Login/Main 及 Foundation 元数据 Bundle 哈希校验通过；用户真机验收确认播放观感明显更流畅。
- Timer 通用 Loop/Once 调度重构尚未执行。