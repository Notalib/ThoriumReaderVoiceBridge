namespace Nota.ThoriumVoiceBridge;

/// <summary>User-visible text. Danish on Danish Windows, English everywhere else.</summary>
public static class Strings
{
    // Set once at startup from the Windows display language.
    public static bool Danish { get; set; } = true;

    public static string Pick(string danish, string english) => Danish ? danish : english;

    public static string AppTitle => Pick("Tilføj stemmer til Thorium", "Add voices to Thorium");
    public static string AdminRequired => Pick("Programmet skal køres som Administrator.", "The program must be run as administrator.");
    public static string AdminRequiredCaption => Pick("Mangler rettigheder", "Missing permissions");

    public static string Status => Pick(
        "Tilføj eller fjern oplæsningsstemmer, så de kan bruges i Thorium Reader.",
        "Add or remove text-to-speech voices so they can be used in Thorium Reader.");
    public static string DanishOnly => Pick("Vis kun Dansk", "Show Danish only");
    public static string NotInThorium => Pick("Ikke i Thorium", "Not in Thorium");
    public static string InThorium => Pick("I Thorium", "In Thorium");
    public static string ColumnName => Pick("Navn", "Name");
    public static string ColumnVendor => Pick("Leverandør", "Vendor");
    public static string ColumnLanguage => Pick("Sprog", "Language");
    public static string MoveButton => Pick("Flyt\n→", "Add\n→");
    public static string RemoveButton => Pick("←\nFjern", "←\nRemove");
    public static string VoiceAdded => Pick(
        "Stemmen er tilføjet til Thorium. Genstart Thorium for at bruge den nye stemme.",
        "The voice was added to Thorium. Restart Thorium to use the new voice.");
    public static string VoiceRemoved => Pick(
        "Stemmen er fjernet fra Thorium. Genstart Thorium for at bruge ændringen.",
        "The voice was removed from Thorium. Restart Thorium to apply the change.");

    public static string Refreshed(int notInThorium, int inThorium) => Pick(
        $"Opdateret. Ikke i Thorium: {notInThorium} | I Thorium: {inThorium}",
        $"Refreshed. Not in Thorium: {notInThorium} | In Thorium: {inThorium}");
    public static string Error(string tokenName, string message) => Pick($"FEJL ({tokenName}): {message}", $"ERROR ({tokenName}): {message}");

    public static string SapiTokenMissing(string tokenName) => Pick($"SAPI token findes ikke: {tokenName}", $"SAPI token does not exist: {tokenName}");
    public static string OneCoreTokenMissing(string tokenName) => Pick($"OneCore-token findes ikke: {tokenName}", $"OneCore token does not exist: {tokenName}");
    public static string MicrosoftVoiceSkipped => Pick("Microsoft-stemmer kan ikke flyttes.", "Microsoft voices cannot be added.");
    public static string AlreadyInThorium => Pick("Stemmen findes allerede i Thorium.", "The voice is already in Thorium.");
    public static string CannotRemoveNative => Pick("Native/ukendte OneCore-stemmer kan ikke fjernes.", "Native or unknown OneCore voices cannot be removed.");
    public static string RemoveFailed => Pick("Stemmen kunne ikke fjernes fra Thorium.", "The voice could not be removed from Thorium.");
    public static string UnknownVoice => Pick("Ukendt", "Unknown");

    public static string RegistryOpenFailed => Pick("Kunne ikke åbne stemmeregisteret.", "Could not open the voice registry.");
    public static string TokenCreateFailed => Pick("Kunne ikke oprette stemmetokenet.", "Could not create the voice token.");
    public static string AttributesCreateFailed => Pick("Kunne ikke oprette stemmens attributter.", "Could not create the voice attributes.");
}
