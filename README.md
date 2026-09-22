# QPDF GUI

QPDF 的桌面图形界面客户端，基于 .NET 10 与 Avalonia 构建。提供 PDF 合并、拆分、加密、解密、旋转与修复功能。

## 下载

前往 [Releases 页面](https://github.com/czefan/qpdf-gui/releases) 下载对应压缩包，解压后运行 `QpdfGui.exe`：

| 版本 | 说明 |
| :--- | :--- |
| **独立免装版** | 内置运行时，解压即用 |
| **框架依赖版** | 体积较小，需先安装 [.NET 10 运行时](https://dotnet.microsoft.com/download/dotnet/10.0) |

---

## 主要功能

- **合并**：支持多文件拖拽排序、指定页码抽取，或通过规则表达式批量导入（如 `1.1-11, 2.1, 1.8-55`）。
- **拆分**：支持按固定页数切分、按自定义范围切分或单独提取特定页面。
- **加密**：支持设置打开密码与权限密码（AES-256），可控制打印、复制、修改等权限。
- **解密**：移除 PDF 打开密码及各项操作权限限制。
- **旋转**：支持全本或指定页码按 90°、180°、270° 旋转。
- **修复**：重建受损的 PDF 交叉引用表与对象流，支持输出 Web 优化的线性化文档。
- **其他**：执行完成后可打开文件、定位目录或复制等效命令行；支持深浅主题、界面缩放（75% ~ 175%）与中英双语切换。

---

## 技术栈

- **运行时**：[.NET 10](https://dotnet.microsoft.com/)
- **界面**：[Avalonia 12](https://github.com/AvaloniaUI/Avalonia) + [Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia) + [FluentIcons](https://github.com/davidxuang/FluentIcons)
- **MVVM**：[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- **进程调用**：[CliWrap](https://github.com/Tyrrrz/CliWrap)
- **测试**：[xUnit](https://github.com/xunit/xunit) + [Avalonia.Headless](https://docs.avaloniaui.net/docs/guides/testing/headless-testing)

---

## 项目结构

```text
qpdf-gui/
├── src/
│   ├── QpdfGui.Core/          # 核心库：Job 模型、进程调度、规则解析
│   └── QpdfGui.App/           # 桌面端：Views、ViewModels、资源与主题
├── tests/
│   ├── fixtures/              # 测试 PDF 文件
│   └── QpdfGui.Core.Tests/    # 单元测试、集成测试与界面冒烟测试
└── tools/
    ├── fetch-qpdf.ps1         # 下载当前平台 QPDF 二进制
    └── ci-guards.ps1          # 代码规范检查脚本
```

---

## 本地开发

### 环境要求

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [QPDF](https://github.com/qpdf/qpdf) (≥ 11.0)

### 准备 QPDF

引擎查找顺序：

1. 设置中的自定义路径
2. 本地目录 `runtimes/<RID>/native/`
3. `%LocalAppData%/QpdfGui/runtimes/<RID>/native/`
4. 系统环境变量 `PATH`

Windows 下可运行脚本自动拉取官方二进制：

```powershell
pwsh tools/fetch-qpdf.ps1
```

### 构建与测试

```bash
# 启动应用
dotnet run --project src/QpdfGui.App

# 运行全量测试
dotnet test

# 代码规范检查
pwsh tools/ci-guards.ps1
```

### 发布

推送版本标签自动触发 GitHub Actions 构建并发布 Release：

```bash
git tag v0.0.1
git push origin v0.0.1
```

---

## License

[MIT](LICENSE)
