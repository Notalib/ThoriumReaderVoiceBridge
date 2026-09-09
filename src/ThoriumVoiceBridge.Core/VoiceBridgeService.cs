namespace Nota.ThoriumVoiceBridge;

public sealed class VoiceBridgeService
{
    public const string BridgeMarkerName = "ThoriumBridge";
    public const string BridgeMarkerValue = "1";

    private readonly IVoiceRegistry registry;

    public VoiceBridgeService(IVoiceRegistry registry)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public IReadOnlyList<VoiceToken> GetSapiVoices() => registry.GetVoices(VoiceRegistryHive.Sapi);

    public IReadOnlyList<VoiceToken> GetOneCoreVoices() => registry.GetVoices(VoiceRegistryHive.OneCore);

    public VoiceOperationResult InstallVoice(string tokenName)
    {
        var source = registry.GetVoice(VoiceRegistryHive.Sapi, tokenName)
            ?? throw new InvalidOperationException($"SAPI token findes ikke: {tokenName}");

        if (source.IsMicrosoft)
        {
            return VoiceOperationResult.Skipped("Microsoft-stemmer kan ikke flyttes.");
        }

        if (registry.GetVoice(VoiceRegistryHive.OneCore, tokenName) is not null)
        {
            return VoiceOperationResult.Skipped("Stemmen findes allerede i Thorium.");
        }

        var bridged = new VoiceToken(
            TokenName: source.TokenName,
            Name: source.Name,
            Vendor: source.Vendor,
            Language: source.LanguageHex,
            Clsid: source.Clsid,
            Display: source.DisplayName,
            Age: source.Age,
            Gender: source.Gender,
            ThoriumBridge: BridgeMarkerValue);

        registry.SetVoice(VoiceRegistryHive.OneCore, bridged);
        return VoiceOperationResult.Completed(bridged);
    }

    public VoiceOperationResult RemoveVoice(string tokenName)
    {
        var voice = registry.GetVoice(VoiceRegistryHive.OneCore, tokenName)
            ?? throw new InvalidOperationException($"OneCore-token findes ikke: {tokenName}");

        if (!voice.IsBridged)
        {
            return VoiceOperationResult.Skipped("Native/ukendte OneCore-stemmer kan ikke fjernes.");
        }

        return registry.DeleteVoice(VoiceRegistryHive.OneCore, tokenName)
            ? VoiceOperationResult.Completed(voice)
            : VoiceOperationResult.Failed("Stemmen kunne ikke fjernes fra Thorium.");
    }

    public static string? NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var value = language.Trim();
        if (value.All(Uri.IsHexDigit) && value.Length is >= 1 and <= 4)
        {
            return value.PadLeft(4, '0').ToUpperInvariant();
        }

        if (value.All(char.IsDigit) && value.Length <= 5 && int.TryParse(value, out var lcid))
        {
            return lcid.ToString("X4");
        }

        return value;
    }

    public static string LanguageLabel(string? language)
    {
        var languageHex = NormalizeLanguage(language);
        return languageHex?.ToUpperInvariant() switch
        {
            "0406" => "Dansk (DA)",
            "041D" => "Svensk (SV)",
            "0414" => "Norsk Bokmål (NO)",
            "0814" => "Norsk Nynorsk (NO)",
            "040B" => "Finsk (FI)",
            "040F" => "Islandsk (IS)",
            "0409" => "Engelsk (US)",
            "0809" => "Engelsk (UK)",
            "0C09" => "Engelsk (AU)",
            "1009" => "Engelsk (CA)",
            "1409" => "Engelsk (NZ)",
            "1809" => "Engelsk (IE)",
            "0407" => "Tysk (DE)",
            "0807" => "Tysk (CH)",
            "0C07" => "Tysk (AT)",
            "040C" => "Fransk (FR)",
            "080C" => "Fransk (BE)",
            "0C0C" => "Fransk (CA)",
            "100C" => "Fransk (CH)",
            "040A" => "Spansk (ES)",
            "080A" => "Spansk (MX)",
            "0C0A" => "Spansk (ES - modern)",
            "0416" => "Portugisisk (BR)",
            "0816" => "Portugisisk (PT)",
            "0410" => "Italiensk (IT)",
            "0810" => "Italiensk (CH)",
            "0413" => "Hollandsk (NL)",
            "0813" => "Hollandsk (BE)",
            "0405" => "Tjekkisk (CS)",
            "0415" => "Polsk (PL)",
            "0418" => "Rumænsk (RO)",
            "0419" => "Russisk (RU)",
            "0424" => "Slovensk (SL)",
            "041B" => "Slovakisk (SK)",
            "040E" => "Ungarsk (HU)",
            "041A" => "Kroatisk (HR)",
            "0422" => "Ukrainsk (UK)",
            "0425" => "Estisk (ET)",
            "0426" => "Lettisk (LV)",
            "0427" => "Litauisk (LT)",
            "0411" => "Japansk (JA)",
            "0412" => "Koreansk (KO)",
            "0804" => "Kinesisk (CN)",
            "0404" => "Kinesisk (TW)",
            "0C04" => "Kinesisk (HK)",
            "1004" => "Kinesisk (SG)",
            "041E" => "Thai (TH)",
            "042A" => "Vietnamesisk (VI)",
            "0401" => "Arabisk (SA)",
            "040D" => "Hebræisk (HE)",
            "0429" => "Persisk (FA)",
            "041F" => "Tyrkisk (TR)",
            _ => languageHex ?? string.Empty
        };
    }

    public static bool IsMicrosoftVendor(string? vendor) =>
        !string.IsNullOrWhiteSpace(vendor) && vendor.TrimStart().StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase);

    public static string MakeDisplayName(string? name, string? vendor)
    {
        var cleanName = string.IsNullOrWhiteSpace(name) ? "Ukendt" : name.Trim();
        var cleanVendor = vendor?.Trim();
        return string.IsNullOrWhiteSpace(cleanVendor) ? cleanName : $"{cleanName} ({cleanVendor})";
    }
}

public sealed record VoiceOperationResult(bool Changed, string Message, VoiceToken? Voice = null)
{
    public static VoiceOperationResult Completed(VoiceToken voice) =>
        new(true, "OK", voice);

    public static VoiceOperationResult Skipped(string message) =>
        new(false, message);

    public static VoiceOperationResult Failed(string message) =>
        new(false, message);
}
