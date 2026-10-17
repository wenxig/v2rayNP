namespace ServiceLib.Services.CoreConfig;

public class CoreConfigJuicityService(Config config)
{
    public RetResult GenerateClientConfig(ProfileItem node, int? listenPort = null)
    {
        var result = new RetResult { Msg = ResUI.InitialConfiguration };
        try
        {
            var validation = NodeValidator.Validate(node, ECoreType.juicity);
            if (!validation.Success) return new RetResult { Msg = string.Join(Environment.NewLine, validation.Errors) };
            var extra = node.GetProtocolExtra();
            var content = new Dictionary<string, object?>
            {
                ["listen"] = $"{Global.Loopback}:{(listenPort ?? config.Inbound.First().LocalPort)}",
                ["server"] = $"{FormatAddress(node.Address)}:{node.Port}",
                ["uuid"] = extra.JuicityUuid ?? node.Username,
                ["password"] = extra.JuicityPassword ?? node.Password,
                ["sni"] = extra.JuicitySni.IsNotEmpty() ? extra.JuicitySni : node.Sni.IsNotEmpty() ? node.Sni : null,
                ["allow_insecure"] = extra.JuicityAllowInsecure ?? node.GetAllowInsecure(),
                ["congestion_control"] = extra.JuicityCongestionControl.IsNotEmpty() ? extra.JuicityCongestionControl : "bbr",
                ["pinned_certchain_sha256"] = extra.JuicityPinnedCertchainSha256.IsNotEmpty() ? extra.JuicityPinnedCertchainSha256 : null,
                ["log_level"] = config.CoreBasicItem.Loglevel switch { "warning" => "warn", "none" => "error", var level => level },
            };
            result.Data = JsonUtils.Serialize(content);
            result.Msg = string.Format(ResUI.SuccessfulConfiguration, node.GetSummary());
            result.Success = result.Data is string text && text.IsNotEmpty();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(CoreConfigJuicityService), ex);
            result.Msg = ResUI.FailedGenDefaultConfiguration;
        }
        return result;
    }

    private static string FormatAddress(string address) => Utils.IsIpv6(address) ? $"[{address}]" : address;
}
