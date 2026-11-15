using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The drill pod's team gate (<c>DrillPod.cs:18-33</c>). The native pod fires after ONE body crouches
/// under it for 5 s; in a session every living, connected member has to be inside the pod's own
/// 1.5 x 5 area. The acceptance harness drives the native sink directly (<c>pod-descent</c>) with
/// the host's body standing where the pod is; a member 100 m away must keep the layer unchanged,
/// and a member standing with the host must take both clients down the pod's two layers. Dead and
/// disconnected members are covered by <c>PodTeamGateTests</c>.
/// </summary>
internal sealed class PodTeamWaitCase : AcceptanceCase
{
	public override string Name => "pod-team-wait";

	public override CaseCost Cost => CaseCost.NewWorld;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		context.Note(await context.Session.RestartRunAsync(cancellationToken).ConfigureAwait(false));
		var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		Require(members.Length == 1, "the fresh run does not have exactly one member on the host: " + members.Length);
		context.UseMember(members[0]);
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);

		// The host's own body is the pod the driver's native sink fires from. Put it at the origin,
		// and put the member far away: this is the "near one pressed it, the far one must not ride"
		// row. The bodies fall under the game's own physics afterwards; the gate is read at once.
		await PlaceAsync(context, member: false, "0", "0", cancellationToken).ConfigureAwait(false);
		await PlaceAsync(context, member: true, "100", "0", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

		var hostBefore = await context.RecipeAsync("layer-state", cancellationToken).ConfigureAwait(false);
		var memberBefore = await context.MemberRecipeAsync("layer-state", cancellationToken).ConfigureAwait(false);
		context.Read("host:before", hostBefore);
		context.Read("member:before", memberBefore);
		var depthBefore = Int(hostBefore, "biomeDepth");
		Require(depthBefore >= 0, "the host's layer state is not readable: " + Raw(hostBefore));

		var refused = await context.Session.ActionAsync(
			context.Session.HostUrl,
			"pod-descent",
			cancellationToken).ConfigureAwait(false);
		context.Read("host:refused", refused);
		await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken).ConfigureAwait(false);
		var hostRefused = await context.RecipeAsync("layer-state", cancellationToken).ConfigureAwait(false);
		var memberRefused = await context.MemberRecipeAsync("layer-state", cancellationToken).ConfigureAwait(false);
		context.Read("host:after-refused", hostRefused);
		context.Read("member:after-refused", memberRefused);
		Require(Int(hostRefused, "biomeDepth") == depthBefore,
			"the host descended although the member was 100 m from the pod: " + Raw(hostRefused));
		Require(Int(memberRefused, "biomeDepth") == depthBefore,
			"the member descended although it was 100 m from the pod: " + Raw(memberRefused));

		// The host's own refusal names the member. `PodTeamGate.Label` carries the display name and
		// the SteamId; the case's member id makes the line specific to this member.
		var refusalLog = await context.Session.LogTailAsync(
			context.Session.HostUrl, "[PodTrigger]", 8, cancellationToken).ConfigureAwait(false);
		context.Read("host:pod-log", refusalLog);
		var refusalText = refusalLog is { ValueKind: JsonValueKind.String } value ? value.GetString() ?? string.Empty : Raw(refusalLog);
		Require(refusalText.Contains(context.MemberId, StringComparison.Ordinal),
			"the host's pod refusal does not name the missing member " + context.MemberId + ": " + refusalText);

		// Both living bodies at the pod: the same native sink must now take both clients down two layers.
		await PlaceAsync(context, member: true, "0", "0", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		var hostReady = await context.RecipeAsync("layer-state", cancellationToken).ConfigureAwait(false);
		var memberReady = await context.MemberRecipeAsync("layer-state", cancellationToken).ConfigureAwait(false);
		context.Read("host:ready", hostReady);
		context.Read("member:ready", memberReady);
		Require(Int(hostReady, "biomeDepth") == depthBefore,
			"placing the member at the pod changed the layer before the trigger: " + Raw(hostReady));

		var fired = await context.Session.ActionAsync(
			context.Session.HostUrl,
			"pod-descent",
			cancellationToken).ConfigureAwait(false);
		context.Read("host:fired", fired);
		var descended = await WaitForDepthAsync(context, depthBefore + 2, cancellationToken).ConfigureAwait(false);
		context.Read("host:descended", descended.host);
		context.Read("member:descended", descended.member);
		Require(descended.hostDepth == depthBefore + 2,
			"the host did not take the pod's two layers: before=" + depthBefore + " " + Raw(descended.host));
		Require(descended.memberDepth == depthBefore + 2,
			"the member did not follow the pod's two layers: before=" + depthBefore + " " + Raw(descended.member));
		Require(descended.hostDepth == descended.memberDepth,
			"the two clients disagree on the layer after the pod descent: host=" + descended.hostDepth + " member=" + descended.memberDepth);
	}

	private static async Task<(int hostDepth, int memberDepth, JsonElement? host, JsonElement? member)> WaitForDepthAsync(
		CaseContext context, int expected, CancellationToken cancellationToken)
	{
		JsonElement? host = null;
		JsonElement? member = null;
		for (var attempt = 0; attempt < 90; attempt++)
		{
			host = await context.RecipeAsync("layer-state", cancellationToken).ConfigureAwait(false);
			member = await context.MemberRecipeAsync("layer-state", cancellationToken).ConfigureAwait(false);
			var hostDepth = Int(host, "biomeDepth");
			var memberDepth = Int(member, "biomeDepth");
			if (hostDepth == expected && memberDepth == expected)
			{
				return (hostDepth, memberDepth, host, member);
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		return (Int(host, "biomeDepth"), Int(member, "biomeDepth"), host, member);
	}

	private static async Task PlaceAsync(CaseContext context, bool member, string x, string y, CancellationToken cancellationToken)
	{
		var placed = member
			? await context.MemberRecipeAsync("body-place", cancellationToken, ("x", x), ("y", y)).ConfigureAwait(false)
			: await context.RecipeAsync("body-place", cancellationToken, ("x", x), ("y", y)).ConfigureAwait(false);
		context.Read(member ? "place:member" : "place:host", placed);
	}

	private static int Int(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
			&& element.TryGetProperty(property, out var value)
			&& value.ValueKind == JsonValueKind.Number
			&& value.TryGetInt32(out var number)
				? number
				: -1;

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
