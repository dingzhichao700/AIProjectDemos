# Game Framework 工具链规范

本仓库是一套可复用的 Unity Game Framework 模板与导出工具。它不包含供日常开发直接打开的工程；`Templates/GameFramework/ProjectFiles` 是复制源，复制后形成可打开的 Unity 工程。

## 权威顺序与文档索引

发生冲突时依次以：用户本次明确指令 → 目标工程 `AGENTS.md` → 专项规范 → Skill 默认规则 → 示例与历史记录为准。

- [`文档/开发规范/目录与分包原则.md`](文档/开发规范/目录与分包原则.md)：Assets、Group、图集和 UI Prefab 落位。
- [`文档/开发规范/模板与导出规范.md`](文档/开发规范/模板与导出规范.md)：自包含模板、导出器、验证和外部依赖边界。
- [`文档/开发规范/UI功能逻辑规范.md`](文档/开发规范/UI功能逻辑规范.md)：BasePanel 生命周期、监听命名和 C# 换行风格。
- [`文档/开发规范/游戏业务与配置规范.md`](文档/开发规范/游戏业务与配置规范.md)：业务配置接入和 Luban 导表脚本约束。
- [`文档/开发规范/README.md`](文档/开发规范/README.md)：专项文档索引。

专项细节只在对应文档维护，本文不复制完整路径表。

用户说“进规范”时，执行对应规范更新，并在完成回复中自动精简列出本次实际写入的规则；不得只回复“已写入”，也不默认包含提交或推送。

## 仓库定位

```text
game-framework-toolchain/
├── SKILL.md
├── AGENTS.md
├── agents/
├── 文档/
│   └── 开发规范/
├── Tools/
└── Templates/GameFramework/
    └── ProjectFiles/          完整但不含生成缓存的 Unity 工程源文件
        ├── Assets/
        ├── Packages/
        └── ProjectSettings/
```

普通导出禁止读取 Golden、b1 或任何外部样例工程。外部样例只能在用户明确要求“标准化回写”时作为已验收改动来源。

## 标准化回写

当用户说 **“标准化回写”** 时，将已经在样例工程中敲定并验证的可复用改动同步到本仓库：

1. Unity 源文件写入 `Templates/GameFramework/ProjectFiles`。
2. 导出流程写入 `Tools`。
3. 规范写入 `AGENTS.md` 或对应 `文档/开发规范` 专项文件。
4. Addressables 或运行时资源变化时，由下一次导出现场重新构建，不维护预构建缓存。
5. 不迁入单次 Blueprint、实验 UI、美术参考图、日志、Library、Temp 或样例专属 Addressables entry。
6. 保留无关的已有修改；未经要求不提交 Git。

## 已拍板工程决策

| 项 | 决策 |
|----|------|
| Unity | `ProjectVersion.txt` 为权威版本 |
| 渲染管线 | Built-in，禁止 URP |
| 入口类 | `RookieEngine` |
| 场景 | `GameEntrance`、`UIEditor` 两个场景 |
| Input System | `activeInputHandler: 2`（Both） |
| Addressables | opening、title、Default Local Group、scene、cutScene；暂不含 region |
| 配置 | title baseline 只带必要配置，不迁全量业务表 |

界面通过 Panel 与 Addressables 切换，不为每个界面增加 Unity Scene。

## 模板必须包含

- 完整 `Assets`、`Packages`、`ProjectSettings`。
- 内嵌 `Packages/com.code-philosophy.luban`。
- TMP Essentials、DOTween、TexturePacker importer 和必要 Editor 工具。
- `GameEntrance`、`UIEditor`。
- Opening、Title 与公共 UI baseline。
- Addressables 设置及五组定义。

模板禁止包含 `Library`、`Temp`、`Logs`、`UserSettings`、`.vs`、工程文件，以及 bag、bagcartoon、baglegendary、shop、AIUI 临时生成器等实验内容。

## 导出流程

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Generate-GameFrameworkProject.ps1 `
  -ProjectName GameFrameworkTest9 `
  -OutputRoot E:\UnityTemplateTest
```

流程：模板复制到系统临时 staging → 写入工程实例信息与文档 → Unity 导入编译 → 初始化并构建 Addressables → 验证 → 清理可再生 Library 缓存 → 移至最终目录。

- 默认拒绝覆盖；只有用户明确授权时使用 `-Force`。
- Addressables 不允许跳过；每次导出都从当前模板现场构建。
- 最终交付只保留 `Library/com.unity.addressables`；其他 Library 内容由接收方首次打开时重新生成。
- Unity 未显式指定时，按模板版本在常见安装目录自动查找。
- 任何编译错误、批处理错误、缺失 marker 或无 bundle 均视为导出失败。
- 导出目标只有 `{OutputRoot}/{ProjectName}`，staging 不出现在目标目录。

## 最小验证

修改模板或导出器后运行：

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Test-GameFrameworkTemplate.ps1
```

完整验收还包括：从仓库独立导出新样例、Unity 无编译错误、Addressables 构建成功、无 Missing Script/Sprite，并人工验证 `GameEntrance → Opening → Title → Option`。
