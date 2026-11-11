using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The host's `/heal` reaches every player in the session, not only the host: a wounded member is
/// restored on its OWN client. A row from the session/console sweep (the split body-verb service's
/// acceptance rows 1-2, which named `/heal @a`), read on the release build over the sandboxed
/// IP-direct session.
/// </summary>
internal sealed class ConsoleHealCase : AcceptanceCase
{
	public override string Name => "console-heal";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The member must be wounded for the heal to have something to restore: a cut on a limb is
		// the game's own wound seed, and the member's own client reads the limb it carries.
		var seeded = await context.MemberRecipeAsync("wound-seed", cancellationToken, ("limb", "0"), ("force", "300"), ("infection", "0")).ConfigureAwait(false);
		context.Read("member-wound", seeded);
		var wounded = await context.MemberRecipeAsync("limb-read", cancellationToken, ("source", "local"), ("limb", "0"), ("target", context.MemberId)).ConfigureAwait(false);
		context.Read("member-limb-wounded", wounded);

		// The host runs the console verb; its answer is the reading that the command was accepted.
		var answer = await context.Session.ActionAsync(context.Session.HostUrl, "console", cancellationToken, ("-Text", "/heal @a")).ConfigureAwait(false);
		context.Read("console-answer", answer);

		// The heal is applied on the member's client: the member's own limb is the judge, polled
		// because the command travels host → member before the body is rewritten.
		JsonElement? healed = null;
		var restored = false;
		for (var wait = 0; wait < 20; wait++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			healed = await context.MemberRecipeAsync("limb-read", cancellationToken, ("source", "local"), ("limb", "0"), ("target", context.MemberId)).ConfigureAwait(false);
			restored = Boolean(healed, "dismembered") == false
				&& Number(healed, "skinHealth") >= 99
				&& Number(healed, "bleedAmount") <= 1;
			if (restored)
			{
				break;
			}
		}

		context.Read("member-limb-healed", healed);
		Require(restored, "the member's limb was not restored by the host's /heal: " + Raw(healed));
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool? Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.True
			: null;

	private static double Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: throw new AssertionException($"the reading has no number '{property}': {Raw(reading)}");

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
