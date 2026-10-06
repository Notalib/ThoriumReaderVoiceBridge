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
            ?? throw new InvalidOperationException(Strings.SapiTokenMissing(tokenName));

        if (source.IsMicrosoft)
        {
            return VoiceOperationResult.Skipped(Strings.MicrosoftVoiceSkipped);
        }

        if (registry.GetVoice(VoiceRegistryHive.OneCore, tokenName) is not null)
        {
            return VoiceOperationResult.Skipped(Strings.AlreadyInThorium);
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
            ?? throw new InvalidOperationException(Strings.OneCoreTokenMissing(tokenName));

        if (!voice.IsBridged)
        {
            return VoiceOperationResult.Skipped(Strings.CannotRemoveNative);
        }

        return registry.DeleteVoice(VoiceRegistryHive.OneCore, tokenName)
            ? VoiceOperationResult.Completed(voice)
            : VoiceOperationResult.Failed(Strings.RemoveFailed);
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
            "0406" => Strings.Pick("Dansk (DA)", "Danish (DA)"),
            "041D" => Strings.Pick("Svensk (SV)", "Swedish (SV)"),
            "0414" => Strings.Pick("Norsk Bokmål (NO)", "Norwegian Bokmål (NO)"),
            "0814" => Strings.Pick("Norsk Nynorsk (NO)", "Norwegian Nynorsk (NO)"),
            "040B" => Strings.Pick("Finsk (FI)", "Finnish (FI)"),
            "040F" => Strings.Pick("Islandsk (IS)", "Icelandic (IS)"),
            "0409" => Strings.Pick("Engelsk (US)", "English (US)"),
            "0809" => Strings.Pick("Engelsk (UK)", "English (UK)"),
            "0C09" => Strings.Pick("Engelsk (AU)", "English (AU)"),
            "1009" => Strings.Pick("Engelsk (CA)", "English (CA)"),
            "1409" => Strings.Pick("Engelsk (NZ)", "English (NZ)"),
            "1809" => Strings.Pick("Engelsk (IE)", "English (IE)"),
            "0407" => Strings.Pick("Tysk (DE)", "German (DE)"),
            "0807" => Strings.Pick("Tysk (CH)", "German (CH)"),
            "0C07" => Strings.Pick("Tysk (AT)", "German (AT)"),
            "040C" => Strings.Pick("Fransk (FR)", "French (FR)"),
            "080C" => Strings.Pick("Fransk (BE)", "French (BE)"),
            "0C0C" => Strings.Pick("Fransk (CA)", "French (CA)"),
            "100C" => Strings.Pick("Fransk (CH)", "French (CH)"),
            "040A" => Strings.Pick("Spansk (ES)", "Spanish (ES)"),
            "080A" => Strings.Pick("Spansk (MX)", "Spanish (MX)"),
            "0C0A" => Strings.Pick("Spansk (ES - modern)", "Spanish (ES - modern)"),
            "0416" => Strings.Pick("Portugisisk (BR)", "Portuguese (BR)"),
            "0816" => Strings.Pick("Portugisisk (PT)", "Portuguese (PT)"),
            "0410" => Strings.Pick("Italiensk (IT)", "Italian (IT)"),
            "0810" => Strings.Pick("Italiensk (CH)", "Italian (CH)"),
            "0413" => Strings.Pick("Hollandsk (NL)", "Dutch (NL)"),
            "0813" => Strings.Pick("Hollandsk (BE)", "Dutch (BE)"),
            "0405" => Strings.Pick("Tjekkisk (CS)", "Czech (CS)"),
            "0415" => Strings.Pick("Polsk (PL)", "Polish (PL)"),
            "0418" => Strings.Pick("Rumænsk (RO)", "Romanian (RO)"),
            "0419" => Strings.Pick("Russisk (RU)", "Russian (RU)"),
            "0424" => Strings.Pick("Slovensk (SL)", "Slovenian (SL)"),
            "041B" => Strings.Pick("Slovakisk (SK)", "Slovak (SK)"),
            "040E" => Strings.Pick("Ungarsk (HU)", "Hungarian (HU)"),
            "041A" => Strings.Pick("Kroatisk (HR)", "Croatian (HR)"),
            "0422" => Strings.Pick("Ukrainsk (UK)", "Ukrainian (UK)"),
            "0425" => Strings.Pick("Estisk (ET)", "Estonian (ET)"),
            "0426" => Strings.Pick("Lettisk (LV)", "Latvian (LV)"),
            "0427" => Strings.Pick("Litauisk (LT)", "Lithuanian (LT)"),
            "0411" => Strings.Pick("Japansk (JA)", "Japanese (JA)"),
            "0412" => Strings.Pick("Koreansk (KO)", "Korean (KO)"),
            "0804" => Strings.Pick("Kinesisk (CN)", "Chinese (CN)"),
            "0404" => Strings.Pick("Kinesisk (TW)", "Chinese (TW)"),
            "0C04" => Strings.Pick("Kinesisk (HK)", "Chinese (HK)"),
            "1004" => Strings.Pick("Kinesisk (SG)", "Chinese (SG)"),
            "041E" => Strings.Pick("Thai (TH)", "Thai (TH)"),
            "042A" => Strings.Pick("Vietnamesisk (VI)", "Vietnamese (VI)"),
            "0401" => Strings.Pick("Arabisk (SA)", "Arabic (SA)"),
            "040D" => Strings.Pick("Hebræisk (HE)", "Hebrew (HE)"),
            "0429" => Strings.Pick("Persisk (FA)", "Persian (FA)"),
            "041F" => Strings.Pick("Tyrkisk (TR)", "Turkish (TR)"),
            _ => languageHex ?? string.Empty
        };
    }

    public static bool IsMicrosoftVendor(string? vendor) =>
        !string.IsNullOrWhiteSpace(vendor) && vendor.TrimStart().StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase);

    public static string MakeDisplayName(string? name, string? vendor)
    {
        var cleanName = string.IsNullOrWhiteSpace(name) ? Strings.UnknownVoice : name.Trim();
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
