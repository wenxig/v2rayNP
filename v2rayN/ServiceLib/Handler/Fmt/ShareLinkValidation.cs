namespace ServiceLib.Handler.Fmt;

internal static class ShareLinkValidation
{
    public static bool IsEscapedCorrectly(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '%') continue;
            if (i + 2 >= value.Length || !Uri.IsHexDigit(value[i + 1]) || !Uri.IsHexDigit(value[i + 2])) return false;
            i += 2;
        }
        return true;
    }

    public static bool? Boolean(string? value) => value switch
    {
        null => null, "0" => false, "1" => true, _ => throw new FormatException()
    };

    public static int? PositiveInt(string? value) => value == null ? null
        : int.TryParse(value, out var number) && number > 0 ? number : throw new FormatException();
}
