# v2rayN Agent Guide

本文件是仓库级开发指南，适用于 `/Users/wenxig/Documents/v2rayNP` 下的修改。先阅读本文件，再根据任务范围阅读 `README.md`、对应的 `.csproj` 和相关源码。

## 项目概览

v2rayN 是支持 Windows、Linux 和 macOS 的桌面代理客户端，负责节点订阅、协议分享链接、路由规则、系统代理、Xray/sing-box 核心管理和流量统计。仓库基于 v2rayN 并维护自己的发布与更新地址。

技术基线：

- C# / .NET 10；版本和公共 MSBuild 设置在 `v2rayN/Directory.Build.props`。
- 包版本集中在 `v2rayN/Directory.Packages.props`，项目文件通常不写版本号。
- `ServiceLib.Tests` 使用 TUnit 和 Microsoft Testing Platform。
- 代码格式以根目录 `.editorconfig` 为准：4 空格、CRLF、文件末尾换行；YAML 使用 2 空格。
- `v2rayN/GlobalHotKeys` 是 Git submodule，首次构建需要初始化它。

## 目录与职责

- `v2rayN/ServiceLib/`：跨平台共享业务层。包含模型、协议格式转换、订阅/配置处理、核心配置生成、系统代理、下载、统计、数据库和 ViewModel。
- `v2rayN/v2rayN.Desktop/`：Avalonia 桌面宿主，面向 Linux/macOS/Windows；界面主要在 `Views/*.axaml`，入口是 `Program.cs`。
- `v2rayN/v2rayN/`：Windows WPF 宿主，面向 Windows；界面主要在 `Views/*.xaml`。
- `v2rayN/ServiceLib.Tests/`：共享业务和配置逻辑测试，按 `Fmt`、`CoreConfig`、`Handler`、`Manager`、`Services` 等主题组织。
- `v2rayN/ServiceLib.UdpTest/`：被 `ServiceLib` 引用的 UDP 测试支持代码，不是常规测试项目入口。
- `v2rayN/AmazTool/`：随发布产物构建的辅助工具，资源在 `AmazTool/Resx`。
- `v2rayN/GlobalHotKeys/`：外部 submodule，不要把其生成文件当作本仓库源码修改。
- `package-*.sh`：Linux/macOS 发行版打包脚本；`.github/workflows/`：CI 和发布自动化。

常见调用链是“宿主 View/ViewModel -> `ServiceLib` Manager/Handler/Service -> CoreConfig/Model”。共享行为优先修改 `ServiceLib`，只有平台 API 或 UI 差异才改对应宿主。修改配置格式时同时检查 V2ray、sing-box、Clash 及分享链接解析路径。

## 开发前检查

```bash
git status --short
git submodule sync --recursive
git submodule update --init --recursive
```

只修改与任务相关的文件。不要提交 `bin/`、`obj/`、`publish/`、本地 IDE 配置、用户配置数据库或包含凭据的文件。不要手工修改 `*.Designer.cs` 等由 `.resx` 生成的文件；应修改资源文件并让构建工具重新生成。

`v2rayN/ServiceLib/Global.cs`、配置模型、协议格式转换器、核心配置生成器和系统代理处理器会影响大量功能。改动这些位置时，先搜索所有调用方，再补充或调整对应测试；不要为了通过单个测试改变协议字段含义或默认值。

## 常用构建与测试

命令从仓库根目录执行。需要已安装 .NET 10 SDK；当前仓库的 `TargetFramework` 是 `net10.0`，Windows 项目还使用 `net10.0-windows10.0.19041.0`。CI 工作流中的 `setup-dotnet` 版本可能滞后于源码目标框架，不能把 CI 配置中的旧版本当成本地 SDK 要求。

```bash
# 还原整个解决方案
dotnet restore v2rayN/v2rayN.slnx

# 构建跨平台 Avalonia 宿主
dotnet build v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj -c Debug

# 构建 Windows WPF 宿主（在非 Windows 主机上需要额外的 Windows targeting 支持）
dotnet build v2rayN/v2rayN/v2rayN.csproj -c Debug -p:EnableWindowsTargeting=true

# 运行共享业务测试
dotnet test v2rayN/ServiceLib.Tests/ServiceLib.Tests.csproj -c Debug
```

涉及发布或运行时资源时，使用对应 CI 中的 `dotnet publish` 命令验证，而不是只依赖 `dotnet build`。典型 RID 包括 `win-x64`、`win-arm64`、`linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64`；riscv64 和 loong64 使用专门的 Debian/RHEL 脚本及对应 SDK。发布脚本会下载 Xray、sing-box 和规则数据，需要网络，且可能修改工作树或切换 tag；除非任务明确要求，不要在本地随意运行它们。

`ServiceLib.Tests` 是当前最可靠的回归入口。配置生成、协议解析、订阅转换、下载请求头、速度测试和跨平台文件权限等行为应优先做单元测试。网络、真实核心进程、系统代理和 sudo 相关行为不能仅凭单元测试视为已验证，需要在目标平台上单独说明验证结果。

## 修改约定

- 遵守现有命名、命名空间和目录布局；优先复用已有 `ServiceLib` helper/manager，不要引入第二套配置或 HTTP 抽象。
- 使用 `async` API 时保留取消令牌、异常传播和现有生命周期清理；不要把阻塞 IO 放入 UI 线程。
- 配置文件、分享 URI、YAML/JSON 和核心命令行参数都属于外部兼容面。保持旧配置可读，新增字段应考虑缺省值和跨版本迁移。
- UI 修改要同时检查 WPF 和 Avalonia 是否需要对应实现；资源文本应放入 `ServiceLib/Resx/ResUI*.resx`，不要在 ViewModel 中硬编码用户可见文案。
- 共享包引用使用中央版本管理；升级依赖时同步检查兼容性、许可证和发布脚本，不要只改单个项目文件。
- 处理下载的压缩包、更新包和外部输入时保留路径遍历、URI 编码、证书校验和凭据脱敏保护。
- 不要提交真实订阅地址、节点凭据、访问令牌、用户数据库或本地发布产物。测试中使用明显的假数据。

## 发布与更新

客户端自身的更新检查和 Linux 包脚本的版本查询使用 `wenxig/v2rayNP`。平台发布由对应 GitHub Actions 工作流负责；涉及版本号、更新地址或发布文件时，应同时检查应用更新逻辑、打包脚本和工作流中的 artifact 名称、RID、输出目录和 release tag。

平台发布由 `build-windows.yml`、`build-windows-desktop.yml`、`build-linux.yml` 和 `build-osx.yml` 负责；Linux 发行版还会调用 `package-debian*.sh`、`package-rhel*.sh`。发布工作流属于高风险自动化，修改后至少做 YAML 语法检查，并确认 artifact 名称、RID、输出目录和 release tag 在上下游步骤中一致。

## 提交前检查清单

1. `git diff --check` 通过，且没有误改生成目录、submodule 或本地配置。
2. 根据改动范围运行相关构建和 `ServiceLib.Tests`；无法运行时说明缺失的 SDK、平台或外部核心。
3. 若涉及 UI，分别检查受影响的 WPF/Avalonia 资源和本地化键。
4. 若涉及协议或核心配置，检查 V2ray/sing-box 生成结果及已有 round-trip/配置测试。
5. 若涉及脚本或工作流，使用 shell/YAML 检查工具并人工核对路径、权限和输入参数。
6. 最终报告列出实际运行的命令、结果和未验证的部分，不要把“代码已编译”表述成“所有平台可运行”。
