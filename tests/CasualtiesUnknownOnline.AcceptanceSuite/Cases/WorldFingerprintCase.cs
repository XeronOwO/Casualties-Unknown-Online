using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// Row 1 — both clients see one world, and a block the host's player digs out by hand is gone on the
/// member's screen too.
///
/// The row used to compare one product fingerprint hash against another. That proved neither half of the
/// claim (the hash covers no drawn tile and no sound), and it passed while a client drew placeholders. So the
/// row now does what the player does: it asks the game which cell beside the player can be broken, points the
/// game's own pointer at that cell's screen point, holds the game's own attack there until the cell breaks,
/// and then reads the same absolute cell on both clients — before the first hit, at the last crack stage, and
/// after the block is gone.
/// </summary>
internal sealed class WorldFingerprintCase(string name = "world-fingerprint") : AcceptanceCase
{
	public override string Name => name;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var candidates = await FindBreakableCellsAsync(context, cancellationToken).ConfigureAwait(false);
		// A player digs a block they can see: the game's scan order is not the screen's order, so the row takes the
		// first candidate whose point lands inside the drawn area.
		(int X, int Y) cell = default;
		JsonElement? point = null;
		foreach (var candidate in candidates)
		{
			var projected = await ProjectAsync(context, candidate, cancellationToken).ConfigureAwait(false);
			if (IsOnScreen(projected))
			{
				cell = candidate;
				point = projected;
				break;
			}
		}
		context.Read("host-cell-point", point);
		Require(
			point is not null,
			$"none of the {candidates.Count} breakable cells beside the host's player is on the host's screen");

		var hostBefore = await ReadCellAsync(context, cell, false, cancellationToken).ConfigureAwait(false);
		var memberBefore = await ReadCellAsync(context, cell, true, cancellationToken).ConfigureAwait(false);
		context.Read("host-cell-before", hostBefore);
		context.Read("member-cell-before", memberBefore);
		Require(
			Integer(hostBefore, "block") > 0,
			$"the host does not see a block at the cell the game called breakable ({Describe(cell)}): {Raw(hostBefore)}");
		Require(
			Integer(memberBefore, "block") == Integer(hostBefore, "block"),
			$"the member does not draw the same block at ({Describe(cell)}): host block {Integer(hostBefore, "block")}, member block {Integer(memberBefore, "block")}");

		var height = Integer(point, "screenHeight");
		var screenX = Integer(point, "screenX");
		var screenY = Integer(point, "screenY");

		await UseTheGamePointerAsync(context, screenX, screenY, cancellationToken).ConfigureAwait(false);

		// The game reads the attack while it is held: press, watch the crack stage rise, and keep holding
		// until the cell is gone (the block's own health decides how long that takes).
		await PressAsync(context, "press-key", cancellationToken).ConfigureAwait(false);
		// The staging is only worth anything if the game actually asks it: the report carries how many of the
		// game's own key queries the patch answered, so a row that never swings can say which side is missing.
		var staged = await context.RecipeAsync(
			"virtual-input",
			cancellationToken,
			("mode", "report"),
			("x", "0"),
			("y", "0"),
			("button", "0")).ConfigureAwait(false);
		context.Read("the input the game sees", staged);
		JsonElement? hostDuring = null;
		JsonElement? hostAfter = null;
		try
		{
			var damageRose = false;
			var broke = false;
			for (var attempt = 0; attempt < 60 && !broke; attempt++)
			{
				await Task.Delay(250, cancellationToken).ConfigureAwait(false);
				var now = await ReadCellAsync(context, cell, false, cancellationToken).ConfigureAwait(false);
				if (Integer(now, "damage") > Integer(hostBefore, "damage"))
				{
					damageRose = true;
					hostDuring ??= now;
				}
				broke = Integer(now, "block") <= 0;
				hostAfter = now;
			}
			context.Read("host-cell-during", hostDuring);
			context.Read("host-cell-after", hostAfter);
			Require(
				damageRose,
				$"holding the game's attack at the cell ({Describe(cell)}) never raised its damage: {Raw(hostAfter)}; the input the game saw was {Raw(staged)}");
			Require(
				broke,
				$"the cell ({Describe(cell)}) did not break while the attack was held: {Raw(hostAfter)}");
		}
		finally
		{
			await PressAsync(context, "release-key", cancellationToken).ConfigureAwait(false);
		}

