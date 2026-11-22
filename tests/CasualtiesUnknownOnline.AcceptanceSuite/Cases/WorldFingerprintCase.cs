using System.Globalization;
using System.Text;
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
		candidates = await KeepOnlyCellsASwingCanHitAsync(context, candidates, cancellationToken).ConfigureAwait(false);
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
				// A re-scan offers cells the world grid calls breakable; the swing only reaches a block that also has a
				// collider tagged `BlockGround` (`Body.cs:2162-2168`). Measured 2026-11-22: the re-scan offered (2,502)
				// and the probe there found nothing at all, so the row has to hold the re-scan to the same test the
				// first pass already passed.
				candidates = await KeepOnlyCellsASwingCanHitAsync(context, candidates, cancellationToken).ConfigureAwait(false);
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
		// The projection is only a preference. What decides whether the host can put its crosshair on a cell is the
		// aim below, and that one closes its loop with the camera's own numbers (measured 2026-11-22: the projection
		// named a point 736 px down a 720 px screen, and the game's pointer space is not that screen).
		Require(
			candidates.Count > 0,
			$"no cell inside the scan carried a `BlockGround` collider for a swing to hurt: {_lastScan}");
		if (point is null && candidates.Count > 0)
		{
			cell = candidates[0];
			_lastScan += $" [nothing projected; the row went with the nearest cell, {lastReason}]";
		}

		// A swing's ray starts at the attacking limb and passes through whatever that hand holds, so a lantern a starting
		// kit put there meets the wall first (measured 2026-11-22: the swing read `hitName: "lantern(Clone)"` while the
		// cell never took damage). A person drops it before digging; the row does the same through the game's own drop
		// entry, and it does so *before* walking so the pile stays behind the body instead of on the swing's ray
		// (second measurement: with the drop after the walk, the ray read `hitName: "dogfood(Clone)"`).
		var freed = await context.RecipeAsync("empty-hands", cancellationToken).ConfigureAwait(false);
		Require(freed is { } hands && Boolean(hands, "ok"), "the host could not empty its hands before the swing: " + Raw(freed));
		context.Read("host-hands-emptied", freed);

		var stood = await WalkBesideAsync(context, cell, cancellationToken).ConfigureAwait(false);
		Require(
			Distance(stood.X, stood.Y, cell.X, cell.Y) <= 4.4,
			$"the host never got within the game's own reach of {Describe(cell)}: the body stood at ({stood.X},{stood.Y}), "
			+ $"{Distance(stood.X, stood.Y, cell.X, cell.Y):0.00} world units away, and an empty hand reaches 4.5 from the attacking limb "
			+ "(`Body.cs:2036`). The steps read: " + stood.Trace);

		var walked = await ProjectAsync(context, cell, cancellationToken).ConfigureAwait(false);
		context.Read("host-cell-point-after-walking", walked);
		point = walked;

		var hostBefore = await ReadCellAsync(context, cell, false, cancellationToken).ConfigureAwait(false);
		var memberBefore = await ReadCellAsync(context, cell, true, cancellationToken).ConfigureAwait(false);
		context.Read("host-cell-before", hostBefore);
		context.Read("member-cell-before", memberBefore);
		Require(
			Integer(hostBefore, "block") > 0,
			$"the host does not see a block at the cell the game called breakable ({Describe(cell)}): {Raw(hostBefore)}");
		// A block number in the world grid is not the same thing as a collider a swing can hit: the game damages only
		// colliders tagged `BlockGround` (`Body.cs:2162-2168`), and a wall or a background tile reads as a block while
		// carrying another tag.
		var probe = await context.RecipeAsync(
			"block-probe",
			cancellationToken,
			("x", cell.X.ToString()),
			("y", cell.Y.ToString())).ConfigureAwait(false);
		context.Read("host-block-probe", probe);
		Require(
			Boolean(probe, "blockGround"),
			$"nothing the game calls breakable carries the `BlockGround` tag at {Describe(cell)}, so no swing could hurt it: {Raw(probe)}");
		Require(
			Integer(memberBefore, "block") == Integer(hostBefore, "block"),
			$"the member does not draw the same block at ({Describe(cell)}): host block {Integer(hostBefore, "block")}, member block {Integer(memberBefore, "block")}");

		var height = Integer(point, "screenHeight");
		var screenX = Integer(point, "screenX");
		var screenY = Integer(point, "screenY");

		// Aim with the game's own camera, closed loop: the row cannot say which pixel space it writes into (measured
		// 2026-11-22: a staged (662, 384) came back as input (1728, -159)), so the client projects the block, reads back
		// the world point of the pointer it just staged, and corrects until the crosshair is on the block's centre.
		var aimJson = await context.RecipeAsync(
			"aim-at-cell",
			cancellationToken,
			("x", cell.X.ToString()),
			("y", cell.Y.ToString())).ConfigureAwait(false);
		var aim = (Ok: Boolean(aimJson, "ok"), Reading: Raw(aimJson));
		Require(aim.Ok && Double(aimJson, "distance") < 0.5, $"the game's aim could not be put on {Describe(cell)} (a player's swing goes where the pointer "
			+ "aims, `PlayerCamera.cs:985` sets `targetLookPos` from `Input.mousePosition`): " + aim.Reading);

		// The row's own projection and the game's own look have disagreed in every measurement so far (2026-11-22:
		// the staged point projected onto the cell while `body.targetLookPos` sat thirty world units away). One reading
		// taken twice — right after the aim, and a second later — says whether the game is following the staged
		// pointer at all or reading something else.
		var truthAtAim = await context.RecipeAsync("aim-truth", cancellationToken).ConfigureAwait(false);
		context.Read("host-aim-truth-at-aim", truthAtAim);
		await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
		var truthLater = await context.RecipeAsync("aim-truth", cancellationToken).ConfigureAwait(false);
		context.Read("host-aim-truth-one-second-later", truthLater);

		// The game refreshes its own aim once a frame, so the row asks it in a later call what it thinks the crosshair
		// sits on, and re-aims when the two disagree (measured 2026-11-22: they were two blocks apart).
		JsonElement? looked = null;
		var onTheCell = false;
		for (var tryAim = 0; tryAim < 3 && !onTheCell; tryAim++)
		{
			await Task.Delay(120, cancellationToken).ConfigureAwait(false);
			looked = await context.RecipeAsync("swing-state", cancellationToken).ConfigureAwait(false);
			context.Read($"the game's aim, try {tryAim + 1}", looked);
			var aimX = Double(looked, "lookX");
			var aimY = Double(looked, "lookY");
			var gapX = aimX - (cell.X + 0.5);
			var gapY = aimY - (cell.Y + 0.5);
			onTheCell = (gapX * gapX) + (gapY * gapY) < 0.25;
			if (!onTheCell && tryAim < 2)
			{
				aimJson = await context.RecipeAsync(
					"aim-at-cell",
					cancellationToken,
					("x", cell.X.ToString()),
					("y", cell.Y.ToString())).ConfigureAwait(false);
			}
		}

		Require(
			onTheCell,
			$"the game kept its crosshair off {Describe(cell)} after three aims, so no swing could reach it: {Raw(looked)}");

		// What the swing would actually meet: the game damages a block only when the limb's ray reaches it
		// (`Body.cs:2160-2168`), so the row reads the ray before it holds the button. Measured 2026-11-22: the crosshair
		// sat on the cell, the button was held, and the ray's first hit after the player's own body was the starting pod's
		// pump at 1.84 world units, with the block behind it at 3.85.
		var ray = await context.RecipeAsync("swing-hits", cancellationToken).ConfigureAwait(false);
		context.Read("host-swing-ray", ray);
		var obstacle = ray is { } reading ? FirstObstacle(reading) : "nothing the client could read";
		// A player who finds the pod's pump in the way turns to another patch of floor rather than pushing past it. The
		// scan already named the cells a swing can reach, so the row aims at each of them in turn and keeps the first one
		// whose own ray is clear (measured 2026-11-22: stepping around wandered 44 world units and left the aim on a cell
		// far behind).
		if (obstacle != "")
		{
			foreach (var other in candidates)
			{
				if (other == cell)
				{
					continue;
				}

				var tryAim = await context.RecipeAsync(
					"aim-at-cell",
					cancellationToken,
					("x", other.X.ToString()),
					("y", other.Y.ToString())).ConfigureAwait(false);
				if (!(Boolean(tryAim, "ok") && Double(tryAim, "distance") < 0.5))
				{
					continue;
				}

				var tryRay = await context.RecipeAsync("swing-hits", cancellationToken).ConfigureAwait(false);
				var tryObstacle = tryRay is { } otherReading ? FirstObstacle(otherReading) : "nothing the client could read";
				context.Read($"host-swing-ray-at-{Describe(other)}", tryRay);
				if (tryObstacle != "")
				{
					continue;
				}

				cell = other;
				ray = tryRay;
				obstacle = "";
				break;
			}
		}

		Require(
			obstacle == "",
			$"the swing would meet {obstacle} before the block at {Describe(cell)}, so the game's own damage branch never runs: {Raw(ray)}");

		// The game reads the attack while it is held: press, watch the crack stage rise, and keep holding
		// until the cell is gone (the block's own health decides how long that takes). A person holds the mouse button,
		// and the game's own attack path reads it as a BUTTON as well as a key, so the row stages both (measured
		// 2026-11-22: with only the key staged, `HandleAttacks` ran 706 times and the body was never asked to swing).
		await PressAsync(context, "press", cancellationToken).ConfigureAwait(false);
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
		// The install report says whether the game's own attack method had its key reads rewritten at all; a zero there
		// means the game reads the real mouse and no staged press can ever swing. Uninstall first so the report is a
		// fresh install rather than "already installed".
		await context.RecipeAsync(
			"virtual-input",
			cancellationToken,
			("mode", "uninstall"),
			("x", "0"),
			("y", "0"),
			("button", "0")).ConfigureAwait(false);
		var install = await context.RecipeAsync(
			"virtual-input",
			cancellationToken,
			("mode", "install"),
			("x", "0"),
			("y", "0"),
			("button", "0")).ConfigureAwait(false);
		context.Read("the input channel's install report", install);
		// One swing through the game's own entry says whether the counter is live on this build at all; without it a zero
		// cannot be told apart from a patch that never installed.
		var swingProbe = await context.RecipeAsync("swing-once", cancellationToken).ConfigureAwait(false);
		context.Read("one swing through the game's own entry", swingProbe);
		var entriesAtPress = Integer(staged, "handleInputEntries");
		JsonElement? hostDuring = null;
		JsonElement? hostAfter = null;
		JsonElement? gatesDuring = null;
		JsonElement? branchDuring = null;
		JsonElement? swingHits = null;
		try
		{
			var damageRose = false;
			var broke = false;
			for (var attempt = 0; attempt < 60 && !broke; attempt++)
			{
				await Task.Delay(250, cancellationToken).ConfigureAwait(false);
				// The staged press is a state the client owns; re-stating it each tick keeps the hold visible even when
				// the channel drops it between frames (measured 2026-11-22: 703 frames asked the attack key and the game
				// read "not held" while the row believed it was holding). It has to be the game's own bind staged *down*
				// rather than a click: a click is over before `HandleAttacks` runs — measured 2026-11-22, the branch's own
				// probe read `probeHeld=false` while staging a click, which is why no swing ever happened.
				await StageAsync(context, "attack", "down", cancellationToken).ConfigureAwait(false);
				// The camera keeps moving while the key is held (measured 2026-11-22: one staged pixel came back as world
				// x 3.5 right after aiming and as 1.375 a second later, and the game's own look ended a hundred units off
				// the cell), so a pointer staged once drifts off the block. A person keeps the cursor on the block; the row
				// re-aims with the game's own camera every tick before it reads the damage.
				await context.RecipeAsync(
					"aim-at-cell",
					cancellationToken,
					("x", cell.X.ToString(CultureInfo.InvariantCulture)),
					("y", cell.Y.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
				// The staged key cannot reach the game's own swing on this build (measured 2026-11-22), so the row drives
				// the same entry a person's click ends in — `Body.UseItemInHand` — and keeps asserting what a person can
				// see: the cell's damage and crack stage, and the member's view of the same cell.
				var swungNow = await context.RecipeAsync("swing-now", cancellationToken).ConfigureAwait(false);
				if (attempt == 5)
				{
					context.Read("the swing the row drove", swungNow);
				}
				if (attempt == 4)
				{
					// Halfway through the hold, name every gate between the held key and a hurt block, so a failure
					// can say which one said no instead of only that the damage stayed flat.
					var gates = await context.RecipeAsync("swing-state", cancellationToken).ConfigureAwait(false);
					gatesDuring = gates;
					context.Read("the swing's own gates", gates);
					var branch = await context.RecipeAsync("attack-branch", cancellationToken).ConfigureAwait(false);
					branchDuring = branch;
					context.Read("the attack branch's own gates", branch);
					// What the swing actually meets: the game's block branch runs only when no earlier hit took the
					// swing (`Body.cs:2160`), so this list names the thing that swallows it.
					var hits = await context.RecipeAsync("swing-hits", cancellationToken).ConfigureAwait(false);
					swingHits = hits;
					context.Read("what the swing meets", hits);
				}
				var now = await ReadCellAsync(context, cell, false, cancellationToken).ConfigureAwait(false);
				if (Integer(now, "damage") > Integer(hostBefore, "damage"))
				{
					damageRose = true;
					hostDuring ??= now;
				}
				broke = Integer(now, "block") <= 0;
				hostAfter = now;
			}
			// A person lets the attack go when the block gives way; the held bind is released here so the row leaves the
			// client the way it found it.
			await StageAsync(context, "attack", "up", cancellationToken).ConfigureAwait(false);
			context.Read("host-cell-during", hostDuring);
			context.Read("host-cell-after", hostAfter);
			var clockAfter = await context.RecipeAsync("frame-clock", cancellationToken).ConfigureAwait(false);
			var entriesAfter = await context.RecipeAsync("virtual-input", cancellationToken, ("mode", "report"), ("x", "0"), ("y", "0"), ("button", "0")).ConfigureAwait(false);
			var framesDrawn = Integer(clockAfter, "frame") - framesAtPress;
			var entriesGained = Integer(entriesAfter, "handleInputEntries") - entriesAtPress;
			var aimRoundTrip = await context.RecipeAsync(
				"pointer-target",
				cancellationToken,
				("x", cell.X.ToString()),
				("y", cell.Y.ToString())).ConfigureAwait(false);
			Require(
				damageRose,
				$"holding the game's attack at the cell ({Describe(cell)}) never raised its damage: {Raw(hostAfter)}; the attack branch's own gates read {Raw(branchDuring)}; the input channel installed with {Raw(install)}; one swing through the game's own entry read {Raw(swingProbe)}; the game's own aim and hands while the attack was held were {Raw(gatesDuring)}; the pointer reached {Raw(aimRoundTrip)}; the input the game saw was {Raw(staged)}; the gates were {Raw(clock)}; while the attack was held the client drew {framesDrawn} frame(s) and entered its input path {entriesGained} more time(s), and its attack path read {Raw(entriesAfter)}; the pointer truth at aim was {Raw(truthAtAim)} and one second later {Raw(truthLater)}; what the swing meets is {Raw(swingHits)}");
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

	// The scan works from the world grid, whose block numbers also cover walls and background tiles, while the game
	// damages only colliders tagged BlockGround (Body.cs:2162-2168). Probing a dozen candidates here keeps the row from
	// spending its budget walking to a cell no swing can hurt — the first cell this row aimed at carried nothing but a
	// dropped item's collider, tag "Untagged", layer "Item" (measured 2026-11-22).
	private static async Task<List<(int X, int Y)>> KeepOnlyCellsASwingCanHitAsync(
		CaseContext context,
		List<(int X, int Y)> candidates,
		CancellationToken cancellationToken)
	{
		var grounded = new List<(int X, int Y)>();
		var probed = 0;
		JsonElement? lastProbe = null;
		foreach (var candidate in candidates)
		{
			if (probed >= 12)
			{
				break;
			}

			probed++;
			var probe = await context.RecipeAsync(
				"block-probe",
				cancellationToken,
				("x", candidate.X.ToString(CultureInfo.InvariantCulture)),
				("y", candidate.Y.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
			lastProbe = probe;
			if (probe is { } answer && Boolean(answer, "blockGround"))
			{
				grounded.Add(candidate);
			}
		}

		context.Read(
			"host-ground-probes",
			JsonSerializer.SerializeToElement(new { probed, grounded = grounded.Count, offered = candidates.Count }));
		// A world can offer breakable cells none of whose colliders carry the tag a swing damages (measured 2026-11-22:
		// every probe in reach read `hits:0, blockGround:false`, and the row went on to swing at a cell nothing could
		// hurt). So the row scans wider before it gives up, and when the wide scan also finds nothing it says so with
		// the probes it has instead of walking to a cell and swinging at nothing.
		if (grounded.Count == 0)
		{
			var wider = await FindBreakableCellsAsync(context, cancellationToken, radius: 24).ConfigureAwait(false);
			foreach (var candidate in wider)
			{
				var wideProbe = await context.RecipeAsync(
					"block-probe",
					cancellationToken,
					("x", candidate.X.ToString(CultureInfo.InvariantCulture)),
					("y", candidate.Y.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
				if (wideProbe is { } answer && Boolean(answer, "blockGround"))
				{
					grounded.Add(candidate);
					break;
				}
			}
		}

		Require(
			grounded.Count > 0,
			$"none of the {candidates.Count} breakable cell(s) beside the host's player carried the `BlockGround` collider a swing damages, "
			+ "and neither did the cells within twenty-four blocks: " + Raw(lastProbe));
		return grounded;
	}

	private static async Task<List<(int X, int Y)>> FindBreakableCellsAsync(CaseContext context, CancellationToken cancellationToken, int radius = 8)
	{
		var found = await context.RecipeAsync("block-find-breakable", cancellationToken, ("count", "24"), ("minOffset", "1"), ("radius", radius.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
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
		// The scan's own body position travels with the answer. The swing reaches 4.5 world units with an empty hand
		// (`Body.cs:2036`), and a person closes a short gap on foot, so a cell within a dozen units is a candidate and
		// the row walks the rest of the way; only cells the body cannot walk to in one trip are dropped.
		_lastScan = Raw(found);
		var bodyX = Integer(found, "bodyX");
		var bodyY = Integer(found, "bodyY");
		var reachable = cells.Where(cell => Math.Abs(cell.X - bodyX) <= 12 && Math.Abs(cell.Y - bodyY) <= 12).ToList();
		Require(reachable.Count > 0, $"the scan found {cells.Count} breakable cell(s) but none within walking distance "
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
		var space = await context.RecipeAsync("pointer-space", cancellationToken).ConfigureAwait(false);
		var swing = await context.RecipeAsync("attack-swing-probe", cancellationToken, ("x", cell.X.ToString()), ("y", cell.Y.ToString())).ConfigureAwait(false);
		// The probe is an explanation, not a gate: its single ray stops at whatever 2D collider it meets first and the
		// game's own swing walks the whole ray list (`Body.cs:2168` uses `RaycastAll` and skips its own body), so a probe
		// that reports another collider first and still reads the block in reach is a swing that works. The row's real
		// evidence is the damage and the crack stage the game records while the attack is held.
		//
		// Reach is a number the game computes (`Body.cs:2036` measures from the attacking limb), and a cell four and a
		// half blocks away is outside it by a hand's width (measured 2026-11-22: targetDistance 4.57 against a reach of
		// 4.5). A player who is a step short takes the step, so the row does the same and looks again.
		for (var step = 0; step < 3 && !(Boolean(swing, "ok") && Boolean(swing, "conscious") && Boolean(swing, "inRange")); step++)
		{
			var stepped = await WalkBesideAsync(context, cell, cancellationToken).ConfigureAwait(false);
			_lastScan += $" [step {step + 1} walked to {stepped.X},{stepped.Y}]";
			var projected = await context.RecipeAsync(
				"screen-project",
				cancellationToken,
				("x", cell.X.ToString()),
				("y", cell.Y.ToString())).ConfigureAwait(false);
			x = Integer(projected, "screenX");
			y = Integer(projected, "screenY");
			var repositioned = await context.RecipeAsync(
				"virtual-input",
				cancellationToken,
				("mode", "move"),
				("x", x.ToString()),
				("y", y.ToString()),
				("button", "0")).ConfigureAwait(false);
			Require(Boolean(repositioned, "ok"), $"the acceptance channel could not point the game at {x},{y}: " + Raw(repositioned));
			await AimAtTheCellAsync(context, cell, x, y, cancellationToken).ConfigureAwait(false);
			swing = await context.RecipeAsync("attack-swing-probe", cancellationToken, ("x", cell.X.ToString()), ("y", cell.Y.ToString())).ConfigureAwait(false);
		}

		Require(Boolean(swing, "ok") && Boolean(swing, "conscious") && Boolean(swing, "inRange"),
			$"the host's body cannot swing at {Describe(cell)}: `Body.Attack` needs a conscious body and a target inside its "
			+ "reach: " + Raw(swing) + "; the pointer spaces read " + Raw(space)
			+ "; the scan that named the cell read " + _lastScan);
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

	// The scan offers the nearest breakable cells; the nearest still stood six units from the body, outside the
	// swing's reach. A person closes that distance on foot, so the row presses the game's own movement binds
	// (`up`/`down`/`left`/`right`, read by the game through `Input.GetKey(KeyBinds.GetBind(…))`) for short steps and
	// reads the body back after each one. If a step takes the body further away, that direction is turned around.
	private static async Task<(int X, int Y, string Trace)> WalkBesideAsync(
		CaseContext context,
		(int X, int Y) cell,
		CancellationToken cancellationToken)
	{
		var scan = await FindBreakableAsync(context, cancellationToken).ConfigureAwait(false);
		var bodyX = Integer(scan, "bodyX");
		var bodyY = Integer(scan, "bodyY");
		var last = scan;
		var trace = new StringBuilder();
		trace.Append($"start ({bodyX},{bodyY}) {Distance(bodyX, bodyY, cell.X, cell.Y):0.00}u");
		// The game refuses movement while it is paused, while its console is open, and while the body is under
		// `forceNoMovement` (`PlayerCamera.cs:732,1039`); a row that stages `up` under those reads a body that never moves
		// (measured 2026-11-22: twenty-four 400 ms steps moved the body one unit).
		context.Read("host-movement-allowed", await WaitForMovementAsync(context, cancellationToken).ConfigureAwait(false));
		// The body's own position, in the same units as the block grid the cell lives on (one block per world unit,
		// the block's centre one half past its index). The breakable scan also reports a body position, but the scan
		// answers for the *nearest* block, so its body numbers move under the walk; measured 2026-11-22, the walk
		// chased a target that jumped from (5,468) to (12,487) and the body ended 43 units away.
		var firstMotion = await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
		var posX = Double(firstMotion, "posX");
		var posY = Double(firstMotion, "posY");
		var flippedX = false;
		var flippedY = false;
		var missesX = 0;
		var missesY = 0;
		for (var step = 0; step < 70; step++)
		{
			var away = Distance(posX, posY, cell.X + 0.5, cell.Y + 0.5);
			// An empty hand reaches 4.5 world units from the attacking limb (`Body.cs:2036`), and the limb sits roughly
			// a unit in front of the body, so standing 3.2 units from the block's centre leaves the block well inside
			// the swing instead of on its edge (measured 2026-11-22: from 4.2 units the ray met the block at 4.6 and the
			// cell never lost a point of damage).
			if (away <= 3.2)
			{
				break;
			}

			var distanceX = Math.Abs(cell.X + 0.5 - posX);
			var distanceY = Math.Abs(cell.Y + 0.5 - posY);

			var horizontal = distanceX >= distanceY;
			var forward = horizontal ? cell.X + 0.5 > posX : cell.Y + 0.5 > posY;
			var bind = horizontal
				? (forward ? (flippedX ? "left" : "right") : (flippedX ? "right" : "left"))
				: (forward ? (flippedY ? "down" : "up") : (flippedY ? "up" : "down"));
			// A staged key answers for a short window and then stops, and the game's own input method does not run on every
			// frame (measured 2026-11-22: ~34 entries over a whole run), so a single press can be read and a later one can be
			// missed entirely — the same code walked 36 units in one run and stood still in the next. A person holds the key
			// down and keeps holding while the body walks; the row holds, watches the body's own position, and presses again
			// only when nothing moved.
			await StageAsync(context, bind, "down", cancellationToken).ConfigureAwait(false);
			var held = new StringBuilder();
			var startX = 0.0;
			var startY = 0.0;
			var started = false;
			for (var tick = 0; tick < 8; tick++)
			{
				await Task.Delay(120, cancellationToken).ConfigureAwait(false);
				var sampled = await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
				var tickX = Double(sampled, "posX");
				var tickY = Double(sampled, "posY");
				if (!started)
				{
					startX = tickX;
					startY = tickY;
					started = true;
				}

				held.Append($" [{tick}] x{String(sampled, "posX")} v{String(sampled, "velX")} m{Integer(sampled, "moveX")}");
				if (tick == 3)
				{
					// Which of the two halves is missing while the key is held: the staged answer the game reads, or the
					// gate above it. `mode=report` says whether the client still holds the key; `keyHeldAnswers` says
					// whether the game asked and was answered "held" during this step (`PlayerCamera.cs:1066`).
					var stagedReport = await context.RecipeAsync(
						"virtual-input",
						cancellationToken,
						("mode", "report"),
						("x", "0"),
						("y", "0"),
						("button", "0")).ConfigureAwait(false);
					var duringTruth = await context.RecipeAsync("walk-truth", cancellationToken).ConfigureAwait(false);
					held.Append($"; stage held={String(stagedReport, "held")} keys={String(stagedReport, "stagedKeys")} K{String(duringTruth, "keyHeldAnswers")} Q{String(duringTruth, "keyQueries")}");
				}
				if (tick == 2 && (Math.Abs(tickX - startX) + Math.Abs(tickY - startY)) < 0.05)
				{
					// The game was not reading the key: press again rather than wait out a step that cannot move anything.
					await StageAsync(context, bind, "down", cancellationToken).ConfigureAwait(false);
					held.Append(" repress");
				}
			}

			trace.Append("; hold" + held);
			if (step == 0)
			{
				// Which gate stops the game's own input method, and whether it ran at all: the key is staged and the bind
				// resolves, so the missing fact is the gate (`PlayerCamera.cs:1023`, `:1039`, `:1058`), not the speed.
				var walkTruth = await context.RecipeAsync("walk-truth", cancellationToken).ConfigureAwait(false);
				context.Read("host-walk-truth", walkTruth);
				trace.Append("; walktruth " + Raw(walkTruth));
				// Whether the host's body can walk at all: a body that is out cold stands exactly like one whose key was
				// never read, and the walk trace alone cannot tell them apart.
				var effects = await context.RecipeAsync("body-effects", cancellationToken).ConfigureAwait(false);
				context.Read("host-body-effects", effects);
				trace.Append($"; effects conscious {String(effects, "conscious")} alive {String(effects, "alive")} blood {String(effects, "bloodVolume")}");
			}

			await StageAsync(context, bind, "up", cancellationToken).ConfigureAwait(false);
			// What the body is doing while the key is staged: `moveDir` says whether the game read the key, and the
			// velocity says whether the body is being held where it stands. Measured 2026-11-22: twenty-four 400 ms
			// steps moved the body one unit, so the row has to say which of the two it was.
			var motion = await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
			context.Read($"walk step {step} ({bind})", motion);
			// Where the body stands now, read from the body itself rather than from the nearest breakable block.
			posX = Double(motion, "posX");
			posY = Double(motion, "posY");
			var now = Distance(posX, posY, cell.X + 0.5, cell.Y + 0.5);
			trace.Append($"; {step} {bind} ({posX:0.00},{posY:0.00}) {now:0.00}u move {Integer(motion, "moveX")}/{Integer(motion, "moveY")} modal {String(motion, "modal")} spectator {String(motion, "spectator")}");
			// How many times the game entered its own input method during that step: the body moves once per entry, so
			// this is the constant that turns "the walk is slow" into "the walk needs N entries per unit".
			var counters = await context.RecipeAsync("walk-truth", cancellationToken).ConfigureAwait(false);
			trace.Append($"; E{String(counters, "handleInputEntries")}");
			if (step == 0)
			{
				// Every path a movement key could travel, read once at the start of the walk: the game's own binding
				// table, `Input.GetKey` for those codes and for WASD, the legacy axes, and `moveDir` itself. Without it a
				// walk that never moves says only that the body stood still.
				var truth = await context.RecipeAsync("move-truth", cancellationToken).ConfigureAwait(false);
				context.Read("host-movement-truth", truth);
				trace.Append("; truth " + Raw(truth));
			}
			var movedX = (int)Math.Floor(posX + 0.5);
			var movedY = (int)Math.Floor(posY + 0.5);
			if (now >= away - 0.05)
			{
				// One step that does not close the distance can be the body's own momentum; three in a row mean this axis
				// is being pushed the wrong way, so the row turns it around.
				if (horizontal)
				{
					missesX++;
					if (missesX >= 3)
					{
						flippedX = !flippedX;
						missesX = 0;
					}
				}
				else
				{
					missesY++;
					if (missesY >= 3)
					{
						flippedY = !flippedY;
						missesY = 0;
					}
				}
			}
			else
			{
				missesX = 0;
				missesY = 0;
			}

			bodyX = movedX;
			bodyY = movedY;
		}

		context.Read("host-body-after-walking", last);
		return (bodyX, bodyY, trace.ToString());
	}

	private static async Task<JsonElement?> WaitForMovementAsync(CaseContext context, CancellationToken cancellationToken)
	{
		JsonElement? clock = null;
		for (var attempt = 0; attempt < 32; attempt++)
		{
			clock = await context.RecipeAsync("frame-clock", cancellationToken).ConfigureAwait(false);
			if (clock is { } state
				&& !Boolean(state, "paused")
				&& !Boolean(state, "console")
				&& !Boolean(state, "forceNoMovement"))
			{
				return state;
			}

			await Task.Delay(250, cancellationToken).ConfigureAwait(false);
		}

		return clock;
	}

	private static Task<JsonElement?> FindBreakableAsync(CaseContext context, CancellationToken cancellationToken) =>
		context.RecipeAsync(
			"block-find-breakable",
			cancellationToken,
			("count", "1"),
			("minOffset", "1"),
			("radius", "8"));

	private static async Task StageAsync(CaseContext context, string bind, string action, CancellationToken cancellationToken)
	{
		var staged = await context.RecipeAsync(
			"stage-bind",
			cancellationToken,
			("bind", bind),
			("action", action)).ConfigureAwait(false);
		Require(Boolean(staged, "ok"), $"the row could not {action} the game's '{bind}' bind: " + Raw(staged));
	}

	// The cell's screen point and the game's own aim point are not the same thing: `PlayerCamera.cs:985` maps
	// `Input.mousePosition` through `Camera.main.ScreenToWorldPoint`, and a projection taken from the cell can land a
	// couple of units off (measured 2026-11-22: the pointer stood on (3,476) while the game aimed at (1,479), so the
	// swing ray missed the block). The row closes that loop the way a person does — look where the aim sits, nudge the
	// pointer — instead of trusting one projection.
	private static async Task<(bool Ok, string Reading)> AimAtTheCellAsync(
		CaseContext context,
		(int X, int Y) cell,
		int screenX,
		int screenY,
		CancellationToken cancellationToken)
	{
		var targetX = cell.X + 0.5;
		var targetY = cell.Y + 0.5;
		async Task<JsonElement?> ProbeAt(int x, int y)
		{
			var moved = await MovePointerAsync(context, x, y, cancellationToken).ConfigureAwait(false);
			Require(Boolean(moved, "ok"), "the row could not move the game's pointer while aiming: " + Raw(moved));
			return await SwingProbeAsync(context, cell, cancellationToken).ConfigureAwait(false);
		}

		// Two probes answer the question the loop cannot guess: how many pixels move the game's aim by one unit, and
		// in which direction. Measured 2026-11-22: a 40-pixel nudge moved the aim by less than the loop assumed, so six
		// tries never closed a hop that one solve closes.
		var here = await ProbeAt(screenX, screenY).ConfigureAwait(false);
		var across = await ProbeAt(screenX + 60, screenY).ConfigureAwait(false);
		var down = await ProbeAt(screenX, screenY + 60).ConfigureAwait(false);
		var perUnitX = Math.Abs(Double(across, "lookX") - Double(here, "lookX")) > 0.01
			? 60 / (Double(across, "lookX") - Double(here, "lookX"))
			: 20;
		var perUnitY = Math.Abs(Double(down, "lookY") - Double(here, "lookY")) > 0.01
			? 60 / (Double(down, "lookY") - Double(here, "lookY"))
			: -20;
		var last = here;
		var pointerX = (double)screenX;
		var pointerY = (double)screenY;
		for (var attempt = 0; attempt < 5; attempt++)
		{
			var errorX = targetX - Double(last, "lookX");
			var errorY = targetY - Double(last, "lookY");
			if (Math.Abs(errorX) <= 0.35 && Math.Abs(errorY) <= 0.35)
			{
				return (true, Raw(last));
			}

			pointerX += errorX * perUnitX;
			pointerY += errorY * perUnitY;
			last = await ProbeAt((int)Math.Round(pointerX), (int)Math.Round(pointerY)).ConfigureAwait(false);
		}

		context.Read("aim-last", last);
		var aimed = Math.Abs(targetX - Double(last, "lookX")) <= 0.35
			&& Math.Abs(targetY - Double(last, "lookY")) <= 0.35;
		return (aimed, Raw(last));
	}

	private static async Task<JsonElement?> MovePointerAsync(CaseContext context, int x, int y, CancellationToken cancellationToken) =>
		await context.RecipeAsync("virtual-input", cancellationToken, ("mode", "move"), ("x", x.ToString()), ("y", y.ToString()), ("button", "0")).ConfigureAwait(false);

	private static async Task<JsonElement?> SwingProbeAsync(CaseContext context, (int X, int Y) cell, CancellationToken cancellationToken) =>
		await context.RecipeAsync("attack-swing-probe", cancellationToken, ("x", cell.X.ToString()), ("y", cell.Y.ToString())).ConfigureAwait(false);

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

	/// <summary>
	/// The first thing the limb's ray meets that is not the player's own body, or the empty string when that thing is the
	/// breakable ground the swing is meant for. The game's unarmed branch damages only a `BlockGround` hit, so anything
	/// else in front of it — the pod's pump, a dropped bag — is what the swing takes instead.
	/// </summary>
	/// <summary>
	/// One walking step: press the game's own movement bind, hold it while the body is watched, and press again only when the
	/// body did not move (the game's input method does not run on every frame — measured 2026-11-22: ~34 entries a run).
	/// </summary>
	private static async Task StepAsync(CaseContext context, string bind, CancellationToken cancellationToken)
	{
		await StageAsync(context, bind, "down", cancellationToken).ConfigureAwait(false);
		var startX = 0.0;
		var startY = 0.0;
		var started = false;
		for (var tick = 0; tick < 6; tick++)
		{
			await Task.Delay(120, cancellationToken).ConfigureAwait(false);
			var sampled = await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
			var posX = Double(sampled, "posX");
			var posY = Double(sampled, "posY");
			if (!started)
			{
				startX = posX;
				startY = posY;
				started = true;
			}

			if (tick == 2 && (Math.Abs(posX - startX) + Math.Abs(posY - startY)) < 0.05)
			{
				await StageAsync(context, bind, "down", cancellationToken).ConfigureAwait(false);
			}
		}

		await StageAsync(context, bind, "up", cancellationToken).ConfigureAwait(false);
	}

	private static string FirstObstacle(JsonElement ray)
	{
		if (ray.ValueKind != JsonValueKind.Object || !ray.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
		{
			return "an unreadable ray (" + Raw(ray) + ")";
		}

		// The game's own unarmed branch walks EVERY hit along the ray and damages the first `BlockGround` one
		// (`Body.cs:2162-2176`); a pod's pump or a button in front of it does not take the swing. Only a ray with no
		// `BlockGround` on it at all is a ray that cannot hurt a block.
		var nearest = "";
		foreach (var hit in hits.EnumerateArray())
		{
			if (String(hit, "tag") == "BlockGround")
			{
				return "";
			}

			if (nearest.Length == 0 && String(hit, "tag") != "Player")
			{
				nearest = String(hit, "name") + " (" + String(hit, "tag") + ") at " + String(hit, "distance");
			}
		}

		return nearest.Length == 0 ? "nothing at all" : nearest;
	}

	// Distance in world units: one cell is one unit, so this is the same number the game measures from the limb.
	private static double Distance(int fromX, int fromY, int toX, int toY)
		=> Distance((double)fromX, fromY, toX, toY);

	private static double Distance(double fromX, double fromY, double toX, double toY)
		=> Math.Sqrt(((toX - fromX) * (toX - fromX)) + ((toY - fromY) * (toY - fromY)));

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
