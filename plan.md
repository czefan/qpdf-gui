# QPDF GUI 重构方案（第二版）

> 基线：`main` @ `d517488`。`dotnet build -c Release` 0 警告 0 错误，`dotnet test` 38/38 通过。
> 上一版方案中的 B1–B7 已修复并经复核确认，本版不再重复。
> 本版三个主题：① 复核后残留的问题；② 回答"是否需要这么复杂"并给出精简路线；③ 体积 / 内存 / 启动的实测数据与优化项。
> 所有数字均为本机实测（Windows 11，AMD 显卡，qpdf 12.4.1，.NET 10.0.11）。

---

## 0. 复核结果

### 0.1 已确认修好

| 项 | 复核方式 |
| :-- | :-- |
| B1 合并/提取/拆分 `--empty` | 读 `QpdfService.cs` 三处改为 `Empty = ""`；集成测试 `MergeAsync_WithEmptyInput_Succeeds`、`SplitByRangesAsync_GeneratesMultipleFiles` 真跑 qpdf 通过。 |
| B2 旋转角度 | `EqualityToBoolConverter.ConvertBack` 返回参数值，未选中时返回 `DoNothing`，实现正确。 |
| B3 仅打开密码加密 | owner 为空时回退为 user；两者皆空时给出错误。 |
| B4 警告展示 | `Warnings` 集合 + 面板在六个页面均已接入。 |
| B5/B6 主题与缺失画刷 | `DropZoneBackgroundBrush` 及 `Feedback*` 画刷已在 Light/Dark 两套字典中定义。 |
| B7 固件 | `tests/fixtures/three-pages.pdf` 已入库，`qpdf --check` 无错误。 |

### 0.2 残留问题（本轮必须处理）

| # | 问题 | 位置 | 说明 |
| :-- | :-- | :-- | :-- |
| R1 | 集成测试在 CI 里仍然假绿 | `QpdfServiceIntegrationTests` 每个用例开头 `if (!File.Exists(_qpdfExe)) return;`；`runtimes/**/native/*` 被 gitignore；`release.yml` 直接 `dotnet test` | CI 上引擎不存在，全部集成用例静默通过。要么 CI 先跑 `tools/fetch-qpdf.ps1`，要么用 `Assert.Skip` 让跳过可见。 |
| R2 | B3 的回退逻辑没有被测试覆盖 | `EncryptAsync_UserPasswordOnly_Succeeds` 显式传了 `OwnerPassword = "open123"` | 测的是 qpdf，不是回退逻辑。回退逻辑在 `EncryptViewModel` 里写了两遍（`UpdateEquivalentCommand` 与 `ExecuteAsync`），应下沉到 Core 一处并加单测。 |
| R3 | 新增了 8 处硬编码中文 | 六个视图的警告面板标题 `"QPDF 提示与警告记录："`；`SingleFileToolViewModel.cs:476` 与 `MergeViewModel.cs:609` 的 `"条提示/警告"` | 英文界面下会出现中文。 |
| R4 | 硬编码颜色仍有 24 处 | `MainWindow.axaml` 引导横幅 7 处；五个单文件视图 + Merge 的"已加密"徽章 `#20FAAD14`/`#D48806`；`SettingsView` 状态点 `#52C41A`/`#FAAD14`；`MergeView` 规则错误 `#20F53F3F`/`#F53F3F` | 深色模式下横幅仍是浅黄底。 |
| R5 | 复制命令按钮又复制了六份 | 六个视图各 ~12 行完全相同 | 视图重复度进一步上升（单文件视图 159–201 行，Merge 235 行）。 |
| R6 | 警告识别靠关键字 | `QpdfRunner.cs:86` `Contains("warning")` | qpdf 的结尾行 `operation succeeded with warnings` 也被当成一条警告；应只取以 `WARNING:` 开头的行并去掉文件路径前缀。 |
| R7 | 拆分页 `BaseRange` 语义未变 | `SplitView.axaml:76-83`，`SplitViewModel` | 输入框始终可见，但仅"固定页数"模式生效；"按范围"模式下填了无效果、无提示。 |
| R8 | 合并页两套入口未变 | `MergeViewModel.ExecuteAsync` | 填了表达式则列表内每行"提取页码"被静默忽略。 |
| R9 | Core 层仍有 27 处中文、探查仍启动 4 个进程 | `PdfInspector.cs` 46/54/62/97 行 | 见第 2 节精简方案。 |
| R10 | 发布包体积 / 启动 / 内存未优化 | 见第 3 节 | 当前独立版 51 MB，启动 1.1 s，工作集 ~280 MB。 |