		// The claim is about the member's screen: the same cell has to become the same empty cell there.
		JsonElement? memberAfter = null;
		for (var attempt = 0; attempt < 20; attempt++)
		{
			memberAfter = await ReadCellAsync(context, cell, true, cancellationToken).ConfigureAwait(false);
			if (Integer(memberAfter, "block") <= 0)
			{
				break;
			}
			await Task.Delay(250, cancellationToken).ConfigureAwait(false);
		}
		context.Read("member-cell-after", memberAfter);
		Require(
			Integer(memberAfter, "block") <= 0,
			$"the block the host dug out at ({Describe(cell)}) is still drawn on the member's client: {Raw(memberAfter)}");
	}

	private static async Task<List<(int X, int Y)>> FindBreakableCellsAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var found = await context.RecipeAsync("block-find-breakable", cancellationToken, ("count", "24")).ConfigureAwait(false);
		context.Read("host-breakable", found);
		Require(Boolean(found, "ok"), "the game did not answer which cells beside the host's player can be broken: " + Raw(found));
		var cells = new List<(int X, int Y)>();
		if (found is { } answer && answer.TryGetProperty("cells", out var array) && array.ValueKind == JsonValueKind.Array)
		{
			foreach (var cell in array.EnumerateArray())
			{
				var x = Integer(cell, "x");
				var y = Integer(cell, "y");
				if (x != 0 || y != 0)
				{
					cells.Add((x, y));
				}
			}
		}
		Require(cells.Count > 0, "the game named no breakable cell beside the host's player: " + Raw(found));
		return cells;
	}

	private static bool IsOnScreen(JsonElement? projected)
	{
		if (!Boolean(projected, "ok"))
		{
			return false;
		}
		var height = Integer(projected, "screenHeight");
		var x = Integer(projected, "screenX");
		var y = Integer(projected, "screenY");
		return height > 0 && x > 0 && y > 0 && y < height;
	}

	private static Task<JsonElement?> ProjectAsync(CaseContext context, (int X, int Y) cell, CancellationToken cancellationToken) =>
		context.RecipeAsync("screen-project", cancellationToken, ("x", cell.X.ToString()), ("y", cell.Y.ToString()));

	private static Task<JsonElement?> ReadCellAsync(CaseContext context, (int X, int Y) cell, bool member, CancellationToken cancellationToken)
	{
		var args = new (string Key, string Value)[] { ("x", cell.X.ToString()), ("y", cell.Y.ToString()) };
		return member
			? context.MemberRecipeAsync("block-read-at", cancellationToken, args)
			: context.RecipeAsync("block-read-at", cancellationToken, args);
	}

	// The game's own pointer: the acceptance channel stages the position the game reads through Input.mousePosition,
	// so no window is activated and the OS input queue never sees a mouse (the host runs behind other windows).
	private static async Task UseTheGamePointerAsync(CaseContext context, int x, int y, CancellationToken cancellationToken)
	{
		var installed = await context.RecipeAsync(
			"virtual-input",
			cancellationToken,
			("mode", "install"),
			("x", "0"),
			("y", "0"),
			("button", "0")).ConfigureAwait(false);
		Require(Boolean(installed, "ok"), "the acceptance channel could not install the game's pointer: " + Raw(installed));
		var moved = await context.RecipeAsync(
			"virtual-input",
			cancellationToken,
			("mode", "move"),
			("x", x.ToString()),
			("y", y.ToString()),
			("button", "0")).ConfigureAwait(false);
		context.Read("host-pointer", moved);
		Require(Boolean(moved, "ok"), $"the acceptance channel could not point the game at {x},{y}: {Raw(moved)}");
	}

	private static async Task PressAsync(CaseContext context, string mode, CancellationToken cancellationToken)
	{
		var answer = await context.RecipeAsync(
			"virtual-input",
			cancellationToken,
			("mode", mode),
			("x", "0"),
			("y", "0"),
			("button", "0")).ConfigureAwait(false);
		Require(Boolean(answer, "ok"), $"the acceptance channel could not {mode} the game's attack button: {Raw(answer)}");
	}

	private static string Describe((int X, int Y) cell) => cell.X + "," + cell.Y;

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool Boolean(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty(property, out var value))
		{
			return false;
		}

		return value.ValueKind == JsonValueKind.True;
	}

	private static int Integer(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element ? Integer(element, property) : 0;

	private static int Integer(JsonElement element, string property)
	{
		if (!element.TryGetProperty(property, out var value))
		{
			return 0;
		}

		return value.ValueKind switch
		{
			JsonValueKind.Number when value.TryGetInt32(out var number) => number,
			JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
			_ => 0,
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
