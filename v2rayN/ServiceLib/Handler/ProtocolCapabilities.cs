namespace ServiceLib.Handler;

public static class ProtocolCapabilities
{
    public static bool IsStandaloneCore(ECoreType type) => type is ECoreType.juicity or ECoreType.shadowquic;

    public static bool IsExtendedProtocol(EConfigType type) => type is EConfigType.SSR or EConfigType.Mieru or EConfigType.ShadowQUIC or EConfigType.Juicity;

    public static ECoreType DefaultCore(EConfigType type) => type switch
    {
        EConfigType.SSR or EConfigType.Mieru => ECoreType.mihomo,
        EConfigType.ShadowQUIC => ECoreType.shadowquic,
        EConfigType.Juicity => ECoreType.juicity,
        _ => ECoreType.Xray
    };

    public static IEnumerable<string> AvailableCores(EConfigType type)
    {
        if (type == EConfigType.Custom) return Global.CoreTypes;
        if (IsExtendedProtocol(type)) return [DefaultCore(type).ToString()];
        return new[] { ECoreType.Xray, ECoreType.sing_box }
            .Where(core => (core == ECoreType.Xray ? Global.XraySupportConfigType : Global.SingboxSupportConfigType).Contains(type))
            .Select(core => core.ToString());
    }

    public static string Text(string key) => ResUI.ResourceManager.GetString(key, ResUI.Culture) ?? key;
}
