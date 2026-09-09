namespace Nota.ThoriumVoiceBridge;

internal static class Program
{
    private static int Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var registry = new WindowsVoiceRegistry();
        var service = new VoiceBridgeService(registry);
        try
        {
            Application.Run(new MainForm(service, AppContext.BaseDirectory));
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show("Programmet skal køres som Administrator.", "Mangler rettigheder",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 5;
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Tilføj stemmer til Thorium",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
