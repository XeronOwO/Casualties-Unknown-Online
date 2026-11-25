using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The natural order row: continue a world with no session, then invite. Until 2026-11-17 the
/// IP-direct host refused that order ("IP-direct start refused: a world is running or generating";
/// <c>IpDirectActions.CanStart</c> asked the same question for create and join), so the player had to
/// open the session at the menu first. The row saves, ends the session on the host, leaves the world,
/// continues from the menu alone, opens the session over that restored world, and requires the member
/// to land in the SAME run — the create must adopt the running world, not restart it.
/// </summary>
internal sealed class HostAfterContinueCase : AcceptanceCase
{
	public override string Name => "host-after-continue";

	public override CaseCost Cost => CaseCost.NewWorld;

	/// <summary>
	/// It gives up the session and restores a world (tens of seconds); that restore is the cost it declares.
	/// </summary>

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var logOffset = LogLength(context.HostLogPath);
		var hostState = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		if (!IsTrue(hostState, "inWorld"))
		{
			await context.Session.EnsureInWorldAsync(cancellationToken).ConfigureAwait(false);
		}

		// The save gives Continue a world to restore; the case does not assume the pair's world is
		// already CUO's own.
		var save = await context.Session.ActionAsync(context.Session.HostUrl, "console", cancellationToken, ("-Text", "/save")).ConfigureAwait(false);
		context.Read("save", save);
		var committed = await WaitForLogAsync(context.HostLogPath, logOffset, "Cut Command committed", TimeSpan.FromSeconds(25), cancellationToken).ConfigureAwait(false);
		Require(committed is not null, "the save cut did not commit, so the continue below would open no world");

		// End the session while both players stand in the world: the host's leave ends the member's
		// session with it (the member's own watchdog allows 15 s of host silence).
		logOffset = LogLength(context.HostLogPath);
		await EndSessionAsync(context, cancellationToken).ConfigureAwait(false);
		var hostLeft = await WaitForStateAsync(
			context.Session,
			context.Session.HostUrl,
			state => Text(state, "role") == "None" && !IsTrue(state, "active"),
			TimeSpan.FromSeconds(15),
			cancellationToken).ConfigureAwait(false);
		context.Read("host after ending the session", hostLeft);
		Require(Text(hostLeft, "role") == "None", "the host kept a role after ending its session: " + Raw(hostLeft));
		var memberLeft = await WaitForStateAsync(
			context.Session,
			context.Session.MemberUrl,
			state => Text(state, "role") == "None",
			TimeSpan.FromSeconds(25),
			cancellationToken).ConfigureAwait(false);
		context.Read("member after the session ended", memberLeft);
		Require(Text(memberLeft, "role") == "None", "the member kept its role after the session ended: " + Raw(memberLeft));

		// The natural order: the host leaves the world and continues from the menu with NO session.
		await context.Session.ActionAsync(context.Session.HostUrl, "leave-world", cancellationToken).ConfigureAwait(false);
		Require(await context.Session.WaitWorldExitAsync(context.Session.HostUrl, cancellationToken).ConfigureAwait(false), "the host did not leave the world before continuing");
		var continued = await context.Session.ActionAsync(context.Session.HostUrl, "continue-run", cancellationToken).ConfigureAwait(false);
		context.Read("continue with no session", continued);
		var restored = await WaitForLogAsync(context.HostLogPath, logOffset, "Continue restored world", TimeSpan.FromSeconds(40), cancellationToken).ConfigureAwait(false);
		Require(restored is not null, "the host's session-less continue did not restore the world");
		var inWorld = await WaitForStateAsync(
			context.Session,
			context.Session.HostUrl,
			state => IsTrue(state, "inWorld") && Text(state, "role") == "None",
			TimeSpan.FromSeconds(20),
			cancellationToken).ConfigureAwait(false);
		context.Read("host standing in the restored world", inWorld);
		Require(IsTrue(inWorld, "inWorld"), "the host did not enter the restored world");
		Require(Text(inWorld, "role") == "None", "the host's own session opened with the continue, so the row would not test the natural order");
		var runBefore = ReadInt(inWorld, "runId");
		Require(runBefore != 0, "the restored world reports no run id: " + Raw(inWorld));

