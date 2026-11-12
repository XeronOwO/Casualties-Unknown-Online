using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A host's own eat plays on the member's machine: the host provides a snack, the member arms its
/// one-shot sound recorder, the host uses the item from its hand, and the member's recorder must carry
/// the eat's own call (the sound the game plays on the body that ate, relayed to the other client).
/// The sounds domain's cross-client row.
/// </summary>
internal sealed class EatSoundCase : AcceptanceCase
{
	public override string Name => "eat-sound";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForCustodyAsync(cancellationToken).ConfigureAwait(false);

		await context.ClearBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.ClearMemberBodyAsync(cancellationToken).ConfigureAwait(false);

		var provided = await context.RecipeAsync("item-provide", cancellationToken, ("type", "dogfood"), ("mode", "create")).ConfigureAwait(false);
		context.Read("provided", provided);
		Require(Text(provided, "ok") == "true" || Boolean(provided, "ok") == true, "the host could not be given a snack: " + Raw(provided));
		var slot = Number(provided, "slot");

		var memberArmed = await context.MemberRecipeAsync("sound-log", cancellationToken, ("mode", "start"), ("match", "eatFlesh")).ConfigureAwait(false);
		context.Read("member armed", memberArmed);

		var used = await context.RecipeAsync("item-use", cancellationToken,
			("type", "dogfood"),
			("aim-x", "0"),
			("aim-y", "0")).ConfigureAwait(false);
		context.Read("host use", used);
		if (slot is null)
		{
			context.Notes.Add("the provided snack did not name its slot; the use reads the hand's own item");
		}

		var heard = await context.MemberRecipeAsync("sound-log", cancellationToken, ("mode", "read"), ("match", "eatFlesh")).ConfigureAwait(false);
		context.Read("member heard", heard);
		var line = Text(heard, "line");
		Require(line.Contains("eatFlesh@", StringComparison.Ordinal),
			"the member's recorder did not carry the eat: " + Raw(heard));
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool? Boolean(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty(property, out var value))
		{
			return null;
		}

		return value.ValueKind switch
		{
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			_ => null,
		};
	}

	private static double? Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: null;

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
