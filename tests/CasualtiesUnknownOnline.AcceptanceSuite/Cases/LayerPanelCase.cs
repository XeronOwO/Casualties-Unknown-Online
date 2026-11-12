using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The end-of-layer panel as objects, on the host and on the member: before the body reaches the floor
/// the panel is inactive on the host, its floor line follows the world's own <c>halfHeight</c>
/// (<c>-(float)halfHeight + 3.1</c>), and the member's own copy shows no choice to anybody (the
/// user's ruling: a member's panel shows the choices to nobody). A row of the world domain.
/// </summary>
internal sealed class LayerPanelCase : AcceptanceCase
{
	public override string Name => "layer-panel";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);

		var host = await context.RecipeAsync("layer-panel-state", cancellationToken).ConfigureAwait(false);
		context.Read("host-panel", host);
		Require(Boolean(host, "ok") == true, "the host's panel could not be read: " + Raw(host));
		Require(Boolean(host, "panelActive") == false, "the panel was already open at the run's start: " + Raw(host));

		// The floor line is the game's own: the case checks the arithmetic the panel reports, so a
		// wrong halfHeight or a wrong offset is a visible reading rather than a silent pass.
		var halfHeight = Number(host, "halfHeight");
		var floorY = Number(host, "floorY");
		if (halfHeight is null || floorY is null)
		{
			throw new AssertionException("the panel did not report its floor line: " + Raw(host));
		}

		var expected = -halfHeight.Value + 3.1;
		Require(Math.Abs(floorY.Value - expected) <= 0.001, $"the floor line {floorY.Value} is not -halfHeight + 3.1 ({expected}).");

		var member = await context.MemberRecipeAsync("layer-panel-state", cancellationToken).ConfigureAwait(false);
		context.Read("member-panel", member);
		Require(Boolean(member, "ok") == true, "the member's panel could not be read: " + Raw(member));
		Require(Boolean(member, "choicesVisible") == false, "the member's own panel showed a choice: " + Raw(member));
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool? Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.True
			: null;

	private static double? Number(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty(property, out var value))
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