---

## 1. 关于"需要这么复杂吗"

结论：**不需要。** 上一版方案按"可扩展框架"设计，对一个六页的 CLI 包装器过度了。本版改为"精简优先"：目标是**删代码而不是加抽象**，`src/` 从 6255 行降到约 3500 行，文件数减少而不是增加。

上一版中**撤回**的设计（理由：收益不抵复杂度）：

| 撤回项 | 替代 |
| :-- | :-- |
| `PdfOperation` 八个记录类型 + `ToJobs()` | 每个页面 VM 直接构造 `QpdfJob`（已有模型）；等效命令由 `QpdfJob.ToCommandLine()` 从 Job 派生，六份手写字符串删掉即可达到"单一真源"。 |
| `WeakReferenceMessenger` 四种消息 | `SettingsStore` 暴露一个 `Changed` 事件；引擎状态由 `QpdfLocator` 缓存 + 一个静态事件。两个事件足够。 |
| `ILocalizer` + 生成 `StringKeys` + `{loc:Loc}` 标记扩展 | 保留现有静态 `LocalizationManager` 与 `{DynamicResource}`。补齐缺失键，加一个 CI grep 守护中文硬编码即可。 |
| `IShellService` 三平台实现 | 一个静态 `Shell` 类，三个方法，`OperatingSystem.IsWindows()` 分支。 |
| 四个设置子 VM + `EngineBannerViewModel` | `SettingsViewModel` 保留单文件，但把"引擎状态"抽成一个 `EngineStatusViewModel`（横幅与设置页共用，约 80 行）。其余分区不拆。 |
| `IQpdfEngine` 接口 + `EngineStatus` 记录 | `QpdfLocator` 增加 `static Task<EngineInfo> ProbeAsync()` 并缓存结果。 |
| `ErrorCode` 枚举 + `ValidationException` | Core 返回英文技术性消息或 `null`，App 只翻译三种情况（未找到引擎、需要密码、缺少密码）。 |
| 独立 `QpdfGui.App.Tests` 项目 + xunit v3 迁移 | 留在现有测试项目里加一个 `Avalonia.Headless.XUnit` 冒烟测试类（约 40 行，加载六个页面断言无绑定错误，这是 B2 类问题的唯一自动化防线）。xunit 版本不动，用 `Xunit.SkippableFact` 解决 R1。 |
| `Microsoft.Extensions.DependencyInjection` | 删除。八个单例在 `App.axaml.cs` 里 `new` 出来，少一个包、少一次容器构建。 |
| `IQpdfService` / `QpdfService` 两层 | 删除。`QpdfRunner.RunAsync(QpdfJob)` 就是唯一入口；"按范围拆分"这种多 job 场景在 VM 里循环调用。 |
| `IUpdateService` + `IQpdfDownloaderService` 两个服务 | 合并为一个 `GitHubReleases` 类（取最新版本 + 下载资产），约 120 行。 |

**保留**的设计：

- 一个工具页基类 `ToolViewModel`（合并页也继承它），承载：输出目录/文件名/完整路径三向同步、执行管线、进度、取消、警告、错误、等效命令、打开文件/目录、复制命令。目前这些在 `SingleFileToolViewModel` 与 `MergeViewModel` 各写一份，合并后删约 200 行。
- 三个可复用 `UserControl`，不带独立 VM，直接绑定基类属性：`InputFileCard`（单文件选择 + 摘要 + 密码）、`OutputCard`（目录 + 文件名）、`RunPanel`（执行/复制/取消 + 进度 + 成功/警告/错误面板）。五个单文件视图各从 160–200 行降到 40–60 行，Merge 视图降到约 100 行。
- `EqualityToBoolConverter`、`OutputPathResolver`、`PageRange`、`MergeRuleParser` 及其测试。

### 1.1 精简后的目录

