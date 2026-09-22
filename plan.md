# QPDF GUI 全面重构方案

> 基线：`main` @ `752b78d`，.NET 10 / Avalonia 12.1.2 / Semi.Avalonia / CommunityToolkit.Mvvm / CliWrap。
> 当前状态：`dotnet build` 0 警告 0 错误，`dotnet test` 34/34 通过。**但通过的测试没有覆盖真实 qpdf 调用，下面列出的功能性缺陷全部处于"测试绿灯、功能红灯"状态。**
> 本地引擎：qpdf 12.4.1（`runtimes/win-x64/native`），以下所有 qpdf 行为均在该版本实测确认。

---

## 0. 现状评估

### 0.1 已验证的功能缺陷（必须修）

| # | 缺陷 | 证据 | 影响 |
| :-- | :-- | :-- | :-- |
| B1 | **合并、提取、按范围拆分全部失败** | `QpdfService.MergeAsync/ExtractAsync/SplitByRangesAsync` 写入 `"inputFile": "--empty"`。qpdf 实测报错 `open --empty: No such file or directory`，退出码 2。Job JSON 的正确写法是 `"empty": ""`。 | 六大功能中三个不可用。旧 plan 第 2 节的映射表本身就是错的。 |
| B2 | **旋转角度无法切换** | `RotateView.axaml` 用 `ObjectConverters.Equal` 双向绑定 `RadioButton.IsChecked`（默认 TwoWay）。实测该转换器 `ConvertBack` 抛 `NotImplementedException`，Avalonia 记为绑定错误，源属性不更新。 | 旋转永远是 +90。 |
| B3 | **只设打开密码时加密失败** | qpdf 对"用户密码非空 + 所有者密码为空 + 256 位"直接拒绝（退出码 2，要求 `--allow-insecure`）。`EncryptViewModel` 不处理；等效命令预览却偷偷把 owner 显示成 `"owner"`，与实际 Job 不一致。 | 最常见的加密用法报错，且错误信息对用户不可理解。 |
| B4 | **警告信息不展示** | 退出码 3（成功但有警告）时 `RunProcessTaskAsync` 走成功分支，`Warnings` 列表被丢弃。 | 修复页的核心价值（告诉用户修了什么）不存在；解密/合并遇到损坏文件时用户毫无感知。 |
| B5 | 深色模式下反馈面板错乱 | 成功/错误面板、顶部横幅使用硬编码浅色十六进制（`#FFF1F0`、`#CF1322`、`#FFFBE6` 等）。 | 深色主题下出现浅粉色块。 |
| B6 | 缺失资源 | 五个视图引用 `DropZoneBackgroundBrush`，`App.axaml` 中不存在。 | 密码提示区无背景，静默降级。 |
| B7 | 集成测试假绿 | `QpdfServiceIntegrationTests` 找不到样例 PDF（依赖未入库的 `scratch/` 目录）时直接 `return`。 | B1 长期未被发现。 |

### 0.2 结构性问题（重构动机）

- **重复代码**：`MergeViewModel` 与 `SingleFileToolViewModel` 各自复制了输出路径三向同步（~50 行）、执行管线、打开文件/目录、复制命令（~150 行）。五个单文件视图各复制 ~120 行完全相同的 XAML（输入卡片、输出卡片、执行区、结果面板）。
- **等效命令手写六份**：每个 ViewModel 用字符串拼接自己的 CLI，与真正执行的 Job JSON 没有共享来源，已经出现不一致（B3）。
- **Core 层污染**：27 处中文硬编码，`OutputPathResolver` 在"解析"时创建目录，`PdfInspector` 每次探查启动 4 个进程，`QpdfLocator` 注释说三级查找实际五级且顺序与文档不符。
- **状态传播混乱**：`MainWindowViewModel` 用三种机制（`Action` 回调、`PropertyChanged` 转发、静态事件）从 `SettingsViewModel` 拿引擎状态和缩放；`SettingsViewModel` 构造函数里 fire-and-forget 异步探测。
- **DI 名不副实**：工具页注册为 Transient，却被 Singleton 的 `MainWindowViewModel` 持有；`QpdfService` 用 `Func<string?>` 闭包读取设置。
- **测试屏障**：`LocalizationManager` 静态且依赖 `Application.Current`，`Process.Start("explorer.exe")` 直接写在 VM 里，ViewModel 无法单测。
- **启动代码混入测试工装**：`App.axaml.cs` 一半是截图参数解析。
- **无用依赖**：`Avalonia.Themes.Fluent`（未在 `App.axaml` 使用）、Core 项目引用 `Microsoft.Extensions.DependencyInjection`（未使用）。
- **拆分页语义混乱**：`BaseRange` 输入框始终显示，但只在"固定页数"模式生效；"按范围"模式下单个范围和多个范围走两条不同路径，用户看不出区别；按范围拆分的输出忽略用户填写的文件名。
- **合并页两套入口**：列表内每行的"提取页码"与"高级编排规则"表达式描述的是同一件事，规则一旦填写列表里的范围就被忽略，且没有提示。

