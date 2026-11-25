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
	/// <summary>The name the registry gives this instance (the after-mutation row is a second instance).</summary>
	public override string Name => name;

	/// <summary>
	/// The hand the row empties for its own aim and the wounds its hits leave. The dug-out block is a change to the
	/// world itself, which no game verb puts back: both instances report the cell they removed, and a later row reads
	/// the world as the batch left it.
	/// </summary>
	public override CaseLeftovers Leaves => CaseLeftovers.Hands | CaseLeftovers.Body;

	/// <summary>This row breaks blocks, so it needs a cell the world can dig under the pair.</summary>
	public override bool NeedsDiggableGround => true;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The install report says whether the game's own attack method had its key reads rewritten at all; a zero there
		// means the game reads the real mouse and no staged press can ever swing. It is taken HERE, before anything is
		// staged: `mode=uninstall` drops the staged pointer and buttons with the patches, and the row used to run this
		// between aiming and holding, which threw away the aim it had just closed (measured 2026-11-22: the game read
		// `pointerX -1, pointerY -1` for the whole hold and the swing never met the block).
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
		// The row leaves the capsule before it looks for rock: the world it can dig is the world outside the shell, and
		// a scan taken inside it answers with cells on the far side that no walk can reach (measured 2026-11-22: the
		// body exited leftward to x -8 while every candidate sat forty units to the right).
		//
		// The exit is paid only when the body has nothing to dig within its empty hand's reach (4.5 from the attacking
		// limb, `Body.cs:2036`). The world guarantee may already have carried the host onto ground the game can dig
		// (SessionDriver's ground repair, for a world whose spawn pocket has no diggable cell), and walking out of the
		// pod then takes the body off that ground: in `.acceptance/wf-3` the exit walked the host from (-4,492) down to
		// (-9,413), fifty units below the cell it had been standing beside, and the row failed on a reach it had lost.
		// The scan below answers within the guarantee's own promise (sixteen cells), because a four-cell scan answered
		// nothing while the guarantee's sixteen-cell probe answered three and had already placed the body beside them:
		// measured 2026-11-26 (`.acceptance/ground-1`), the row then left that ground, fell to y 391, and failed to walk
		// back up to the cell at y 473 it had been standing beside.
		var besideTheBody = await FindBreakableCellsAsync(context, cancellationToken, radius: 16, count: 6).ConfigureAwait(false);
		var hittableBeside = await KeepOnlyCellsASwingCanHitAsync(context, besideTheBody, cancellationToken).ConfigureAwait(false);
		context.Note($"beside the body: {besideTheBody.Count} cell(s) the grid calls breakable, {hittableBeside.Count} of them carrying the collider a swing hurts");
		var alreadyOnGround = hittableBeside.Count > 0;
		var podExit = alreadyOnGround
			? (X: 0.0, Y: 0.0, Trace: "the body already stands beside a cell a swing can hurt, so the row stayed where the guarantee put it")
			: await LeaveThePodAsync(context, cancellationToken).ConfigureAwait(false);
		context.Note(podExit.Trace);
		context.Read("host-left-the-pod", await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false));
		var candidates = await FindBreakableCellsAsync(context, cancellationToken).ConfigureAwait(false);
		// A collider on the cell is a preference, not a gate: the row walked into a world where the grid named
		// twenty-four breakable cells and not one of them carried a `BlockGround` collider at its centre, three runs
		// in a row (`.acceptance/row1-137`, `-139`, `-141`, 2026-11-22). A player does not stand still because the
		// floor under the wall cannot name a collider — the player walks up and swings, and the damage reading says
		// whether the block was there. So the grounded cells are tried first and the rest keep their place behind them.
		var grounded = await KeepOnlyCellsASwingCanHitAsync(context, candidates, cancellationToken).ConfigureAwait(false);
		if (grounded.Count > 0)
		{
			candidates = grounded;
		}
		else
		{
			_lastScan += " [not one of the scanned cells carries a `BlockGround` collider; the row walks to the nearest one and lets the damage reading decide]";
		}
		// A player digs a block they can see: the game's scan order is not the screen's order, so the row takes the
		// first candidate whose point lands inside the drawn area. The camera settles after the world loads, so a scan
		// that finds nothing visible is repeated before it is called a failure (measured 2026-11-22: the same pairing
		// offered 24 cells, none of them inside the drawn area, and a minute later several were).
		(int X, int Y) cell = default;
		JsonElement? point = null;
		var lastReason = "";
		// The nearest on-screen cell, not the first one the world's scan order offers. The scan answers in the world
		// grid's own order, and that order named a cell twenty-six units away while a diggable one stood beside the
		// body (measured 2026-11-25, `.acceptance/wf-2`: the body at (-23,501), the first offered cell at (3,492), the
		// nearest inside walking distance). A player digs the block beside them; the row now does the same.
		var hereBeforeScan = await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
		var hereXBeforeScan = Double(hereBeforeScan, "posX");
		var hereYBeforeScan = Double(hereBeforeScan, "posY");
		for (var scan = 0; scan < 6 && point is null; scan++)
		{
			if (scan > 0)
			{
				// The chunks around the pod get their colliders as the world finishes building, so a scan taken too
				// early finds cells the grid calls breakable with nothing to hit (measured 2026-11-22: twelve scans four
				// seconds apart all came back empty, and the same pairing offered grounded cells once the world had
				// settled). The row waits like a player watching the world load rather than calling it a defect.
				await Task.Delay(1500, cancellationToken).ConfigureAwait(false);
				// Each retry reaches further out, the way a player walks to the nearest wall instead of giving up on the
				// patch of floor it happens to stand on (measured 2026-11-22: two runs in a row offered twenty-four cells
				// inside radius 16 and not one of them carried a `BlockGround` collider).
				candidates = await FindBreakableCellsAsync(context, cancellationToken, 16 + (scan * 8), 24 + (scan * 24)).ConfigureAwait(false);
				// A re-scan offers cells the world grid calls breakable; a collider tagged `BlockGround` is what a swing
				// can hurt (`Body.cs:2162-2168`). Measured 2026-11-22: the re-scan offered (2,502) with nothing at the
				// centre. The re-scan keeps a grounded order when it has one and keeps the rest behind it, the same way
				// the first pass does — the damage reading, not the collider list, says whether the block was there.
				var reGrounded = await KeepOnlyCellsASwingCanHitAsync(context, candidates, cancellationToken).ConfigureAwait(false);
				if (reGrounded.Count > 0)
				{
					candidates = reGrounded;
				}
			}

			var nearest = double.MaxValue;
			foreach (var candidate in candidates)
			{
				var projected = await ProjectAsync(context, candidate, cancellationToken).ConfigureAwait(false);
				if (!IsOnScreen(projected))
				{
					lastReason = Raw(projected);
					continue;
				}

				var distance = Distance(hereXBeforeScan, hereYBeforeScan, candidate.X + 0.5, candidate.Y + 0.5);
				if (distance < nearest)
				{
					nearest = distance;
					cell = candidate;
					point = projected;
				}
			}
		}
		context.Read("host-cell-point", point);
		// The projection is only a preference. What decides whether the host can put its crosshair on a cell is the
		// aim below, and that one closes its loop with the camera's own numbers (measured 2026-11-22: the projection
		// named a point 736 px down a 720 px screen, and the game's pointer space is not that screen).
		Require(
			candidates.Count > 0,
			$"the game's own scan found no breakable cell beside the host's player: {_lastScan}");
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

		// With empty hands, the question is whether the body needs to move at all: the game can already name the block
		// the hand's ray meets where the body stands, and a player digs the wall beside them before walking anywhere
		// (measured 2026-11-22: the walk overshot a slippery floor by ten units and the row then aimed at air, while the
		// first run inside the capsule already had a `Chunk(BlockGround)` 2.3 units down the ray).
		var here = await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
		var hereX = Double(here, "posX");
		var hereY = Double(here, "posY");
		var hereRay = await context.RecipeAsync("swing-hits", cancellationToken).ConfigureAwait(false);
		context.Read("host-swing-ray-where-it-stands", hereRay);
		var stood = default((int X, int Y, string Trace));
		var nearRayCarries = hereRay is { } nearRay && FirstBlockCell(nearRay) is not null;
		var swingAlreadyInReach = nearRayCarries && FirstBlockCell(hereRay!.Value) is { } touching
			&& Distance(hereX, hereY, touching.X + 0.5, touching.Y + 0.5) <= 4.4;
		if (swingAlreadyInReach)
		{
			cell = FirstBlockCell(hereRay!.Value)!.Value;
			stood = ((int)Math.Floor(hereX), (int)Math.Floor(hereY), $"the body stayed where it spawned ({hereX:0.00},{hereY:0.00}) "
				+ $"and the hand's ray already met {Describe(cell)}");
			context.Read("host-stayed-in-place", here);
		}
		else
		{
			stood = await WalkBesideAsync(context, cell, cancellationToken).ConfigureAwait(false);
		}
		// A body that stops while the key is held has met something — the pod wall, a machine, a rock — and a player
		// walks to another block rather than reporting the wall as a defect (measured 2026-11-22: the body moved four
		// units, its velocity fell to zero with the key still staged, and the row failed the whole run there). The next
		// block is the nearest one the body can now reach, and the row only walks again when it is a different cell.
		if (Distance(stood.X, stood.Y, cell.X, cell.Y) > 4.4)
		{
			lastReason = $"the host could not walk within the game's own reach of {Describe(cell)}: it stood at "
				+ $"({stood.X},{stood.Y}), {Distance(stood.X, stood.Y, cell.X, cell.Y):0.00} world units away, and an empty hand "
				+ "reaches 4.5 from the attacking limb (`Body.cs:2036`); the walk read: " + stood.Trace
				+ "; the pod exit read: " + podExit.Trace;
			var nearby = candidates
				.Where(other => other != cell && Distance(stood.X, stood.Y, other.X, other.Y) <= 4.2)
				.OrderBy(other => Distance(stood.X, stood.Y, other.X, other.Y))
				.ToList();
			if (nearby.Count == 0)
			{
				// The cells the row started with were measured beside the spawn, inside the capsule; once the body has
				// walked out they no longer describe what is next to it (measured 2026-11-22: the exit left the body at
				// x -8 with every candidate forty units away on the other side of the pod). So when nothing on the old
				// list is in reach, the row asks the world again from where the body now stands.
				var again = await context.RecipeAsync(
					"block-find-breakable",
					cancellationToken,
					("count", "12"),
					("minOffset", "1"),
					("radius", "14")).ConfigureAwait(false);
				context.Read("host-breakable-after-the-exit", again);
				if (again is { } fresh && fresh.TryGetProperty("cells", out var freshCells) && freshCells.ValueKind == JsonValueKind.Array)
				{
					nearby = [.. freshCells
						.EnumerateArray()
						.Select(entry => (X: Integer(entry, "x"), Y: Integer(entry, "y")))
						.Where(other => (other.X != 0 || other.Y != 0) && other != cell)
						.OrderBy(other => Distance(stood.X, stood.Y, other.X, other.Y))];
					lastReason += $" [the world was asked again from ({stood.X},{stood.Y}) and named {nearby.Count} cell(s)]";
				}
			}
			if (nearby.Count > 0)
			{
				cell = nearby[0];
				stood = await WalkBesideAsync(context, cell, cancellationToken).ConfigureAwait(false);
				lastReason += $" [the row walked to the block now in reach, {Describe(cell)}]";
			}
		}
		Require(
			Distance(stood.X, stood.Y, cell.X, cell.Y) <= 4.4,
			lastReason);

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
		// The probe is a reading, not a gate: the grid called this cell breakable and the row is about to swing at it,
		// so what matters is whether the block loses damage. Measured 2026-11-22: the grid named the cell, the probe
		// found no `BlockGround` collider at its centre, and a player standing there would still swing.
		if (!Boolean(probe, "blockGround"))
		{
			_lastScan += $" [the grid calls {Describe(cell)} breakable and no `BlockGround` collider sits at its centre; the swing decides: {Raw(probe)}]";
		}
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
		var obstacle = ray is { } reading ? FirstObstacle(reading, cell) : "nothing the client could read";
		// A player who sees the swing land on the block in front of the crosshair digs THAT block. When the ray's first
		// `BlockGround` is a different cell than the one the camera points at (an up-slope block, or one whose collider
		// sits half a cell over), the row aims at the block the swing would really hit and holds there instead of
		// swinging into air (measured 2026-11-22: aim on (3,490) and the limb's 4.5-unit ray met nothing but the host's
		// own body, while the cell's collider stood elsewhere on the slope).
		if (obstacle != "" && ray is { } current && FirstBlockCell(current) is { } inFront && inFront != cell)
		{
			// On a slope the ray's first block is the one the player is really facing, and the block behind it can sit
			// one cell further up again. A player digs the block that is in front, then the next one appears; so the row
			// follows the ray for a few hops instead of giving up after one (measured 2026-11-22: aiming at (3,481)
			// reported (2,481) in front of it, which reported (3,481) again, a chain that ends only at the block the
			// camera can point at without another block between).
			for (var hop = 0; hop < 4 && obstacle != "" && inFront != cell; hop++)
			{
				var retarget = await context.RecipeAsync(
					"aim-at-cell",
					cancellationToken,
					("x", inFront.X.ToString(CultureInfo.InvariantCulture)),
					("y", inFront.Y.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
				if (!Boolean(retarget, "ok") || Double(retarget, "distance") >= 0.5)
				{
					break;
				}
				var retargetRay = await context.RecipeAsync("swing-hits", cancellationToken).ConfigureAwait(false);
				if (retargetRay is not { } second)
				{
					obstacle += "; nothing the client could read after aiming at the block in front (" + Describe(inFront) + ")";
					break;
				}
				var retargetObstacle = FirstObstacle(second, inFront);
				if (retargetObstacle == "")
				{
					cell = inFront;
					ray = second;
					obstacle = "";
					break;
				}
				context.Read($"host-swing-ray-at-the-block-in-front-{hop}", retargetRay);
				obstacle = retargetObstacle;
				cell = inFront;
				ray = second;
				inFront = FirstBlockCell(second) is { } next ? next : cell;
			}
		}
		// Whatever the crosshair sits on, the damage lands on the first block the hand's ray meets. A player standing on
		// the floor and looking up at a wall digs the block the swing connects with, not the one under the crosshair, so
		// the row adopts that block once the hops above are done: the swing it is about to hold is the one the game will
		// score, and the damage reading follows the same cell on both clients (measured 2026-11-22: aim on (4,452) and
		// the ray's first block was (4,451) at 2.24 — the row then refused a swing the game would have landed).
		if (obstacle != "" && ray is { } rayReading && FirstBlockCell(rayReading) is { } landOn)
		{
			cell = landOn;
			obstacle = "";
			context.Read("host-swing-target", rayReading);
		}

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
				var tryObstacle = tryRay is { } otherReading ? FirstObstacle(otherReading, other) : "nothing the client could read";
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

		// The cell the damage lands in is the cell of the SWING's hit point, not the one the crosshair names: a
		// `Chunk` collider covers many cells, and `Body.cs:2162-2168` maps `hit.point + dir * 0.05f` to the block that
		// loses damage. The row reads that answer one last time before it holds, so the cell it watches is the cell the
		// game is about to hurt (measured 2026-11-25: the aimed cell stayed at damage -1 while the hit point stood one
		// cell over and took the swings).
		var landRay = await context.RecipeAsync("swing-hits", cancellationToken).ConfigureAwait(false);
		if (landRay is { } landed && FirstBlockCell(landed) is { } landsIn && landsIn != cell)
		{
			// A chunk's collider covers many cells, so the point a swing meets can fall in a cell the grid holds no
			// block in; adopting that cell would have the row demand a block the world never had (measured 2026-11-25,
			// `.acceptance/wf-11`: the hit point named (19,463), `block-read-at` answered 0 for it while the scan's own
			// cells carried blocks). The row keeps the cell it aimed at and only follows the point when a block is there.
			var landsInHost = await ReadCellAsync(context, landsIn, false, cancellationToken).ConfigureAwait(false);
			if (Integer(landsInHost, "block") > 0)
			{
				context.Note($"the swing's hit point lands in {Describe(landsIn)}, not in {Describe(cell)}: the row watches the cell the game hurts");
				cell = landsIn;
				ray = landed;
			}
			else
			{
				context.Note($"the swing's hit point lands in {Describe(landsIn)}, a cell the grid holds no block in, so the row keeps {Describe(cell)}: {Raw(landsInHost)}");
			}
		}

		// A player whose swing meets nothing at all is standing on top of the block they aimed at: the hand's ray
		// starts above the cell and passes over it, so the 4.5-unit reach never touches the block under the feet. The
		// person steps back until the wall is in front of them (measured 2026-11-25: the aim sat on the cell centre,
		// `swing-hits` answered count 0, and 117 swings hurt nothing at that cell while the damage went elsewhere).
		for (var back = 0; back < 3 && !(ray is { } standing && FirstBlockCell(standing) is not null); back++)
		{
			var standingAt = await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
			var awayX = Double(standingAt, "posX") - (cell.X + 0.5);
			var awayY = Double(standingAt, "posY") - (cell.Y + 0.5);
			var away = Math.Abs(awayX) >= Math.Abs(awayY)
				? (awayX >= 0 ? "right" : "left")
				: (awayY >= 0 ? "up" : "down");
			await StageAsync(context, away, "down", cancellationToken).ConfigureAwait(false);
			await Task.Delay(400, cancellationToken).ConfigureAwait(false);
			await StageAsync(context, away, "up", cancellationToken).ConfigureAwait(false);
			await Task.Delay(250, cancellationToken).ConfigureAwait(false);
			var reaim = await context.RecipeAsync(
				"aim-at-cell",
				cancellationToken,
				("x", cell.X.ToString(CultureInfo.InvariantCulture)),
				("y", cell.Y.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
			if (!(Boolean(reaim, "ok") && Double(reaim, "distance") < 0.5))
			{
				continue;
			}

			ray = await context.RecipeAsync("swing-hits", cancellationToken).ConfigureAwait(false);
			if (ray is { } stepped && FirstBlockCell(stepped) is { } meets && meets != cell)
			{
				context.Note($"stepped back to {Describe(meets)}: the hand meets that block, not {Describe(cell)}");
				cell = meets;
			}
			else if (ray is { } clear && FirstBlockCell(clear) is not null)
			{
				context.Note($"stepped back: the hand now meets {Describe(cell)}");
			}
		}

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
			JsonElement? holdStaged = null;
			for (var attempt = 0; attempt < 24 && !broke; attempt++)
			{
				await Task.Delay(250, cancellationToken).ConfigureAwait(false);
				// The staged press is a state the client owns; re-stating it each tick keeps the hold visible even when
				// the channel drops it between frames (measured 2026-11-22: 703 frames asked the attack key and the game
				// read "not held" while the row believed it was holding). It has to be the game's own bind staged *down*
				// rather than a click: a click is over before `HandleAttacks` runs — measured 2026-11-22, the branch's own
				// probe read `probeHeld=false` while staging a click, which is why no swing ever happened.
				holdStaged = await StageReadingAsync(context, "attack", "down", cancellationToken).ConfigureAwait(false);
				// The game asks for the attack with `Input.GetKey(<the attack bind>)`, and that bind is a mouse button.
				// A staged key and a staged button are two different tables in the input seam (measured 2026-11-22: the
				// key table said held while the game's own probe inside `HandleAttacks` read `probeHeld=false`), so the
				// row holds both, the way a player's finger is on the button and the button is down.
				await context.RecipeAsync(
					"virtual-input",
					cancellationToken,
					("mode", "press"),
					("x", "0"),
					("y", "0"),
					("button", "0")).ConfigureAwait(false);
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

				// The cell the game hurts is the cell of the swing's hit point, and the hit point moves with the aim: the
				// crosshair sits on the cell the camera names, the hand's 4.5-unit ray enters the block a fraction over,
				// and a `Chunk` collider covers many cells (measured 2026-11-25: the row held the attack on (4,469) while
				// every hit point fell in (3,469), which is where the damage went). So the row watches the cell the game
				// is really about to hurt: after each swing it reads the ray and follows the block the hand lands in.
				var landedRay = await context.RecipeAsync("swing-hits", cancellationToken).ConfigureAwait(false);
				if (landedRay is { } land && FirstBlockCell(land) is { } handLandsIn && handLandsIn != cell)
				{
					var atLanding = await ReadCellAsync(context, handLandsIn, false, cancellationToken).ConfigureAwait(false);
					context.Note($"the swing lands in {Describe(handLandsIn)}, not in {Describe(cell)}: the row follows the block the game hurts");
					cell = handLandsIn;
					if (Integer(atLanding, "damage") > 0)
					{
						damageRose = true;
						hostAfter = atLanding;
					}
					else
					{
						hostBefore = atLanding;
					}
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
				$"holding the game's attack at the cell ({Describe(cell)}) never raised its damage: {Raw(hostAfter)}; the attack branch's own gates read {Raw(branchDuring)}; the input channel installed with {Raw(install)}; one swing through the game's own entry read {Raw(swingProbe)}; the game's own aim and hands while the attack was held were {Raw(gatesDuring)}; the pointer reached {Raw(aimRoundTrip)}; the input the game saw was {Raw(staged)}; the gates were {Raw(clock)}; while the attack was held the client drew {framesDrawn} frame(s) and entered its input path {entriesGained} more time(s), and its attack path read {Raw(entriesAfter)}; the pointer truth at aim was {Raw(truthAtAim)} and one second later {Raw(truthLater)}; what the swing meets is {Raw(swingHits)}; the row's own hold staged {Raw(holdStaged)}");
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
			if (probed >= 6)
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
			var held = await context.RecipeAsync(
				"block-read-at",
				cancellationToken,
				("x", candidate.X.ToString(CultureInfo.InvariantCulture)),
				("y", candidate.Y.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
			// The scan can name a cell whose block is already gone: measured 2026-11-22, the row aimed at a cell that read
			// `block 0`, the game's attack method never ran (`handleAttacksEntries 0`) and nothing could have been dug.
			// A player cannot dig air, so a candidate must still hold its block.
			if (held is not { } row || Integer(row, "block") < 1)
			{
				continue;
			}

			if (probe is { } answer && Boolean(answer, "blockGround"))
			{
				grounded.Add(candidate);
			}
		}

		context.Read(
			"host-ground-probes",
			JsonSerializer.SerializeToElement(new { probed, grounded = grounded.Count, offered = candidates.Count, last = Raw(lastProbe) }));
		// A world can offer breakable cells none of whose colliders carry the tag a swing damages (measured 2026-11-22:
		// every probe in reach read `hits:0, blockGround:false`, and the row went on to swing at a cell nothing could
		// hurt). So the row scans wider before it gives up, and when the wide scan also finds nothing it says so with
		// the probes it has instead of walking to a cell and swinging at nothing.
		if (grounded.Count == 0)
		{
			// Each probe is a round trip through the client, so the wider pass takes the nearest few only: the scan
			// answers nearest-first, and a player digs what they can see rather than the farthest thing in reach.
			var wider = await FindBreakableCellsAsync(context, cancellationToken, radius: 24).ConfigureAwait(false);
			var wideProbed = 0;
			foreach (var candidate in wider)
			{
				if (wideProbed >= 6)
				{
					break;
				}

				wideProbed++;
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

		// The caller decides whether an empty answer is patience or a defect: the world can still be building its
		// colliders on the first pass, so a hard failure here would call a loading world a broken one.
		return grounded;
	}

	private static async Task<List<(int X, int Y)>> FindBreakableCellsAsync(CaseContext context, CancellationToken cancellationToken, int radius = 16, int count = 24)
	{
		var found = await context.RecipeAsync("block-find-breakable", cancellationToken, ("count", count.ToString(CultureInfo.InvariantCulture)), ("minOffset", "1"), ("radius", radius.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
		context.Read("host-breakable", found);
		// A body that has just left the capsule can stand in open ground with nothing breakable inside the first
		// radius: the pod lands near the map's edge in some worlds (measured 2026-11-22: the scan answered `count 0`
		// from (-16,481)). A player walks further and looks again, so the row widens the scan once before giving up.
		if (found is { } empty && Integer(empty, "count") == 0)
		{
			found = await context.RecipeAsync(
				"block-find-breakable",
				cancellationToken,
				("count", (count * 2).ToString(CultureInfo.InvariantCulture)),
				("minOffset", "1"),
				("radius", (radius * 2).ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
			context.Read("host-breakable-wider", found);
		}
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
		var reachable = cells.Where(cell => Math.Abs(cell.X - bodyX) <= 20 && Math.Abs(cell.Y - bodyY) <= 20).ToList();
		Require(reachable.Count > 0, $"the scan found {cells.Count} breakable cell(s) but none within walking distance "
			+ $"(body at {bodyX},{bodyY}; first cells " + string.Join(" ", cells.Take(6).Select(Describe)) + "): " + Raw(found));
		// A person digs the wall in front of them, at the height they stand on: the body walks left and right (the
		// game writes `moveDir.x` into the velocity, `Body.cs:2384`) and only climbs or jumps on `moveDir.y`, so a block
		// several rows above or below is not something a walk can reach (measured 2026-11-22: the scan's nearest cell
		// sat five rows up, the walk pressed `up` for forty steps, and `afterMove` stayed `dir 0,0`). Cells on the
		// body's own level come first; the rest stay as a fallback for worlds whose floor offers nothing level.
		return [.. reachable
			.OrderBy(cell => Math.Abs(cell.Y - bodyY))
			.ThenBy(cell => Math.Abs(cell.X - bodyX) + Math.Abs(cell.Y - bodyY))];
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
	private static async Task<(double X, double Y, string Trace)> LeaveThePodAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var trace = new StringBuilder();
		var motion = await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
		var startX = Double(motion, "posX");
		var startY = Double(motion, "posY");
		// The world is not the player's to steer until the capsule lands: a body pressed at second one stands still no
		// matter the direction (measured 2026-11-22: `[up 0.00] [down 0.00]` from a settled body, while the same keys
		// walked thirty-six units in a later run). So the row waits until the body moves on its own, or twelve seconds.
		for (var wait = 0; wait < 48; wait++)
		{
			var watching = await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
			if (Distance(startX, startY, Double(watching, "posX"), Double(watching, "posY")) > 1.0)
			{
				trace.Append($" [settled at step {wait}]");
				break;
			}

			await Task.Delay(250, cancellationToken).ConfigureAwait(false);
		}

		// The hatch is an interactable of the capsule, and a body that never presses the interact key stays inside.
		await StageAsync(context, "iteminteract", "down", cancellationToken).ConfigureAwait(false);
		await Task.Delay(400, cancellationToken).ConfigureAwait(false);
		await StageAsync(context, "iteminteract", "up", cancellationToken).ConfigureAwait(false);
		await Task.Delay(250, cancellationToken).ConfigureAwait(false);
		// Each direction is tested from a body that has stopped: the floor slides, so a direction measured while the
		// body is still gliding from the last one reads as if it moved (measured 2026-11-22: `right 2.31`, `left 4.08`
		// and `up 4.08` in one spawn, all of it the same slide).
		foreach (var brake in new[] { "left", "right", "up", "down" })
		{
			await StageAsync(context, brake, "up", cancellationToken).ConfigureAwait(false);
		}

		await Task.Delay(600, cancellationToken).ConfigureAwait(false);
		var exitBind = "";
		var exitMoved = 0.0;
		foreach (var bind in new[] { "up", "down", "right", "left" })
		{
			var settled = await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
			var fromX = Double(settled, "posX");
			var fromY = Double(settled, "posY");
			await StageAsync(context, bind, "down", cancellationToken).ConfigureAwait(false);
			await Task.Delay(1400, cancellationToken).ConfigureAwait(false);
			var probe = await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
			await StageAsync(context, bind, "up", cancellationToken).ConfigureAwait(false);
			await Task.Delay(300, cancellationToken).ConfigureAwait(false);
			var moved = Distance(fromX, fromY, Double(probe, "posX"), Double(probe, "posY"));
			trace.Append($" [{bind} {moved.ToString("0.00", CultureInfo.InvariantCulture)}]");
			if (moved > exitMoved)
			{
				exitMoved = moved;
				exitBind = bind;
			}
		}

		if (exitBind.Length == 0)
		{
			return (startX, startY, trace.Append(" [the body answered no direction]").ToString());
		}

		var lastX = startX;
		var lastY = startY;
		for (var step = 0; step < 40; step++)
		{
			await StageAsync(context, exitBind, "down", cancellationToken).ConfigureAwait(false);
			await Task.Delay(140, cancellationToken).ConfigureAwait(false);
			var out2 = await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
			lastX = Double(out2, "posX");
			lastY = Double(out2, "posY");
			if (Distance(startX, startY, lastX, lastY) > 3.5)
			{
				break;
			}
		}

		trace.Append($" [out {lastX:0.00},{lastY:0.00} on {exitBind}]");
		return (lastX, lastY, trace.ToString());
	}

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
		var exit = await LeaveThePodAsync(context, cancellationToken).ConfigureAwait(false);
		posX = exit.X;
		posY = exit.Y;
		trace.Append("; leave-pod " + exit.Trace);
		var flippedX = false;
		var flippedY = false;
		var missesX = 0;
		var missesY = 0;
		// Sixteen steps of two ticks is enough to cross the room and keeps the case inside its budget: every tick is a
		// websocket turn on the game thread, and forty steps of four ticks cost 238 s of a 240 s case (`row1-171`).
		for (var step = 0; step < 16; step++)
		{
			var away = Distance(posX, posY, cell.X + 0.5, cell.Y + 0.5);
			// A person does not walk to a coordinate; they stop when the wall is within reach of the swing. The scan's
			// cell can sit behind the pod's shell while the rock the swing would really meet lies beside it (measured
			// 2026-11-22: the body stalled at x -4.08, and the ray from there met a `BlockGround` at 2.4 units), so the
			// walk asks the hand's own ray every few steps and stops at the first block it can actually hit.
			if (step % 4 == 3)
			{
				var reachRay = await context.RecipeAsync("swing-hits", cancellationToken).ConfigureAwait(false);
				if (reachRay is { } reachReading && FirstBlockCell(reachReading) is { } hitNow
					&& Distance(posX, posY, hitNow.X + 0.5, hitNow.Y + 0.5) <= 4.2)
				{
					cell = hitNow;
					trace.Append($"; the swing reached {Describe(cell)} while walking");
					break;
				}
			}

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

			// The ground in this game is slippery enough that a held key carries the body past the block: measured
			// 2026-11-22, three 120 ms presses took the body from x 0 to x 30 with `velX 25`, and the row ended sixty
			// units away. A person lifts their finger and lets the body coast to a stop, so the last few units are
			// covered with every key released and the position re-read after the slide.
			if (away <= 6.0)
			{
				foreach (var brake in new[] { "left", "right", "up", "down" })
				{
					await StageAsync(context, brake, "up", cancellationToken).ConfigureAwait(false);
				}

				await Task.Delay(320, cancellationToken).ConfigureAwait(false);
				var coasted = await context.RecipeAsync("body-motion", cancellationToken).ConfigureAwait(false);
				posX = Double(coasted, "posX");
				posY = Double(coasted, "posY");
				trace.Append($"; coast {posX:0.00},{posY:0.00} v{String(coasted, "velX")}");
				continue;
			}

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
			for (var tick = 0; tick < 2; tick++)
			{
				// Re-stating the hold every tick, not only when the body stalls: the staged answer expires within a few
				// frames (measured 2026-11-22: `held=false` three ticks after a hold was staged), and a person's key
				// stays down for as long as they lean on it.
				await StageAsync(context, bind, "down", cancellationToken).ConfigureAwait(false);
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
					// Which key the seam answered for, and whether it answers at all: `stage held=false` is the real
					// keyboard and proves nothing, while `answered` is the seam's own verdict on the same question the
					// game's movement line asks (measured 2026-11-22: every gate read clear and the body still stood).
					var again = await StageReadingAsync(context, bind, "down", cancellationToken).ConfigureAwait(false);
					held.Append($"; stage held={String(stagedReport, "held")} keys={String(stagedReport, "stagedKeys")} ans={String(again, "answered")} key={String(again, "key")} K{String(duringTruth, "keyHeldAnswers")} Q{String(duringTruth, "keyQueries")}");
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

	private static async Task StageAsync(CaseContext context, string bind, string action, CancellationToken cancellationToken) =>
		await StageReadingAsync(context, bind, action, cancellationToken).ConfigureAwait(false);

	// The same staging, keeping the reading: `answered` says whether the selection seam would answer the game's own
	// `GetKey` for that key at all, which is the difference between "the row staged nothing" and "the row staged a key
	// the game never asks about" (measured 2026-11-22: the row reported held, and the probe inside `HandleAttacks`
	// read not-held, so the two tables had to be told apart at the seam).
	private static async Task<JsonElement> StageReadingAsync(CaseContext context, string bind, string action, CancellationToken cancellationToken)
	{
		var staged = await context.RecipeAsync(
			"stage-bind",
			cancellationToken,
			("bind", bind),
			("action", action)).ConfigureAwait(false);
		Require(Boolean(staged, "ok"), $"the row could not {action} the game's '{bind}' bind: " + Raw(staged));
		return staged ?? default;
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

	private static string FirstObstacle(JsonElement ray, (int X, int Y) cell)
	{
		if (ray.ValueKind != JsonValueKind.Object || !ray.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
		{
			return "an unreadable ray (" + Raw(ray) + ")";
		}

		// The game's own unarmed swing walks the ray in ONE pass and only reaches the block branch while `flag` is
		// still false: a building or an animal hit earlier on the same ray sets it (`Body.cs:2151`), and the
		// `if (!flag)` block loop at `:2160-2176` then never runs. So the block has to be the FIRST thing the swing
		// meets after the player's own body — a pod's pump in front of the wall swallows the damage without any
		// error (measured 2026-11-22: 105 swings, `damage` stayed -1, and the ray read `Body 0` then
		// `lifepodpump 1.843` before the `Chunk(BlockGround) 3.85`).
		//
		// The first block on the ray also has to BE the cell the row means: the loop damages the first `BlockGround`
		// it meets, so a closer chunk on the way takes the damage instead (measured 2026-11-22: the ray was clean,
		// the game called its damage routine 27 times, and the chosen cell lost nothing).
		var nearest = "";
		foreach (var hit in hits.EnumerateArray())
		{
			if (String(hit, "tag") == "Player" && String(hit, "distance") == "0")
			{
				continue;
			}

			if (String(hit, "tag") == "BlockGround")
			{
				var blockCell = BlockCellAt(ray, hit);
				if (blockCell == cell)
				{
					return "";
				}

				return "the block at (" + blockCell.X + "," + blockCell.Y + ") at " + String(hit, "distance")
					+ " instead of " + Describe(cell);
			}

			// Loosely held things do not stop the block branch: `Body.cs:2162-2176` walks the whole ray and damages the
			// first `BlockGround` it contains, whatever sits between the hand and the wall. `flag` (which WOULD skip the
			// loop) is set only by an animal-class building hit at `:2116-2156`. Measured 2026-11-22: the gate reported
			// `lantern(Clone) (Untagged) at 3.184` in front of the block and refused a swing the game would have landed.
			if (String(hit, "tag") != "BlockGround")
			{
				if (nearest.Length == 0 && String(hit, "tag") != "Untagged")
				{
					nearest = String(hit, "name") + " (" + String(hit, "tag") + ") at " + String(hit, "distance");
				}

				continue;
			}
		}

		return nearest.Length == 0 ? "nothing at all" : nearest;
	}

	// The cell the game really damages: the ray recipe answers the WORLD point of the hit, stepped 0.05 along the
	// ray the way `Body.cs:2162-2168` steps it before it maps the point to a block, and a cell is the floor of that
	// point — the same space `block-find-breakable`, `block-probe` and `aim-at-cell` answer in. A `Chunk` collider
	// covers many cells, so the ray's own arithmetic (`from + dir * distance`) can name a neighbouring cell while the
	// hit point stands inside another (measured 2026-11-25: the row held the attack on the cell its crosshair named
	// and the swing's hit point fell one cell over, where the damage went).
	private static (int X, int Y) BlockCellAt(JsonElement ray, JsonElement hit)
	{
		if (hit.TryGetProperty("damageWorldX", out var damageX) && hit.TryGetProperty("damageWorldY", out var damageY))
		{
			return ((int)Math.Floor(damageX.GetDouble()), (int)Math.Floor(damageY.GetDouble()));
		}

		var fromX = Double(ray, "fromX");
		var fromY = Double(ray, "fromY");
		var dirX = Double(ray, "dirX");
		var dirY = Double(ray, "dirY");
		var distance = Double(hit, "distance");
		return ((int)Math.Floor(fromX + (dirX * distance)), (int)Math.Floor(fromY + (dirY * distance)));
	}

	// The block the swing would really hit first, in block coordinates: the row digs what stands in front of the hand,
	// which is not always the cell the camera's crosshair is on.
	private static (int X, int Y)? FirstBlockCell(JsonElement ray)
	{
		if (ray.ValueKind != JsonValueKind.Object || !ray.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
		{
			return null;
		}

		foreach (var hit in hits.EnumerateArray())
		{
			if (String(hit, "tag") == "BlockGround")
			{
				return BlockCellAt(ray, hit);
			}
		}

		return null;
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
