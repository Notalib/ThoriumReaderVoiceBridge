using System.Diagnostics;

namespace Nota.ThoriumVoiceBridge;

/// <summary>
/// Works out how the PowerShell application should be invoked. Deliberately free of
/// Windows-only APIs so the logic can be unit tested on any platform.
/// </summary>
public sealed class LaunchPlan
{
    public const string ScriptFileName = "ThoriumVoiceBridge.ps1";

    private LaunchPlan(string workingDirectory, string scriptPath)
    {
        WorkingDirectory = workingDirectory;
        ScriptPath = scriptPath;
    }

    public string WorkingDirectory { get; }

    public string ScriptPath { get; }

    /// <summary>
    /// Arguments for powershell.exe, unquoted. Quoting is left to
    /// <see cref="ProcessStartInfo.ArgumentList"/>, which applies the Windows rules
    /// correctly for install paths containing spaces and non-ASCII characters.
    /// </summary>
    public IReadOnlyList<string> PowerShellArguments =>
    [
        "-NoProfile",
        "-NonInteractive",
        // Bypass is safe here: the script sits next to the exe in Program Files and is only writable by administrators.
        "-ExecutionPolicy", "Bypass",
        "-WindowStyle", "Hidden",
        "-File", ScriptPath
    ];

    public static LaunchPlan ForApplicationDirectory(string applicationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);

        return new LaunchPlan(
            applicationDirectory,
            Path.Combine(applicationDirectory, ScriptFileName));
    }
}
