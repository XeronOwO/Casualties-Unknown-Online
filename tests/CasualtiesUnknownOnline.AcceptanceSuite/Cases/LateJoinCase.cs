using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The mid-run late join row: a host is already standing in a running world, and a member who joins the
/// session afterwards enters that same run — no restart, no second world, and the member's own client logs
/// the entry instruction.
///
/// The chain read before the fixture: the host's room is the IP session created at the menu; a member that
/// connects while the host is in a world asks through the reentry window and the host answers with the
/// character hand-over and a targeted <c>WorldJoin</c> (<c>HandshakeHandler</c>'s in-world branch,
/// <c>ReentryCoordinator</c>'s join window). The row drives the member through the game's own join controls,
/// not by restarting the run.
///
/// The member first leaves its session so the join happens against a world that is already running: the
/// shared setup joins the pair while the host generates, which is the different moment others already cover.
/// </summary>
internal sealed class LateJoinCase : AcceptanceCase
{
	public override string Name => "late-join";

	public override CaseCost Cost => CaseCost.InRun;


	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		var host = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		var runBefore = ReadUInt64(host, "runId");
		Require(runBefore != 0, "the host is not in a run to join: " + Raw(host));

		// The member goes back to the menu and the world keeps running for a beat, so the join is against a
		// live run rather than the setup's generation window.
		await context.Session.ClearMemberSessionAsync(cancellationToken).ConfigureAwait(false);
		var left = await context.Session.WaitWorldExitAsync(context.Session.MemberUrl, cancellationToken).ConfigureAwait(false);
		Require(left, "the member did not leave the world before the late join");
		var rosterAfterLeave = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		context.Read("host-roster-after-leave", JsonSerializer.SerializeToElement(rosterAfterLeave));
		await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);

		// The menu-side join against a world that is already running: the player's own join controls, the
		// same path a late player uses, not the shared setup's session-first order.
		var hostLogStart = LogLength(context.HostLogPath);
		var joinedAt = Stopwatch.StartNew();
		var joined = await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		joinedAt.Stop();
		context.Read("join-answer", JsonSerializer.SerializeToElement(new { text = joined, seconds = Math.Round(joinedAt.Elapsed.TotalSeconds, 1) }));

		var member = await context.Session.ActionAsync(context.Session.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
		context.Read("member-after-late-join", member);
		Require(string.Equals(Text(member, "role"), "Guest", StringComparison.Ordinal) && IsTrue(member, "inWorld"),
			"the member did not enter the running world: " + Raw(member));
		Require(ReadUInt64(member, "runId") == runBefore, "the member entered a different run: " + Raw(member));

		var hostAfter = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		Require(ReadUInt64(hostAfter, "runId") == runBefore,
			"the host's run changed while the member joined — the join restarted the world: " + Raw(hostAfter));

		var line = await context.Session.LogTailAsync(context.Session.MemberUrl, "World join received", 1, cancellationToken).ConfigureAwait(false);
		context.Read("member-log", line);
		Require((line?.ToString() ?? string.Empty).Contains("World join received", StringComparison.Ordinal),
			"the member's own log names no entry instruction for the late join: " + (line?.ToString() ?? "(null)"));

		var items = await context.MemberRecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		Require(Boolean(items, "ok"), "the late-joined member has no body to carry items: " + Raw(items));

		var roster = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		context.Read("host-roster", JsonSerializer.SerializeToElement(roster));

		// Host-side evidence is what the host said to this member, not the aggregate "World join sent" line (a
		// targeted entry does not write that line) and not the roster the driver reads (it lags a late entry on
		// this build: empty while the member's own state already read Guest/inWorld/runId 2, named a minute later).
		var lateId = Text(member, "steamId");
		var handshake = FindLine(context.HostLogPath, "Handshake confirmed end-to-end with " + lateId, hostLogStart);
		var handover = FindLine(context.HostLogPath, "Sent saved character data to " + lateId, hostLogStart);
		context.Read("host-entry-lines", JsonSerializer.SerializeToElement(new { handshake, handover }));
		// The join itself is the handshake: the host must confirm this exact member end to end. The character
		// hand-over is written only when a saved character exists for that member, so it stays a reading — an
		// `||` over the two let a member that never completed the handshake pass on a hand-over line alone
		// (2026-11-24).
		Require(handshake.Length > 0,
			"the host's log names no confirmed handshake for the late-joined member " + lateId +
			": handshake '" + handshake + "', hand-over '" + handover + "'");

		context.Note("the order (host in a running world, member joins after) is the late-join shape; the world never restarted");
	}

	/// <summary>The log's length, or 0 when the file is not there yet; the offset marks this case's lines.</summary>
	private static long LogLength(string path)
	{
		// Character count, not UTF-8 bytes; the shared read handle keeps the game's writer out of the way
		// (measured 2026-11-28).
		try
		{
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using var reader = new StreamReader(stream);
			return reader.ReadToEnd().Length;
		}
		catch (IOException)
		{
			return 0;
		}
	}

	/// <summary>The first full line at or after <paramref name="start"/> that contains the needle, else "".</summary>
	private static string FindLine(string path, string needle, long start)
	{
		if (!File.Exists(path))
		{
			return string.Empty;
		}

		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		stream.Seek(Math.Min(start, stream.Length), SeekOrigin.Begin);
		using var reader = new StreamReader(stream);
		string? line;
		while ((line = reader.ReadLine()) is not null)
		{
			if (line.Contains(needle, StringComparison.Ordinal))
			{
				return line;
			}
		}

		return string.Empty;
	}

	private static ulong ReadUInt64(JsonElement? reading, string property)
	{
		var text = Text(reading, property);
		return ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0UL;
	}

	private static bool IsTrue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.True;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.True;

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue)
			? propertyValue.ValueKind == JsonValueKind.String ? propertyValue.GetString() ?? string.Empty : propertyValue.ToString()
			: string.Empty;

	private static string Raw(JsonElement? reading) => reading?.ToString() ?? "(null)";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
