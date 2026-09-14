namespace Nota.ThoriumVoiceBridge;

public enum VoiceRegistryHive
{
    Sapi,
    OneCore
}

public sealed record VoiceToken(
    string TokenName,
    string? Name,
    string? Vendor,
    string? Language,
    string? Clsid,
    string? Display,
    string? Age = null,
    string? Gender = null,
    string? ThoriumBridge = null)
{
    public string LanguageHex => VoiceBridgeService.NormalizeLanguage(Language) ?? "0000";

    public bool IsMicrosoft => VoiceBridgeService.IsMicrosoftVendor(Vendor);

    public bool IsBridged => ThoriumBridge == VoiceBridgeService.BridgeMarkerValue;

    public string DisplayName => VoiceBridgeService.MakeDisplayName(Name ?? Display, Vendor);
}

public interface IVoiceRegistry
{
    IReadOnlyList<VoiceToken> GetVoices(VoiceRegistryHive hive);

    VoiceToken? GetVoice(VoiceRegistryHive hive, string tokenName);

    void SetVoice(VoiceRegistryHive hive, VoiceToken voice);

    bool DeleteVoice(VoiceRegistryHive hive, string tokenName);
}
