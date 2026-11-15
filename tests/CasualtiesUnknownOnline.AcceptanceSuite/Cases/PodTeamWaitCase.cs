using System.Globalization;
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

	public override CaseCost Cost => CaseCost.InRun;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		Require(members.Length == 1, "the current run does not have exactly one member on the host: " + members.Length);
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
		// the SteamId; the case's member id makes the line specific to this member. BepInEx's file is
		// buffered and the line is written from the coroutine's start, so poll the whole tail instead
		// of racing one fixed sleep (the 2026-11-15 first run read count=0 before the line landed).
		var refusalText = string.Empty;
		for (var attempt = 0; attempt < 20; attempt++)
		{
			var tail = await context.Session.LogTailAsync(
				context.Session.HostUrl, "[PodTrigger]", 8, cancellationToken).ConfigureAwait(false);
			refusalText = Raw(tail);
			if (refusalText.Contains(context.MemberId, StringComparison.Ordinal))
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		context.Note("host:pod-log-tail = " + refusalText);
		Require(refusalText.Contains(context.MemberId, StringComparison.Ordinal),
			"the host's pod refusal does not name the missing member " + context.MemberId + " within 5 s: " + refusalText);

		// Both living bodies at the pod: the same native sink must now take both clients down two layers.
		await PlaceAsync(context, member: true, "0", "0", cancellationToken).ConfigureAwait(false);
		var hostReady = await context.RecipeAsync("layer-state", cancellationToken).ConfigureAwait(false);
		var memberReady = await context.MemberRecipeAsync("layer-state", cancellationToken).ConfigureAwait(false);
		context.Read("host:ready", hostReady);
		context.Read("member:ready", memberReady);
		Require(Int(hostReady, "biomeDepth") == depthBefore,
			"placing the member at the pod changed the layer before the trigger: " + Raw(hostReady));

		// The host acts on its replicated copy of the member's position. The copy reaches the host
		// asynchronously, so retry the native sink until the gate accepts it; a refusal returns an
		// empty coroutine and cannot start a generation. Stop as soon as the world is generating.
		var fired = (JsonElement?)null;
		for (var attempt = 0; attempt < 20; attempt++)
		{
			var hostDuring = await context.RecipeAsync("layer-state", cancellationToken).ConfigureAwait(false);
			if (Int(hostDuring, "biomeDepth") != depthBefore
				|| IsTrue(hostDuring, "generating")
				|| IsTrue(hostDuring, "regenerating"))
			{
				break;
			}

			fired = await context.Session.ActionAsync(
				context.Session.HostUrl,
				"pod-descent",
				cancellationToken).ConfigureAwait(false);
			context.Read("host:fired[" + attempt.ToString(CultureInfo.InvariantCulture) + "]", fired);
			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		var hostAfterFire = await context.RecipeAsync("layer-state", cancellationToken).ConfigureAwait(false);
		Require(Int(hostAfterFire, "biomeDepth") != depthBefore
			|| IsTrue(hostAfterFire, "generating")
			|| IsTrue(hostAfterFire, "regenerating"),
			"the host never accepted the pod trigger after the member reached it: " + Raw(hostAfterFire));
		// A single native sink starts the descent; polling re-fires it while the world is generating
		// and produced a corrupt half-descent on 2026-11-15. Wait for the in-flight descent.
		var descended = await WaitForDepthAsync(context, depthBefore + 2, cancellationToken, polls: 90).ConfigureAwait(false);
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
		CaseContext context, int expected, CancellationToken cancellationToken, int polls = 90)
	{
		JsonElement? host = null;
		JsonElement? member = null;
		for (var attempt = 0; attempt < polls; attempt++)
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

	private static bool IsTrue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

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
