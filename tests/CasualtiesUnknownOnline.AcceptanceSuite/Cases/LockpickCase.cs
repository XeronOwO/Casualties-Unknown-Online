using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The lockpick minigame completes through the game's own entry: the host opens the nearest lockable,
/// aims the hand at the picked angle, holds the attack key, and the game's own completion sound is the
/// reading.
/// </summary>
internal sealed class LockpickCase : AcceptanceCase
{
    public override string Name => "lockpick";

    public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
    {
        // The CUO window covers the screen centre; the game's own Escape closes it.
        await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "1")).ConfigureAwait(false);
        await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "0")).ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);

        var opened = await context.RecipeAsync("lockpick-open", cancellationToken).ConfigureAwait(false);
        context.Read("open", opened);
        Require(Boolean(opened, "ok"), "the lockpick minigame did not open");

        var aim = await context.RecipeAsync("lockpick-aim", cancellationToken).ConfigureAwait(false);
        context.Read("aim", aim);
        Require(Boolean(aim, "ok"), "the aim did not compute");

        var x = NumberText(aim, "screenX");
        var y = NumberText(aim, "screenY");
        var moved = await context.RecipeAsync("virtual-input", cancellationToken,
            ("mode", "move"), ("x", x), ("y", y), ("button", "0")).ConfigureAwait(false);
        context.Read("moved", moved);
        // The minigame's hand springs toward the pointer over frames; let it settle on the ring.
        await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken).ConfigureAwait(false);

        var pressed = await context.RecipeAsync("virtual-input", cancellationToken,
            ("mode", "press-key"), ("x", "0"), ("y", "0"), ("button", "0")).ConfigureAwait(false);
        context.Read("pressed", pressed);
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);

        var live = await context.RecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
        context.Read("live", live);

        var sounds = await context.RecipeAsync("sound-log", cancellationToken, ("mode", "read"), ("match", "unlock")).ConfigureAwait(false);
        context.Read("sounds", sounds);
        var line = sounds is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty("line", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
        Require(line.Contains("unlock@", StringComparison.Ordinal), $"the game did not play its unlock sound: {line}");
    }

    private static string NumberText(JsonElement? reading, string property) =>
        reading is { ValueKind: JsonValueKind.Object } element
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.Number
            ? ((int)Math.Round(value.GetDouble())).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : throw new AssertionException($"the reading has no number '{property}'");

    private static bool Boolean(JsonElement? reading, string property) =>
        reading is { ValueKind: JsonValueKind.Object } element
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.True;

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new AssertionException(message);
        }
    }
}
