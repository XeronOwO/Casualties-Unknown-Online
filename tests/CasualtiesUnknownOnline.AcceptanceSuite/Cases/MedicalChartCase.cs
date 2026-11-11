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

		// The chart only opens while the LOCAL body is alive (the game's own wound view refuses on a
		// dead viewer). The host may carry a wound from an earlier case; the console's own heal puts
		// the viewer back on its feet without touching the member.
		var healed = await context.RecipeAsync("native-heal", cancellationToken);
		context.Note("host heal: " + Raw(healed));

		// A member that joined a running world stands at its own pod; the game's own wound view opens
		// on a body the viewer can see, so the member is placed on the host's own point first (the
		// placement `GiveItemCase` proves).
		var hostBody = await context.WaitForHostBodyAsync(cancellationToken);
		context.Read("host-body", hostBody);
		Require(hostBody is not null, "the host's body never reached a world position");
		var (hostX, hostY) = Position(hostBody);
		Require(hostX != 0 || hostY > 0, "the host's body has no position yet: " + Raw(hostBody));
		var placed = await context.MemberRecipeAsync("teleport", cancellationToken,
			("x", hostX.ToString(CultureInfo.InvariantCulture)),
			("y", hostY.ToString(CultureInfo.InvariantCulture)));
		context.Note("member placed: " + Raw(placed));
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);

		var opened = await context.RecipeAsync("medical-open", cancellationToken, ("steamId", context.MemberId), ("name", ""));
		context.Read("medical-open", opened);
		Require(Boolean(opened, "opened"), "the remote medical view did not open");
		Require(Boolean(opened, "nativeOpen"), "the game's own wound view did not open");

		// The local-only surfaces are hidden by the adapter's per-frame pass; the first frame after
		// the open may still show them, so the panel is read after it has had a frame to settle.
		await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);

		var panel = await context.RecipeAsync("panel-controls", cancellationToken);
		context.Read("panel-controls", panel);
		Require(Boolean(panel, "viewOpen"), "the panel is not open");
		Require(Boolean(panel, "remoteFocus"), "the panel is not focused on the member");
		Require(!Boolean(panel, "napActive"), "the member's panel presents the local-only nap control");
		Require(!Boolean(panel, "sleepImageActive"), "the member's panel presents the local-only sleep icon");
		Require(!Boolean(panel, "workoutActive"), "the member's panel presents the local-only workout list");
		Require(!Boolean(panel, "handSwapGraphicEnabled"), "the HUD hand-swap graphic stays drawn for the member's chart");
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

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

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
