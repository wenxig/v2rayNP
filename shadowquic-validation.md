# ShadowQUIC 本地回环与远端节点分组验证

测试时间：2026-10-18 12:31:22 UTC+8 至 2026-10-18 12:33:39 UTC+8。

使用当前已提交代码生成配置；官方 ShadowQUIC v0.4.4，sing-box v1.14.3，macOS x64。仅测试 ShadowQUIC，不代表其他协议节点的验收结果。

## 方法

- 清除 HTTP_PROXY、HTTPS_PROXY、ALL_PROXY、NO_PROXY 及小写变量；curl 显式指定任务回环代理；无代理基线显式禁用代理。未更改系统代理或 TUN，也未停止或重启正在使用的原版 v2rayN。
- 本地官方 SQ 服务端监听 UDP 127.0.0.1:24443，转发到本地 HTTP 回应器 127.0.0.1:24580；成功标准为 HTTP 200 且正文逐字节等于固定测试内容。公网 HTTPS 保留证书校验；Google 要求 204 空正文，example.com 要求 200 且正文包含 Example Domain。
- 本版生成普通配置、混合端口前置配置和独立测速配置。混合端口 24180，官方后端 24188，测速 24221。后端与测速核心持续运行完成一组请求，测试期间不反复重启。
- 用户节点取自 /Users/wenxig/Downloads/nodes/shadowquic/client.yaml；保留其 SNI、ALPN、MTU、拥塞控制、zero-rtt=true 与 over-stream 等参数。报告不包含服务器地址或凭据。
- 原始官方配置对照仅调整本地监听为 24230，保留原始 outbound。
- curl 每次连接超时 8 秒、总超时 12 秒，无自动重试；下表耗时为成功样本的请求总耗时中位数，不是 QUIC RTT。
- 应用真连接延迟调用 ConnectionHandler.GetRealPingTime，显式传入对应 SOCKS 代理，使用应用本身的 5 秒时限及两次请求取较快值算法；每入口调用 3 次。

## 请求结果

| 节点 | 入口 | 目标 | 成功/次数 | 成功耗时中位数 | 失败退出码 |
|---|---|---|---:|---:|---|
| 本机无代理直连 | 直接访问 | Google HTTPS 204 | 0/1 | — | 28 ×1 |
| 本机无代理直连 | 直接访问 | example.com HTTPS 200 | 1/1 | 1021.16 ms | — |
| 本地回环 SQ 服务端 | 官方客户端 SOCKS | 本地 HTTP 正文 | 5/5 | 1.99 ms | — |
| 本地回环 SQ 服务端 | 官方客户端 SOCKS | Google HTTPS 204 | 0/3 | — | 28 ×3 |
| 本地回环 SQ 服务端 | 官方客户端 SOCKS | example.com HTTPS 200 | 1/1 | 1533.90 ms | — |
| 本地回环 SQ 服务端 | sing-box 混合端口 SOCKS | 本地 HTTP 正文 | 5/5 | 2.71 ms | — |
| 本地回环 SQ 服务端 | sing-box 混合端口 SOCKS | Google HTTPS 204 | 0/3 | — | 28 ×3 |
| 本地回环 SQ 服务端 | sing-box 混合端口 SOCKS | example.com HTTPS 200 | 1/1 | 961.10 ms | — |
| 本地回环 SQ 服务端 | sing-box 混合端口 HTTP | 本地 HTTP 正文 | 5/5 | 2.72 ms | — |
| 本地回环 SQ 服务端 | sing-box 混合端口 HTTP | Google HTTPS 204 | 0/3 | — | 28 ×3 |
| 本地回环 SQ 服务端 | sing-box 混合端口 HTTP | example.com HTTPS 200 | 1/1 | 1991.77 ms | — |
| 本地回环 SQ 服务端 | 独立测速 SOCKS | 本地 HTTP 正文 | 5/5 | 2.04 ms | — |
| 本地回环 SQ 服务端 | 独立测速 SOCKS | Google HTTPS 204 | 0/3 | — | 28 ×3 |
| 本地回环 SQ 服务端 | 独立测速 SOCKS | example.com HTTPS 200 | 1/1 | 927.12 ms | — |
| 用户提供的远端 SQ 节点 | 官方客户端 SOCKS | Google HTTPS 204 | 3/3 | 425.94 ms | — |
| 用户提供的远端 SQ 节点 | 官方客户端 SOCKS | example.com HTTPS 200 | 0/1 | — | 35 ×1 |
| 用户提供的远端 SQ 节点 | sing-box 混合端口 SOCKS | Google HTTPS 204 | 3/3 | 433.95 ms | — |
| 用户提供的远端 SQ 节点 | sing-box 混合端口 SOCKS | example.com HTTPS 200 | 0/1 | — | 35 ×1 |
| 用户提供的远端 SQ 节点 | sing-box 混合端口 HTTP | Google HTTPS 204 | 3/3 | 431.90 ms | — |
| 用户提供的远端 SQ 节点 | sing-box 混合端口 HTTP | example.com HTTPS 200 | 0/1 | — | 35 ×1 |
| 用户提供的远端 SQ 节点 | 独立测速 SOCKS | Google HTTPS 204 | 2/3 | 608.94 ms | 35 ×1 |
| 用户提供的远端 SQ 节点 | 独立测速 SOCKS | example.com HTTPS 200 | 0/1 | — | 35 ×1 |
| 远端原始官方配置对照 | 原始官方 YAML / SOCKS | Google HTTPS 204 | 3/3 | 421.60 ms | — |
| 远端原始官方配置对照 | 原始官方 YAML / SOCKS | example.com HTTPS 200 | 1/1 | 472.60 ms | — |

