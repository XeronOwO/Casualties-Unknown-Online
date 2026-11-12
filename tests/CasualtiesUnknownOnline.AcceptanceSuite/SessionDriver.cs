using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CasualtiesUnknownOnline.AcceptanceSuite;

/// <summary>
/// The session the cases run inside. The cases judge the real game, so the suite owns the
/// session it judges: it asks the acceptance driver for the world state of the host and of the
/// member, starts a run when no world is up, and joins the member to the host's IP session,
/// instead of judging whatever happened to be running.
/// </summary>
internal sealed class SessionDriver(Options options)
{
	private static readonly TimeSpan StartBudget = TimeSpan.FromSeconds(60);
	private static readonly TimeSpan JoinBudget = TimeSpan.FromSeconds(30);

	private readonly Options _options = options;

	/// <summary>
	/// Restarts the run from scratch: the host leaves the world, starts a fresh one, and the
	/// member rejoins it. A long-lived world's surface can be a pit over a cave (the pair ends up
	/// inside terrain and the interaction gate blocks every use), so a case that needs the pair
	/// side by side asks for a fresh run instead of trusting where the old one left the bodies.
	/// </summary>
	public string MemberUrl => _options.MemberUrl;

	public string HostUrl => _options.HostUrl;

	public async Task<string> RestartRunAsync(CancellationToken cancellationToken)
	{
		// Never leave a world that is still loading: the host's start gate would wait for a member
		// that has just been sent back to the menu, and the window the player sees is a frozen
		// loading line nobody can explain. Wait until the world is up and the gate has released.
		await WaitUntilAsync(_options.HostUrl, candidate => IsTrue(candidate, "inWorld") && !IsTrue(candidate, "gateWaiting"), TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
		await ActionAsync(_options.HostUrl, "leave-world", cancellationToken).ConfigureAwait(false);
		var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < TimeSpan.FromSeconds(20))
		{
			await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);
			var state = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			if (!IsTrue(state, "inWorld"))
			{
				break;
			}
		}

