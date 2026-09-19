# QPDF GUI

基于 .NET 10 与 Avalonia 12 构建的现代化跨平台 QPDF 图形化界面客户端。通过可视化操作调用 QPDF 引擎，提供 PDF 文档的合并、拆分、加密、解密、旋转与结构修复能力。

## 下载与使用

前往 [Releases 页面](https://github.com/czefan/qpdf-gui/releases) 下载最新压缩包，解压后双击 `QpdfGui.exe` 即可使用：

| 版本 | 说明 |
| :--- | :--- |
| 🚀 **独立免装版 (推荐)** | 开箱即用，免装任何依赖（适合大多数用户） |
| ⚡ **框架依赖版** | 体积极小（约 19MB），需电脑已安装 [.NET 10 运行时](https://dotnet.microsoft.com/download/dotnet/10.0) |

---

## 主要功能

- **合并 (Merge)**：支持多文件拖拽重排、单文件指定抽取页码，并提供高级交叉编排语法（如 `1.1-11, 2.1, 1.8-55`）。
- **拆分 (Split)**：支持按自定义页码范围批量切分，或按固定页数连续均匀切分；支持基于基准范围局部切分。
- **加密与权限 (Encrypt)**：基于 AES-256 标准，支持独立设置打开密码与管理密码，精细化配置打印、复制、修改及批注权限。
- **解密 (Decrypt)**：解除文档打开密码或移除打印、复制等权限限制。
- **旋转 (Rotate)**：支持全本或指定页码范围进行顺时针 90°、逆时针 90° 或 180° 翻转。
- **修复与优化 (Repair & Linearize)**：重建损坏的 PDF 对象流与交叉引用表，支持生成适合 Web 快速首屏加载的线性化（Linearized）文档。
- **辅助特性**：
  - 处理完成后支持一键「打开文件」、「定位所在目录」或「复制底层 QPDF 执行命令」。
  - 内置深浅色彩主题、界面缩放调节（100% ~ 150%）及中英双语国际化支持。
  - 自动探查 QPDF 引擎状态，支持检查软件及引擎最新版本。

---

## 技术架构

采用 Clean Architecture 分层设计，核心业务（Core）与桌面界面（App）彻底解耦：

- **运行平台**：.NET 10.0 SDK
- **界面框架**：Avalonia UI 12.1（跨平台 XAML 界面系统）
- **设计主题**：Semi.Avalonia 12.1（现代化设计语言与控件主题）
- **矢量图标**：FluentIcons.Avalonia 2.1
- **架构模式**：CommunityToolkit.Mvvm 8.4（MVVM 源码生成器）+ Microsoft.Extensions.DependencyInjection（依赖注入）
- **进程调度**：CliWrap 3.10（异步 CLI 进程调度、实时输出流解析与取消控制）
- **测试框架**：xUnit 2.9（核心业务模型、规则解析与路径冲突测试）

---

## 项目结构

```text
qpdf-gui/
├── Directory.Build.props      # 统一 TargetFramework、Nullable 及 C# 语言版本
├── Directory.Packages.props   # NuGet 中央包版本管理 (CPM)
├── QpdfGui.slnx               # 解决方案
├── runtimes/                  # 本地运行引擎目录（按 RID 存放 native 二进制）
├── src/
│   ├── QpdfGui.Core/          # 核心类库（无 UI 依赖）：Job 模型、CLI 封装、页面表达式解析
│   └── QpdfGui.App/           # 桌面客户端：MVVM、Views、国际化资源、主题样式
├── tests/
│   └── QpdfGui.Core.Tests/    # 单元测试工程（表达式解析、输出命名冲突、序列化测试）
└── tools/
    ├── fetch-qpdf.ps1         # 自动下载并放置对应平台 QPDF 原生二进制
    └── scratch_capture.ps1    # 自动化界面截图调试脚本
```

---

## 本地开发

### 环境依赖

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) 或更高版本
- [QPDF](https://github.com/qpdf/qpdf) 引擎（版本 ≥ 11.0）

### 1. 准备 QPDF 运行环境

项目运行时会按以下顺序自动探查 QPDF 引擎：

1. 本地目录：`runtimes/<RID>/native/qpdf[.exe]`
2. 设置界面中用户自定义指定的绝对路径
3. 系统环境变量 `PATH`

在 Windows 平台下，可直接运行内置脚本拉取官方预编译二进制：

```powershell
pwsh tools/fetch-qpdf.ps1
```

### 2. 运行与测试

```bash
# 启动桌面客户端（自动增量编译并启动界面）
dotnet run --project src/QpdfGui.App

# 运行单元测试
dotnet test
```

### 3. CI/CD 发布

推送 Git 标签（如 `v1.0.0`）即可由 GitHub Actions 全自动测试并构建上述两种 Release 发布包。

---

## 鸣谢与参考

- [QPDF](https://github.com/qpdf/qpdf) - 强大的结构感知 PDF 变换引擎
- [Avalonia UI](https://github.com/AvaloniaUI/Avalonia) - 跨平台 XAML 界面生态
