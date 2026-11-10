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

        // A long-lived world can leave the surface a pit over a cave; the pair then stands inside
        // terrain and the interaction gate blocks every use. The case starts from a fresh run so
        // the two bodies spawn where the game puts them.
        var restarted = await context.Session.RestartRunAsync(cancellationToken).ConfigureAwait(false);
        context.Note("session: " + restarted);
        var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
        if (members.Length == 0)
        {
            throw new AssertionException("the fresh run names no member to treat");
        }

        context.UseMember(members[0]);
        context.Note("member: " + members[0]);

        // The chart opens on a closed view from a living viewer.
        var closed = await context.RecipeAsync("wound-read", cancellationToken, ("toggle", "off"));
        context.Note("pre-close: " + Raw(closed));
        var healed = await context.RecipeAsync("native-heal", cancellationToken);
        context.Note("host heal: " + Raw(healed));

        // The doctor carries the cream; the create path gives it a filled container (the give verb's
        // defect on the demo is gone on 1.0: either path now hands a full bottle, but create is the
        // one the case can assert without a slot scan).
        // The release path picks a carried item by slot; the case makes the cream the FIRST free
        // slot by emptying the bag through the player's own drop entry, so the item the release
        // carries is the cream and not a leftover from an earlier case.
        var bag = await context.RecipeAsync("body-items", cancellationToken);
        context.Read("body-items", bag);
        if (bag is { ValueKind: JsonValueKind.Object } bagElement
            && bagElement.TryGetProperty("ids", out var idsElement)
            && idsElement.ValueKind == JsonValueKind.String)
        {
            foreach (var entry in (idsElement.GetString() ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                var slot = entry.Split(':', 2)[0];
                if (int.TryParse(slot, out var slotIndex))
                {
                    await context.RecipeAsync("item-drop-hand", cancellationToken, ("slot", slot));
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        var cream = await context.RecipeAsync("item-provide", cancellationToken, ("type", "paincream"), ("mode", "create"));
        context.Read("paincream", cream);
        Require(Boolean(cream, "ok"), "the host could not take a paincream");

        // The interaction gate is a Ground linecast: the member joins at its own spawn and a floor
        // between the pair blocks the use. The fixture moves the member beside the doctor first.
        var host = await context.RecipeAsync("body-read", cancellationToken);
        context.Read("host-body", host);
        JsonElement localBody = default;
        if (host is { ValueKind: JsonValueKind.Object } hostElement && hostElement.TryGetProperty("localBody", out var bodyElement))
        {
            localBody = bodyElement;
        }
        else
        {
            throw new AssertionException("the host body reading has no localBody: " + Raw(host));
        }

        var hostX = Number(localBody, "positionX");
        var hostY = Number(localBody, "positionY");
        var moved = await context.MemberRecipeAsync("teleport", cancellationToken, ("x", (hostX + 1).ToString("0.###", CultureInfo.InvariantCulture)), ("y", hostY.ToString("0.###", CultureInfo.InvariantCulture)));
        context.Note("member teleport: " + Raw(moved));
        for (var settle = 0; settle < 12; settle++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            var memberBody = await context.MemberRecipeAsync("body-read", cancellationToken);
            if (memberBody is { ValueKind: JsonValueKind.Object } memberElement
                && memberElement.TryGetProperty("localBody", out var memberLocal)
                && memberLocal.TryGetProperty("positionX", out var memberX)
                && memberLocal.TryGetProperty("positionY", out var memberY)
                && Math.Abs(memberX.GetDouble() - (hostX + 1)) < 2
                && Math.Abs(memberY.GetDouble() - hostY) < 2)
            {
                context.Note("member settled beside the doctor after " + ((settle + 1) * 500) + " ms");
                break;
            }
        }

        // A freshly created container is filled by its own Awake on the next frame; a use request in
        // the same frame finds an empty bottle and is refused as "no usable consumable".
        await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);

        var opened = await context.RecipeAsync("medical-open", cancellationToken, ("steamId", context.MemberId), ("name", ""));
        context.Read("medical-open", opened);
        Require(Boolean(opened, "opened"), "the member's medical view did not open");

        // The member's own limb 0, before the dose.
        var before = await context.MemberRecipeAsync("limb-read", cancellationToken, ("source", "local"), ("limb", "3"), ("target", context.MemberId));
        context.Read("member-limb-before", before);
        var beforeDisinfection = Number(before, "disinfectionTime");

        // The release on the displayed limb; the case reads the adapter's own answer and then the
        // member's limb — the dose is the member's marker, not the host's rendering.
        var released = await context.RecipeAsync("remote-medical-release", cancellationToken, ("item", "paincream"), ("limb", "3"));
        context.Read("release", released);
        Require(Boolean(released, "ok"), "the release answered not-ok");

        await Task.Delay(TimeSpan.FromMilliseconds(900), cancellationToken).ConfigureAwait(false);
        var after = await context.MemberRecipeAsync("limb-read", cancellationToken, ("source", "local"), ("limb", "3"), ("target", context.MemberId));
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