```text
src/QpdfGui.Core/
  Jobs/        QpdfJob.cs（+ ToCommandLine）  PagesSpec.cs  EncryptOptions.cs（+ 密码回退规则）
  Process/     QpdfLocator.cs（+ ProbeAsync 缓存）  QpdfRunner.cs  QpdfResult.cs  TempJobFile.cs
  Inspect/     PdfInspector.cs（2 次进程）  PdfInfo.cs
  Text/        PageRange.cs  MergeRuleParser.cs  OutputPathResolver.cs
src/QpdfGui.App/
  App.axaml(.cs)  Program.cs
  Services/    LocalizationManager.cs  SettingsStore.cs  DialogService.cs  GitHubReleases.cs  Shell.cs
  ViewModels/  MainWindowViewModel.cs  EngineStatusViewModel.cs  SettingsViewModel.cs
               ToolViewModel.cs  MergeViewModel.cs  SplitViewModel.cs  EncryptViewModel.cs
               DecryptViewModel.cs  RotateViewModel.cs  RepairViewModel.cs
  Views/       MainWindow.axaml  SettingsView.axaml  六个 *View.axaml
    Controls/  InputFileCard.axaml  OutputCard.axaml  RunPanel.axaml  Icons.axaml
  Converters/  EqualityToBoolConverter.cs
  Themes/      Brushes.axaml  Styles.axaml
  Resources/   Strings.zh-CN.axaml  Strings.en-US.axaml
tests/QpdfGui.Core.Tests/   现有 + HeadlessSmokeTests.cs
tests/fixtures/             three-pages.pdf  encrypted-user.pdf  encrypted-owner-only.pdf  damaged.pdf
```

---

## 2. 精简重构的具体改动

### 2.1 Core

1. **`QpdfJob.ToCommandLine()`**：按 Job 字段拼接 `qpdf` 命令行（`empty` → `--empty`，`pages` → `--pages f r ... --`，`encrypt` → `--encrypt u o 256 --print=...`，等）。删除六个 VM 里的 `UpdateEquivalentCommand` 实现，基类改为 `EquivalentCommand = BuildJob()?.ToCommandLine()`。单测：每种 Job 一条期望字符串。
2. **`EncryptOptions.Normalize()`**：owner 为空取 user；两者皆空返回 `null` 表示不可执行。`EncryptViewModel` 两处重复逻辑删掉，只调这一处。单测覆盖三种输入。
3. **`QpdfRunner`**：
   - 警告只收以 `WARNING:` 开头的行，去掉前面的文件路径；结尾 `operation succeeded with warnings` 行不计入。
   - 退出码 2 时 `ErrorText` 取 stderr 最后一行非空内容（当前是整段 stderr 塞进 `ErrorMessage`）。
   - `splitPages` 进度会按输出文件从 0% 重计（实测），检测到百分比回落时视为下一文件，按 `ceil(页数/N)` 折算。
   - 取消后删除 `OutputFile`（若已产生半成品）。
4. **`PdfInspector`** 由 4 次进程降为 2 次：`--json --json-key=encrypt [--password]`（一次拿到 encrypted / userpasswordmatched / ownerpasswordmatched / 权限）+ `--show-npages`。退出码 2 且 stderr 含 `invalid password` → `RequiresPassword`。实测两种调用在错密码时都返回退出码 2 与同一条消息。
5. **`QpdfLocator.ProbeAsync()`**：定位 + `--version` 一次，结果缓存；`Reset()` 在用户改路径或下载完成后调用。查找顺序固定并与注释一致：用户路径 → `<app>/runtimes/<rid>/native` → `%LocalAppData%/QpdfGui/runtimes/<rid>/native` → `PATH`。删掉"上一层或当前目录"这个未文档化的第五级。
6. **`OutputPathResolver`** 不再创建目录，只算路径；目录创建移到 `QpdfRunner` 执行前。
7. Core 内 27 处中文改为英文技术消息或删除（UI 层自己决定显示什么）。CI 用 grep 守护。
8. 删除 `IQpdfService`、`QpdfService`；Core 的 `Microsoft.Extensions.DependencyInjection` 包引用删除。

### 2.2 App

