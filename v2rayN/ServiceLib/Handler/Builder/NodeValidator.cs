namespace ServiceLib.Handler.Builder;

public record NodeValidatorResult(List<string> Errors, List<string> Warnings)
{
    public bool Success => Errors.Count == 0;

    public static NodeValidatorResult Empty()
    {
        return new NodeValidatorResult([], []);
    }
}

public class NodeValidator
{
    // Static validator rules
    private static readonly HashSet<string> SingboxUnsupportedTransports =
        [nameof(ETransport.kcp), nameof(ETransport.xhttp)];

    private static readonly HashSet<EConfigType> SingboxTransportSupportedProtocols =
        [EConfigType.VMess, EConfigType.VLESS, EConfigType.Trojan, EConfigType.Shadowsocks];

    private static readonly HashSet<string> SingboxShadowsocksAllowedTransports =
        [nameof(ETransport.raw), nameof(ETransport.ws)];

    public static NodeValidatorResult Validate(ProfileItem item, ECoreType coreType)
    {
        var v = new ValidationContext();
        ValidateNodeAndCoreSupport(item, coreType, v);
        return v.ToResult();
    }

    private static bool IsPortRange(string? range)
    {
        if (range.IsNullOrEmpty()) return true;
        var parts = range.Split('-');
        return parts.Length == 2 && int.TryParse(parts[0], out var from) && int.TryParse(parts[1], out var to) && from > 0 && to >= from && to <= 65535;
    }

    private static bool IsBase64(string? value, int? length = null)
    {
        if (value.IsNullOrEmpty()) return true;
        try { var bytes = Convert.FromBase64String(value); return length == null || bytes.Length == length; }
        catch (FormatException) { return false; }
    }