### 0.3 值得保留的部分

- 技术选型正确：Job JSON 调用方式、CliWrap、CTK.Mvvm 源生成、Semi 主题、CPM 集中版本管理。
- `PageRange`、`MergeRuleParser`、`OutputPathResolver` 的纯逻辑测试思路。
- 引擎三来源查找 + 一键下载 + 便携优先配置的产品思路。
- 视觉方向（左侧窄导航 + 卡片式内容 + 顶部引导条）不需要推翻，只需要系统化。

---

## 1. 目标与非目标

**目标**

1. 六大功能全部真实可用，并有跑真 qpdf 的自动化测试守护。
2. 一个操作只描述一次：ViewModel 构造 `PdfOperation` → 生成 `QpdfJob` → 同时派生执行参数、等效命令、预期输出。
3. 工具页由可复用组件拼装，新增一个功能页的成本控制在 "一个参数 VM + 一个参数 View" 以内。
4. ViewModel 100% 可单测；关键界面有 headless 冒烟测试。
5. 深浅色主题、双语无遗漏。

**非目标（本轮不做，见第 8 节）**

- 页面缩略图预览、任务队列/历史、批量目录处理、macOS/Linux 打包验证。

---

## 2. 目标架构

### 2.1 分层

```text
QpdfGui.Core      纯 .NET，无 Avalonia，无中文，无静态全局状态
   ├─ Engine/     IQpdfEngine：引擎定位、版本校验、状态缓存与变更事件
   ├─ Jobs/       QpdfJob 及子模型（与 qpdf job schema 一一对应）
   ├─ Operations/ PdfOperation 及六种具体操作（领域模型 → Job 的唯一映射点）
   ├─ Execution/  QpdfRunner、QpdfResult、进度/警告解析、临时文件
   ├─ Inspection/ PdfInspector、PdfInfo（页数、加密、权限）
   ├─ Paths/      OutputPathResolver（纯函数）、PageRange
   └─ Errors/     ErrorCode 枚举 + ValidationException（App 负责翻译）

QpdfGui.App       Avalonia 壳
   ├─ Services/   ILocalizer、IShellService、ISettingsStore、IDialogService、IEngineInstaller、IUpdateChecker
   ├─ ViewModels/ Shell/、Components/、Tools/、Settings/
   ├─ Views/      与 VM 镜像 + Controls/（可复用控件）
   ├─ Themes/     Brushes.axaml（语义色）、Controls.axaml（样式）
   ├─ Resources/  Strings.*.axaml + 生成的 StringKeys 常量
   └─ Diagnostics/ ScreenshotHarness（仅 DEBUG）
```

依赖方向：`App → Core`，`Core` 不反向依赖，也不依赖 DI 容器。

### 2.2 Core 设计

**Job 模型修正**

```csharp
public sealed class QpdfJob
{
    [JsonPropertyName("inputFile")]    public string? InputFile { get; init; }
    [JsonPropertyName("empty")]        public string? Empty { get; init; }        // 值为 "" 表示启用，替代错误的 "--empty"
    [JsonPropertyName("outputFile")]   public string? OutputFile { get; init; }
    [JsonPropertyName("password")]     public string? Password { get; init; }
    [JsonPropertyName("pages")]        public IReadOnlyList<PagesSpec>? Pages { get; init; }
    [JsonPropertyName("splitPages")]   public string? SplitPages { get; init; }
    [JsonPropertyName("rotate")]       public IReadOnlyList<string>? Rotate { get; init; }
    [JsonPropertyName("decrypt")]      public string? Decrypt { get; init; }
    [JsonPropertyName("encrypt")]      public EncryptOptions? Encrypt { get; init; }
    [JsonPropertyName("linearize")]    public string? Linearize { get; init; }
    [JsonPropertyName("objectStreams")]public string? ObjectStreams { get; init; }
    [JsonPropertyName("progress")]     public string? Progress { get; init; }
}
```

