# QPDF GUI 实施方案（初版）

> 技术栈评价：.NET 10 + Avalonia 12 + CommunityToolkit.Mvvm + CliWrap 是一个成熟、轻量、跨平台的组合，适合做「CLI 包装型」桌面工具。无需更改。
> 当前 NuGet 最新版本（2026-09 核实）：Avalonia 12.1.2、CommunityToolkit.Mvvm 8.4.2、CliWrap 3.10.5、FluentIcons.Avalonia 2.1.341。

---

## 0. 核心设计决策

| 决策 | 选择 | 理由 |
| :--- | :--- | :--- |
| 调用 qpdf 的方式 | **`--job-json-file`（Job JSON）**，而非拼接命令行参数 | qpdf 11+ 官方支持。GUI 状态 → C# 对象 → JSON 序列化，天然避免引号/转义/参数顺序问题；同一份 JSON 还能导出给用户复现。 |
| 读取 PDF 信息 | `qpdf --show-npages` 与 `qpdf --show-encryption` | 轻量高效探查页数与加密状态，无需额外解析庞大 JSON 语法树。 |
| 进度 | `--progress` + CliWrap 逐行读 stdout | qpdf 写入文件时输出 `qpdf: <file>: write progress: N%`，正则解析驱动进度条。 |
| qpdf 二进制来源 | 三级查找：`runtimes/<rid>/native/qpdf` → 用户设置路径 → `PATH` | 开发者机器上 qpdf 可能不存在（本机就没有）。发布时按 RID 附带二进制及运行时 DLL，用户也可自行指定。 |
| 错误处理 | qpdf 退出码：0 成功 / 2 错误 / 3 警告但完成 | 不带 `warningExit0`，退出码 3 原生表示成功但带警告/已修复，stderr 收集日志。 |
| 输出安全 | 永远不原地覆盖输入；默认输出到 `<name>_<op>.pdf`，冲突时自动加序号 | 防止用户误操作丢文件。qpdf 原地写入需 `--replace-input`，本项目不使用。 |
| 依赖注入 | `Microsoft.Extensions.DependencyInjection` | 服务（QpdfService、DialogService、Settings）可替换、可测试。 |

---

## 1. 项目结构

```text
qpdf-gui/
├─ QpdfGui.sln
├─ Directory.Build.props            # 统一 TargetFramework/Nullable/LangVersion
├─ Directory.Packages.props         # 中央包版本管理
├─ src/
│  ├─ QpdfGui.Core/                 # 无 UI 依赖，可单测
│  │  ├─ Jobs/                      # Job JSON 模型（对应 qpdf job schema）
│  │  │  ├─ QpdfJob.cs              # 根对象：inputFile/outputFile/options...
│  │  │  ├─ EncryptOptions.cs
│  │  │  ├─ PagesSpec.cs            # --pages 的 file/range 列表
│  │  │  └─ PageRange.cs            # 范围表达式解析+校验（"1-5,7,z-1"）
│  │  ├─ Process/
│  │  │  ├─ QpdfLocator.cs          # 三级查找 qpdf 可执行文件
│  │  │  ├─ QpdfRunner.cs           # CliWrap 封装：运行 job、解析进度、超时/取消
│  │  │  └─ QpdfResult.cs           # ExitCode、Warnings、Errors、Duration
│  │  ├─ Inspect/
│  │  │  ├─ PdfInfo.cs              # 页数、版本、是否加密、权限
│  │  │  └─ PdfInspector.cs         # 调 qpdf --json 并反序列化
│  │  └─ Services/
│  │     ├─ IQpdfService.cs         # Merge/Split/Encrypt/Decrypt/Rotate/Repair
│  │     ├─ QpdfService.cs
│  │     └─ OutputPathResolver.cs   # 默认输出名+冲突处理
│  └─ QpdfGui.App/                  # Avalonia 桌面
│     ├─ App.axaml / Program.cs
│     ├─ ViewModels/
│     │  ├─ MainWindowViewModel.cs  # 左侧导航 + 当前功能页 + 任务面板
│     │  ├─ Tools/
│     │  │  ├─ MergeViewModel.cs
│     │  │  ├─ SplitViewModel.cs
│     │  │  ├─ EncryptViewModel.cs
│     │  │  ├─ DecryptViewModel.cs
│     │  │  ├─ RotateViewModel.cs
│     │  │  └─ RepairViewModel.cs
│     │  ├─ TaskQueueViewModel.cs   # 任务列表、进度、日志
│     │  └─ SettingsViewModel.cs    # qpdf 路径、默认输出目录、主题
│     ├─ Views/                      # 与 ViewModel 一一对应的 .axaml
│     ├─ Services/
│     │  ├─ IDialogService.cs / DialogService.cs   # 文件/文件夹选择（StorageProvider）
│     │  └─ SettingsStore.cs        # JSON 持久化到 %APPDATA%/qpdf-gui
│     └─ Assets/
├─ tests/
│  └─ QpdfGui.Core.Tests/           # xunit：PageRange 解析、Job 序列化、输出路径、结果解析
└─ tools/
   └─ fetch-qpdf.ps1                # 从 GitHub Releases 下载各平台 qpdf 到 runtimes/
```

---

## 2. 功能 → qpdf 映射

