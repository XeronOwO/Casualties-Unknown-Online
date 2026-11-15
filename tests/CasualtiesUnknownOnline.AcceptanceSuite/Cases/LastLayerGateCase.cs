using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The last-layer attack/use gate is open on each client after the unchipped
/// cutscene. The host starts the game's own unchipped preset, both clients
/// generate the final layer, the native <c>EndLabScript</c> clears the local
/// <c>PlayerCamera.blockItemUsage</c>, and a staged attack on each client must
/// move that client's own native attack state.
/// </summary>
internal sealed class LastLayerGateCase : AcceptanceCase
{
	public override string Name => "last-layer-gate";

	public override CaseCost Cost => CaseCost.NewWorld;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var preset = await context.RecipeAsync("run-preset", cancellationToken, ("name", "unchipped")).ConfigureAwait(false);
		context.Read("preset", preset);
		Require(Boolean(preset, "ok") == true, "the host's run preset could not be selected: " + Raw(preset));

		var restart = await context.Session.RestartRunAsync(cancellationToken).ConfigureAwait(false);
		context.Note(restart);
		await UseCurrentMemberAsync(context, cancellationToken).ConfigureAwait(false);
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);

			var setLayer = await context.RecipeAsync("set-layer", cancellationToken, ("depth", "10")).ConfigureAwait(false);
			context.Read("set-layer", setLayer);
			Require(Boolean(setLayer, "ok") == true, "the host's layer could not be moved to the last layer: " + Raw(setLayer));

			var descent = await context.RecipeAsync("layer-descend", cancellationToken, ("twice", "0")).ConfigureAwait(false);
			context.Read("descent", descent);
			Require(Boolean(descent, "ok") == true, "the host's final-layer descent did not start: " + Raw(descent));

			var host = await WaitForLayerAsync(context, cancellationToken).ConfigureAwait(false);
			var member = await WaitForLayerAsync(context, cancellationToken, member: true).ConfigureAwait(false);

			Require(Int(host, "layer") == 11, "the host did not generate the ending lab: " + Raw(host));
			Require(Int(member, "layer") == 11, "the member did not generate the ending lab: " + Raw(member));
			Require(Boolean(host, "unchipped") == true, "the host's last layer is not unchipped: " + Raw(host));
			Require(Boolean(member, "unchipped") == true, "the member's last layer is not unchipped: " + Raw(member));
			Require(Boolean(host, "endLab") == true, "the host has no EndLabScript instance: " + Raw(host));
			Require(Boolean(member, "endLab") == true, "the member has no EndLabScript instance: " + Raw(member));

			await NudgeIntoCutsceneAsync(context, cancellationToken).ConfigureAwait(false);
			host = await WaitForGateOpenAsync(context, cancellationToken).ConfigureAwait(false);
			member = await WaitForGateOpenAsync(context, cancellationToken, member: true).ConfigureAwait(false);
			context.Read("host", host);
			context.Read("member", member);
			Require(Boolean(host, "blockItemUsage") == false, "the host's last-layer cutscene did not clear blockItemUsage: " + Raw(host));
			Require(Boolean(member, "blockItemUsage") == false, "the member's last-layer cutscene did not clear blockItemUsage: " + Raw(member));

			var hostBefore = await context.RecipeAsync("last-layer-read", cancellationToken).ConfigureAwait(false);
			var memberBefore = await context.MemberRecipeAsync("last-layer-read", cancellationToken).ConfigureAwait(false);
			context.Read("host-before-attack", hostBefore);
			context.Read("member-before-attack", memberBefore);

			var pressHost = await context.RecipeAsync(
				"virtual-input",
				cancellationToken,
				("mode", "press-key"),
				("x", "0"),
				("y", "0"),
				("button", "0")).ConfigureAwait(false);
			var pressMember = await context.MemberRecipeAsync(
				"virtual-input",
				cancellationToken,
				("mode", "press-key"),
				("x", "0"),
				("y", "0"),
				("button", "0")).ConfigureAwait(false);
			context.Read("press-host", pressHost);
			context.Read("press-member", pressMember);

			await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
			var hostAfter = await context.RecipeAsync("last-layer-read", cancellationToken).ConfigureAwait(false);
			var memberAfter = await context.MemberRecipeAsync("last-layer-read", cancellationToken).ConfigureAwait(false);
			context.Read("host-after-attack", hostAfter);
			context.Read("member-after-attack", memberAfter);

			Require(Boolean(hostAfter, "blockItemUsage") == false, "the host's attack gate closed before the attack: " + Raw(hostAfter));
			Require(Boolean(memberAfter, "blockItemUsage") == false, "the member's attack gate closed before the attack: " + Raw(memberAfter));
			Require(Attacked(hostBefore, hostAfter), "the host's staged attack did not reach the native attack state: before " + Raw(hostBefore) + " after " + Raw(hostAfter));
			Require(Attacked(memberBefore, memberAfter), "the member's staged attack did not reach the native attack state: before " + Raw(memberBefore) + " after " + Raw(memberAfter));
		}
		finally
		{
			try
			{
				await context.RecipeAsync("virtual-input", cancellationToken, ("mode", "uninstall"), ("x", "0"), ("y", "0"), ("button", "0")).ConfigureAwait(false);
				await context.MemberRecipeAsync("virtual-input", cancellationToken, ("mode", "uninstall"), ("x", "0"), ("y", "0"), ("button", "0")).ConfigureAwait(false);
			}
			catch (Exception exception)
			{
				context.Note("could not uninstall the virtual input: " + exception.Message);
			}

			try
			{
				await context.RecipeAsync("run-preset", cancellationToken, ("name", "")).ConfigureAwait(false);
			}
			catch (Exception exception)
			{
				context.Note("could not clear the run preset after the case: " + exception.Message);
			}
		}
	}

	private static async Task NudgeIntoCutsceneAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The ending lab places the two local bodies at different x positions. The cutscene's
		// own first wait is body.x < 17, so put each local body at the lab mouth after the
		// layer has settled; this is the same place the player's walk reaches.
		await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		var host = await context.RecipeAsync("last-layer-read", cancellationToken).ConfigureAwait(false);
		var member = await context.MemberRecipeAsync("last-layer-read", cancellationToken).ConfigureAwait(false);
		var movedHost = await context.RecipeAsync("body-place", cancellationToken, ("x", "16"), ("y", Double(host, "bodyY").ToString("0.###", CultureInfo.InvariantCulture))).ConfigureAwait(false);
		context.Read("host-body-place", movedHost);
		var movedMember = await context.MemberRecipeAsync("body-place", cancellationToken, ("x", "16"), ("y", Double(member, "bodyY").ToString("0.###", CultureInfo.InvariantCulture))).ConfigureAwait(false);
		context.Read("member-body-place", movedMember);
	}

	private static async Task<JsonElement?> WaitForLayerAsync(CaseContext context, CancellationToken cancellationToken, bool member = false)
	{
		JsonElement? last = null;
		for (var attempt = 0; attempt < 120; attempt++)
		{
			last = member
				? await context.MemberRecipeAsync("last-layer-read", cancellationToken).ConfigureAwait(false)
				: await context.RecipeAsync("last-layer-read", cancellationToken).ConfigureAwait(false);
			if (Boolean(last, "ok") == true
				&& Int(last, "layer") == 11
				&& Boolean(last, "endLab") == true)
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	private static async Task<JsonElement?> WaitForGateOpenAsync(CaseContext context, CancellationToken cancellationToken, bool member = false)
	{
		JsonElement? last = null;
		for (var attempt = 0; attempt < 120; attempt++)
		{
			try
			{
				if (member)
				{
					await context.MemberRecipeAsync("cutscene-skip", cancellationToken).ConfigureAwait(false);
				}
				else
				{
					await context.RecipeAsync("cutscene-skip", cancellationToken).ConfigureAwait(false);
				}
			}
			catch (Exception exception)
			{
				context.Note("cutscene skip did not answer: " + exception.Message);
			}

			last = member
				? await context.MemberRecipeAsync("last-layer-read", cancellationToken).ConfigureAwait(false)
				: await context.RecipeAsync("last-layer-read", cancellationToken).ConfigureAwait(false);
			if (Boolean(last, "ok") == true
				&& Int(last, "layer") == 11
				&& Boolean(last, "blockItemUsage") == false)
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	private static async Task UseCurrentMemberAsync(CaseContext context, CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < 40; attempt++)
		{
			var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
			if (members.Length > 0)
			{
				context.UseMember(members[0]);
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}
	}

	private static bool Attacked(JsonElement? before, JsonElement? after) =>
		Double(after, "attackCooldown") > Double(before, "attackCooldown") + 0.01
		|| Math.Abs(Double(after, "attackRot") - Double(before, "attackRot")) > 0.1;

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool? Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.True
			: null;

	private static int? Int(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.TryGetInt32(out var number)
			? number
			: null;

	private static double Double(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.TryGetDouble(out var number)
			? number
			: 0;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