统一在 `JsonSerializerOptions` 上设置 `WhenWritingNull`，删除每个属性上重复的 `[JsonIgnore]`。`EncryptOptions.Aes256.AllowInsecure` 保留（schema 中位于 `256bit` 下）。

**操作模型（单一真源）**

```csharp
public sealed record SourceDocument(string Path, string? Password);
public sealed record PageSegment(SourceDocument Source, string Range);

public abstract record PdfOperation
{
    public abstract OperationKind Kind { get; }
    public abstract IReadOnlyList<QpdfJob> ToJobs();          // 一个操作可能是多个 job（按范围拆分）
    public abstract IReadOnlyList<string> ExpectedOutputs();  // 结果面板、冲突检查、取消时清理
}

public sealed record MergeOperation(IReadOnlyList<PageSegment> Segments, string OutputPath) : PdfOperation;
public sealed record ExtractOperation(PageSegment Segment, string OutputPath) : PdfOperation;
public sealed record SplitEveryNOperation(SourceDocument Source, int PagesPerFile, string? Range, string OutputPattern) : PdfOperation;
public sealed record SplitByRangesOperation(SourceDocument Source, IReadOnlyList<string> Ranges, string OutputDirectory, string BaseName) : PdfOperation;
public sealed record EncryptOperation(SourceDocument Source, EncryptionSettings Settings, string OutputPath) : PdfOperation;
public sealed record DecryptOperation(SourceDocument Source, string OutputPath) : PdfOperation;
public sealed record RotateOperation(SourceDocument Source, RotationAngle Angle, string Range, string OutputPath) : PdfOperation;
public sealed record RepairOperation(SourceDocument Source, bool Linearize, bool RegenerateObjectStreams, string OutputPath) : PdfOperation;

public static class QpdfCommandLine
{
    public static string Format(QpdfJob job);   // 由 Job 派生 CLI，六份手写字符串全部删除
}
```

`EncryptionSettings` 内置规则：owner 为空时自动取 user 密码；两者都为空则 `ToJobs()` 抛 `ValidationException(ErrorCode.EncryptNeedsPassword)`。

**执行器**

```csharp
public enum Outcome { Succeeded, SucceededWithWarnings, Failed, Cancelled }

public sealed record QpdfResult(
    Outcome Outcome, int ExitCode, TimeSpan Duration,
    IReadOnlyList<string> Warnings,      // stderr 中 "WARNING: " 前缀行，去掉路径前缀
    string? ErrorText,                   // 退出码 2 时 stderr 末行
    IReadOnlyList<string> Outputs);      // 实际产生的文件（splitPages 结束后按模式 glob）

public interface IQpdfRunner
{
    Task<QpdfResult> RunAsync(PdfOperation op, IProgress<OperationProgress>? progress, CancellationToken ct);
}
```

- 进度：stdout 行 `qpdf: <out>: write progress: N%`。`splitPages` 会对每个输出文件从 0% 重新计数，运行器检测百分比回落即视为下一个文件，按 `ceil(页数 / N)` 折算总进度。
- 取消：CliWrap 负责杀进程；运行器随后删除 `ExpectedOutputs()` 中已产生的半成品。
- 退出码映射：0 → Succeeded；3 → SucceededWithWarnings；其他 → Failed。不使用 `--warning-exit-0`。
- 临时 Job 文件写入 `%TEMP%`，UTF-8 无 BOM，用后即删。密码会随 JSON 落盘，属已知取舍（与 `--password-file` 等价），在设置页"关于"里说明。

**探查器**：由 4 次进程调用降为 2 次。

1. `qpdf --json --json-key=encrypt [--password=..] <file>`：一次拿到 `encrypted`、`userpasswordmatched`、`ownerpasswordmatched`、`capabilities`（打印/复制/修改/批注等权限）。退出码 2 且 stderr 含 `invalid password` → `RequiresPassword = true`。
2. `qpdf --show-npages [--password=..] <file>`：页数。

`PdfInfo` 新增 `Permissions`（供解密页展示"将解除哪些限制"，供加密页预填当前权限）。

**引擎**

```csharp
public sealed record EngineStatus(string? Path, Version? Version, EngineState State, string? Detail);
public enum EngineState { NotFound, Invalid, TooOld, Ready }

public interface IQpdfEngine
{
    EngineStatus Status { get; }
    Task<EngineStatus> RefreshAsync(string? customPath, CancellationToken ct);
    event EventHandler<EngineStatus>? StatusChanged;
}
```