curl 28 表示超时；35 表示 TLS 连接失败；97 表示代理握手/代理侧错误。成功须同时满足退出码、响应状态和正文检查，监听成功不计作远端连通成功。

## 应用真连接延迟

| 节点 | 入口 | 目标 | 三次结果（ms） |
|---|---|---|---|
| 本地回环 SQ 服务端 | 官方客户端 SOCKS | 本地 HTTP 正文 | 10, 2, 3 |
| 本地回环 SQ 服务端 | sing-box 混合端口 SOCKS | 本地 HTTP 正文 | 14, 3, 5 |
| 本地回环 SQ 服务端 | 独立测速 SOCKS | 本地 HTTP 正文 | 11, 1, 3 |
| 用户提供的远端 SQ 节点 | 官方客户端 SOCKS | Google HTTPS 204 | -1, -1, -1 |
| 用户提供的远端 SQ 节点 | sing-box 混合端口 SOCKS | Google HTTPS 204 | 200, 196, 196 |
| 用户提供的远端 SQ 节点 | 独立测速 SOCKS | Google HTTPS 204 | 232, 231, -1 |

`-1` 表示应用在时限内未测得成功响应。两类节点的应用延迟目标不同：本地节点使用本地 HTTP 回应器以隔离公网影响，远端节点使用 Google HTTPS；其数值不能直接视为同一链路的速度比较。

## 交错复测

完成于 2026-10-18 12:35:38 UTC+8。生成配置与原始官方配置同时保持运行，每轮交替先后顺序，各请求 Google 和 example.com 3 次，均保持 HTTPS 证书校验。

| 配置 | Google 204 | example.com 200 | 应用真连接延迟（ms） |
|---|---:|---:|---|
| 本版生成配置 | 3/3 | 3/3 | 232, 230, 306 |
| 原始官方配置 | 3/3 | 3/3 | 232, 227, 228 |

- 交错复测全部通过，首轮远端 TLS 失败无法按固定配置错误复现。不能据此宣称长期稳定；首轮失败仍计入总样本。
- 复测进程已清理：是。

## 汇总结论

| 验证对象 | 合并结果（只统计 curl 请求） | 结论 |
|---|---|---|
| 本地回环 SQ | 本地 HTTP 正文 20/20；example.com 4/4；Google 0/12 | 协议、混合 SOCKS/HTTP 入口和独立测速入口均可传递真实响应；Google 直连基线也超时，不能将此归咎于回环协议实现。 |
| 用户远端 SQ，经本版生成配置 | Google 14/15；example.com 3/7，总计 17/22 | 节点可用但本轮存在 TLS 失败，稳定性部分通过。混合入口真延迟 196–200 ms，官方入口交错复测 230–306 ms。 |
| 用户远端 SQ，原始官方配置对照 | Google 6/6；example.com 4/4，总计 10/10 | 此次对照全部通过，应用真延迟 227–232 ms；有限样本不能排除其他时段的网络问题。 |

延迟数字是不同时间窗口的应用请求测量，不作为引擎性能优劣排名。未向失效结论或固定配置缺陷作过度推断。

## 进程与范围

- SOCKS5 就绪检查：7/7，前置配置通过 sing-box check。
- 本次启动的客户端、服务端及前置进程已全部退出：是。
- 未验证真实 TUN 流量、UDP 业务转发、多平台运行或长期稳定性。

