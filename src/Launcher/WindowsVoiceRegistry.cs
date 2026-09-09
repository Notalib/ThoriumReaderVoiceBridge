using Microsoft.Win32;
using Nota.ThoriumVoiceBridge;

namespace Nota.ThoriumVoiceBridge;

internal sealed class WindowsVoiceRegistry : IVoiceRegistry, IDisposable
{
    private const string SapiPath = @"SOFTWARE\Microsoft\Speech\Voices\Tokens";
    private const string OneCorePath = @"SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens";

    private readonly RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

    public IReadOnlyList<VoiceToken> GetVoices(VoiceRegistryHive hive)
    {
        using var root = baseKey.OpenSubKey(PathFor(hive));
        if (root is null)
        {
            return [];
        }

        return root.GetSubKeyNames()
            .Select(tokenName => ReadVoice(root, tokenName))
            .Where(voice => voice is not null)
            .Cast<VoiceToken>()
            .ToArray();
    }

    public VoiceToken? GetVoice(VoiceRegistryHive hive, string tokenName)
    {
        using var root = baseKey.OpenSubKey(PathFor(hive));
        return root is null ? null : ReadVoice(root, tokenName);
    }

    public void SetVoice(VoiceRegistryHive hive, VoiceToken voice)
    {
        using var root = baseKey.CreateSubKey(PathFor(hive), writable: true)
            ?? throw new InvalidOperationException("Kunne ikke åbne stemmeregisteret.");
        using var token = root.CreateSubKey(voice.TokenName, writable: true)
            ?? throw new InvalidOperationException("Kunne ikke oprette stemmetokenet.");
        using var attributes = token.CreateSubKey("Attributes", writable: true)
            ?? throw new InvalidOperationException("Kunne ikke oprette stemmens attributter.");

        token.SetValue(string.Empty, voice.DisplayName, RegistryValueKind.String);
        token.SetValue(voice.LanguageHex, voice.DisplayName, RegistryValueKind.String);
        SetOptional(token, "CLSID", voice.Clsid);
        SetOptional(attributes, "Age", voice.Age);
        SetOptional(attributes, "Gender", voice.Gender);
        attributes.SetValue("Language", voice.LanguageHex, RegistryValueKind.String);
        SetOptional(attributes, "Name", voice.Name);
        SetOptional(attributes, "Vendor", voice.Vendor);
        attributes.SetValue(VoiceBridgeService.BridgeMarkerName, VoiceBridgeService.BridgeMarkerValue, RegistryValueKind.String);
    }

    public bool DeleteVoice(VoiceRegistryHive hive, string tokenName)
    {
        using var root = baseKey.OpenSubKey(PathFor(hive), writable: true);
        if (root is null)
        {
            return true;
        }

        try
        {
            root.DeleteSubKeyTree(tokenName, throwOnMissingSubKey: false);
            using var remaining = root.OpenSubKey(tokenName);
            return remaining is null;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public void Dispose() => baseKey.Dispose();

    private static VoiceToken? ReadVoice(RegistryKey root, string tokenName)
    {
        using var token = root.OpenSubKey(tokenName);
        if (token is null)
        {
            return null;
        }

        using var attributes = token.OpenSubKey("Attributes");
        return new VoiceToken(
            TokenName: tokenName,
            Name: GetString(attributes, "Name"),
            Vendor: GetString(attributes, "Vendor"),
            Language: GetString(attributes, "Language"),
            Clsid: GetString(token, "CLSID"),
            Display: GetString(token, string.Empty) ?? GetString(attributes, "Name") ?? tokenName,
            Age: GetString(attributes, "Age"),
            Gender: GetString(attributes, "Gender"),
            ThoriumBridge: GetString(attributes, VoiceBridgeService.BridgeMarkerName));
    }

    private static string? GetString(RegistryKey? key, string name) =>
        key?.GetValue(name)?.ToString();

    private static void SetOptional(RegistryKey key, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            key.SetValue(name, value, RegistryValueKind.String);
        }
    }

    private static string PathFor(VoiceRegistryHive hive) => hive switch
    {
        VoiceRegistryHive.Sapi => SapiPath,
        VoiceRegistryHive.OneCore => OneCorePath,
        _ => throw new ArgumentOutOfRangeException(nameof(hive), hive, null)
    };
}