查找顺序固定为：用户指定路径 → `<app>/runtimes/<rid>/native` → `%LocalAppData%/QpdfGui/runtimes/<rid>/native` → `PATH`。版本下限 11.0（Job JSON 引入版本）。`QpdfRunner`、`PdfInspector` 通过构造注入 `IQpdfEngine`，不再各自调用静态 `Locate`。

**错误与本地化边界**：Core 只抛/返回 `ErrorCode`（枚举）+ 参数，App 用 `Err_<Code>` 资源键翻译。Core 内不允许出现任何自然语言文案。

**路径解析**：`OutputPathResolver` 变为纯函数，接收 `Func<string, bool> exists` 便于测试；目录创建移到运行器执行前。

### 2.3 App 设计

**可复用组件（VM + 控件成对）**

| 组件 | ViewModel | 控件 | 职责 |
| :-- | :-- | :-- | :-- |
| 单文档选择 | `DocumentPickerViewModel` | `DocumentPicker` | 拖放/点击选择、文件摘要（名称、大小、页数、加密徽章）、密码输入与重试、探查失败提示 |
| 多文档列表 | `DocumentListViewModel` | `DocumentList` | 有序列表、每行页码范围、上移/下移/移除/再次添加、拖入追加 |
| 输出位置 | `OutputLocationViewModel` | `OutputLocationEditor` | 三种模式：单文件（目录+文件名）、模式串（含 `%d`）、目录+基名；冲突自动编号；跟随全局默认目录 |
| 执行与结果 | `OperationRunnerViewModel` | `OperationResultPanel` | 执行/取消、进度、成功/警告/失败三态面板、警告列表可展开、打开文件/定位目录/复制命令 |
| 页面头 | 无 | `PageHeader` | 标题 + 说明 |

**工具页基类**

```csharp
public abstract partial class ToolPageViewModel : ObservableObject
{
    public OutputLocationViewModel Output { get; }
    public OperationRunnerViewModel Runner { get; }
    public string? EquivalentCommand { get; }          // 由 TryBuildOperation 派生
    public string? BlockingReason { get; }             // 为什么现在不能执行（资源键）

    protected abstract bool TryBuildOperation(out PdfOperation op, out ErrorCode? error);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task RunAsync() => Runner.RunAsync(BuildOperationOrThrow());
}
```

执行按钮的可用性由 `TryBuildOperation` 成功 + 引擎就绪 + 非忙碌共同决定，不可用时按钮禁用并在旁显示 `BlockingReason`（取代现在的"点了没反应"）。

**具体工具页**

- `RepairViewModel`：`DocumentPicker` + 线性化开关 + 对象流重建开关。结果面板重点展示警告条数与明细（"已修复 N 处问题"）。
- `DecryptViewModel`：`DocumentPicker`。文件未加密时 `BlockingReason = Err_NotEncrypted`；展示 `Permissions` 说明将解除的限制。
- `RotateViewModel`：角度改为枚举 `RotationAngle { Cw90, Ccw90, Rotate180 }`，用 `EnumToBoolConverter`（实现 `ConvertBack`）或三个独立 bool 属性绑定。页码范围实时校验。
- `EncryptViewModel`：打开密码、管理密码、四项权限。owner 为空自动等于 user（界面提示）；两者皆空禁用执行。
- `SplitViewModel`：三种互斥模式明确呈现：① 每 N 页一个文件（可选作用范围）② 按多个范围拆成多个文件 ③ 提取单个范围为一个文件。输出组件按模式切换到模式串/目录+基名/单文件。删除始终可见但只半生效的 `BaseRange`。
- `MergeViewModel`：只有一个有序"片段列表"（文件 + 范围，同一文件可出现多次），这就是 `MergeOperation.Segments`。原"高级编排规则"表达式降级为"从表达式导入"按钮：解析成功后生成片段行，之后编辑的是行而不是表达式，消除两套入口互相覆盖的问题。`MergeRuleParser` 保留并继续测试。

**状态传播**：引入 CTK `WeakReferenceMessenger`，消息类型：`EngineStatusChanged`、`LanguageChanged`、`UiScaleChanged`、`DefaultOutputDirectoryChanged`。删除 `SettingsViewModel` 上的 `Action` 回调和 `MainWindowViewModel` 中的 `PropertyChanged` 转发。

**设置页拆分**：`SettingsViewModel` 仅做分区路由；`EngineSettingsViewModel`（状态、指定路径、一键下载、检查引擎更新）同时被顶部横幅 `EngineBannerViewModel` 复用；`AppearanceSettingsViewModel`（语言、主题、缩放）；`GeneralSettingsViewModel`（默认输出目录）；`AboutViewModel`（版本、检查更新、主页）。