| 功能 | Job JSON 关键字段 | 备注 |
| :--- | :--- | :--- |
| 合并 | `"inputFile": "--empty"`（或第一个文件）, `"pages": [{file, range:"1-z"}...]` | 列表可拖动排序；每项可指定范围。 |
| 提取范围 | `"pages": [{file, range}]` | 复用合并逻辑，单文件。 |
| 固定页数拆分 | `"splitPages": "N"` | 输出名需含 `%d` 占位，qpdf 自动编号。 |
| 解密 | `"decrypt": ""` + `"password"` | 打开密码交给用户输入；仅权限限制时密码可留空。 |
| 加密 | `"encrypt": {"userPassword", "ownerPassword", "256bit": {print, modify, extract, annotate, ...}}` | 固定使用 AES-256（Job Schema 属性名为 `"256bit"`）。 |
| 旋转 | `"rotate": ["+90:1-z"]`，格式为 `["[+/-]angle:range"]` | 支持数组形式指定范围。 |
| 修复 | 无特殊字段，qpdf 读写本身即修复；加 `"objectStreams": "generate"` | 退出码 3 表示修复了问题，显示警告详情。 |
| 通用 | `"progress": ""` | 所有 job 默认附带以驱动实时进度。 |

每个功能页顶部都有「文件信息条」：调 `PdfInspector` 显示页数/加密状态，加密文件自动弹密码框。

---

## 3. UI 布局

```text
┌──────────────┬──────────────────────────────────────────┐
│ 合并          │  [功能页内容：拖拽区 / 文件列表 / 参数表单]   │
│ 拆分与提取     │                                          │
│ 加密          │                                          │
│ 解密          │                                          │
│ 旋转          │              [ 执行 ]                     │
│ 修复          ├──────────────────────────────────────────┤
│              │  任务面板：进度条 / 状态 / 日志(可折叠)       │
│ ⚙ 设置        │                                          │
└──────────────┴──────────────────────────────────────────┘
```

- 使用 Avalonia 内置 `SplitView` + `ListBox` 做导航，`FluentTheme` 深浅色跟随系统。
- 拖拽文件到窗口任意位置即添加（`DragDrop.DropEvent`）。
- 所有功能页共用 `ToolPageBase`（输入文件列表 + 输出目录选择 + 执行按钮），减少重复。

---

## 4. 实施阶段

### 阶段 1：骨架（可编译、可运行）

1. 创建 sln、三个项目、`Directory.Build.props`（`net10.0`、`Nullable enable`、`ImplicitUsings`）、`Directory.Packages.props`。
2. Avalonia 主窗口 + 左侧导航 + 空白页面切换，DI 容器接入。
3. `QpdfLocator` + 设置页可手动指定 qpdf 路径；启动时找不到 qpdf 给出明确提示（含下载链接）。
4. `tools/fetch-qpdf.ps1`：下载 Windows x64 的 qpdf release zip，解压 `qpdf.exe` 及 DLL 到 `runtimes/win-x64/native/`，csproj 里以 `Content` 复制到输出目录。

### 阶段 2：Core 与测试

1. `QpdfJob` 模型 + `System.Text.Json` 序列化（`JsonIgnoreCondition.WhenWritingNull`，字段名与 qpdf schema 一致）。
2. `PageRange` 解析与校验（支持 `1-5`、`z`、`r1`、`1-z:even/odd`），越界即时报错。
3. `QpdfRunner`：写临时 job 文件 → CliWrap 执行 → 逐行解析进度 → 收集 stderr → 映射退出码 → 删除临时文件。支持 `CancellationToken`。
4. `PdfInspector`：`--json --json-key=pages --json-key=encrypt`，返回 `PdfInfo`。
5. xunit 覆盖：范围解析、JSON 输出、输出路径冲突、退出码映射。

### 阶段 3：六个功能页

按「修复 → 解密 → 旋转 → 加密 → 提取/拆分 → 合并」顺序实现（从简单到复杂），每页完成后手动跑一次真实 PDF。

### 阶段 4：体验完善

1. 任务队列：多任务顺序执行，可取消，完成后「打开所在文件夹」。
2. 「显示等效命令」按钮：把 job JSON 转成可复制的 `qpdf` 命令行，方便高级用户。
3. 设置持久化、窗口尺寸记忆、最近使用目录。
4. `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`，验证 qpdf 二进制随发布包一起工作。

### 阶段 5（可选）

- macOS / Linux RID 的 qpdf 打包与验证。
- 页面缩略图预览（需引入 PDFium，属于新依赖，谨慎评估）。
- 批处理：对文件夹内所有 PDF 应用同一操作。

---

## 5. 风险与注意事项

- **qpdf 版本下限 11.0**：Job JSON 在 11 引入，`QpdfLocator` 找到二进制后先跑 `--version` 校验并在设置页显示。
- **中文/空格路径**：Job JSON 方式已规避命令行转义问题，但临时 job 文件本身要用 UTF-8 无 BOM 写入。
- **加密文件的信息读取**：`PdfInspector` 遇到需要打开密码的文件会返回退出码 2，UI 捕获后提示输入密码再重试。
- **拆分输出名**：`splitPages` 要求输出路径含 `%d`，`OutputPathResolver` 需专门处理。
- **Avalonia 12 API 变动**：文件对话框只用 `TopLevel.StorageProvider`，不要用已移除的 `OpenFileDialog`。