1. **`ToolViewModel` 基类**：把 `SingleFileToolViewModel` 与 `MergeViewModel` 的公共部分合并，抽象方法只剩 `QpdfJob? BuildJob()`（返回 `null` 表示当前不能执行，并给出 `BlockingReason` 资源键）和可选的 `IEnumerable<QpdfJob> BuildJobs()`（按范围拆分用）。执行按钮 `CanExecute` 绑到 `BuildJob() != null && EngineReady && !IsBusy`，按钮旁显示 `BlockingReason`，取代"点了没反应"。
2. **三个 `UserControl`** 替换六个视图里的重复 XAML：`InputFileCard`、`OutputCard`、`RunPanel`。它们不设 `DataContext`，直接继承父视图的 `ToolViewModel`。
3. **拆分页**：删除 `BaseRange` 输入框；模式改为三选一（每 N 页 / 按多个范围 / 提取单个范围），只有"每 N 页"模式显示"作用范围"输入。输出卡片随模式切换文件名占位（`_%d` 模式串 / 目录 + 基名 / 单文件名）。
4. **合并页**：列表行本身就是片段（同一文件可加多次），表达式输入框改为"从表达式导入"按钮，解析后生成行；执行只看列表。
5. **`EngineStatusViewModel`**：横幅与设置页共用；`MainWindowViewModel` 中三种转发机制删除。
6. **主题**：`Themes/Brushes.axaml` 定义 `Warning*` / `Danger*` / `Success*` 语义画刷（直接引用 Semi 令牌 `SemiColorWarning`、`SemiColorDanger`、`SemiColorSuccess` 及其 `Light` 变体），清掉 R4 的 24 处；CI grep 守护 `#[0-9A-Fa-f]{6}`。
7. **本地化**：补齐 R3 的 8 处；`DialogService` 与 `SettingsViewModel` 中约 20 处中文改为资源键。
8. **`App.axaml.cs`**：截图工装移到 `Diagnostics/ScreenshotHarness.cs` 并 `#if DEBUG`；主文件只剩对象构造与主窗口创建。
9. **`Shell` 静态类**：`OpenFile` / `RevealInFolder` / `OpenUrl`，替换 VM 里的 `Process.Start("explorer.exe")`。
10. `tools/scratch_capture.ps1` 删除（被 `ScreenshotHarness` 取代），README 同步。

### 2.3 测试

- 保留现有 34 个纯逻辑测试 + 4 个集成测试；集成测试改用 `[SkippableFact]`，引擎缺失时显示为 Skipped 而不是 Passed。
- 补固件：`encrypted-user.pdf`、`encrypted-owner-only.pdf`、`damaged.pdf`（用 qpdf 从 three-pages 生成，各 < 2 KB）。
- 新增：`ToCommandLine` 快照、`EncryptOptions.Normalize`、警告行解析（用实测 stderr 文本）、进度回落解析、探查器 JSON 解析（固定样本）、损坏文件修复后 `Warnings.Count > 0`。
- 新增 `HeadlessSmokeTests`：`Avalonia.Headless.XUnit` 加载六个页面与设置页，断言无 `BindingError` 日志。
- 新增 `.github/workflows/ci.yml`：push/PR 触发；先 `tools/fetch-qpdf.ps1` 再 `dotnet test`，集成测试在 CI 上必须真跑。

---

## 3. 体积、内存、启动

### 3.1 实测基线（当前代码，Release）

| 指标 | 值 | 说明 |
| :-- | :-- | :-- |
| 窗口出现耗时（热启动） | 1.1 s | 冷启动（首次）3.2 s |
| 工作集 / 私有内存 | 280 MB / 200 MB | 窗口出现 1.5 s 后采样 |
| 线程数 / 已加载模块 | 79 / 137 | |
| 独立单文件（现 release.yml 配置） | 51.2 MB | 压缩开启 |
| 框架依赖单文件 | 36.7 MB | |

内存构成（按已加载模块大小）：显卡驱动 `amdxx64.dll` 47.7 MB + ANGLE `av_libGLESv2.dll` 5.2 MB + `d3dcompiler_47.dll` 4.5 MB（GPU 渲染路径），`libSkiaSharp` 11.1 MB，`FluentIcons.Resources.Avalonia.dll` 4.9 MB（整套图标字体，实际只用 25 个图标），`Semi.Avalonia.dll` 1.7 MB，`Avalonia.Fonts.Inter.dll` 1.8 MB。

