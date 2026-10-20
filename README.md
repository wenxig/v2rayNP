# v2rayNP

**v2rayNP** is a community-maintained desktop proxy client for Windows, Linux, and macOS. It manages subscriptions, proxy profiles, routing, system proxy settings, and supported proxy cores from one interface.

**v2rayNP** 是面向 Windows、Linux 和 macOS 的社区维护桌面代理客户端，用于管理订阅、节点、路由规则、系统代理和代理核心。

v2rayNP is based on [v2rayN](https://github.com/2dust/v2rayN). This fork changes the product identity and distribution metadata while retaining upstream compatibility where required. It is not affiliated with or endorsed by the upstream maintainers.

v2rayNP is free software licensed under [GNU GPL v3](LICENSE). See the upstream project and its contributors for the original application and notices.

## Features

- Windows WPF and cross-platform Avalonia desktop applications / Windows WPF 与跨平台 Avalonia 桌面界面
- Xray and sing-box configuration and core management / Xray 与 sing-box 配置及核心管理
- mihomo, Juicity, and ShadowQUIC integrations / mihomo、Juicity 与 ShadowQUIC 集成
- Subscription and common proxy share-link management / 订阅与常见代理分享链接管理
- Routing rules, system proxy controls, and traffic statistics / 路由规则、系统代理控制和流量统计
- Windows, Linux, and macOS packages for supported architectures / 支持架构的 Windows、Linux、macOS 安装包

Protocol availability depends on the selected core and platform. See the [documentation wiki](https://github.com/wenxig/v2rayNP/wiki) for setup details and known limitations. 协议可用性取决于所选核心和平台，使用方法与已知限制请参阅[项目文档](https://github.com/wenxig/v2rayNP/wiki)。

## Download

Download releases only from the [v2rayNP releases page](https://github.com/wenxig/v2rayNP/releases). The [release files guide](https://github.com/wenxig/v2rayNP/wiki/Release-files-introduction) lists supported operating systems and architectures.

请从 [v2rayNP Releases](https://github.com/wenxig/v2rayNP/releases) 下载。各系统和架构对应的安装包请参阅[发布文件说明](https://github.com/wenxig/v2rayNP/wiki/Release-files-introduction)。

| Platform | Architectures |
| --- | --- |
| Windows | x64, arm64 |
| Linux | x64, arm64, riscv64, loong64 |
| macOS | x64, arm64 |

## Build

Requirements: .NET 10 SDK and Git submodules initialized.

构建需要 .NET 10 SDK，并先初始化 Git 子模块。

```sh
git submodule update --init --recursive
dotnet restore v2rayNP/v2rayNP.slnx
dotnet build v2rayNP/v2rayNP.Desktop/v2rayNP.Desktop.csproj -c Debug
dotnet test v2rayNP/ServiceLib.Tests/ServiceLib.Tests.csproj -c Debug
```

The Windows WPF host project is `v2rayNP/v2rayNP/v2rayNP.csproj`. On non-Windows systems, build it with `-p:EnableWindowsTargeting=true`.

## Upstream

- Original project: [2dust/v2rayN](https://github.com/2dust/v2rayN)
- Fork releases and issue tracking: [wenxig/v2rayNP](https://github.com/wenxig/v2rayNP)
- License and notices: [LICENSE](LICENSE)

The `v2rayn://` internal share URI and upstream core archive names remain compatible with existing v2rayN data and integrations.
