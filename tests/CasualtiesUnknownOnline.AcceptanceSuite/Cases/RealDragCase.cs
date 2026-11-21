using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The real-drag row: a player's own hand path — pick an item out of the bag with the game's drag gesture,
/// move the pointer onto the wound view's limb diagram, release — starts the item's own treatment.
///
/// The chain read before the fixture: <c>PlayerCamera.TryPickupFromUI</c> fills <c>dragItem</c> when the
/// <c>iteminteract</c> key fires over a slot; <c>drag-start</c> reproduces exactly that state. The release runs
/// <c>PlayerCamera.HandleReleaseDragging</c> → <c>TryPerformUIActions</c>, which on the wound view's limb image
/// opens the item's own minigame (<c>BandageMinigame</c>) — the same entry <c>limb-use mode=use</c> reaches, but
/// here through the pointer. <c>wound-limb-points</c> reports where the game puts each limb rect (GUI coordinates,
/// origin top-left); the virtual pointer uses the game's own screen coordinates (origin bottom-left), so the row
/// converts with the screen height it reads from <c>screen-size</c>.
///
/// The seeded wound is a setup substitution (<c>wound-seed</c>); the drag, the release and the minigame are the
/// game's own. Asserting the minigame is what names the difference between "the item moved" and "the hand path
/// reached the treatment": a release that merely dropped the item starts no minigame.
/// </summary>
internal sealed class RealDragCase : AcceptanceCase
{
	public override string Name => "real-drag";

	public override CaseCost Cost => CaseCost.InRun;


	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);

		// The game's own Escape closes the CUO window; the wound view's open path needs the pointer free.
		await CloseOverlayAsync(context, cancellationToken).ConfigureAwait(false);

		var revived = await context.RecipeAsync("body-force", cancellationToken,
			("sleep", "0"), ("consciousness", "100"), ("energy", "100"), ("brainHealth", "100"),
			("badSleepAmount", "0"), ("idleTime", "0"), ("intelligence", "10"),
			("heartRate", "80"), ("bloodPressure", "120"), ("bloodOxygen", "98"), ("strength", "10")).ConfigureAwait(false);
		Require(Boolean(revived, "ok"), "the host body could not be made conscious: " + Raw(revived));

		var seeded = await context.RecipeAsync("wound-seed", cancellationToken, ("limb", "2"), ("force", "200"), ("infection", "0")).ConfigureAwait(false);
		context.Read("seeded", seeded);
		Require(Boolean(seeded, "ok") && NumberValue(seeded, "bleedAmount") > 0.001,
			"the limb did not take the seeded wound: " + Raw(seeded));

		var selected = await context.RecipeAsync("select-limb", cancellationToken, ("limb", "2")).ConfigureAwait(false);
		Require(Boolean(selected, "ok"), "the limb could not be selected: " + Raw(selected));

		var opened = await context.RecipeAsync("open-own-wound", cancellationToken).ConfigureAwait(false);
		context.Read("wound-view", opened);
		Require(Boolean(opened, "open"), "the host's own wound view did not open: " + Raw(opened));

		var granted = await context.RecipeAsync("item-provide", cancellationToken, ("type", "bandage"), ("mode", "create")).ConfigureAwait(false);
		Require(Boolean(granted, "ok"), "the bandage could not be granted: " + Raw(granted));

		var screen = await context.RecipeAsync("screen-size", cancellationToken).ConfigureAwait(false);
		var height = NumberValue(screen, "height");
		Require(height > 0f, "the screen height was not readable: " + Raw(screen));

		var points = await context.RecipeAsync("wound-limb-points", cancellationToken).ConfigureAwait(false);
		context.Read("limb-points", points);
		var point = LimbPoint(points, limb: 2);
		Require(point.X > 0f && point.Y > 0f, "the wound view did not report the limb's own rect: " + Raw(points));

		var gameX = point.X.ToString("0", CultureInfo.InvariantCulture);
		var gameY = (height - point.Y).ToString("0", CultureInfo.InvariantCulture);

		var started = await context.RecipeAsync("drag-start", cancellationToken, ("type", "bandage")).ConfigureAwait(false);
		context.Read("drag-start", started);
		Require(Boolean(started, "ok"), "the game's drag gesture could not pick the bandage up: " + Raw(started));

		var moved = await context.RecipeAsync("virtual-input", cancellationToken, ("mode", "move"), ("x", gameX), ("y", gameY), ("button", "0")).ConfigureAwait(false);
		Require(Boolean(moved, "ok"), "the staged pointer did not move onto the limb: " + Raw(moved));

		var released = await context.RecipeAsync("drag-release", cancellationToken, ("x", gameX), ("y", gameY)).ConfigureAwait(false);
		context.Read("drag-release", released);
		Require(Boolean(released, "ok"), "the release was not answered: " + Raw(released));
		Require(Text(released, "hadItem").Length > 0, "the release path did not see a dragged item: " + Raw(released));

		// The treatment itself: the game opened the bandage minigame from the wound view. A release that only
		// dropped the item on the ground reports a world mode and leaves no minigame.
		string? minigame = null;
		for (var attempt = 0; attempt < 20 && minigame is null; attempt++)
		{
			var live = await context.RecipeAsync("minigame-live", cancellationToken).ConfigureAwait(false);
			var kind = Text(live, "kind");
			if (Boolean(live, "hasMain") && string.Equals(kind, "BandageMinigame", StringComparison.Ordinal))
			{
				minigame = kind;
				context.Read("minigame", live);
			}
			else
			{
				await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
			}
		}

		// Cancel the treatment and close the view so the shared pair returns to its normal surface.
		try
		{
			await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "1")).ConfigureAwait(false);
			await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "0")).ConfigureAwait(false);
		}
		finally
		{
			Require(minigame is not null, "the drag onto the wound view did not start the bandage treatment: " + Raw(released));
		}
	}

	private static async Task CloseOverlayAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var state = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		if (!IsTrue(state, "windowVisible"))
		{
			return;
		}

		await context.Session.ActionAsync(context.Session.HostUrl, "click", cancellationToken, ("-Control", "window.close")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
	}

	private static (float X, float Y) LimbPoint(JsonElement? reading, int limb)
	{
		var text = Text(reading, "limbs");
		foreach (var entry in text.Split('|', StringSplitOptions.RemoveEmptyEntries))
		{
			var parts = entry.Split(':');
			if (parts.Length >= 3 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index == limb
				&& float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
				&& float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
			{
				return (x, y);
			}
		}

		return (0f, 0f);
	}

	private static bool IsTrue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.True;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.True;

	private static float NumberValue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.Number
			? (float)propertyValue.GetDouble()
			: float.NaN;

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue)
			? propertyValue.ValueKind == JsonValueKind.String ? propertyValue.GetString() ?? string.Empty : propertyValue.ToString()
			: string.Empty;

	private static string Raw(JsonElement? reading) => reading?.ToString() ?? "(null)";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
