using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The remote medical panel draws the MEMBER, not the host: it opens on the member's body, its
/// header is the member's identity, and the three local-only surfaces (nap, workout, HUD hand swap)
/// are not presented for a remote focus. The row came from
/// `remote-medical-panel-hide-local-only-actions`.
/// </summary>
internal sealed class MedicalChartCase : AcceptanceCase
{
	public override string Name => "medical-chart";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// A leftover focus from an earlier run would answer `opened=false`; the case begins from the
		// game's own closed state.
		var closed = await context.RecipeAsync("wound-read", cancellationToken, ("toggle", "off"));
		context.Note("pre-close: " + Raw(closed));

		// The chart only opens while the LOCAL body is awake (the game's own wound view refuses on a
		// sleeping or dead viewer). A run's own character can come back asleep after a restart, so the
		// row waits for the game's own recovery instead of forcing the state: a viewer that is still
		// asleep after the wait is a reading this row reports, not something it papers over
		// (2026-11-23: `native-heal` and `body-force` used to write the state the row then judged).
		for (var wait = 0; wait < 40; wait++)
		{
			var viewer = await context.RecipeAsync("body-effects", cancellationToken).ConfigureAwait(false);
			if (Number(viewer, "consciousness") >= 60 && Boolean(viewer, "alive"))
			{
				break;
			}

			context.Note($"viewer asleep: {Raw(viewer)}");
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		var awake = await context.RecipeAsync("body-effects", cancellationToken).ConfigureAwait(false);
		context.Read("viewer", awake);
		Require(Boolean(awake, "alive"), "the host's body is not alive, so the game's wound view cannot open: " + Raw(awake));
		Require(Number(awake, "consciousness") >= 60,
			"the host's body stayed asleep, so the game's own wound view refuses to open: " + Raw(awake));

		// The member is focused where the game itself put it: no teleport, because a member standing at
		// its own pod is still a body the viewer can see and the chart is about that member either way.
		var memberBody = await context.MemberRecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
		context.Read("member-body", memberBody);
		var memberName = DrawnBody(memberBody);
		Require(memberName != "none", "the member's client could not name its own body: " + Raw(memberBody));

		// A real wound on the member, through the game's own damage entry on the member's client, so the
		// chart has a fact only the member carries: its bleed total. The game's own armour and limb
		// reduction can swallow a light hit, so the force climbs until the member's skin actually gives
		// way; a wound the game did not record proves nothing about the chart.
		JsonElement? seeded = null;
		foreach (var force in new[] { "60", "140", "260" })
		{
			seeded = await context.MemberRecipeAsync("wound-seed", cancellationToken, ("limb", "0"), ("force", force), ("infection", "0")).ConfigureAwait(false);
			context.Note($"member wound force {force}: {Raw(seeded)}");
			if (Number(seeded, "skinHealth") < 100 || Number(seeded, "bleedAmount") > 0)
			{
				break;
			}
		}

		context.Read("member-wound", seeded);
		Require(Boolean(seeded, "ok"), "the member could not take a wound from the game's own damage entry: " + Raw(seeded));
		Require(Number(seeded, "skinHealth") < 100 || Number(seeded, "bleedAmount") > 0,
			"the game's own damage entry left the member unwounded, so there is nothing for the chart to draw: " + Raw(seeded));

		// Our own panel is modal and would sit over the game's chart: a player reads the chart with the
		// online panel closed, so the row closes it first (2026-11-23: the frames showed the panel over
		// the whole screen while the row read the chart's objects underneath it).
		await context.Session.CloseWindowAsync(context.Session.HostUrl, cancellationToken).ConfigureAwait(false);

		JsonElement? opened = null;
		for (var attempt = 0; attempt < 3; attempt++)
		{
			if (attempt > 0)
			{
				// The game may still be closing its view from the previous case; a close and a fresh
				// ask in a later frame is the only path it offers.
				await context.RecipeAsync("wound-read", cancellationToken, ("toggle", "off"));
				await Task.Delay(TimeSpan.FromMilliseconds(900), cancellationToken).ConfigureAwait(false);
			}

			opened = await context.RecipeAsync("medical-open", cancellationToken, ("steamId", context.MemberId), ("name", ""));
			if (Boolean(opened, "opened"))
			{
				break;
			}

			context.Note($"open attempt {attempt + 1} answered false: {Raw(opened)}");
		}

		context.Read("medical-open", opened);
		Require(Boolean(opened, "opened"), "the remote medical view did not open");
		Require(Boolean(opened, "nativeOpen"), "the game's own wound view did not open");

		// The local-only surfaces are hidden by the adapter's per-frame pass; the first frame after
		// the open may still show them, so the panel is read after it has had a frame to settle.
		await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);

