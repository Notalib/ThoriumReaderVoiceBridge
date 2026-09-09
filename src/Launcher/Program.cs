using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Nota.ThoriumVoiceBridge;

/// <summary>
/// Elevated, windowless launcher for the PowerShell application. The app edits HKLM
/// speech tokens, so app.manifest forces a UAC prompt before this runs.
/// </summary>
internal static partial class Program
{
    private const string Caption = "Tilføj stemmer til Thorium";

    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONERROR = 0x00000010;
    private const uint MB_SETFOREGROUND = 0x00010000;

    private static int Main()
    {
        // Correct under single-file publish, unlike Assembly.Location.
        var plan = LaunchPlan.ForApplicationDirectory(AppContext.BaseDirectory);

        if (!File.Exists(plan.ScriptPath))
        {
            ShowError($"""
                Kunne ikke finde {LaunchPlan.ScriptFileName}.

                Forventet placering:
                {plan.ScriptPath}

                Installationen ser ud til at være beskadiget. Installer programmet igen.
                """);
            return 2;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = plan.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in plan.PowerShellArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                ShowError("Windows PowerShell kunne ikke startes.");
                return 3;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            ShowError($"""
                Windows PowerShell kunne ikke startes.

                {ex.Message}
                """);
            return 3;
        }
    }

    private static void ShowError(string message) =>
        MessageBox(IntPtr.Zero, message, Caption, MB_OK | MB_ICONERROR | MB_SETFOREGROUND);

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