### 3.2 发布参数对比（同一份代码）

| 变体 | exe 体积 | 热启动 | 工作集 |
| :-- | --: | --: | --: |
| 框架依赖，单文件 | 36.7 MB | 1.09 s | 282 MB |
| 框架依赖，单文件 + ReadyToRun | 54.5 MB | 0.62 s | 283 MB |
| 独立，单文件，压缩（现状） | 51.2 MB | 1.17 s | 326 MB |
| 独立，裁剪（partial），压缩 | 25.8 MB | 1.37 s | 276 MB |
| **独立，裁剪，压缩 + ReadyToRun** | **37.5 MB** | **0.53 s** | 294 MB |
| 独立，裁剪，ReadyToRun，不压缩 | 74.8 MB | 0.47 s | 264 MB |
| 独立，裁剪 + `InvariantGlobalization` | 25.8 MB | **崩溃** | Semi 主题构造 `CultureInfo("zh-CN")`，不能用 invariant 模式 |
| NativeAOT | 见 3.5 | | |

结论：**ReadyToRun 是性价比最高的单项改动**，启动时间减半，体积 +12 MB。单文件压缩会让启动慢约 0.2–0.3 s，但体积减半；推荐保留压缩。

### 3.3 代码级实验（Release，未发布，JIT）

| 改动 | 热启动 | 工作集 | 备注 |
| :-- | --: | --: | :-- |
| 基线 | 1.1 s | 280 MB | |
| 去掉 Inter 字体 | 1.1 s | 276 MB | 无收益；但可减 1.8 MB 包体积。中文界面本来就走系统字体回退。 |
| 软件渲染（`Win32RenderingMode.Software`） | 1.0 s | **182 MB** | 省 100 MB，不加载显卡驱动与 ANGLE。本应用无动画和大图，肉眼无差异。 |
| Semi 主题 → 内置 Fluent 主题 + 去 Inter | 0.95 s | 211 MB | 省 70 MB、快 15%；代价是重做视觉（13 个 Semi 资源键 + `clearButton`、`SolidButtonTheme`、`BorderlessButtonTheme` 需替换）。 |
| 运行时旋钮（`TieredPGO=0`、`gcConcurrent=0`、`GCConserveMemory`） | -3% ~ -5% | 无变化 | 不值得。 |

### 3.4 优化项（按收益排序）

| # | 改动 | 预期收益 | 代价 |
| :-- | :-- | :-- | :-- |
| P1 | 发布加 `PublishReadyToRun=true`，保留裁剪 + 压缩 | 启动 1.17 s → 0.53 s；体积 51 → 37.5 MB | 无 |
| P2 | 默认软件渲染 | 工作集 -100 MB；不再依赖显卡驱动 | 无（设置页可留一个"硬件加速"开关以防万一） |
| P3 | 去掉 `FluentIcons.Avalonia`，25 个图标改为 `Icons.axaml` 内的 `StreamGeometry` + 一个 `PathIcon` | 包体积 -5.6 MB（4.9 MB 资源 dll + 0.7 MB）；少加载一个大 dll | 一次性从 Fluent UI System Icons 复制 25 条路径数据 |
| P4 | 删除未使用的 `Avalonia.Themes.Fluent`、`Microsoft.Extensions.DependencyInjection`；去掉 `Avalonia.Fonts.Inter` | 包体积 -3 MB | 无 |
| P5 | `System.Text.Json` 改用源生成（`JsonSerializerContext`），覆盖 `QpdfJob`、`AppSettings`、GitHub API 模型 | 裁剪安全；去掉反射序列化的首次调用开销；NativeAOT 前置条件 | 约 30 行 |
| P6 | `SettingsStore` 不再每次启动写 `.write_test` 探测文件；只在首次保存时决定位置 | 少一次磁盘写 | 无 |
| P7 | 启动时 `qpdf --version` 探测延后到主窗口 `Opened` 之后，用 `Dispatcher.UIThread.Post(..., Background)` | 进程启动不与窗口首帧竞争 | 无 |
| P8 | 只针对 win-x64 发布时把 `Avalonia.Desktop` 换成 `Avalonia.Win32`（csproj 按 RID 条件） | 少打包 X11 / FreeDesktop / AtSpi 约 1.5 MB | 跨平台构建仍走 `Avalonia.Desktop` |
| P9 | Semi → Fluent（可选） | 工作集 -70 MB，启动 -15%，包 -1.7 MB | 视觉重做，且 Semi 的设计语言是当前界面的主要观感来源。**建议先做 P1–P8，再决定** |
| P10 | NativeAOT（可选，见 3.5） | 启动可能降到 0.2–0.3 s | 需要 C++ 工具链；本机未能实测 |