    private static void ValidateNodeAndCoreSupport(ProfileItem item, ECoreType coreType, ValidationContext v)
    {
        if (item.ConfigType is EConfigType.Custom)
        {
            return;
        }

        if (item.ConfigType is EConfigType.Outbound)
        {
            if (item.CoreType != coreType)
            {
                v.Error(string.Format(ResUI.MsgCoreNotSupportProtocol, coreType.ToString(), item.ConfigType));
            }
            return;
        }

        if (item.ConfigType.IsGroupType())
        {
            if (coreType == ECoreType.juicity || (coreType == ECoreType.mihomo && item.ConfigType != EConfigType.PolicyGroup))
            {
                v.Error(string.Format(ResUI.MsgCoreNotSupportProtocol, coreType, item.ConfigType));
            }
            return;
        }

        // Basic Property Validation
        v.Assert(!item.Address.IsNullOrEmpty(), string.Format(ResUI.MsgInvalidProperty, ResUI.TbAddress));
        v.Assert(item.Port is > 0 and <= 65535, string.Format(ResUI.MsgInvalidProperty, ResUI.TbPort));

        // Network & Core Logic
        var net = item.GetNetwork();
        if (coreType == ECoreType.sing_box)
        {
            var transportError = ValidateSingboxTransport(item.ConfigType, net);
            if (transportError != null)
            {
                v.Error(transportError);
            }

            if (!Global.SingboxSupportConfigType.Contains(item.ConfigType))
            {
                v.Error(string.Format(ResUI.MsgCoreNotSupportProtocol, nameof(ECoreType.sing_box), item.ConfigType));
            }
        }
        else if (coreType is ECoreType.Xray)
        {
            if (!Global.XraySupportConfigType.Contains(item.ConfigType))
            {
                v.Error(string.Format(ResUI.MsgCoreNotSupportProtocol, nameof(ECoreType.Xray), item.ConfigType));
            }
        }
        else if (coreType == ECoreType.mihomo && !Global.MihomoSupportConfigType.Contains(item.ConfigType))
        {
            v.Error(string.Format(ResUI.MsgCoreNotSupportProtocol, nameof(ECoreType.mihomo), item.ConfigType));
        }
        else if (coreType == ECoreType.juicity && item.ConfigType != EConfigType.Juicity)
        {
            v.Error(string.Format(ResUI.MsgCoreNotSupportProtocol, nameof(ECoreType.juicity), item.ConfigType));
        }

        // Protocol Specifics
        var protocolExtra = item.GetProtocolExtra();
        switch (item.ConfigType)
        {
            case EConfigType.SSR:
                v.Assert(item.Password.IsNotEmpty() && protocolExtra.SsrCipher.IsNotEmpty(), ResUI.CheckServerSettings);
                break;
            case EConfigType.Mieru:
                v.Assert(item.Username.IsNotEmpty() && item.Password.IsNotEmpty(), ResUI.FillPassword);
                v.Assert(protocolExtra.MieruTransport is null or "" or "TCP" or "UDP", ResUI.CheckServerSettings);
                v.Assert(IsPortRange(protocolExtra.MieruPortRange), ResUI.FillCorrectServerPort);
                v.Assert(protocolExtra.MieruMultiplexing is null or "" or "MULTIPLEXING_DEFAULT" or "MULTIPLEXING_OFF" or "MULTIPLEXING_LOW" or "MULTIPLEXING_MIDDLE" or "MULTIPLEXING_HIGH", ResUI.CheckServerSettings);
                v.Assert(protocolExtra.MieruHandshakeMode is null or "" or "HANDSHAKE_DEFAULT" or "HANDSHAKE_STANDARD" or "HANDSHAKE_NO_WAIT", ResUI.CheckServerSettings);
                v.Assert(IsBase64(protocolExtra.MieruTrafficPattern), ResUI.CheckServerSettings);
                break;
            case EConfigType.ShadowQUIC:
                v.Assert((protocolExtra.ShadowquicUsername ?? item.Username).IsNotEmpty() && (protocolExtra.ShadowquicPassword ?? item.Password).IsNotEmpty(), ResUI.FillPassword);
                v.Assert(protocolExtra.ShadowquicCongestionControl is null or "" or "cubic" or "bbr" or "new_reno", ResUI.CheckServerSettings);
                v.Assert((protocolExtra.ShadowquicQuicVersions ?? "v1").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).All(x => x is "v1" or "v2"), ResUI.CheckServerSettings);
                v.Assert(!item.GetAllowInsecure(), ProtocolCapabilities.Text("MsgShadowquicInsecureUnsupported"));
                v.Assert(new[] { protocolExtra.ShadowquicUpMbps, protocolExtra.ShadowquicDownMbps, protocolExtra.ShadowquicCwnd, protocolExtra.ShadowquicMaxDatagramFrameSize, protocolExtra.ShadowquicMaxOpenStreams, protocolExtra.ShadowquicRecvWindowConn, protocolExtra.ShadowquicRecvWindow }.All(x => x is null or >= 0), ResUI.CheckServerSettings);
                break;
            case EConfigType.Juicity:
                v.Assert(Guid.TryParse(protocolExtra.JuicityUuid ?? item.Username, out _), ResUI.FillUUID);
                v.Assert((protocolExtra.JuicityPassword ?? item.Password).IsNotEmpty(), ResUI.FillPassword);
                v.Assert(protocolExtra.JuicityCongestionControl is null or "" or "cubic" or "bbr" or "new_reno", ResUI.CheckServerSettings);
                v.Assert(IsBase64(protocolExtra.JuicityPinnedCertchainSha256, 32), ResUI.CheckServerSettings);
                break;
            case EConfigType.VMess:
                v.Assert(!item.Password.IsNullOrEmpty() && Utils.IsGuidByParse(item.Password),
                    string.Format(ResUI.MsgInvalidProperty, ResUI.TbId));
                break;

            case EConfigType.VLESS:
                v.Assert(
                    !item.Password.IsNullOrEmpty()
                    && (Utils.IsGuidByParse(item.Password) || item.Password.Length <= 30),
                    string.Format(ResUI.MsgInvalidProperty, ResUI.TbId5)
                );
                v.Assert(Global.Flows.Contains(protocolExtra.Flow ?? string.Empty),
                    string.Format(ResUI.MsgInvalidProperty, ResUI.TbFlow5));
                break;

            case EConfigType.Shadowsocks:
                v.Assert(!item.Password.IsNullOrEmpty(), string.Format(ResUI.MsgInvalidProperty, ResUI.TbId3));
                v.Assert(
                    !string.IsNullOrEmpty(protocolExtra.SsMethod) &&
                    Global.SsSecuritiesInSingbox.Contains(protocolExtra.SsMethod),
                    string.Format(ResUI.MsgInvalidProperty, ResUI.TbSecurity3));
                break;
        }

