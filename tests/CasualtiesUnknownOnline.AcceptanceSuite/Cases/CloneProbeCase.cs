using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The host's view of a teammate. The game itself has no second player, so the body the host draws for the member is CUO's
/// own clone; the player's claim is that the teammate is where they really are and that the picture follows them when they
/// walk. The old row only asked whether a marker existed — it never compared the clone with the member's own position
/// (fault sheet, 2026-11-23), so a clone frozen at the spawn point passed.
/// </summary>
internal sealed class CloneProbeCase : AcceptanceCase
{
	private const double PlacementTolerance = 1.5;

	public override string Name => "clone-probe";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		_ = await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);

		var placed = await WaitForCloneAtMemberAsync(context, cancellationToken).ConfigureAwait(false);
		Require(placed is not null, "the host's copy of the teammate never stood where the teammate stands: " + Raw(placed));

		var components = StringValue(placed, "components");
		Require(components.Contains("RemoteCloneRender", StringComparison.Ordinal) || components.Contains("RemoteBodyDriver", StringComparison.Ordinal),
			"the host's copy of the teammate does not carry CUO's own marker: " + Trim(components));

		var facing = await context.RecipeAsync("remote-clone-probe", cancellationToken, ("mode", "facing")).ConfigureAwait(false);
		context.Read("remote-clone-probe-facing", facing);
		Require(Boolean(facing, "ok"), "the clone's facing probe did not answer ok: " + Raw(facing));

		// The member walks; the host's picture has to follow. The member's own movement key is held for a moment, the way a
		// player holds a direction, and the row waits for the picture instead of reading it on one frame.
		var before = Number(placed, "x");
		var walked = await WalkMemberAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("member after the walk", walked);
		var moved = await WaitForCloneAwayAsync(context, before, cancellationToken).ConfigureAwait(false);
		Require(moved is not null, "the host's copy of the teammate stayed put while the teammate walked: before x " + before + ", after " + Raw(moved));

		var after = Number(moved, "x");
		context.Note($"clone x {before} -> {after} while the member walked");
	}

	// The clone must stand where the member's own body says it stands; the row waits for the display to catch up rather
	// than reading one frame of a scene that is still settling.
	private static async Task<JsonElement?> WaitForCloneAtMemberAsync(CaseContext context, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var attempt = 0; attempt < 20; attempt += 1)
		{
			var member = await context.MemberRecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
			last = await context.RecipeAsync("remote-clone-probe", cancellationToken, ("mode", "where")).ConfigureAwait(false);
			var memberX = Number(member, "posX");
			var memberY = Number(member, "posY");
			var cloneX = Number(last, "x");
			var cloneY = Number(last, "y");
			if (memberX is not null && memberY is not null && cloneX is not null && cloneY is not null
				&& Math.Abs(cloneX.Value - memberX.Value) <= PlacementTolerance
				&& Math.Abs(cloneY.Value - memberY.Value) <= PlacementTolerance)
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return null;
	}

	private static async Task<JsonElement?> WalkMemberAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var down = await context.MemberRecipeAsync("stage-bind", cancellationToken, ("bind", "right"), ("action", "down")).ConfigureAwait(false);
		Require(Boolean(down, "ok"), "the member's walk key could not be staged down: " + Raw(down));
		await Task.Delay(TimeSpan.FromMilliseconds(900), cancellationToken).ConfigureAwait(false);
		var up = await context.MemberRecipeAsync("stage-bind", cancellationToken, ("bind", "right"), ("action", "up")).ConfigureAwait(false);
		Require(Boolean(up, "ok"), "the member's walk key could not be staged up: " + Raw(up));
		return await context.MemberRecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
	}

	private static async Task<JsonElement?> WaitForCloneAwayAsync(CaseContext context, double? before, CancellationToken cancellationToken)
	{
		if (before is null)
		{
			return null;
		}

		for (var attempt = 0; attempt < 20; attempt += 1)
		{
			var read = await context.RecipeAsync("remote-clone-probe", cancellationToken, ("mode", "where")).ConfigureAwait(false);
			var x = Number(read, "x");
			if (x is not null && Math.Abs(x.Value - before.Value) >= 2.0)
			{
				return read;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return null;
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static string Trim(string text) => text.Length <= 200 ? text : text[..200] + "…";

	private static string StringValue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: string.Empty;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static double? Number(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty(property, out var value))
		{
			return null;
		}

		return value.ValueKind switch
		{
			JsonValueKind.Number => value.GetDouble(),
			JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
			_ => null,
		};
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