**本地化**

- 新增 `ILocalizer`（`string this[string key]`、`Format(key, args)`、`CurrentCulture`、`Changed` 事件），Avalonia 实现基于现有资源字典；测试用直通实现。
- 用 MSBuild 内联任务或 T4 从 `Strings.zh-CN.axaml` 生成 `StringKeys` 常量类，XAML 与 C# 都引用常量，键名拼错编译期报错。
- 提供 `{loc:Loc Key}` 标记扩展替代 `{DynamicResource}`，语义清晰且支持格式化参数。
- 状态类文案在 VM 里保存资源键 + 参数，收到 `LanguageChanged` 后重新渲染。
- 清空 Core 27 处、Views 16 处硬编码中文；`DialogService` 中的"PDF 文档/所有文件"也走资源。

**主题**

- `Themes/Brushes.axaml` 定义语义色：`SurfaceBrush`、`CardBrush`、`CardBorderBrush`、`Success/Warning/Danger` 的 `Foreground/Background/Border` 各一套，Light/Dark 两份字典。优先直接引用 Semi 已有令牌（`SemiColorSuccess`、`SemiColorWarning`、`SemiColorDanger`、`SemiColorFill0-2`）。
- 视图中禁止出现十六进制颜色（用 analyzer 或 CI grep 守护）。
- 缩放仍用 `LayoutTransformControl`，但记录已知限制：弹出层（下拉、Tooltip）不随缩放。

**平台操作**：`IShellService { OpenFile(path); RevealInFolder(path); OpenUrl(url); }`，Windows 用 `explorer /select,`，macOS 用 `open -R`，Linux 用 `xdg-open` 目录。

**拖放**：主窗口继续做全局接收，但分发给当前页的组件（`DocumentPicker` / `DocumentList`），拖入时组件高亮（`:dragover` 伪类或 `Classes.dragover`）。单文件页拖入多个文件取第一个并提示。

**窗口状态**：记忆尺寸、位置、最大化状态到设置。

**启动**：`App.axaml.cs` 只剩 DI 注册与主窗口创建；截图工装移入 `Diagnostics/ScreenshotHarness.cs`，`#if DEBUG` 编译，参数解析也在其中。

### 2.4 功能 → qpdf 映射（实测修正版）

| 功能 | Job JSON 关键字段 | 实测备注 |
| :-- | :-- | :-- |
| 合并 / 提取 | `"empty": ""`, `"pages": [{file, range, password?}]` | 不能用 `inputFile: "--empty"`。 |
| 每 N 页拆分 | `"inputFile"`, `"splitPages": "N"`, `"outputFile": "..._%d.pdf"` | 输出名为 `_1-2.pdf`、`_3-3.pdf` 形式（页码区间，不是序号）；每个文件进度从 0% 重计。带作用范围时改为 `"empty"` + `pages` + `splitPages`。 |
| 按范围拆分 | N 个 提取 job 顺序执行 | 单次 qpdf 无法输出多个不同范围文件。 |
| 解密 | `"decrypt": ""`, `"password"` | 仅权限限制无打开密码时 `password` 可省略。 |
| 加密 | `"encrypt": {"userPassword","ownerPassword","256bit":{print,modify,extract,annotate}}` | user 非空 + owner 空 → 退出码 2；两者皆空 → 成功但产物无意义，须在 UI 拦截。 |
| 旋转 | `"rotate": ["+90:1-z"]` | 数组，可多条。 |
| 修复 | `"objectStreams": "generate"`, 可选 `"linearize": ""` | 退出码 3 + stderr `WARNING:` 行即修复明细；空文档线性化会失败（退出码 2），由页数校验拦截。 |
| 探查 | `--json --json-key=encrypt`；`--show-npages` | 需要密码而未提供时退出码 2、stderr `invalid password`。 |
| 进度 | `"progress": ""` | stdout：`qpdf: <out>: write progress: N%`。 |

### 2.5 目标目录结构