        if (coreType is ECoreType.Xray
            && (protocolExtra.Flow ?? string.Empty).StartsWith("xtls", StringComparison.OrdinalIgnoreCase)
            && item.MuxEnabled == true)
        {
            v.Warning(string.Format(ResUI.MsgOptionsConflict, "XTLS", "Mux.Cool"));
        }

        if (item.GetNetwork() is nameof(ETransport.ws)
            && item.EchConfigList.IsNullOrEmpty()
            && item.GetAlpn()?.FirstOrDefault() is "h3" or "h2")
        {
            v.Warning(
                $"WebSocket but ALPN is set to {item.Alpn}, the core may ignore the ALPN setting or cause unexpected issues.");
        }

        // TLS & Security
        if (item.StreamSecurity == Global.StreamSecurity)
        {
            var isCertProvided = !item.Cert.IsNullOrEmpty();
            if (!item.Cert.IsNullOrEmpty() && CertPemManager.ParsePemChain(item.Cert).Count == 0)
            {
                v.Error(string.Format(ResUI.MsgInvalidProperty, ResUI.TbFullCertTips));
                isCertProvided = false;
            }

            if ((coreType == ECoreType.Xray
                && item.GetAllowInsecure()
                && !isCertProvided
                && item.CertSha.IsNullOrEmpty())
                || (coreType == ECoreType.sing_box
                    && item.GetAllowInsecure()
                    && !isCertProvided))
            {
                if (coreType == ECoreType.Xray)
                {
                    v.Warning(ResUI.MsgAllowInsecureDeprecated);
                }
                v.Warning(ResUI.MsgInsecureConfiguration);
            }
        }

        if (item.StreamSecurity == Global.StreamSecurityReality)
        {
            v.Assert(!item.PublicKey.IsNullOrEmpty(), string.Format(ResUI.MsgInvalidProperty, ResUI.TbPublicKey));
        }

        var transport = item.GetTransportExtra();
        if (item.Network == nameof(ETransport.xhttp) && !transport.XhttpExtra.IsNullOrEmpty())
        {
            if (JsonUtils.ParseJson(transport.XhttpExtra) is not JsonObject)
            {
                v.Error(string.Format(ResUI.MsgInvalidProperty, ResUI.TransportExtra));
            }
        }

        if (!item.Finalmask.IsNullOrEmpty())
        {
            if (JsonUtils.ParseJson(item.Finalmask) is not JsonObject)
            {
                v.Error(string.Format(ResUI.MsgInvalidProperty, ResUI.TbFinalmask));
            }
        }
    }

    private static string? ValidateSingboxTransport(EConfigType configType, string net)
    {
        // sing-box does not support xhttp / kcp transports
        if (SingboxUnsupportedTransports.Contains(net))
        {
            return string.Format(ResUI.MsgCoreNotSupportNetwork, nameof(ECoreType.sing_box), net);
        }

        // sing-box does not support non-tcp transports for protocols other than vmess/trojan/vless/shadowsocks
        if (!SingboxTransportSupportedProtocols.Contains(configType) && net != nameof(ETransport.raw))
        {
            return string.Format(ResUI.MsgCoreNotSupportProtocolTransport,
                nameof(ECoreType.sing_box), configType.ToString(), net);
        }

        // sing-box shadowsocks only supports tcp/ws/quic transports
        if (configType == EConfigType.Shadowsocks && !SingboxShadowsocksAllowedTransports.Contains(net))
        {
            return string.Format(ResUI.MsgCoreNotSupportProtocolTransport,
                nameof(ECoreType.sing_box), configType.ToString(), net);
        }

        return null;
    }

    private class ValidationContext
    {
        public List<string> Errors { get; } = [];
        public List<string> Warnings { get; } = [];

        public void Error(string message)
        {
            Errors.Add(message);
        }

        public void Warning(string message)
        {
            Warnings.Add(message);
        }

        public void Assert(bool condition, string errorMsg)
        {
            if (!condition)
            {
                Error(errorMsg);
            }
        }

        public NodeValidatorResult ToResult()
        {
            return new NodeValidatorResult(Errors, Warnings);
        }
    }
}
