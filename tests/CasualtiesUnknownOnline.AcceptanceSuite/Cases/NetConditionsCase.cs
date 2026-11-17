using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The network-conditions family the user asked for (2026-11-11): the pair runs at 150 ms one-way and
/// 20% loss of unreliable payloads, and a fact that travels on a RELIABLE message must still cross the
/// link — the member dies once, its own client reports the character over the reliable path, and the
/// host's own answer about that member reads the new count. The case also requires the world to still
/// be running under the loss (the unreliable stream is allowed to drop and self-heal), reads the
/// profile back on both clients, and clears it even when an assertion throws, because the profile
/// shapes the pair every later row shares.
/// </summary>
internal sealed class NetConditionsCase : AcceptanceCase
{
	private const int LatencyMs = 150;
	private const int LossPercent = 20;

	public override string Name => "net-conditions";

	public override CaseCost Cost => CaseCost.InRun;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var memberId = await SingleMemberAsync(context, cancellationToken).ConfigureAwait(false);
		context.UseMember(memberId);
		var hostProfile = await SetAsync(context, host: true, cancellationToken).ConfigureAwait(false);
		var memberProfile = await SetAsync(context, host: false, cancellationToken).ConfigureAwait(false);
		context.Read("host profile", hostProfile);
		context.Read("member profile", memberProfile);

		try
		{
			Require(Number(hostProfile, "latencyMs") == LatencyMs && Number(hostProfile, "lossPercent") == LossPercent,
				"the host did not take the profile: " + Raw(hostProfile));
			Require(Number(memberProfile, "latencyMs") == LatencyMs && Number(memberProfile, "lossPercent") == LossPercent,
				"the member did not take the profile: " + Raw(memberProfile));

			// The reliable round trip: the member's own client observes the death edge and reports the
			// character (reliable by default); the host's answer about that member reads the report.
			await SetMemberAliveAsync(context, alive: false, cancellationToken).ConfigureAwait(false);
			var hostCount = await HostDeathsAsync(context, memberId, 1, cancellationToken).ConfigureAwait(false);
			Require(ShowsCount(hostCount, 1), "the host did not learn the member's death over the lossy link: " + hostCount);

			await SetMemberAliveAsync(context, alive: true, cancellationToken).ConfigureAwait(false);

			// The unreliable state stream is allowed to drop; the session itself must survive it.
			var hostState = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			var memberState = await context.Session.ActionAsync(context.Session.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
			context.Read("host state under loss", hostState);
			context.Read("member state under loss", memberState);
			Require(IsTrue(hostState, "inWorld"), "the host left the world while the link was lossy: " + Raw(hostState));
			Require(IsTrue(memberState, "inWorld"), "the member left the world while the link was lossy: " + Raw(memberState));
		}
		finally
		{
			await ClearAsync(context, host: true).ConfigureAwait(false);
			await ClearAsync(context, host: false).ConfigureAwait(false);
		}
	}

	private static async Task<JsonElement?> SetAsync(CaseContext context, bool host, CancellationToken cancellationToken) =>
		host
			? await context.RecipeAsync("net-conditions", cancellationToken,
				("mode", "set"),
				("latencyMs", LatencyMs.ToString(CultureInfo.InvariantCulture)),
				("jitterMs", "0"),
				("lossPercent", LossPercent.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false)
			: await context.MemberRecipeAsync("net-conditions", cancellationToken,
				("mode", "set"),
				("latencyMs", LatencyMs.ToString(CultureInfo.InvariantCulture)),
				("jitterMs", "0"),
				("lossPercent", LossPercent.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);

	private static async Task ClearAsync(CaseContext context, bool host)
	{
		var answer = host
			? await context.RecipeAsync("net-conditions", CancellationToken.None, ("mode", "clear")).ConfigureAwait(false)
			: await context.MemberRecipeAsync("net-conditions", CancellationToken.None, ("mode", "clear")).ConfigureAwait(false);
		context.Read(host ? "cleared on the host" : "cleared on the member", answer);
		Require(IsTrue(answer, "ok") && !IsTrue(answer, "enabled"),
			"the network profile was not cleared: " + Raw(answer));
	}

	private static async Task<string> HostDeathsAsync(CaseContext context, string memberId, int deaths, CancellationToken cancellationToken)
	{
		var line = string.Empty;
		for (var wait = 0; wait < 40; wait++)
		{
			line = await ConsoleAsync(context, context.Session.HostUrl, "/deaths " + memberId, cancellationToken).ConfigureAwait(false);
			if (ShowsCount(line, deaths))
			{
				return line;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return line;
	}

	private static async Task SetMemberAliveAsync(CaseContext context, bool alive, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var wait = 0; wait < 20; wait++)
		{
			last = await context.MemberRecipeAsync(
				"body-force",
				cancellationToken,
				("sleep", "-1"),
				("consciousness", "-1"),
				("energy", "-1"),
				("brainHealth", alive ? "100" : "0"),
				("badSleepAmount", "-1"),
				("idleTime", "-1"),
				("intelligence", "-1"),
				("heartRate", "-1"),
				("bloodPressure", "-1"),
				("bloodOxygen", "-1"),
				("strength", "-1")).ConfigureAwait(false);
			if (IsTrue(last, "ok") && IsTrue(last, "alive") == alive)
			{
				context.Read(alive ? "member alive" : "member dead", last);
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		Require(false, "the member's body never read alive=" + alive.ToString(CultureInfo.InvariantCulture) + ": " + Raw(last));
	}

	private static async Task<string> ConsoleAsync(CaseContext context, string url, string text, CancellationToken cancellationToken)
	{
		var answer = await context.Session.ActionAsync(url, "console", cancellationToken, ("-Text", text)).ConfigureAwait(false);
		context.Read("console " + text, answer);
		Require(IsTrue(answer, "applied"), "the console refused '" + text + "': " + Raw(answer));
		return Text(answer, "lastLine") ?? string.Empty;
	}

	private static async Task<string> SingleMemberAsync(CaseContext context, CancellationToken cancellationToken)
	{
		for (var wait = 0; wait < 20; wait++)
		{
			var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
			if (members.Length == 1 && members[0].Length > 0)
			{
				return members[0];
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		var final = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		Require(false, "the host's roster did not name exactly one member: " + string.Join(", ", final));
		return string.Empty;
	}

	private static bool ShowsCount(string line, int count) =>
		Regex.IsMatch(line, "(?<![0-9])" + count.ToString(CultureInfo.InvariantCulture) + "(?![0-9])");

	private static int Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
			? value.GetInt32()
			: -1;

	private static bool IsTrue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static string? Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
