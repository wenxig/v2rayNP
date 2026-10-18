using CliWrap;
using CliWrap.Buffered;
using Microsoft.Win32;

namespace ServiceLib.Handler.SysProxy;

public static class SystemProxySnapshot
{
    // Never log snapshot contents; a failed read must not authorize automatic clearing.
    public static async Task<string?> ReadAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            async Task<string> Run(string file, params string[] args)
            {
                var output = await Cli.Wrap(file).WithArguments(args).ExecuteBufferedAsync(timeout.Token);
                return output.StandardOutput;
            }
            if (Utils.IsWindows())
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
                if (key == null) return null;
                var values = new List<string?>();
                foreach (var name in new[] { "ProxyEnable", "ProxyServer", "ProxyOverride", "AutoConfigURL" })
                    values.Add(key.GetValue(name)?.ToString());
                return JsonUtils.Serialize(values);
            }
            if (Utils.IsMacOS())
            {
                var services = (await Run("/usr/sbin/networksetup", "-listallnetworkservices"))
                    .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Skip(1);
                var values = new List<string>();
                foreach (var service in services.Where(s => !s.StartsWith('*')).Order(StringComparer.Ordinal))
                {
                    values.Add(service);
                    foreach (var option in new[] { "-getwebproxy", "-getsecurewebproxy", "-getsocksfirewallproxy", "-getautoproxyurl", "-getproxybypassdomains" })
                        values.Add(await Run("/usr/sbin/networksetup", option, service));
                }
                return values.Count > 0 ? JsonUtils.Serialize(values) : null;
            }
            if (Utils.IsLinux())
            {
                var values = new List<string> { await Run("gsettings", "list-recursively", "org.gnome.system.proxy") };
                foreach (var protocol in new[] { "http", "https", "ftp", "socks" })
                    values.Add(await Run("gsettings", "list-recursively", $"org.gnome.system.proxy.{protocol}"));
                var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";
                if (desktop.Contains("KDE", StringComparison.OrdinalIgnoreCase) || desktop.Contains("plasma", StringComparison.OrdinalIgnoreCase))
                {
                    var reader = Environment.GetEnvironmentVariable("KDE_SESSION_VERSION") == "6" ? "kreadconfig6" : "kreadconfig5";
                    foreach (var name in new[] { "ProxyType", "httpProxy", "httpsProxy", "ftpProxy", "socksProxy", "NoProxyFor" })
                        values.Add(await Run(reader, "--file", "kioslaverc", "--group", "Proxy Settings", "--key", name));
                }
                return JsonUtils.Serialize(values);
            }
        }
        catch { }
        return null;
    }
}
