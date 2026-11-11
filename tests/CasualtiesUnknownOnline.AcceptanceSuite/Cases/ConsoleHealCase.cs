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
		// The verb resets a body that is in the world; a host that is still at the menu answers
		// "this client is not in the world, so there is no body to reset" — the reading an earlier
		// suite run produced. Wait for the world, then read the member the host will heal.
		var hostState = await context.Session
			.WaitUntilAsync(context.Session.HostUrl, state => SessionDriver.StateTrue(state, "inWorld") && !SessionDriver.StateTrue(state, "gateWaiting"), TimeSpan.FromSeconds(60), cancellationToken)
			.ConfigureAwait(false);
		Require(SessionDriver.StateTrue(hostState, "inWorld"), "the host is not in a world");

		var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		if (members.Length > 0)
		{
			context.UseMember(members[0]);
		}

		// The heal is EXECUTED on the target's own client (the host only relays the verb), so the
		// member must be in the world too — a member at the menu answers "no body to reset".
		var joined = await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		context.Note("member before the heal: " + joined);
		var memberState = await context.Session
			.WaitUntilAsync(context.Session.MemberUrl, state => SessionDriver.StateTrue(state, "inWorld"), TimeSpan.FromSeconds(30), cancellationToken)
			.ConfigureAwait(false);
		Require(SessionDriver.StateTrue(memberState, "inWorld"), "the member is not in the world to be healed");

		// The reset needs a BODY, not just an in-world flag: the member's own body must have a
		// position before the verb lands ("this client is not in the world, so there is no body to
		// reset" was the answer a host-side heal got while the scene was still coming up).
		for (var wait = 0; wait < 40; wait++)
		{
			var probe = await context.MemberRecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
			if (probe is { ValueKind: JsonValueKind.Object } probeBody
				&& probeBody.TryGetProperty("localBody", out var local)
				&& local.ValueKind == JsonValueKind.Object
				&& local.TryGetProperty("positionY", out var y)
				&& y.ValueKind == JsonValueKind.Number
				&& y.GetDouble() > 0)
			{
				context.Read("member-body", probe);
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		// The verb itself runs in the HOST's world context: its own body must exist (the answer
		// "this client is not in the world, so there is no body to reset" was a host whose state
		// said in-world while its body was still coming up).
		for (var wait = 0; wait < 40; wait++)
		{
			var hostBody = await context.RecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
			if (hostBody is { ValueKind: JsonValueKind.Object } hostBodyElement
				&& hostBodyElement.TryGetProperty("localBody", out var hostLocal)
				&& hostLocal.ValueKind == JsonValueKind.Object
				&& hostLocal.TryGetProperty("positionY", out var hostY)
				&& hostY.ValueKind == JsonValueKind.Number
				&& hostY.GetDouble() > 0)
			{
				context.Read("host-body", hostBody);
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

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
		for (var wait = 0; wait < 60; wait++)
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
