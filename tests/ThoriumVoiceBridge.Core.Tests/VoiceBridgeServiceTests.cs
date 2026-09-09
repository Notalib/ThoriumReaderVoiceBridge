using Nota.ThoriumVoiceBridge;

namespace ThoriumVoiceBridge.Core.Tests;

public sealed class VoiceBridgeServiceTests
{
    [Fact]
    public void InstallCopiesVoiceAndMarksItAsBridged()
    {
        var registry = new InMemoryVoiceRegistry();
        var source = new VoiceToken("AcmeVoice", "Acme", "Acme Ltd", "1033", "{clsid}", "Acme", "2", "Female");
        registry.SetVoice(VoiceRegistryHive.Sapi, source);

        var result = new VoiceBridgeService(registry).InstallVoice("AcmeVoice");
        var installed = registry.GetVoice(VoiceRegistryHive.OneCore, "AcmeVoice");

        Assert.True(result.Changed);
        Assert.NotNull(installed);
        Assert.Equal("1033", installed.LanguageHex);
        Assert.Equal("Acme (Acme Ltd)", installed.Display);
        Assert.Equal(VoiceBridgeService.BridgeMarkerValue, installed.ThoriumBridge);
        Assert.Equal("{clsid}", installed.Clsid);
    }

    [Fact]
    public void InstallDoesNotOverwriteExistingOneCoreVoice()
    {
        var registry = new InMemoryVoiceRegistry();
        registry.SetVoice(VoiceRegistryHive.Sapi, new VoiceToken("Voice", "SAPI", "Vendor", "0406", null, null));
        registry.SetVoice(VoiceRegistryHive.OneCore, new VoiceToken("Voice", "Native", "Vendor", "0406", null, null));

        var result = new VoiceBridgeService(registry).InstallVoice("Voice");

        Assert.False(result.Changed);
        Assert.Equal("Native", registry.GetVoice(VoiceRegistryHive.OneCore, "Voice")!.Name);
    }

    [Fact]
    public void MicrosoftVoicesAreSkipped()
    {
        var registry = new InMemoryVoiceRegistry();
        registry.SetVoice(VoiceRegistryHive.Sapi, new VoiceToken("MicrosoftVoice", "David", "Microsoft Corporation", "0409", null, null));

        var result = new VoiceBridgeService(registry).InstallVoice("MicrosoftVoice");

        Assert.False(result.Changed);
        Assert.Null(registry.GetVoice(VoiceRegistryHive.OneCore, "MicrosoftVoice"));
    }

    [Fact]
    public void RemoveOnlyDeletesOurMarkedVoice()
    {
        var registry = new InMemoryVoiceRegistry();
        registry.SetVoice(VoiceRegistryHive.OneCore, new VoiceToken("Bridged", "Acme", "Acme", "0406", null, null, ThoriumBridge: "1"));
        registry.SetVoice(VoiceRegistryHive.OneCore, new VoiceToken("Native", "Windows", "Microsoft", "0409", null, null));
        var service = new VoiceBridgeService(registry);

        Assert.True(service.RemoveVoice("Bridged").Changed);
        Assert.False(service.RemoveVoice("Native").Changed);
        Assert.Null(registry.GetVoice(VoiceRegistryHive.OneCore, "Bridged"));
        Assert.NotNull(registry.GetVoice(VoiceRegistryHive.OneCore, "Native"));
    }

    [Theory]
    [InlineData("406", "0406")]
    [InlineData("0406", "0406")]
    [InlineData("1033", "1033")]
    [InlineData("", null)]
    public void NormalizeLanguagePreservesTheScriptRules(string input, string? expected)
    {
        Assert.Equal(expected, VoiceBridgeService.NormalizeLanguage(input));
    }

    private sealed class InMemoryVoiceRegistry : IVoiceRegistry
    {
        private readonly Dictionary<(VoiceRegistryHive Hive, string TokenName), VoiceToken> voices = new();

        public IReadOnlyList<VoiceToken> GetVoices(VoiceRegistryHive hive) =>
            voices.Where(item => item.Key.Hive == hive).Select(item => item.Value).ToArray();

        public VoiceToken? GetVoice(VoiceRegistryHive hive, string tokenName) =>
            voices.TryGetValue((hive, tokenName), out var voice) ? voice : null;

        public void SetVoice(VoiceRegistryHive hive, VoiceToken voice) =>
            voices[(hive, voice.TokenName)] = voice;

        public bool DeleteVoice(VoiceRegistryHive hive, string tokenName) =>
            voices.Remove((hive, tokenName));
    }
}