		// The one action the defect refused: open the session over the world that is already running.
		logOffset = LogLength(context.HostLogPath);
		await context.Session.ActionAsync(context.Session.HostUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.HostUrl, "click", cancellationToken, ("-Control", "tab.home")).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.HostUrl, "click", cancellationToken, ("-Control", "ip.create_host")).ConfigureAwait(false);
		var hostHosting = await WaitForStateAsync(
			context.Session,
			context.Session.HostUrl,
			state => Text(state, "role") == "Host" && IsTrue(state, "active"),
			TimeSpan.FromSeconds(15),
			cancellationToken).ConfigureAwait(false);
		context.Read("host session over the running world", hostHosting);
		Require(Text(hostHosting, "role") == "Host", "ip.create_host did not open a session over the running world: " + Raw(hostHosting));
		Require(IsTrue(hostHosting, "inWorld"), "opening the session moved the host out of its world: " + Raw(hostHosting));
		Require(ReadInt(hostHosting, "runId") == runBefore, "opening the session replaced the running world: run " + ReadInt(hostHosting, "runId").ToString(CultureInfo.InvariantCulture) + " instead of " + runBefore.ToString(CultureInfo.InvariantCulture));
		var createLine = await WaitForLogAsync(context.HostLogPath, logOffset, "IP-direct hosting opened over the solo world", TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
		context.Notes.Add("the host logged the adoption: " + (createLine ?? "NO LINE"));

		// The member joins the running world. The host's own log is the readable side here: the
		// member's log path resolves inside its sandbox, so a suite-side read of that path opens a
		// different file (2026-11-17: the path answered a sandbox view and the join line was
		// invisible). The host's send line plus the member's own state are the two sides of the relay.
		await context.Session.ActionAsync(context.Session.MemberUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.MemberUrl, "click", cancellationToken, ("-Control", "tab.home")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
		await TryClickAsync(context.Session, context.Session.MemberUrl, "ip.notice_confirm", cancellationToken).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.address"), ("-Text", "127.0.0.1")).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.port"), ("-Text", "7777")).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.MemberUrl, "click", cancellationToken, ("-Control", "ip.join")).ConfigureAwait(false);

		var member = await WaitForStateAsync(
			context.Session,
			context.Session.MemberUrl,
			state => Text(state, "role") == "Guest" && IsTrue(state, "inWorld"),
			TimeSpan.FromSeconds(30),
			cancellationToken,
			onMiss: async attempt =>
			{
				if (attempt % 6 == 5)
				{
					// The member's window may still be building the IP section when the first click lands
					// (measured 2026-11-14: the state offered no ip control yet); ask again before giving up.
					await TryClickAsync(context.Session, context.Session.MemberUrl, "ip.join", cancellationToken).ConfigureAwait(false);
				}
			}).ConfigureAwait(false);
		context.Read("member joined the running world", member);
		Require(Text(member, "role") == "Guest", "the member did not join the session the host opened over its world: " + Raw(member));
		Require(IsTrue(member, "inWorld"), "the member joined the session but never entered the host's world: " + Raw(member));
		Require(ReadInt(member, "runId") == runBefore, "the member entered run " + ReadInt(member, "runId").ToString(CultureInfo.InvariantCulture) + " instead of the host's running run " + runBefore.ToString(CultureInfo.InvariantCulture));
		Require(!IsTrue(member, "gateWaiting"), "the member is still held at the start gate: " + Raw(member));

		// The instruction that seats the member is sent through three of the product's own paths, and only two of
		// them log a line: the broadcast that announces a run to everyone waiting ("World join sent to N member(s)")
		// and the targeted re-seat (`[Respawn] sent targeted world join to …`). A member that handshakes while the
		// host already holds the run is answered inside the handshake itself
		// (`src/CasualtiesUnknownOnline.Runtime/Session/Handlers/HandshakeHandler.cs:230`, `RunBaselineFollows:
		// false`, no log line), and the join pump then skips it because it is already in the world. The row's claim
		// is the member's own run id and world entry (required above); the host line is the witness, and a silent
		// handshake is a witness the product does not print, not a join that did not happen (measured 2026-11-25,
		// `.acceptance/all-11`: the member stood in run 6 with `inWorld` true and no host line, while the host's own
		// traffic line read `Send WorldJoin`).
		var joinSent = await WaitForLogAsync(context.HostLogPath, logOffset, "World join sent to", TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
		joinSent ??= await WaitForLogAsync(context.HostLogPath, logOffset, "targeted world join to", TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
		context.Notes.Add(joinSent is null
			? "host sent the entry instruction: no line (the handshake path answers without one); the member's run id and world entry carry the claim"
			: "host sent the entry instruction: " + joinSent);
		var hostRun = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		Require(ReadInt(hostRun, "runId") == runBefore, "the host's run changed while the member joined");

		// The host's roster lists peers whose world entry completed, so it trails the member's own
		// in-world flag by the world's load (2026-11-17: the roster was still empty 9 s after the
		// member's state read inWorld true, then named it). Poll it, do not read it once.
		var roster = Array.Empty<string>();
		var rosterDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(25);
		while (DateTime.UtcNow < rosterDeadline)
		{
			roster = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
			if (roster.Length > 0)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		context.Notes.Add("roster after the join: " + string.Join(", ", roster));
		Require(roster.Length > 0, "the host's roster does not name the member it just accepted");

		// A5: the member that joined a session opened over a running world must not be left
		// with a ghost role when that session ends, and the next run must accept it again on
		// the same processes. End the adopted session, require the member's role to clear,
		// then open a second session+run and join it.
		await EndSessionAsync(context, cancellationToken).ConfigureAwait(false);
		JsonElement? memberAfterEnd = null;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			memberAfterEnd = await context.Session.ActionAsync(context.Session.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
			if (Text(memberAfterEnd, "role") == "None")
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		context.Read("member after the adopted session ended", memberAfterEnd);
		Require(Text(memberAfterEnd, "role") == "None",
			"the member kept a role after the adopted session ended, so it is a ghost: " + Raw(memberAfterEnd));

		await context.Session.ActionAsync(context.Session.HostUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.HostUrl, "click", cancellationToken, ("-Control", "tab.home")).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.HostUrl, "click", cancellationToken, ("-Control", "ip.create_host")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.HostUrl, "start-run", cancellationToken).ConfigureAwait(false);
		var hostNext = await WaitForStateAsync(
			context.Session,
			context.Session.HostUrl,
			state => Text(state, "role") == "Host" && IsTrue(state, "inWorld"),
			TimeSpan.FromSeconds(20),
			cancellationToken).ConfigureAwait(false);
		context.Read("host next run", hostNext);
		Require(Text(hostNext, "role") == "Host", "the host did not open the next session: " + Raw(hostNext));
		Require(IsTrue(hostNext, "inWorld"), "the host's next run did not enter the world: " + Raw(hostNext));

		await context.Session.ActionAsync(context.Session.MemberUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.MemberUrl, "click", cancellationToken, ("-Control", "tab.home")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
		await TryClickAsync(context.Session, context.Session.MemberUrl, "ip.notice_confirm", cancellationToken).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.address"), ("-Text", "127.0.0.1")).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.port"), ("-Text", "7777")).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.MemberUrl, "click", cancellationToken, ("-Control", "ip.join")).ConfigureAwait(false);
		var memberNext = await WaitForStateAsync(
			context.Session,
			context.Session.MemberUrl,
			state => Text(state, "role") == "Guest" && IsTrue(state, "inWorld"),
			TimeSpan.FromSeconds(30),
			cancellationToken,
			onMiss: async attempt =>
			{
				if (attempt % 6 == 5)
				{
					await TryClickAsync(context.Session, context.Session.MemberUrl, "ip.join", cancellationToken).ConfigureAwait(false);
				}
			}).ConfigureAwait(false);
		context.Read("member in the next run", memberNext);
		Require(Text(memberNext, "role") == "Guest",
			"the member did not join the next run after the adopted session ended: " + Raw(memberNext));
		Require(IsTrue(memberNext, "inWorld"),
			"the member joined the next session but never entered its world: " + Raw(memberNext));
	}

	/// <summary>Home &gt; Leave: ends the lobby (and, for IP direct, the connection that is the room).</summary>
	private static async Task EndSessionAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.ActionAsync(context.Session.HostUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.HostUrl, "click", cancellationToken, ("-Control", "tab.home")).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.HostUrl, "click", cancellationToken, ("-Control", "home.leave")).ConfigureAwait(false);
	}

	private static async Task TryClickAsync(SessionDriver session, string url, string control, CancellationToken cancellationToken)
	{
		try
		{
			await session.ActionAsync(url, "click", cancellationToken, ("-Control", control)).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Not offered yet — the poll re-clicks; the address/port fields are offered with it.
		}
	}

	private static async Task<JsonElement> WaitForStateAsync(
		SessionDriver session,
		string url,
		Func<JsonElement, bool> until,
		TimeSpan budget,
		CancellationToken cancellationToken,
		Func<int, Task>? onMiss = null)
	{
		var deadline = DateTime.UtcNow + budget;
		var state = default(JsonElement);
		for (var attempt = 0; DateTime.UtcNow < deadline; attempt++)
		{
			state = await session.ActionAsync(url, "state", cancellationToken).ConfigureAwait(false);
			if (state.ValueKind == JsonValueKind.Object && until(state))
			{
				return state;
			}

			if (onMiss is not null)
			{
				await onMiss(attempt).ConfigureAwait(false);
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return state;
	}

	private static async Task<string?> WaitForLogAsync(string path, long offset, string needle, TimeSpan budget, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + budget;
		while (DateTime.UtcNow < deadline)
		{
			var text = ReadLog(path);
			var index = text.LastIndexOf(needle, StringComparison.Ordinal);
			if (index >= 0 && index >= offset)
			{
				var lineStart = text.LastIndexOf('\n', index) + 1;
				var lineEnd = text.IndexOf('\n', index);
				return text[lineStart..(lineEnd >= 0 ? lineEnd : text.Length)].Trim();
			}

			await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
		}

		return null;
	}

	private static long LogLength(string path)
	{
		try
		{
			return new FileInfo(path) is { Exists: true } file ? file.Length : 0;
		}
		catch (IOException)
		{
			return 0;
		}
	}

	private static string ReadLog(string path)
	{
		try
		{
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using var reader = new StreamReader(stream, Encoding.UTF8);
			return reader.ReadToEnd();
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(property, out var value)
			? value.ValueKind switch
			{
				JsonValueKind.String => value.GetString() ?? string.Empty,
				JsonValueKind.Number => value.GetRawText(),
				_ => string.Empty,
			}
			: string.Empty;

	private static int ReadInt(JsonElement? reading, string property) =>
		int.TryParse(Text(reading, property), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

	private static bool IsTrue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } state
		&& state.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
