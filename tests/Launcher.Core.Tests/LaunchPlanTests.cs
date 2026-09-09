using Nota.ThoriumVoiceBridge;

namespace Launcher.Core.Tests;

public class LaunchPlanTests
{
    private static readonly string AppDirectory =
        Path.Combine("C:", "Program Files", "Tilføj stemmer til Thorium");

    [Fact]
    public void ScriptPath_SitsNextToTheExecutable()
    {
        var plan = LaunchPlan.ForApplicationDirectory(AppDirectory);

        Assert.Equal(Path.Combine(AppDirectory, "ThoriumVoiceBridge.ps1"), plan.ScriptPath);
        Assert.Equal(AppDirectory, plan.WorkingDirectory);
    }

    [Fact]
    public void PowerShellArguments_PassTheScriptAsASingleUnquotedArgument()
    {
        var plan = LaunchPlan.ForApplicationDirectory(AppDirectory);
        var arguments = plan.PowerShellArguments;

        var fileIndex = arguments.ToList().IndexOf("-File");
        Assert.True(fileIndex >= 0, "-File switch is missing.");

        // ProcessStartInfo.ArgumentList quotes each entry, so the path must not be pre-quoted.
        var scriptArgument = arguments[fileIndex + 1];
        Assert.Equal(plan.ScriptPath, scriptArgument);
        Assert.DoesNotContain('"', scriptArgument);
    }

    [Fact]
    public void PowerShellArguments_SuppressProfileAndWindow()
    {
        var arguments = LaunchPlan.ForApplicationDirectory(AppDirectory).PowerShellArguments;

        Assert.Contains("-NoProfile", arguments);
        Assert.Contains("-NonInteractive", arguments);
        Assert.Equal("Hidden", ValueAfter(arguments, "-WindowStyle"));
        Assert.Equal("Bypass", ValueAfter(arguments, "-ExecutionPolicy"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ForApplicationDirectory_RejectsAMissingDirectory(string? directory)
    {
        Assert.ThrowsAny<ArgumentException>(() => LaunchPlan.ForApplicationDirectory(directory!));
    }

    private static string ValueAfter(IReadOnlyList<string> arguments, string name)
    {
        var index = arguments.ToList().IndexOf(name);
        Assert.True(index >= 0, $"{name} switch is missing.");
        return arguments[index + 1];
    }
}