		var started = await EnsureInWorldAsync(cancellationToken).ConfigureAwait(false);
		var joined = await EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		return "fresh run: " + started + "; " + joined;
	}
	/// <summary>Starts a run when no world is up and returns the note for the report.</summary>
	public async Task<string> EnsureInWorldAsync(CancellationToken cancellationToken)
	{
		var state = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		if (IsTrue(state, "inWorld"))
		{
			// A world that is already running belongs to an earlier session: its roster may be empty
			// (a run whose members never joined), and the cases would then read a world nobody
			// shares. The suite starts its own run every time; leaving before the first case costs
			// one load and is never done mid-case.
			await TryActionAsync(_options.HostUrl, "leave-world", cancellationToken).ConfigureAwait(false);
			await WaitUntilAsync(_options.HostUrl, candidate => !IsTrue(candidate, "inWorld"), TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
			state = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		}

		// A run needs the host's IP session first: at the menu, switch the transport and create
		// the host, then start the run. The home page answers only while its window is up, and a
		// client loading a scene can refuse the first click, so it retries with a fresh open.
		await ActionAsync(_options.HostUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);
		// The window reopens on the tab it last showed; the transport controls live on Home.
		await TryActionAsync(_options.HostUrl, "click", cancellationToken, ("-Control", "tab.home")).ConfigureAwait(false);
		await TryActionAsync(_options.HostUrl, "click", cancellationToken, ("-Control", "ip.notice_confirm")).ConfigureAwait(false);
		try
		{
			// A host that still holds an IP session offers no transport switch; the run starts
			// on the session it already has.
			await ActionAsync(_options.HostUrl, "click", cancellationToken, ("-Control", "home.transport.ip")).ConfigureAwait(false);
		}
		catch (Exception)
		{
		}
		await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
		try
		{
			// A host whose earlier IP session is still alive refuses a second create; the run
			// then simply starts on the existing session.
			await ActionAsync(_options.HostUrl, "click", cancellationToken, ("-Control", "ip.create_host")).ConfigureAwait(false);
		}
		catch (Exception)
		{
		}
		// The member can only join a session that EXISTS: wait for the host's own role before the
		// join is attempted. Without this read the member joined nothing, kept a world waiting for a
		// host, and the run's start gate then waited for a member the host never had (the reading is
		// on `docs/backlog/review/1.0-member-waits-for-a-host-that-waits-for-it.md`).
		var hostReady = false;
		for (var wait = 0; wait < 40 && !hostReady; wait++)
		{
			var hostState = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			hostReady = string.Equals(StringValue(hostState, "role"), "Host", StringComparison.Ordinal);
			if (!hostReady)
			{
				await TryActionAsync(_options.HostUrl, "click", cancellationToken, ("-Control", "ip.create_host")).ConfigureAwait(false);
				await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			}
		}

		if (!hostReady)
		{
			throw new InvalidOperationException("the suite could not create the host's IP session");
		}

		// Join the member to the session BEFORE the run starts: both players then enter the world
		// together. A member that joins a running world spawns at its own pod, which is how a later
		// case found the pair out of each other's line of sight.
		// The pre-run join (both players enter together) is the defect filed as
		// `docs/backlog/review/1.0-member-waits-for-a-host-that-waits-for-it.md`: the session accepts
		// the member, the run start drops it again, and both gates wait for each other. Until that
		// side is decided, the fixture uses the order the manual sessions proved: the host starts the
		// run, the member joins the running world (and may spawn at its own pod — a case that needs
		// the pair together places the member, as `GiveItemCase` does).
		// The member joins the SESSION before the run starts, so the run's world join reaches it: a
		// member that joins a running world keeps its own pod and never receives the item or clone
		// stream (measured 2026-11-12: the host logged `World join sent to 0 member(s)` while the
		// member sat in a world with an empty bag and no clone).
		// Parked 2026-11-12: the join is refused or dies at the menu — the member's own log ends the
		// session after 15 s of silence ("No frame from the host"), the host pings only while its
		// session is active and the guest never confirms. See `1.0-member-waits-for-a-host-that-waits-for-it.md`.
		await ActionAsync(_options.HostUrl, "start-run", cancellationToken).ConfigureAwait(false); var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < StartBudget)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);
			state = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			if (IsTrue(state, "inWorld"))
			{
				return "the suite started a fresh run";
			}
		}

		throw new InvalidOperationException("the suite could not start a world for the cases");
	}

	/// <summary>Joins the member to the host's IP session when it is not in the world.</summary>
	public Task<string> EnsureMemberAsync(CancellationToken cancellationToken) => JoinMemberAsync(cancellationToken, allowRelaunch: true);

	/// <summary>
	/// The member's client can keep a session it no longer owns (after an ending, its role stays
	/// Guest while the lobby is gone, and leaving answers ok without clearing it). When the join
	/// budget runs out, the suite relaunches the member once and tries again: the session it
	/// judges is the suite's, so the suite may recreate the client too.
	/// </summary>
	/// <summary>
	/// The member joins the host's session while the host is still at the menu, so both players
	/// enter the run together. Success is the session role, not the world: the world starts after.
	/// </summary>
	private async Task<string> JoinSessionBeforeRunAsync(CancellationToken cancellationToken)
	{
		// The member's client can still hold the previous run (a role Guest and a world of its own):
		// it then answers `role Guest` immediately while the host's new session knows nothing of it.
		// A member that is in a world — or whose session predates this run — leaves first, so the
		// host's run start can carry it into the world (measured 2026-11-12: the pair looked joined
		// while the member's bag and clone stream stayed empty).
		var memberState = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
		if (IsTrue(memberState, "inWorld") || string.Equals(StringValue(memberState, "role"), "Guest", StringComparison.Ordinal))
		{
			await TryActionAsync(_options.MemberUrl, "leave-world", cancellationToken).ConfigureAwait(false);
			await WaitUntilAsync(_options.MemberUrl, candidate => !IsTrue(candidate, "inWorld"), TimeSpan.FromSeconds(25), cancellationToken).ConfigureAwait(false);
		}

		await ActionAsync(_options.MemberUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
		await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "tab.home")).ConfigureAwait(false);

		var before = await HandshakeCountAsync(cancellationToken).ConfigureAwait(false);
		var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < JoinBudget)
		{
			var state = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
			if (string.Equals(StringValue(state, "role"), "Guest", StringComparison.Ordinal)
				&& await HandshakeCountAsync(cancellationToken).ConfigureAwait(false) > before)
			{
				// The host's own handshake line is the confirmation that THIS session knows the member;
				// a stale Guest role alone was the reading that made the pair look joined.
				return "the member joined the session before the run";
			}

			var controls = Controls(state);
			if (controls.Contains("ip.notice_confirm"))
			{
				await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "ip.notice_confirm")).ConfigureAwait(false);
			}
			else if (controls.Contains("ip.join"))
			{
				await TryActionAsync(_options.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.address"), ("-Text", "127.0.0.1")).ConfigureAwait(false);
				await TryActionAsync(_options.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.port"), ("-Text", "7777")).ConfigureAwait(false);
				await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "ip.join")).ConfigureAwait(false);
			}
			else if (controls.Contains("home.transport.ip"))
			{
				await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "home.transport.ip")).ConfigureAwait(false);
			}

			await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException("the member could not join the session before the run");
	}

	private async Task<string> JoinMemberAsync(CancellationToken cancellationToken, bool allowRelaunch)
	{
		var state = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
		var hostMembers = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		if (IsTrue(state, "inWorld") && hostMembers.Length > 0)
		{
			return "the member was already in a world";
		}

		if (IsTrue(state, "inWorld"))
		{
			// The member still sits in an earlier session; the host's fresh run does not list it,
			// so it leaves and joins the host's current one. The leave races the scene, and the
			// joiner refuses while THIS client is still generating a world ("IP-direct start
			// refused: a world is running or generating"), so the wait is the judge.
			await TryActionAsync(_options.MemberUrl, "leave-world", cancellationToken).ConfigureAwait(false);
			await WaitUntilAsync(_options.MemberUrl, candidate => !IsTrue(candidate, "inWorld"), TimeSpan.FromSeconds(25), cancellationToken).ConfigureAwait(false);
			state = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
		}
		// The window can be closed (a case pressed Escape) or the client can still be loading after
		// a restart; the home page's controls answer only while the page is up. A fresh offline
		// client shows the notice INSTEAD of the join controls, so the loop acts on what the state
		// offers instead of clicking a control that may not be built yet.
		await ActionAsync(_options.MemberUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
		await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "tab.home")).ConfigureAwait(false);

		var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < JoinBudget)
		{
			state = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
			if (IsTrue(state, "inWorld"))
			{
				// A member can be in a world the host does not share (it joined a run the host
				// started and then waited for a host that never listed it). Success is both sides
				// naming the same world: the member in it AND the host's roster carrying it. The
				// roster read is cheap and is the judge.
				hostMembers = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
				if (hostMembers.Length > 0)
				{
					return "the suite joined the member to the host's session";
				}

				// The ghost: the menu is up (page Home, no role) while the world presence still says
				// in world, so every join answers "a world is running or generating" (measured on the
				// boxed member). The window offers no leave control there; the driver's own leave is
				// the only way out of a world the window does not show.
				var ghostControls = Controls(state);
				if (!ghostControls.Contains("home.leave") && string.Equals(StringValue(state, "page"), "Home", StringComparison.Ordinal))
				{
					await TryActionAsync(_options.MemberUrl, "leave-world", cancellationToken).ConfigureAwait(false);
					await Task.Delay(TimeSpan.FromMilliseconds(1200), cancellationToken).ConfigureAwait(false);
					continue;
				}
			}

			var controls = Controls(state);
			if (controls.Contains("home.leave") && !string.Equals(StringValue(state, "role"), "Guest", StringComparison.Ordinal))
			{
				// A member that holds a session the host's fresh run does not name (a guest of the
				// run the restart replaced) leaves it, and the join below is the judge. A member that
				// is already a Guest of the CURRENT session must NOT leave: every leave/rejoin pair
				// resets the run announcement and the member never enters (measured livelock).
				await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "home.leave")).ConfigureAwait(false);
				await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
			}
			else if (controls.Contains("ip.notice_confirm"))
			{
				await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "ip.notice_confirm")).ConfigureAwait(false);
			}
			else if (controls.Contains("ip.join"))
			{
				await TryActionAsync(_options.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.address"), ("-Text", "127.0.0.1")).ConfigureAwait(false);
				await TryActionAsync(_options.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.port"), ("-Text", "7777")).ConfigureAwait(false);
				await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "ip.join")).ConfigureAwait(false);
			}
			else if (controls.Contains("home.transport.ip"))
			{
				// The Steam home page is up: its transport switch leads to the IP-direct controls.
				await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "home.transport.ip")).ConfigureAwait(false);
			}

			await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
		}

		// The member's client can be stuck in a world it no longer shares: its join is refused with
		// "IP-direct start refused: a world is running or generating" and no click can clear that.
		// The suite owns this client, so it recreates it once — the session being judged is the
		// suite's own, and a member that cannot be driven would fail every case for one bad process.
		if (allowRelaunch)
		{
			await RelaunchMemberAsync(cancellationToken).ConfigureAwait(false);
			return await JoinMemberAsync(cancellationToken, allowRelaunch: false).ConfigureAwait(false);
		}

		throw new InvalidOperationException(
			"the suite could not join the member to the host's session: " + DescribeMemberState(state));
	}

	private static string DescribeMemberState(JsonElement state) =>
		$"role={StringValue(state, "role")}, active={StringValue(state, "active")}, inWorld={StringValue(state, "inWorld")}, "
		+ $"page={StringValue(state, "page")}, controls={string.Join(",", Controls(state))}";

	/// <summary>Quits the member's client, starts it again through the launcher, and waits for its driver.</summary>
	private Task RelaunchMemberAsync(CancellationToken cancellationToken) => RelaunchAsync(_options.MemberUrl, "guest", cancellationToken);

	private Task RelaunchHostAsync(CancellationToken cancellationToken) => RelaunchAsync(_options.HostUrl, "host", cancellationToken);

	private async Task RelaunchAsync(string url, string only, CancellationToken cancellationToken)
	{
		await TryActionAsync(url, "quit", cancellationToken).ConfigureAwait(false);
		// The launcher reuses a client that is still running, so the quit must be observed: a game
		// whose main thread is stuck in a world that never finishes generating ignores
		// Application.Quit (measured: `IP-direct start refused: a world is running or generating`
		// kept looping while the process stayed alive). The suite owns the client it started, so it
		// asks the client for its own pid and reclaims it — no duplicate process is ever started.
		var gone = Stopwatch.StartNew();
		while (gone.Elapsed < TimeSpan.FromSeconds(20))
		{
			await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
			try
			{
				await ActionAsync(url, "state", cancellationToken).ConfigureAwait(false);
			}
			catch (Exception)
			{
				break;
			}
		}

		try
		{
			var pidAnswer = await ActionAsync(url, "pid", cancellationToken).ConfigureAwait(false);
			if (pidAnswer.ValueKind == JsonValueKind.Object
				&& pidAnswer.TryGetProperty("pid", out var pidValue)
				&& pidValue.ValueKind == JsonValueKind.Number
				&& pidValue.TryGetInt32(out var pid))
			{
				using var client = Process.GetProcessById(pid);
				client.Kill(entireProcessTree: true);
				await client.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// The client may have exited on its own; the launcher answers next.
		}

		var launcher = Path.Combine(RepositoryPaths.Root, "tools", "acceptance", "launch-clients.ps1");
		var start = new ProcessStartInfo("pwsh")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		start.ArgumentList.Add("-NoProfile");
		start.ArgumentList.Add("-File");
		start.ArgumentList.Add(launcher);
		start.ArgumentList.Add("-Only");
		start.ArgumentList.Add(only);
		start.ArgumentList.Add("-NoJoin");
		start.ArgumentList.Add("-Restart");

		// The boxed clients' Steam accounts do not own the game: the pair runs offline over IP direct,
		// and a box must never be told to start its own Steam (measured 2026-11-12, the user's report).
		start.ArgumentList.Add("-AllowOfflineSteam");
		if (!string.IsNullOrEmpty(_options.GameDir))
		{
			start.ArgumentList.Add("-GameDir");
			start.ArgumentList.Add(_options.GameDir!);
		}

		using var process = Process.Start(start) ?? throw new InvalidOperationException($"could not start the acceptance launcher for the {only}");
		var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
		await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
		var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < TimeSpan.FromSeconds(120))
		{
			await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
			try
			{
				await ActionAsync(url, "state", cancellationToken).ConfigureAwait(false);
				return;
			}
			catch (Exception)
			{
			}
		}

		throw new InvalidOperationException($"the suite relaunched the {only} but its driver never answered: " + output.Trim());
	}

	/// <summary>
	/// The pair a run is judged on. When the host stops listening (its transport logs a disconnect
	/// loop, its roster stays empty and no member can join it), clicking cannot restore the session —
	/// the host process itself has to be recreated. The suite owns both clients, so it recreates the
	/// host once and builds a fresh session before the cases start; the reading is on
	/// `docs/backlog/in-progress/1.0-acceptance-suite.md`.
	/// </summary>
	public async Task<(string HostNote, string MemberNote)> EnsurePairAsync(CancellationToken cancellationToken)
	{
		// The member's client keeps the previous run (a Guest role and its own world) and a stale
		// session answers "already in a world" while the host's new run knows nothing of it: every
		// cross-player reading then comes back empty (measured 2026-11-12 — an empty bag, no clone,
		// no dose line, all of them a member the host's world never adopted). The suite owns this
		// client, so it recreates it once before the run rather than trusting a leftover session.
		JsonElement? memberBefore;
		try
		{
			memberBefore = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// A client that is down is not a stale session: the join path relaunches it when it must.
			memberBefore = null;
		}
		if (memberBefore is { } memberState
			&& (IsTrue(memberState, "inWorld") || string.Equals(StringValue(memberState, "role"), "Guest", StringComparison.Ordinal)))
		{
			await RelaunchAsync(_options.MemberUrl, "guest", cancellationToken).ConfigureAwait(false);
		}

		string hostNote;
		try
		{
			hostNote = await EnsureInWorldAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// No host is running at all (a previous run reclaimed it): the launcher starts one.
			await RelaunchHostAsync(cancellationToken).ConfigureAwait(false);
			hostNote = await EnsureInWorldAsync(cancellationToken).ConfigureAwait(false);
		}

		string memberNote;
		try
		{
			memberNote = await EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception)
		{
			await RelaunchMemberAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				memberNote = await EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception)
			{
				memberNote = await RecoverPairAsync(cancellationToken).ConfigureAwait(false);
				return (hostNote, memberNote);
			}
		}

		var members = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		var confirmed = await ConfirmedMemberIdAsync(cancellationToken).ConfigureAwait(false);
		if (confirmed is not null && members.Contains(confirmed, StringComparer.Ordinal))
		{
			return (hostNote, memberNote);
		}

		if (confirmed is null && members.Length > 0)
		{
			// No handshake line to judge by (an older host build): the roster is the best reading.
			return (hostNote, memberNote);
		}

		// The roster can carry a row from a session that is gone; a member whose id the host never
		// confirmed is not in this world, whatever its own client believes (measured 2026-11-12).
		memberNote = await RecoverPairAsync(cancellationToken).ConfigureAwait(false);
		return (hostNote, memberNote);
	}

	private async Task<string> RecoverPairAsync(CancellationToken cancellationToken)
	{
		await RelaunchHostAsync(cancellationToken).ConfigureAwait(false);
		await EnsureInWorldAsync(cancellationToken).ConfigureAwait(false);
		var memberNote = await EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		var members = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		if (members.Length == 0)
		{
			throw new InvalidOperationException("the host's roster stayed empty after the host was recreated and the run restarted");
		}

		return memberNote + " (after the host was recreated)";
	}

	/// <summary>Reads the host's in-world members' session ids — the ids the cases address a member by.</summary>
	/// <summary>
	/// The member the host's own log last confirmed in a handshake. The roster can still carry a
	/// row from a session that is gone (measured 2026-11-12: every cross-player case addressed a
	/// stale id while the member sat in its own world), so the cases address this id instead.
	/// </summary>
	public async Task<string?> ConfirmedMemberIdAsync(CancellationToken cancellationToken)
	{
		var tail = await LogTailAsync(_options.HostUrl, "Handshake confirmed end-to-end with", 5, cancellationToken).ConfigureAwait(false);
		if (tail is not { ValueKind: JsonValueKind.Object } answer
			|| !answer.TryGetProperty("count", out var countElement))
		{
			return null;
		}

		// The driver answers a JSON frame's fields as strings; accept both shapes.
		var count = countElement.ValueKind switch
		{
			JsonValueKind.Number when countElement.TryGetInt32(out var number) => number,
			JsonValueKind.String when int.TryParse(countElement.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
			_ => 0,
		};

		string? confirmed = null;
		for (var index = 0; index < count; index++)
		{
			if (!answer.TryGetProperty($"line{index}", out var lineElement) || lineElement.ValueKind != JsonValueKind.String)
			{
				continue;
			}

			var line = lineElement.GetString() ?? string.Empty;
			var marker = line.LastIndexOf(' ');
			if (marker > 0 && ulong.TryParse(line[(marker + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var peer))
			{
				confirmed = peer.ToString(CultureInfo.InvariantCulture);
			}
		}

		return confirmed;
	}

	/// <summary>
	/// How many handshake confirmations the host's log carries. A join this run must make the count
	/// grow: the role alone says the client believes it joined, the host's line says the session does.
	/// </summary>
	private async Task<int> HandshakeCountAsync(CancellationToken cancellationToken)
	{
		var tail = await LogTailAsync(_options.HostUrl, "Handshake confirmed end-to-end with", 200, cancellationToken).ConfigureAwait(false);
		if (tail is not { ValueKind: JsonValueKind.Object } answer
			|| !answer.TryGetProperty("count", out var countElement))
		{
			return 0;
		}

		return countElement.ValueKind switch
		{
			JsonValueKind.Number when countElement.TryGetInt32(out var number) => number,
			JsonValueKind.String when int.TryParse(countElement.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
			_ => 0,
		};
	}

	public async Task<string[]> ReadMembersAsync(CancellationToken cancellationToken)
	{
		var state = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		if (state.ValueKind != JsonValueKind.Object)
		{
			return [];
		}

		// The driver answers its state facts at the top level; an older wrapper kept them under
		// result, so both shapes are accepted.
		var hasMembers = state.TryGetProperty("members", out var members);
		if (!hasMembers
			&& (!state.TryGetProperty("result", out var result) || !result.TryGetProperty("members", out members)))
		{
			return [];
		}

		if (members.ValueKind != JsonValueKind.Array)
		{
			return [];
		}

		return [.. members.EnumerateArray().Select(member => member.ValueKind == JsonValueKind.String ? member.GetString() ?? string.Empty : member.ToString())];
	}

	/// <summary>
	/// Loads one committed eval declaration into a client: probe the type, send the source when it is
	/// absent (the evaluator takes a type at most once), probe again. The declaration's own
	/// <c>// declares: &lt;type&gt;</c> line names what to ask about.
	/// </summary>
	private static async Task<JsonElement> DeclareAsync(DriverClient driver, string name, string source, CancellationToken cancellationToken)
	{
		var directory = Path.Combine(RepositoryPaths.Root, "tools", "acceptance", "driver", "eval-declarations");
		if (string.IsNullOrEmpty(source))
		{
			source = await File.ReadAllTextAsync(Path.Combine(directory, name + ".cs"), cancellationToken).ConfigureAwait(false);
		}

		var match = Regex.Match(source, "(?m)^// declares: ([A-Za-z_][A-Za-z0-9_.]*)[ \t]*\r?$");
		if (!match.Success)
		{
			throw new InvalidOperationException($"the eval declaration '{name}' carries no '// declares: <type>' line");
		}

		var check = (await File.ReadAllTextAsync(Path.Combine(directory, "check-declared.cs"), cancellationToken).ConfigureAwait(false))
			.Replace("{{s:type}}", "\"" + match.Groups[1].Value + "\"", StringComparison.Ordinal);
		var probe = await driver.EvalAsync(check, "declare:" + name + " (probe)", cancellationToken).ConfigureAwait(false);
		if (Declared(probe))
		{
			return JsonSerializer.SerializeToElement(new { ok = true, declared = true, sent = false });
		}

		await driver.EvalAsync(source, "declare:" + name + " (load)", cancellationToken, allowNoValue: true).ConfigureAwait(false);
		var verify = await driver.EvalAsync(check, "declare:" + name + " (verify)", cancellationToken).ConfigureAwait(false);
		if (!Declared(verify))
		{
			throw new InvalidOperationException($"the eval declaration '{name}' was sent but the type is still not in the client's evaluator");
		}

		return JsonSerializer.SerializeToElement(new { ok = true, declared = true, sent = true });
	}

	/// <summary>
	/// Reads a client's state until the condition holds, or the budget runs out, and returns the last
	/// state either way. The game applies a click on its own update, so a read taken in the same
	/// call that clicked can be one frame early; this is a bounded read loop, not a fixed sleep.
	/// </summary>
	public async Task<JsonElement> WaitUntilAsync(string url, Func<JsonElement, bool> until, TimeSpan budget, CancellationToken cancellationToken)
	{
		var deadline = Stopwatch.StartNew();
		var state = await ActionAsync(url, "state", cancellationToken).ConfigureAwait(false);
		while (!until(state) && deadline.Elapsed < budget)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
			state = await ActionAsync(url, "state", cancellationToken).ConfigureAwait(false);
		}

		return state;
	}

	/// <summary>One string of a state answer, at the top level or under the older result wrapper.</summary>
	private static string StringValue(JsonElement state, string name)
	{
		if (state.ValueKind != JsonValueKind.Object)
		{
			return string.Empty;
		}

		if (state.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
		{
			return value.GetString() ?? string.Empty;
		}

		return state.TryGetProperty("result", out var result)
			&& result.TryGetProperty(name, out value)
			&& value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: string.Empty;
	}

	/// <summary>The control ids the window's current page offers, as the state reports them.</summary>
	private static IReadOnlyList<string> Controls(JsonElement state)
	{
		if (state.ValueKind == JsonValueKind.Object
			&& state.TryGetProperty("controls", out var controls)
			&& controls.ValueKind == JsonValueKind.Array)
		{
			return [.. controls.EnumerateArray()
				.Where(item => item.ValueKind == JsonValueKind.String)
				.Select(item => item.GetString() ?? string.Empty)];
		}

		return [];
	}

	/// <summary>One boolean of a state answer, at the top level or under the older result wrapper.</summary>
	internal static bool StateTrue(JsonElement state, string name)
	{
		if (state.ValueKind != JsonValueKind.Object)
		{
			return false;
		}

		return Truth(state, name)
			|| (state.TryGetProperty("result", out var result) && Truth(result, name));

		static bool Truth(JsonElement element, string property) =>
			element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
	}

	private static bool Declared(JsonElement? answer) =>
		answer is { ValueKind: JsonValueKind.Object } element
			&& element.TryGetProperty("declared", out var declared)
			&& declared.ValueKind == JsonValueKind.True;

	/// <summary>Runs one driver action against one client and returns its JSON answer.</summary>
	private async Task TryActionAsync(string url, string action, CancellationToken cancellationToken, params (string Name, string Value)[] extraArguments)
	{
		try
		{
			await ActionAsync(url, action, cancellationToken, extraArguments).ConfigureAwait(false);
		}
		catch (Exception)
		{
		}
	}

	private readonly Dictionary<string, DriverClient> _drivers = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// One Online UI action, answered in-process over the client's own socket: the driver template that
	/// <c>drive-in-process.ps1</c> used to send is expanded here and evaluated by the game's own evaluator.
	/// No process is started per action.
	/// </summary>
	public async Task<JsonElement?> LogTailAsync(string url, string match, int count, CancellationToken cancellationToken) =>
		// The last lines of a client's own BepInEx log that contain the match: a case judges the
		// target's side of a relayed effect from the target's own file, not from the host's answer.
		await ActionAsync(url, "log-tail", cancellationToken, ("-Text", count.ToString(CultureInfo.InvariantCulture)), ("-Argument", match)).ConfigureAwait(false);

	public async Task<JsonElement> ActionAsync(string url, string action, CancellationToken cancellationToken, params (string Name, string Value)[] extraArguments)
	{
		var argument = string.Empty;
		var text = string.Empty;
		foreach (var (name, value) in extraArguments)
		{
			if (string.Equals(name, "-Text", StringComparison.OrdinalIgnoreCase))
			{
				text = value;
			}
			else
			{
				argument = value;
			}
		}

		if (!_drivers.TryGetValue(url, out var driver))
		{
			driver = new DriverClient(url);
			await driver.ConnectAsync(cancellationToken).ConfigureAwait(false);
			_drivers[url] = driver;
		}

		try
		{
			return await SendActionAsync(driver, url, action, argument, text, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// The client was restarted under the suite: its socket died with the old process, and the
			// cached connection answers nothing. One fresh connection is the difference between "the
			// driver never answered" and the action being judged.
			if (!await ReconnectAsync(url, driver).ConfigureAwait(false))
			{
				throw;
			}

			return await SendActionAsync(_drivers[url], url, action, argument, text, cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task<JsonElement> SendActionAsync(DriverClient driver, string url, string action, string argument, string text, CancellationToken cancellationToken)
	{
		// A declaration is not a driver command: it is the source the evaluator must be given, then
		// probed. The PowerShell driver composed these two frames; the suite composes them here.
		if (string.Equals(action, "declare", StringComparison.OrdinalIgnoreCase))
		{
			return await DeclareAsync(driver, argument, text, cancellationToken).ConfigureAwait(false);
		}

		var answer = await driver.EvalAsync(DriverSnippet(action, argument, text), action, cancellationToken).ConfigureAwait(false);
		if (answer is null)
		{
			throw new InvalidOperationException($"the acceptance driver answered '{action}' without a value");
		}

		var answerValue = answer.Value;
		if (answerValue.ValueKind == JsonValueKind.String)
		{
			using var parsed = JsonDocument.Parse(answerValue.GetString() ?? throw new InvalidOperationException($"the acceptance driver answered '{action}' with no text"));
			return parsed.RootElement.Clone();
		}

		if (answerValue.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
		{
			return answerValue.Clone();
		}

		throw new InvalidOperationException($"the acceptance driver answered '{action}' with {answerValue.ValueKind}, not a JSON object or string");
	}

	private async Task<bool> ReconnectAsync(string url, DriverClient driver)
	{
		_drivers.Remove(url);
		try
		{
			await driver.DisposeAsync().ConfigureAwait(false);
		}
		catch (Exception)
		{
		}

		var fresh = new DriverClient(url);
		try
		{
			await fresh.ConnectAsync(CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception)
		{
			await fresh.DisposeAsync().ConfigureAwait(false);
			return false;
		}

		_drivers[url] = fresh;
		return true;
	}

	private static string DriverSnippet(string action, string argument, string text)
	{
		var template = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "tools", "acceptance", "driver", "InProcessDriver.cs"));
		return template
			.Replace("\"{{COMMAND}}\"", Literal(action), StringComparison.Ordinal)
			.Replace("\"{{ARGUMENT}}\"", Literal(argument), StringComparison.Ordinal)
			.Replace("\"{{TEXT}}\"", Literal(text), StringComparison.Ordinal);
	}

	private static string Literal(string value) =>
		"\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"";

	private static bool IsTrue(JsonElement answer, string property)
	{
		if (answer.ValueKind == JsonValueKind.Object)
		{
			if (answer.TryGetProperty(property, out var value))
			{
				return value.ValueKind switch
				{
					JsonValueKind.True => true,
					JsonValueKind.String => string.Equals(value.GetString(), "true", StringComparison.OrdinalIgnoreCase),
					_ => false,
				};
			}

			foreach (var child in answer.EnumerateObject())
			{
				if (child.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array && IsTrue(child.Value, property))
				{
					return true;
				}
			}
		}
		else if (answer.ValueKind == JsonValueKind.Array)
		{
			foreach (var child in answer.EnumerateArray())
			{
				if (IsTrue(child, property))
				{
					return true;
				}
			}
		}

		return false;
	}
}




