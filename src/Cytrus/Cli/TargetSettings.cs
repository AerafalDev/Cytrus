using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Cytrus.Cli;

public class TargetSettings : CommandSettings
{
    [CommandOption("-g|--game <GAME>")]
    [Description("Game to target (dofus, retro, wakfu, ...).")]
    [DefaultValue("dofus")]
    public string Game { get; init; } = "dofus";

    [CommandOption("-p|--platform <PLATFORM>")]
    [Description("Platform (windows, darwin, linux).")]
    [DefaultValue("windows")]
    public string Platform { get; init; } = "windows";

    [CommandOption("-r|--release <RELEASE>")]
    [Description("Release channel (main, beta, dofus3, ...).")]
    [DefaultValue("main")]
    public string Release { get; init; } = "main";

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Game))
            return ValidationResult.Error("--game is required.");

        if (string.IsNullOrWhiteSpace(Platform))
            return ValidationResult.Error("--platform is required.");

        if (string.IsNullOrWhiteSpace(Release))
            return ValidationResult.Error("--release is required.");

        return ValidationResult.Success();
    }
}
