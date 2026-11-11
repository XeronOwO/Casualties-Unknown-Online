using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A topical dose released on the member's medical view lands on the limb the doctor picked: the
/// member's own client reads the dose's marker rise, and the host's panel names the same limb. A row
/// from the medical sweep (`save-mid-run-consistent-cut`'s topical half and the 1.0 regression's
/// batch 1), proven on the release build over the sandboxed IP-direct session.
///
/// <para>
/// The case stages nothing the game would not do: the two players stand where they spawned, the
/// doctor carries a cream, and the view does the work. It waits for BOTH clients to report they are
/// in the world before it reads anything — an action taken mid-load reads the pod's inside on one
/// side and the surface on the other, which is what made an earlier version blame the terrain.
/// </para>
/// </summary>
internal sealed class TopicalDoseCase : AcceptanceCase
{
	public override string Name => "medical-dose";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// Both sides must be in the world and the host's start gate released before any reading:
		// the two clients join at different moments, so the pair is only comparable once both land.
		var hostReady = await context.Session
			.WaitUntilAsync(context.Session.HostUrl, state => SessionDriver.StateTrue(state, "inWorld") && !SessionDriver.StateTrue(state, "gateWaiting"), TimeSpan.FromSeconds(60), cancellationToken)
			.ConfigureAwait(false);
		context.Read("host-state", hostReady);
		Require(SessionDriver.StateTrue(hostReady, "inWorld"), "the host is not in a world");

		var memberReady = await context.Session
			.WaitUntilAsync(context.Session.MemberUrl, state => SessionDriver.StateTrue(state, "inWorld"), TimeSpan.FromSeconds(60), cancellationToken)
			.ConfigureAwait(false);
		context.Read("member-state", memberReady);
		Require(SessionDriver.StateTrue(memberReady, "inWorld"), "the member is not in the world");

		var member = await context.MemberAsync(cancellationToken).ConfigureAwait(false);
		context.Note("member: " + member);

		// The chart opens on a closed view from a living viewer.
		var closed = await context.RecipeAsync("wound-read", cancellationToken, ("toggle", "off"));
		context.Note("pre-close: " + Raw(closed));
		var healed = await context.RecipeAsync("native-heal", cancellationToken);
		context.Note("host heal: " + Raw(healed));

		// The doctor carries the cream; the release path picks a carried item by slot, so the bag is
		// emptied through the player's own drop entry and the cream becomes the item the release finds.
		await context.ClearBodyAsync(cancellationToken).ConfigureAwait(false);

		var cream = await context.RecipeAsync("item-provide", cancellationToken, ("type", "paincream"), ("mode", "create"));
		context.Read("paincream", cream);
		Require(Boolean(cream, "ok"), "the host could not take a paincream");

		// A freshly created container is filled by its own Awake on the next frame; a use request in
		// the same frame finds an empty bottle and is refused as "no usable consumable".
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);

		// Where the two actually stand: the reading that names a blocked use later, if it happens.
		var host = await context.WaitForHostBodyAsync(cancellationToken);
		context.Read("host-body", host);
		var memberBody = await context.MemberRecipeAsync("body-read", cancellationToken);
		context.Read("member-body", memberBody);

		// The member joined a running world and stands at its own pod; the medical view and the use
		// request both need the member within the host's reach, so it is placed on the host's point.
		var (hostX, hostY) = Position(host);
		Require(hostX != 0 || hostY > 0, "the host's body has no position yet: " + Raw(host));
		var placed = await context.MemberRecipeAsync("teleport", cancellationToken,
			("x", hostX.ToString(CultureInfo.InvariantCulture)),
			("y", hostY.ToString(CultureInfo.InvariantCulture)));
		context.Note("member placed: " + Raw(placed));
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);

		var opened = await context.RecipeAsync("medical-open", cancellationToken, ("steamId", context.MemberId), ("name", ""));
		if (!Boolean(opened, "opened"))
		{
			// The chart case closes its own view; the game can still be in the close animation when
			// this case asks for a fresh open. One clean close and a second ask is the game's path.
			context.Note("the first open answered false: " + Raw(opened));
			await context.RecipeAsync("wound-read", cancellationToken, ("toggle", "off"));
			await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);
			opened = await context.RecipeAsync("medical-open", cancellationToken, ("steamId", context.MemberId), ("name", ""));
		}

		context.Read("medical-open", opened);
		Require(Boolean(opened, "opened"), "the member's medical view did not open");

		// The member's own limb, before the dose.
		var before = await context.MemberRecipeAsync("limb-read", cancellationToken, ("source", "local"), ("limb", "3"), ("target", context.MemberId));
		context.Read("member-limb-before", before);
		var beforeDisinfection = Number(before, "disinfectionTime");

		// The release on the displayed limb; the case reads the adapter's own answer and then the
		// member's limb — the dose is the member's marker, not the host's rendering.
		var released = await context.RecipeAsync("remote-medical-release", cancellationToken, ("item", "paincream"), ("limb", "3"));
		context.Read("release", released);
		Require(Boolean(released, "ok"), "the release answered not-ok");

		// The game SETS the limb's disinfection from the dose (10 ml -> `SetDisinfect(300 * clamp01(ml * 0.1))`),
		// so the reading is the absolute marker the formula names, not a rise: a limb an earlier case
		// already treated starts high, and a rise-only check would fail on a correct dose (measured:
		// 263.173 -> 297.236, a correct 10 ml dose refused by the old assertion).
		JsonElement? after = null;
		var afterDisinfection = beforeDisinfection;
		for (var settle = 0; settle < 12; settle++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			after = await context.MemberRecipeAsync("limb-read", cancellationToken, ("source", "local"), ("limb", "3"), ("target", context.MemberId));
			afterDisinfection = Number(after, "disinfectionTime");
			if (afterDisinfection >= 290)
			{
				break;
			}
		}

		context.Read("member-limb-after", after);

		// A 10 ml relief cream sets the marker to about 300; 290 leaves room for a partly-consumed
		// bottle while nothing-at-all cannot reach it.
		Require(afterDisinfection >= 290, $"the dose did not reach the member's limb (disinfection {beforeDisinfection} -> {afterDisinfection})");
	}

	private static (double X, double Y) Position(JsonElement? reading)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("localBody", out var body)
			|| body.ValueKind != JsonValueKind.Object)
		{
			return (0, 0);
		}

		var x = body.TryGetProperty("positionX", out var px) && px.TryGetDouble(out var dx) ? dx : 0;
		var y = body.TryGetProperty("positionY", out var py) && py.TryGetDouble(out var dy) ? dy : 0;
		return (x, y);
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
