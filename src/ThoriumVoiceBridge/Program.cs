using System.Runtime.InteropServices;

namespace Nota.ThoriumVoiceBridge;

internal static partial class Program
{
    private const int LangDanish = 0x06;

    private static int Main()
    {
        Strings.Danish = IsDanishWindows();

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var registry = new WindowsVoiceRegistry();
        var service = new VoiceBridgeService(registry);
        try
        {
            Application.Run(new MainForm(service));
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(Strings.AdminRequired, Strings.AdminRequiredCaption,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 5;
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, Strings.AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    // InvariantGlobalization hides the OS language from CultureInfo, so ask Windows directly.
    // The low 10 bits of a LANGID are the primary language, which covers every Danish locale.
    private static bool IsDanishWindows() => (GetUserDefaultUILanguage() & 0x3FF) == LangDanish;

    [LibraryImport("kernel32.dll")]
    private static partial ushort GetUserDefaultUILanguage();
}
