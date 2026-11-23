using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The give's container half (ticket `1.0-give-container-sound-unread`): the game plays
/// <c>backpack1</c>/<c>backpack2</c> when an item moves out of or into a container
/// (`Body.PickUpItem`, reversing 1.0.2 `Body.cs:1467-1483`), and a cross-player give that names a
/// container destination is exactly that move. A body-slot give is silent natively, so the row
/// stages the container case only: the member carries a trash bag, the host gives a bandage into
/// it, and both one-shot recorders are armed for the clip before the give.
///
/// <para>
/// The row asks the two questions the ticket left open: does the transfer land inside the
/// container, and does either client play the game's own clip for it. The recorders are read from
/// both sides and the failure names both lines, so a silent result says which side is silent
/// rather than only that something is wrong.
/// </para>
/// </summary>
internal sealed class GiveContainerSoundCase : AcceptanceCase
{
	public override string Name => "give-container-sound";

	public override CaseCost Cost => CaseCost.InRun;


	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The receiver's client performs the load into its own container, so it must stand beside
		// the giver and have the container on its body before the intent is sent.
		await context.WaitForCustodyAsync(cancellationToken).ConfigureAwait(false);
		await context.ClearMemberBodyAsync(cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken).ConfigureAwait(false);
		var hostBody = await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		context.Read("host-body", hostBody);
		var (hostX, hostY) = Position(hostBody);
		Require(hostX != 0 || hostY > 0, "the host's body has no position yet: " + Raw(hostBody));
		await context.MemberRecipeAsync("teleport", cancellationToken,
			("x", hostX.ToString(CultureInfo.InvariantCulture)),
			("y", hostY.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);

		// The container the give lands in: a trash bag on the member's own body. Its CUO instance id
		// is what the give names as the destination; `body-items` lists only slot:prefab, so the id
		// comes from the bag-instance recipe, which reads the item's own identity component.
		var bagItem = await context.MemberRecipeAsync("item-provide", cancellationToken,
			("type", "trashbag"),
			("mode", "create")).ConfigureAwait(false);
		context.Read("member bag provided", bagItem);
		Require(Boolean(bagItem, "ok"), "the member's trash bag was not created: " + Raw(bagItem));
		var bag = await context.MemberRecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		context.Read("member bag before", bag);
		var bagInstance = await context.MemberRecipeAsync("bag-instance", cancellationToken, ("type", "trashbag")).ConfigureAwait(false);
		context.Read("member bag instance", bagInstance);
		Require(Boolean(bagInstance, "ok"), "the member's trash bag has no instance id yet: " + Raw(bagInstance));
		var containerId = Number(bagInstance, "instanceId");
		Require(containerId != 0, "the member's bag instance id reads 0: " + Raw(bagInstance));

		// A real world copy of the given item, plus the pair re-placed after the drop's physics.
		var dropped = await context.RecipeAsync("item-drop", cancellationToken, ("type", "bandage"), ("height", "1")).ConfigureAwait(false);
		context.Read("dropped", dropped);
		Require(Boolean(dropped, "ok"), "the ground item was not created");
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);
		await context.MemberRecipeAsync("teleport", cancellationToken,
			("x", hostX.ToString(CultureInfo.InvariantCulture)),
			("y", hostY.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);

		// Both recorders are armed before the gesture: the clip belongs to the move itself, and the
		// native rule plays it on the client whose container the item leaves or enters.
		var hostArmed = await context.RecipeAsync("sound-log", cancellationToken,
			("mode", "start"),
			("match", "backpack")).ConfigureAwait(false);
		var memberArmed = await context.MemberRecipeAsync("sound-log", cancellationToken,
			("mode", "start"),
			("match", "backpack")).ConfigureAwait(false);
		context.Note("recorders armed: host " + Raw(hostArmed) + " member " + Raw(memberArmed));

		var give = await context.RecipeAsync("give-probe", cancellationToken,
			("mode", "give-world"),
			("target", context.MemberId),
			("slot", "-1"),
			("container", ((long)containerId).ToString(CultureInfo.InvariantCulture)),
			("type", "bandage")).ConfigureAwait(false);
		context.Read("give into the container", give);
		Require(Boolean(give, "ok"), "the give into the container was refused: " + Raw(give));

		// Read the member's bag again so the record shows where the item went, then poll both
		// recorders for the clip: the load runs on the receiving client after the intent's round trip.
		var memberBag = await context.MemberRecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		context.Read("member bag after", memberBag);
		var hostLine = Text(await context.RecipeAsync("sound-log", cancellationToken, ("mode", "read"), ("match", "backpack")).ConfigureAwait(false), "line");
		var memberLine = Text(await context.MemberRecipeAsync("sound-log", cancellationToken, ("mode", "read"), ("match", "backpack")).ConfigureAwait(false), "line");
		var deadline = Stopwatch.StartNew();
		while (!HeardBackpackClip(hostLine)
			&& !HeardBackpackClip(memberLine)
			&& deadline.Elapsed < TimeSpan.FromSeconds(8))
		{
			await Task.Delay(100, cancellationToken).ConfigureAwait(false);
			hostLine = Text(await context.RecipeAsync("sound-log", cancellationToken, ("mode", "read"), ("match", "backpack")).ConfigureAwait(false), "line");
			memberLine = Text(await context.MemberRecipeAsync("sound-log", cancellationToken, ("mode", "read"), ("match", "backpack")).ConfigureAwait(false), "line");
		}

		context.Note("host heard the container give: " + hostLine);
		context.Note("member heard the container give: " + memberLine);
		// The clip belongs to the client whose container the item enters: the receiver loads it into its own bag
		// (`Body.PickUpItem`), so "either client heard it" would pass a giver-only play that never reached the
		// receiver (2026-11-24). The giver's line stays a reading, not the judgement.
		Require(
			HeardBackpackClip(memberLine),
			"the receiving client played no backpack clip for a give into its container: member '" + memberLine
			+ "', host '" + hostLine + "'; the member's bag reads " + Raw(memberBag));
	}

	/// <summary>
	/// The game picks one of two clips (<c>backpack1</c>/<c>backpack2</c>, <c>PlayerCamera.PlayBackpackSound</c>'s
	/// <c>Random.Range(1, 3)</c>), so the recorder's line names one of them followed by the play time.
	/// </summary>
	private static bool HeardBackpackClip(string line) =>
		line.Contains("backpack1@", StringComparison.Ordinal) || line.Contains("backpack2@", StringComparison.Ordinal);

	private static double Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: 0;

	private static (double X, double Y) Position(JsonElement? reading)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("localBody", out var body)
			|| body.ValueKind != JsonValueKind.Object
			|| !body.TryGetProperty("positionX", out var x)
			|| !body.TryGetProperty("positionY", out var y)
			|| x.ValueKind != JsonValueKind.Number
			|| y.ValueKind != JsonValueKind.Number)
		{
			return (0, 0);
		}

		return (x.GetDouble(), y.GetDouble());
	}

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: string.Empty;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