```text
src/QpdfGui.Core/
  Engine/        IQpdfEngine.cs  QpdfEngine.cs  EngineStatus.cs
  Jobs/          QpdfJob.cs  PagesSpec.cs  EncryptOptions.cs
  Operations/    PdfOperation.cs  MergeOperation.cs ... RepairOperation.cs  QpdfCommandLine.cs
  Execution/     IQpdfRunner.cs  QpdfRunner.cs  QpdfResult.cs  ProgressParser.cs  StderrParser.cs  TempJobFile.cs
  Inspection/    IPdfInspector.cs  PdfInspector.cs  PdfInfo.cs  PdfPermissions.cs
  Paths/         OutputPathResolver.cs  PageRange.cs  MergeRuleParser.cs
  Errors/        ErrorCode.cs  ValidationException.cs

src/QpdfGui.App/
  App.axaml(.cs)  Program.cs  ServiceCollectionExtensions.cs
  Diagnostics/   ScreenshotHarness.cs
  Services/      ILocalizer.cs AvaloniaLocalizer.cs  IShellService.cs ShellService.cs
                 IDialogService.cs DialogService.cs  ISettingsStore.cs JsonSettingsStore.cs
                 IEngineInstaller.cs GitHubEngineInstaller.cs  IUpdateChecker.cs GitHubUpdateChecker.cs
  Messages/      EngineStatusChanged.cs LanguageChanged.cs UiScaleChanged.cs DefaultOutputDirectoryChanged.cs
  ViewModels/
    Shell/       MainWindowViewModel.cs NavigationItem.cs EngineBannerViewModel.cs
    Components/  DocumentPickerViewModel.cs DocumentListViewModel.cs DocumentItemViewModel.cs
                 OutputLocationViewModel.cs OperationRunnerViewModel.cs
    Tools/       ToolPageViewModel.cs MergeViewModel.cs SplitViewModel.cs EncryptViewModel.cs
                 DecryptViewModel.cs RotateViewModel.cs RepairViewModel.cs
    Settings/    SettingsViewModel.cs GeneralSettingsViewModel.cs AppearanceSettingsViewModel.cs
                 EngineSettingsViewModel.cs AboutViewModel.cs
  Views/
    MainWindow.axaml  Tools/*.axaml  Settings/*.axaml
    Controls/    DocumentPicker.axaml DocumentList.axaml OutputLocationEditor.axaml
                 OperationResultPanel.axaml PageHeader.axaml EngineBanner.axaml
  Converters/    EnumToBoolConverter.cs  OutcomeToBrushConverter.cs
  Themes/        Brushes.axaml  Controls.axaml
  Resources/     Strings.zh-CN.axaml  Strings.en-US.axaml  StringKeys.g.cs（生成）

tests/
  QpdfGui.Core.Tests/       纯逻辑 + Job JSON 快照 + 命令行格式化 + 解析器
  QpdfGui.Core.IntegrationTests/  真 qpdf（找不到引擎则 Skip，不再静默通过）
  QpdfGui.App.Tests/        ViewModel 测试（Fake 服务）+ Avalonia.Headless 冒烟
  fixtures/                 three-pages.pdf  encrypted-user.pdf  encrypted-owner-only.pdf  damaged.pdf
```

---

## 3. 关键决策记录

| 决策 | 选择 | 备选与否决理由 |
| :-- | :-- | :-- |
| 技术栈 | 保持不变 | 换 WPF/WinUI 失去跨平台且无收益；问题在结构不在框架。 |
| 操作建模 | `PdfOperation` 记录类型 → `QpdfJob` | 备选"VM 直接拼 Job"是现状，导致命令预览与执行分叉。 |
| 多输出拆分 | 顺序多 job，运行器聚合进度 | qpdf 单次只能一个 `--pages` 输出，无捷径。 |
| 警告语义 | 退出码 3 = 成功并展示警告，不加 `--warning-exit-0` | 用户需要知道文件被修过。 |
| 加密空 owner | 自动用 user 密码填充 owner | 备选"要求用户必填"增加摩擦；备选 `allowInsecure` 产出可无密码打开的文件，与用户意图相悖。 |
| 合并入口 | 单一片段列表，表达式仅作导入 | 两套并行入口互相覆盖是现状缺陷根源。 |
| 状态通知 | `WeakReferenceMessenger` | 备选"事件聚合器自己写"与 CTK 已有能力重复；备选"共享单例可观察对象"耦合更紧。 |
| 本地化 | 资源字典 + 生成常量 + `ILocalizer` | 备选 `.resx`：改动大、丢失 XAML 热切换；生成常量是最小代价的编译期守护。 |
| 测试框架 | 升级到 xunit v3（支持 `Assert.Skip`） | 备选 `Xunit.SkippableFact` 也可，但 v3 是长期方向。 |
| 密码传递 | 继续走 Job JSON 临时文件 | `--password-file` 同样落盘，`--password=` 会暴露在进程列表中，更差。 |
| 缩放实现 | 保留 `LayoutTransformControl` | Avalonia 12 无更好的全局 DPI 覆盖方案；已知弹出层不缩放，文档化。 |