		var panel = await context.RecipeAsync("panel-controls", cancellationToken);
		context.Read("panel-controls", panel);
		// The chart is what the player looks at: our own online panel is modal and must not be sitting
		// over it. The read is the window's own visible flag, closed again if something reopened it.
		var window = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		context.Read("host-window", window);
		if (Boolean(window, "windowVisible"))
		{
			await context.Session.CloseWindowAsync(context.Session.HostUrl, cancellationToken).ConfigureAwait(false);
			window = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			context.Read("host-window-closed", window);
		}

		Require(!Boolean(window, "windowVisible"), "our online panel is still over the game's chart: " + Raw(window));
		Require(Boolean(panel, "viewOpen"), "the panel is not open");
		Require(Boolean(panel, "remoteFocus"), "the panel is not focused on the member");
		// The game's own chart is drawing the member. The adapter shows it a display copy of the remote
		// body (named `MedicalDisplay_…`), so identity is read as the two facts a viewer can check: the
		// copy stands where the member stands, and it carries the member's own number of limb rows. A
		// chart still pointed at the viewer would fail both.
		Require(DrawnBody(panel) != "none", "the chart holds no body: " + Raw(panel));
		// The copy carries the member's own wound. The game's own chart draws the skin of the limb the
		// damage entry hit, so the chart's first limb must read the member's wounded skin value; a chart
		// still pointed at the viewer would read an untouched 100 here. The game's chart clamps the skin
		// it draws at zero (a limb hurt past zero reads a hair above 0 on screen), so the member's own
		// negative reading is clamped the same way before the two are compared — measured 2026-11-25:
		// the member's limb read -19.96 and the panel read 0.008.
		var memberSkin = Math.Max(0, Number(seeded, "skinHealth"));
		Require(Math.Abs(Number(panel, "skin0") - memberSkin) <= 0.5,
			"the chart shows limb 0 at " + Raw(panel) + " skin, but the member's own limb reads " + memberSkin.ToString("0.###", CultureInfo.InvariantCulture));
		Require(Number(panel, "limbRows") == Number(memberBody, "localBody", "limbs"),
			"the chart's limb rows do not match the member's body: " + Raw(panel));
		Require(!Boolean(panel, "napActive"), "the member's panel presents the local-only nap control");
		Require(!Boolean(panel, "sleepImageActive"), "the member's panel presents the local-only sleep icon");
		Require(!Boolean(panel, "workoutActive"), "the member's panel presents the local-only workout list");
		Require(!Boolean(panel, "handSwapGraphicEnabled"), "the HUD hand-swap graphic stays drawn for the member's chart");
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static string DrawnBody(JsonElement? reading)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element)
		{
			return "none";
		}

		if (element.TryGetProperty("chartBody", out var chart) && chart.ValueKind == JsonValueKind.String)
		{
			return chart.GetString() ?? "none";
		}

		return element.TryGetProperty("localBody", out var body)
			&& body.ValueKind == JsonValueKind.Object
			&& body.TryGetProperty("bodyName", out var name)
			&& name.ValueKind == JsonValueKind.String
			? name.GetString() ?? "none"
			: "none";
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

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static double Number(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty(property, out var value))
		{
			return 0;
		}

		return value.ValueKind switch
		{
			JsonValueKind.Number => value.TryGetDouble(out var number) ? number : 0,
			JsonValueKind.True => 1,
			_ => 0
		};
	}

	private static double Number(JsonElement? reading, string outer, string inner)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty(outer, out var nested)
			|| nested.ValueKind != JsonValueKind.Object)
		{
			return 0;
		}

		return Number(nested, inner);
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