预计 P1–P8 完成后：独立单文件约 **30 MB**，热启动约 **0.5 s**，工作集约 **170 MB**。

### 3.5 NativeAOT 试验

本机没有安装 Visual Studio 的 C++ 桌面开发工作负载，`PublishAot=true` 在链接阶段失败（`Platform linker not found`），**没有拿到运行数据**。但 ILCompiler 的静态分析已经跑完，AOT 阻塞点只有两类：

- `System.Text.Json` 反射序列化：`QpdfJob.ToJson`、`SettingsStore` 读写、`UpdateService` / `QpdfDownloaderService` 的 `GetFromJsonAsync` 共 5 处（P5 一并解决）。
- `LocalizationManager` 用 `AvaloniaXamlLoader.Load(uri)` 动态加载语言字典（IL2026）。改为编译期引用两个字典（`App.axaml` 里 `<ResourceInclude>` 或在代码里 `new Strings_zh_CN()`）即可消除。

Avalonia、Semi、CommunityToolkit.Mvvm、CliWrap 均未报 AOT 警告。做完 P5 与上述本地化改动后，在装有 C++ 工具链的机器（或 CI 的 `windows-latest`）上再试一次；预期 exe 约 20–25 MB，启动 0.2–0.3 s，工作集低于 JIT 版。若 Semi 运行时出问题则放弃，不作为必达项。

### 3.6 发布产物注意

`dotnet publish` 输出目录里会带 `libSkiaSharp.pdb`（80 MB）和 `libHarfBuzzSharp.pdb`（20 MB）。现有 `release.yml` 已在打包前删除 `*.pdb`，保留该步骤；本地手工发布时注意别把它们发出去。

---

## 4. 实施顺序

每步一个 PR，完成后 `dotnet build`（`TreatWarningsAsErrors`）与 `dotnet test` 全绿。

1. **安全网**：R1（SkippableFact + CI 拉引擎）、补三个固件、`ci.yml`、`TreatWarningsAsErrors`。
2. **发布优化**：P1、P4、P5、P6、P7。这一步不改功能代码结构，收益最大、风险最小。
3. **Core 精简**：2.1 全部；R2、R6、R9。
4. **App 精简**：`ToolViewModel` + 三个控件 + 六页重写；R3、R4、R5、R7、R8。
5. **渲染与图标**：P2、P3、P8。
6. **可选评估**：P9、P10。

---

## 5. 决策记录

| 决策 | 选择 | 理由 |
| :-- | :-- | :-- |
| 架构复杂度 | 精简优先，撤回上一版大部分抽象 | 六页 CLI 包装器，代码行数比抽象层次更重要。 |
| 等效命令 | 从 `QpdfJob` 派生 | 用最小改动消除六份手写字符串的分叉。 |
| DI 容器 | 删除 | 八个单例手工构造更直接，少一个包。 |
| 本地化 | 保留静态 `LocalizationManager` | 换方案收益低；用 CI grep 守护即可。 |
| 发布配置 | 独立 + 裁剪 + 压缩 + ReadyToRun | 37.5 MB / 0.53 s 是实测最佳平衡点。 |
| 渲染 | 软件渲染 | 实测省 100 MB，本应用无 GPU 需求。 |
| 图标 | 内联路径 | 25 个图标不值得 5 MB 字体资源。 |
| `InvariantGlobalization` | 不启用 | Semi 主题构造 `CultureInfo("zh-CN")` 会崩。 |
| Semi 主题 | 暂留 | 观感主要来自 Semi；先做无风险项，再看是否值得换。 |

---

## 6. 暂不做

- 任务队列 / 历史、目录批处理、页面缩略图、更细的加密权限、macOS / Linux 打包验证、下载哈希校验。
- 窗口尺寸记忆、快捷键：可做，但放在精简完成之后。