---

## 4. 实施阶段

每个阶段独立成 PR，结束时 `dotnet build`（`TreatWarningsAsErrors`）与 `dotnet test` 全绿，界面可运行。

### 阶段 0：安全网（先让 bug 变红）

1. 入库 `tests/fixtures/*.pdf`：三页正常文档、用户密码加密、仅 owner 加密、损坏文档（各 < 2 KB，用 qpdf 生成并手工校验）。
2. 新建 `QpdfGui.Core.IntegrationTests`，覆盖合并、提取、每 N 页拆分、按范围拆分、加密（含仅 user 密码）、解密、旋转、修复。找不到引擎时 `Assert.Skip`，找得到必须真跑。**此时合并/提取/拆分/加密用例应失败，作为 B1、B3 的基线证据。**
3. 新建 `QpdfGui.App.Tests`，用 `Avalonia.Headless.XUnit` 加载每个页面，断言无绑定错误日志。**旋转页与缺失画刷应在此暴露 B2、B6。**
4. 新增 `.github/workflows/ci.yml`：push/PR 触发 build + test。现有 `release.yml` 不动。
5. 移除无用包引用（`Avalonia.Themes.Fluent`、Core 的 DI 包），开启 `TreatWarningsAsErrors`、`AvaloniaUseCompiledBindingsByDefault`。

完成标准：CI 建立，阶段 0 新增测试中除已知失败项外全部通过，已知失败项在 PR 描述中逐条对应到 B1–B6。

### 阶段 1：Core 重写

1. `Jobs/`：`QpdfJob` 加 `Empty`，统一序列化选项，删除逐属性 `[JsonIgnore]`。
2. `Operations/`：八个操作记录 + `QpdfCommandLine.Format`。Job JSON 快照测试（每个操作一份期望 JSON）。
3. `Engine/`：`IQpdfEngine` + 实现，含版本下限校验与 `StatusChanged`。
4. `Execution/`：`IQpdfRunner`、`QpdfResult(Outcome, Warnings, Outputs)`、`ProgressParser`（含 splitPages 回落检测）、`StderrParser`、取消清理。解析器单测用实测捕获的 stdout/stderr 文本。
5. `Inspection/`：两次调用版探查器，解析 `encrypt` JSON 得到 `PdfPermissions`。用固定 JSON 样本做解析单测。
6. `Errors/`：`ErrorCode` + `ValidationException`；删除 Core 全部中文。
7. `Paths/`：`OutputPathResolver` 纯函数化；`PageRange` 增加对 `1-z:x3-5`（排除）语法的容忍。
8. 删除 `IQpdfService`/`QpdfService`（被 `IQpdfRunner.RunAsync(PdfOperation)` 取代）。

完成标准：阶段 0 集成测试全部转绿；Core 内 `grep` 不到中文字符。

### 阶段 2：App 基础设施

1. `ServiceCollectionExtensions`：注册 Core 服务、`ILocalizer`、`IShellService`、`ISettingsStore`、`IDialogService`、`IEngineInstaller`、`IUpdateChecker`、Messenger；工具页与设置页统一 Singleton（与实际生命周期一致）。
2. `ILocalizer` + `{loc:Loc}` 标记扩展 + `StringKeys` 生成；补齐全部缺失键。
3. `Themes/Brushes.axaml` 语义色；移除视图内所有十六进制颜色；补上 `DropZoneBackgroundBrush` 的替代语义色。
4. `IShellService` 三平台实现；`EnumToBoolConverter`。
5. `Diagnostics/ScreenshotHarness.cs`（DEBUG），`App.axaml.cs` 瘦身。
6. `JsonSettingsStore` 增加窗口状态字段；启动时不再每次写探测文件（只在首次决定存储位置时）。

完成标准：headless 冒烟测试零绑定错误；深色主题截图检查无浅色块。

### 阶段 3：组件与工具页

顺序：组件 → 修复 → 解密 → 旋转 → 加密 → 拆分 → 合并（由简到繁，每页完成后用 fixtures 手工跑一次真实流程并留截图）。

1. 四个组件 VM + 控件 + `ToolPageViewModel` 基类 + `OperationResultPanel` 三态（成功 / 成功含警告可展开 / 失败）。
2. 六个页面按 2.3 节规格重写；旧 `SingleFileToolViewModel`、`MergeViewModel` 中的重复代码全部删除。
3. 每个工具页 VM 的单测：参数 → `TryBuildOperation` 的正反用例、`BlockingReason`、执行后状态。