## 实际执行

```sh
env -u HTTP_PROXY -u HTTPS_PROXY -u ALL_PROXY -u http_proxy -u https_proxy -u all_proxy dotnet run --project /private/tmp/v2raynp-sq-acceptance-20261018/harness/Smoke.csproj --no-restore
python3 /private/tmp/v2raynp-sq-acceptance-20261018/validate.py
python3 /private/tmp/v2raynp-sq-acceptance-20261018/interleave.py
git diff --check
```

临时目录保存原始请求结果 results.json、requests.json 和进程日志；可能含节点信息的输入、配置和日志未纳入 Git。本次只增加验收记录，未修改产品源码。

## 根据实测改进应用（2026-10-18）

逐字段比较用户原始配置和本版生成配置：服务器、认证、SNI、ALPN、initial-mtu、拥塞控制、zero-rtt、over-stream 均一致。生成配置额外设置标签和 mtu-discovery=true，后者也是官方 v0.4.4 的缺省值；inbound/outbound 与 inbounds/outbounds 是官方支持的等价入口。现有数据不能证明这些结构差异影响稳定性。

确认并修复的问题：

- 未设置 zero-rtt 时，旧版生成 false，与官方默认 true 不一致。现在省略该字段交由官方核心处理；编辑器显示官方默认值，用户显式关闭仍可保存。
- 分享 URI 现在区分未设置、显式 false 和 true，避免 zero-rtt=0 在导出后丢失。
- 真连接延迟的每次请求独立处理 HTTP/网络错误，保留最快成功样本；HTTP 错误状态不再计作成功，响应及时释放。仍保持原有两次采样及 5 秒总预算，并传播用户取消。低于 1 ms 的成功结果记为 1 ms。

### 修复后真实核心验证

时间：2026-10-18 13:55:51 至 13:56:43 UTC+8。仍使用官方 v0.4.4 与 sing-box v1.14.3、同一组独立回环端口；清除代理环境变量，未修改系统代理/TUN或正在运行的原版客户端。

本地配置故意不设置 zero-rtt，验证官方默认路径；远端配置保留用户参数。成功正文标准、HTTPS 校验与 curl 超时与前文相同。

| 验证 | 结果 |
|---|---|
| 本地 HTTP 正文，官方 SOCKS/混合 SOCKS/混合 HTTP/测速 | 各 5/5，合计 20/20 |
| 第一次 HTTP 200、第二次超时，旧版应用算法 | -1, -1, -1 ms（误报失败） |
| 相同超时场景，修复后应用算法 | 65, 3, 3 ms（保留成功样本） |
| 持续 HTTP 503，旧版应用算法 | 11, 2, 3 ms（误报成功） |
| 持续 HTTP 503，修复后应用算法 | -1, -1, -1 ms（正确失败） |
| 远端，生成/原始配置交错 curl 对照 | 各 0/6，均 TLS 失败（curl 35） |
| 远端混合 SOCKS/混合 HTTP/测速 curl | 各 0/2，均 TLS 失败（curl 35） |

同一轮应用真连接延迟：生成配置官方入口 1061/199/201 ms，原始配置入口 234/-1/-1 ms，混合 SOCKS -1/-1/-1 ms，测速 -1/1196/-1 ms。curl 与应用使用不同 HTTP/TLS 实现，且请求时刻不同，单独保留结果，不将应用延迟算入 curl 成功率。此次两种配置都出现失败，再次说明远端存在波动；本次没有证据宣称修复提高了该节点传输成功率，也无法认定原始配置始终更好。

sing-box check 通过，7 个 SOCKS 监听均就绪；所有测试进程已退出、监听已释放。测试脚本及含节点信息的临时配置未提交。

实际执行：

```sh
env -u HTTP_PROXY -u HTTPS_PROXY -u ALL_PROXY -u NO_PROXY -u http_proxy -u https_proxy -u all_proxy -u no_proxy dotnet run --project /private/tmp/v2raynp-sq-improvement-20261018/harness/Smoke.csproj
python3 /private/tmp/v2raynp-sq-improvement-20261018/verify.py
dotnet test v2rayN/ServiceLib.Tests/ServiceLib.Tests.csproj -c Debug --no-restore -- --maximum-parallel-tests 1
dotnet build v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj -c Debug
dotnet build v2rayN/v2rayN/v2rayN.csproj -c Debug -p:EnableWindowsTargeting=true
```

共享回归测试与两端构建通过。真实 TUN、UDP 业务、多平台运行及长期稳定性仍未验证。
