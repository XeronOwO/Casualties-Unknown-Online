using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A peg limb fitted on a real stump, through the game's own fitting action: cut a limb, hand the host the game's
/// peglimb item, apply it to the limb that carries the stump (`Item.cs:745-785` runs `Limb.AttachProsthetic`), and read
/// the limb back. The wire carries this state to the member, which is why the row is a session row and not a unit test.
/// </summary>
internal sealed class ProstheticCase : AcceptanceCase
{
	public override string Name => "prosthetic";

	/// <summary>The prosthetic the row stages into a body.</summary>
	public override CaseLeftovers Leaves => CaseLeftovers.Bag | CaseLeftovers.Hands;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.Session.CloseWindowAsync(context.Session.HostUrl, cancellationToken).ConfigureAwait(false);

		var stump = await CutAnExtremityAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("stump", stump);
		Require(Boolean(stump, "ok") == true, "no limb could be amputated through the game's own entry: " + Raw(stump));
		var stumpLimb = Integer(stump, "limb");
		Require(stumpLimb >= 0, "the amputation did not name the limb it cut: " + Raw(stump));

		var provided = await context.RecipeAsync("item-provide", cancellationToken, ("type", "peglimb"), ("mode", "create")).ConfigureAwait(false);
		Require(Boolean(provided, "ok") == true, "the game's peglimb item could not be granted: " + Raw(provided));

		var fitted = await context.RecipeAsync("peg-fit", cancellationToken, ("type", "peglimb")).ConfigureAwait(false);
		context.Read("fitted", fitted);
		Require(Boolean(fitted, "ok") == true, "the game's fitting action did not run: " + Raw(fitted));
		Require(Boolean(fitted, "attachedAfter") == true, "the stump carries no prosthetic after the fitting action: " + Raw(fitted));

		var limb = await context.RecipeAsync("limb-read", cancellationToken, ("source", "local"), ("limb", Integer(fitted, "stumpLimb").ToString()), ("target", "")).ConfigureAwait(false);
		context.Read("fitted-limb", limb);
		Require(Boolean(limb, "ok") == true, "the fitted limb could not be read: " + Raw(limb));
		Require(Boolean(limb, "dismembered") == false, "the limb is still a stump after the game attached the prosthetic: " + Raw(limb));

		var parts = await context.RecipeAsync("limb-prosthetic4", cancellationToken, ("mode", "read"), ("limb", Integer(fitted, "stumpLimb").ToString()), ("item", "")).ConfigureAwait(false);
		context.Read("fitted-parts", parts);
		Require(Boolean(parts, "ok") == true, "the prosthetic list could not be read: " + Raw(parts));
		Require(Text(parts, "result").Contains("peg", StringComparison.OrdinalIgnoreCase), "the limb's prosthetic list does not name the peg: " + Raw(parts));
	}

	/// <summary>
	/// Cuts an extremity through the game's own amputation entry, trying the limbs in order: `Limb.Dismember` refuses
	/// anything that is not an extremity by its own rule, so the row lets the game refuse and keeps the answer.
	/// </summary>
	private static async Task<JsonElement?> CutAnExtremityAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var last = (JsonElement?)null;
		for (var limb = 0; limb < 15; limb++)
		{
			var cut = await context.RecipeAsync("limb-dismember", cancellationToken, ("limb", limb.ToString()), ("mode", "make")).ConfigureAwait(false);
			last = cut;
			if (Boolean(cut, "ok") == true && Boolean(cut, "dismemberedAfter") == true)
			{
				return cut;
			}
		}

		return last;
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool? Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.True
			: null;

	private static int Integer(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.Number ? value.GetInt32() : -1
			: -1;

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
			: string.Empty;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
