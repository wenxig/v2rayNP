# v2rayN

### A GUI client for Windows, Linux and macOS. Support [Xray](https://github.com/XTLS/Xray-core), [sing-box](https://github.com/SagerNet/sing-box), [mihomo](https://github.com/MetaCubeX/mihomo), and the official [Juicity](https://github.com/juicity/juicity) and [ShadowQUIC](https://github.com/spongebob888/shadowquic) clients.

Supported share protocols include SSR, Mieru, ShadowQUIC, and Juicity. SSR and Mieru use mihomo; Juicity uses `juicity-client`, and ShadowQUIC uses the official `shadowquic` client. Their loopback SOCKS listeners use the configured mixed port plus 7 and plus 8, respectively; managed sing-box groups can combine these backends with native sing-box members. Manual member selection and least-ping failover are supported; proxy chains through these standalone clients remain unsupported. Existing ShadowQUIC mihomo mappings migrate to the official client. The official ShadowQUIC client does not expose QUIC version selection or mihomo bandwidth/window settings; non-default values are rejected instead of silently discarded. Per-member traffic accounting, TUN traffic, and real server connectivity require further validation.

ShadowQUIC uses the official client default for an omitted zero-RTT option (enabled in v0.4.4). Explicit disabling is preserved when editing and sharing nodes. Real-ping reports the fastest successful HTTP response within its existing time budget; a later failed sample does not erase an earlier success, and HTTP error responses are rejected.

New configurations use mixed port `20808`; existing configurations retain their saved port. Juicity uses mixed port +7 and ShadowQUIC uses mixed port +8.

> **🧹 去广告纯净版 / Ad-free Mod** \
> 本版本移除了原版的推广/广告入口，「检查更新」与下载均指向本仓库。 \
> Promotion/ad entries removed; update-check & downloads point to this repository.

[![Release](https://img.shields.io/github/v/release/wenxig/v2rayNP?logo=github&label=Release)](https://github.com/wenxig/v2rayNP/releases)
[![Downloads](https://img.shields.io/github/downloads/wenxig/v2rayNP/latest/total?logo=github&label=Downloads)](https://github.com/wenxig/v2rayNP/releases)
 
[![Windows](https://img.shields.io/badge/Windows-supported-0078D6?logo=windows)](https://github.com/wenxig/v2rayNP)
[![Linux](https://img.shields.io/badge/Linux-supported-FCC624?logo=linux&logoColor=000)](https://github.com/wenxig/v2rayNP)
[![macOS](https://img.shields.io/badge/macOS-supported-000000?logo=apple)](https://github.com/wenxig/v2rayNP)


---

## Download / 下载

Download the latest release here:

在这里下载最新版本：

[https://github.com/wenxig/v2rayNP/releases](https://github.com/wenxig/v2rayNP/releases)


> [!TIP]
> v2rayN is the desktop version. For the mobile version, please visit the v2rayNG \
> v2rayN 是电脑版，手机版请访问 v2rayNG
>
> https://github.com/LOVECHEN/v2rayNG-mod

---

## Documentation / 使用文档

Read the Wiki for usage guides and configuration details.

请阅读 Wiki 获取使用说明和配置教程。

[https://github.com/wenxig/v2rayNP/wiki](https://github.com/wenxig/v2rayNP/wiki)

---

## Supported Platforms / 支持平台

| Platform / 平台 | x64 | x86 | arm64 | riscv64 | loong64 |
| --- | --- | --- | --- | --- | --- |
| Windows | ✅ | ✅ | ✅ | - | - |
| Linux | ✅ | - | ✅ | ✅ | ✅ |
| macOS | ✅ | - | ✅ | - | - |

Minimum OS requirements: [Release files introduction](https://github.com/wenxig/v2rayNP/wiki/Release-files-introduction) / 最低系统要求：[发布文件介绍](https://github.com/wenxig/v2rayNP/wiki/Release-files-introduction)
