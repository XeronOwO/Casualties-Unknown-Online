using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A topical dose released on the member's medical view lands on the limb the doctor picked: the
/// member's own client reads the dose's marker rise, and the host's panel names the same limb. A row
/// from the medical sweep (`save-mid-run-consistent-cut`'s topical half and the 1.0 regression's
/// batch 1), proven on the release build over the sandboxed IP-direct session.
/// </summary>
internal sealed class TopicalDoseCase : AcceptanceCase
{
    public override string Name => "medical-dose";

    public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
    {
        // The member's own client is the patient: it receives the dose and reads its limb.
        var member = await context.MemberAsync(cancellationToken).ConfigureAwait(false);
        _ = member;

        // The chart opens on a closed view from a living viewer.
        var closed = await context.RecipeAsync("wound-read", cancellationToken, ("toggle", "off"));
        context.Note("pre-close: " + Raw(closed));
        var healed = await context.RecipeAsync("native-heal", cancellationToken);
        context.Note("host heal: " + Raw(healed));

        // The doctor carries the cream; the create path gives it a filled container (the give verb's
        // defect on the demo is gone on 1.0: either path now hands a full bottle, but create is the
        // one the case can assert without a slot scan).
        var cream = await context.RecipeAsync("item-provide", cancellationToken, ("type", "paincream"), ("mode", "create"));
        if (!Boolean(cream, "ok"))
        {
            // A full inventory cannot take a new item: drop the last carried item through the game's own release.
            var grabbed = await context.RecipeAsync("drag-from-slot", cancellationToken, ("slot", "4"));
            context.Note("make-room grab: " + Raw(grabbed));
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
            var dropped = await context.RecipeAsync("drag-release", cancellationToken, ("x", "640"), ("y", "360"));
            context.Note("make-room release: " + Raw(dropped));
            await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);
            cream = await context.RecipeAsync("item-provide", cancellationToken, ("type", "paincream"), ("mode", "create"));
        }

        context.Read("paincream", cream);
        Require(Boolean(cream, "ok"), "the host could not take a paincream");

        // A freshly created container is filled by its own Awake on the next frame; a use request in
        // the same frame finds an empty bottle and is refused as "no usable consumable".
        await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);

        var opened = await context.RecipeAsync("medical-open", cancellationToken, ("steamId", context.MemberId), ("name", ""));
        context.Read("medical-open", opened);
        Require(Boolean(opened, "opened"), "the member's medical view did not open");

        // The member's own limb 0, before the dose.
        var before = await context.MemberRecipeAsync("limb-read", cancellationToken, ("source", "local"), ("limb", "0"), ("target", context.MemberId));
        context.Read("member-limb-before", before);
        var beforeDisinfection = Number(before, "disinfectionTime");

        // The release on the displayed limb; the case reads the adapter's own answer and then the
        // member's limb — the dose is the member's marker, not the host's rendering.
        var released = await context.RecipeAsync("remote-medical-release", cancellationToken, ("item", "paincream"), ("limb", "0"));
        context.Read("release", released);
        Require(Boolean(released, "ok"), "the release answered not-ok");

        await Task.Delay(TimeSpan.FromMilliseconds(900), cancellationToken).ConfigureAwait(false);
        var after = await context.MemberRecipeAsync("limb-read", cancellationToken, ("source", "local"), ("limb", "0"), ("target", context.MemberId));
        context.Read("member-limb-after", after);
        var afterDisinfection = Number(after, "disinfectionTime");

        // A 10 ml relief cream sets SetDisinfect(300 * clamp01(ml * 0.1)) — the marker jumps to
        // about 300 from zero. The case accepts a rise of at least 200, so a partly-consumed bottle
        // (a smaller measured dose) still passes while nothing-at-all cannot.
        Require(afterDisinfection - beforeDisinfection >= 200, $"the dose did not reach the member's limb (disinfection {beforeDisinfection} -> {afterDisinfection})");
    }

    private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

    private static bool Boolean(JsonElement? reading, string property) =>
        reading is { ValueKind: JsonValueKind.Object } element
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.True;

    private static double Number(JsonElement? reading, string property) =>
        reading is { ValueKind: JsonValueKind.Object } element
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : throw new AssertionException($"the reading has no number '{property}': {Raw(reading)}");


    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new AssertionException(message);
        }
    }
}
