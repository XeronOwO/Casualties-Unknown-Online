using System.Globalization;
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
		// first candidate whose point lands inside the drawn area. The camera settles after the world loads, so a scan
		// that finds nothing visible is repeated before it is called a failure (measured 2026-11-22: the same pairing
		// offered 24 cells, none of them inside the drawn area, and a minute later several were).
		(int X, int Y) cell = default;
		JsonElement? point = null;
		var lastReason = "";
		for (var scan = 0; scan < 10 && point is null; scan++)
		{
			if (scan > 0)
			{
				await Task.Delay(400, cancellationToken).ConfigureAwait(false);
				candidates = await FindBreakableCellsAsync(context, cancellationToken).ConfigureAwait(false);
			}

			foreach (var candidate in candidates)
			{
				var projected = await ProjectAsync(context, candidate, cancellationToken).ConfigureAwait(false);
				if (IsOnScreen(projected))
				{
					cell = candidate;
					point = projected;
					break;
				}

				lastReason = Raw(projected);
			}
		}
		context.Read("host-cell-point", point);
		Require(
			point is not null,
			$"none of the {candidates.Count} breakable cells beside the host's player came onto the host's screen in ten scans; the last projection read {lastReason}");

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

		await UseTheGamePointerAsync(context, cell, screenX, screenY, cancellationToken).ConfigureAwait(false);

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
		// The game's own input gates sit between the staged key and the swing: the console, its pause handler, and the
		// crafting/trade panels each return from PlayerCamera.HandleInput before the attack code (measured 2026-11-22).
		var clock = await context.RecipeAsync("frame-clock", cancellationToken).ConfigureAwait(false);
		context.Read("the game's own gates", clock);
		var framesAtPress = Integer(clock, "frame");
		var entriesAtPress = Integer(staged, "handleInputEntries");
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
			var clockAfter = await context.RecipeAsync("frame-clock", cancellationToken).ConfigureAwait(false);
			var entriesAfter = await context.RecipeAsync("virtual-input", cancellationToken, ("mode", "report"), ("x", "0"), ("y", "0"), ("button", "0")).ConfigureAwait(false);
			var framesDrawn = Integer(clockAfter, "frame") - framesAtPress;
			var entriesGained = Integer(entriesAfter, "handleInputEntries") - entriesAtPress;
			Require(
				damageRose,
				$"holding the game's attack at the cell ({Describe(cell)}) never raised its damage: {Raw(hostAfter)}; the input the game saw was {Raw(staged)}; the gates were {Raw(clock)}; while the attack was held the client drew {framesDrawn} frame(s) and entered its input path {entriesGained} more time(s), and its attack path read {Raw(entriesAfter)}");
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
		var found = await context.RecipeAsync("block-find-breakable", cancellationToken, ("count", "24"), ("minOffset", "1"), ("radius", "8")).ConfigureAwait(false);
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
		// The scan's own body position travels with the answer: a cell beyond the swing's reach (4.5 world units with an
		// empty hand, `Body.cs:2036`) is not something a player could hit, so it never becomes a candidate. Keeping the
		// raw answer also lets a later failure say which body the scan stood on (measured 2026-11-22: cells around
		// x=672 were paired with a body the swing probe read at x=0, and nothing in the report could tell them apart).
		_lastScan = Raw(found);
		var bodyX = Integer(found, "bodyX");
		var bodyY = Integer(found, "bodyY");
		var reachable = cells.Where(cell => Math.Abs(cell.X - bodyX) <= 4 && Math.Abs(cell.Y - bodyY) <= 4).ToList();
		Require(reachable.Count > 0, $"the scan found {cells.Count} breakable cell(s) but none within the body's reach "
			+ $"(body at {bodyX},{bodyY}; first cells " + string.Join(" ", cells.Take(6).Select(Describe)) + "): " + Raw(found));
		return reachable;
	}

	// A cell whose point lands under the game's own HUD cannot be attacked: `PlayerCamera.HandleAttacks` returns at once
	// while the pointer is over a UI element (`PlayerCamera.cs:1997`), so a point at the bottom bar or the corner trays
	// is not something a player could swing at (measured 2026-11-22: cells projected to row 710 of 720, the game asked
	// 21,552 key questions with the attack held, and every swing was refused).
	private static bool IsOnScreen(JsonElement? projected)
	{
		if (!Boolean(projected, "ok"))
		{
			return false;
		}
		var height = Integer(projected, "screenHeight");
		var width = Integer(projected, "screenWidth");
		if (width <= 0)
		{
			width = 1280;
		}
		var x = Integer(projected, "screenX");
		var y = Integer(projected, "screenY");
		return height > 0 && x > 120 && x < width - 120 && y > 120 && y < height - 140;
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
	private static async Task UseTheGamePointerAsync(CaseContext context, (int X, int Y) cell, int x, int y, CancellationToken cancellationToken)
	{
		var awake = await WakeThePlayerAsync(context, cancellationToken).ConfigureAwait(false);
		Require(awake, "the host's body is not awake, so its swing would never start (`Body.Attack` runs only while "
			+ "`this.conscious` and the attack cooldown is spent; `Body.cs:2036`)");
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
		// `HandleAttacks` returns before it reads the attack key while the pointer is over a UI element
		// (`PlayerCamera.cs:1997`), so the row asks the same question the game asks and names what it found.
		var under = await context.RecipeAsync("ui-raycast", cancellationToken).ConfigureAwait(false);
		Require(!Boolean(under, "overUi"), $"the pointer at {x},{y} (cell {Describe(cell)}) is over a UI element, so the game would refuse every "
			+ "swing: " + Raw(under));
		var swing = await context.RecipeAsync("attack-swing-probe", cancellationToken, ("x", cell.X.ToString()), ("y", cell.Y.ToString())).ConfigureAwait(false);
		Require(Boolean(swing, "ok") && Boolean(swing, "conscious") && Boolean(swing, "inRange")
			&& String(swing, "hitName").Contains("Block", StringComparison.OrdinalIgnoreCase),
			$"the host's body cannot swing at {Describe(cell)}: `Body.Attack` needs a conscious body, a target inside its reach, "
			+ "and a swing ray that lands on the block's collider: " + Raw(swing) + "; the scan that named the cell read "
			+ _lastScan);
		context.Read("host-swing", swing);
	}

	// A player who is awake is the precondition of every swing: `Body.Attack` returns at once unless `this.conscious`
	// and the cooldown is spent (`Body.cs:2036`), and a body that has just been carried into a run can be unconscious.
	// The row wakes it the way a player would be woken by the game's own agent — by setting consciousness, not by
	// calling the swing from the outside.
	private static async Task<bool> WakeThePlayerAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var effects = await context.RecipeAsync("body-effects", cancellationToken).ConfigureAwait(false);
		if (Double(effects, "consciousness") >= 60)
		{
			return true;
		}

		var woken = await context.RecipeAsync(
			"body-force",
			cancellationToken,
			("sleep", "-1"),
			("consciousness", "95"),
			("energy", "-1"),
			("brainHealth", "-1"),
			("badSleepAmount", "-1"),
			("idleTime", "-1"),
			("intelligence", "-1"),
			("heartRate", "-1"),
			("bloodPressure", "-1"),
			("bloodOxygen", "-1"),
			("strength", "-1")).ConfigureAwait(false);
		Require(Boolean(woken, "ok"), "the row could not wake the host's body: " + Raw(woken));
		var after = await context.RecipeAsync("body-effects", cancellationToken).ConfigureAwait(false);
		return Double(after, "consciousness") >= 60;
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

	private static string _lastScan = "(no scan yet)";

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

	private static string String(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.String ? (value.GetString() ?? string.Empty) : value.GetRawText()
			: string.Empty;

	private static double Double(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty(property, out var value))
		{
			return 0;
		}

		if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
		{
			return number;
		}

		return value.ValueKind == JsonValueKind.String
			&& double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
				? parsed
				: 0;
	}
}
