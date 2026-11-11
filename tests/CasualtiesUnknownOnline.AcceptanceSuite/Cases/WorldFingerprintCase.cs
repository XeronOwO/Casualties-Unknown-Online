using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// Both clients' live block tables agree: the host and the member re-capture the product's own world
/// fingerprint (the same diagnostic the runtime logs once per world entry) and their totals match.
/// The row is the world-determinism family from the sweep — the worlds must be the same world.
/// </summary>
internal sealed class WorldFingerprintCase : AcceptanceCase
{
	public override string Name => "world-fingerprint";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The device writes the product's own log line on the client it runs on; the line is read
		// back from that client's own log, so the comparison is between two files, not two answers.
		var hostAnswer = await context.RecipeAsync("world-fingerprint", cancellationToken).ConfigureAwait(false);
		context.Read("host-fingerprint-device", hostAnswer);
		Require(Boolean(hostAnswer, "ok"), "the host's fingerprint device did not answer ok: " + Raw(hostAnswer));

		var memberAnswer = await context.MemberRecipeAsync("world-fingerprint", cancellationToken).ConfigureAwait(false);
		context.Read("member-fingerprint-device", memberAnswer);
		Require(Boolean(memberAnswer, "ok"), "the member's fingerprint device did not answer ok: " + Raw(memberAnswer));

		var hostTail = await PollTailAsync(context, false, cancellationToken).ConfigureAwait(false);
		context.Read("host-fingerprint-line", hostTail);
		var memberTail = await PollTailAsync(context, true, cancellationToken).ConfigureAwait(false);
		context.Read("member-fingerprint-line", memberTail);

		var hostLine = Line0(hostTail);
		var memberLine = Line0(memberTail);
		context.Note("host line: " + hostLine);
		context.Note("member line: " + memberLine);

		var hostTotal = Total(hostLine);
		var memberTotal = Total(memberLine);
		Require(hostTotal.Length > 0, "the host's fingerprint line carries no total: " + hostLine);
		Require(memberTotal.Length > 0, "the member's fingerprint line carries no total: " + memberLine);
		Require(string.Equals(hostTotal, memberTotal, StringComparison.OrdinalIgnoreCase),
			$"the worlds differ: host total {hostTotal}, member total {memberTotal}");
		context.Note($"both clients' block tables read total {hostTotal}");
	}

	/// <summary>
	/// The device's own line lands in the client's file a moment after the answer (BepInEx flushes on
	/// its own cadence), so the tail is polled until a line WITH a total appears; the last such line is
	/// the freshest capture.
	/// </summary>
	private static async Task<JsonElement?> PollTailAsync(CaseContext context, bool member, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var wait = 0; wait < 20; wait++)
		{
			last = await TailAsync(context, member, cancellationToken).ConfigureAwait(false);
			if (Total(Line0(last)).Length > 0)
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	private static async Task<JsonElement?> TailAsync(CaseContext context, bool member, CancellationToken cancellationToken) =>
		await context.Session
			.ActionAsync(member ? context.Session.MemberUrl : context.Session.HostUrl, "log-tail", cancellationToken, ("-Argument", "[WorldFingerprint]"), ("-Text", "1"))
			.ConfigureAwait(false);

	private static string Line0(JsonElement? answer) =>
		answer is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty("line0", out var line)
		&& line.ValueKind == JsonValueKind.String
			? line.GetString() ?? string.Empty
			: string.Empty;

	/// <summary>The `total &lt;hash&gt;` field of a fingerprint line, or empty.</summary>
	private static string Total(string line)
	{
		var at = line.LastIndexOf("total ", StringComparison.OrdinalIgnoreCase);
		if (at < 0)
		{
			return string.Empty;
		}

		var start = at + "total ".Length;
		var end = start;
		while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '-' || line[end] == '_'))
		{
			end++;
		}

		return end > start ? line[start..end] : string.Empty;
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

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