完成标准：六页全部通过集成 fixtures 的手工验证；`Views/` 下五个单文件页面 XAML 行数各 < 80。

### 阶段 4：设置、横幅与体验

1. 设置页拆为四个分区 VM；`EngineSettingsViewModel` 与 `EngineBannerViewModel` 共享。
2. 引擎下载：校验下载后 `--version` ≥ 11；失败给出可操作的错误（网络、权限、无匹配资产）。
3. 拖放视觉反馈；执行按钮禁用原因提示；键盘：`Ctrl+O` 选文件，`Ctrl+Enter` 执行，`Esc` 取消。
4. 窗口尺寸/位置记忆。
5. 深浅色 + 中英文四组合截图走查（用 `ScreenshotHarness`）。

### 阶段 5：收尾

1. README 与本文件同步到新结构；记录已知限制（弹出层不缩放、密码落盘临时文件）。
2. `release.yml` 增加对 CI 的依赖；发布 v0.2.0。
3. 删除 `tools/scratch_capture.ps1`（被 `ScreenshotHarness` 取代）。

---

## 5. 测试策略

| 层 | 工具 | 覆盖 |
| :-- | :-- | :-- |
| Core 纯逻辑 | xunit v3 | `PageRange`、`MergeRuleParser`、`OutputPathResolver`、`ProgressParser`、`StderrParser`、`PdfInspector` JSON 解析、`QpdfCommandLine`、每个 `PdfOperation.ToJobs()` 的 JSON 快照 |
| Core 集成 | xunit v3 + 真 qpdf + fixtures | 八种操作端到端，断言输出文件存在、页数正确、加密状态正确、警告列表非空（损坏文档） |
| App ViewModel | xunit v3 + Fake `IQpdfRunner/IDialogService/ILocalizer/IShellService` | 每个工具页的构建/阻断逻辑；`OperationRunnerViewModel` 三态转换与取消；`OutputLocationViewModel` 三模式同步；`DocumentPickerViewModel` 密码重试 |
| App UI | `Avalonia.Headless.XUnit` | 每个页面与控件可加载、无绑定错误；旋转角度点击后源属性变化 |
| 视觉 | `ScreenshotHarness` | 四组合截图，人工走查（不做像素比对） |

CI 中集成测试通过 `tools/fetch-qpdf.ps1` 拉取引擎后运行，不允许 Skip。

---

## 6. 工程化

- `Directory.Build.props`：`TreatWarningsAsErrors`、`EnforceCodeStyleInBuild`、`AnalysisLevel=latest-recommended`、`AvaloniaUseCompiledBindingsByDefault`。
- `.editorconfig`：统一命名与 `var` 规则。
- CI 增加一步 `grep` 守护：`src/QpdfGui.Core` 无中文、`src/QpdfGui.App/Views` 无 `#[0-9A-Fa-f]{6}`。
- 版本号来源统一为 git tag（现状已如此），`AboutViewModel` 读 `AssemblyInformationalVersion`。

---

## 7. 风险

| 风险 | 缓解 |
| :-- | :-- |
| xunit v3 与 `Microsoft.NET.Test.Sdk` / Avalonia.Headless 兼容性 | 阶段 0 第一天验证；不行则退回 xunit 2 + `Xunit.SkippableFact`。 |
| `splitPages` 进度回落检测误判 | 单测用实测 stdout；总进度只作展示，不影响结果判定。 |
| Semi 主题令牌在 12.x 改名 | 语义色集中在 `Brushes.axaml`，改一处。 |
| 合并页改为片段列表后老用户找不到表达式入口 | 保留"从表达式导入"按钮并在 README 说明。 |
| 重构期间 `main` 长期不可用 | 每阶段独立 PR，阶段 0 之后任何时刻 `main` 都可构建、可运行。 |

---

## 8. 暂不做（Backlog）

- 任务队列 / 历史记录面板。
- 目录批处理（同一操作应用到文件夹内全部 PDF）。
- 页面缩略图（需引入 PDFium，体积与授权另议）。
- 加密页高级权限（`form`、`assemble`、`modify-other`、`accessibility` 细分）。
- macOS / Linux 的引擎自动下载与打包验证（`IShellService` 已留接口）。
- 引擎下载的哈希校验（qpdf Release 提供 `.sha256`，可在阶段 4 顺手加）。
