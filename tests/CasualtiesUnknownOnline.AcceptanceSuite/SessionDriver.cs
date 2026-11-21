using System.Collections.Concurrent;
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
	private static readonly TimeSpan StartBudget = TimeSpan.FromSeconds(30);
	// Normal member generation is 21-24 s on this box. The budget is one measured normal plus
	// a small margin: a blocked load fails at 30 s instead of hiding for a minute.
	private static readonly TimeSpan JoinBudget = TimeSpan.FromSeconds(50);

	private readonly Options _options = options;

	// Setup is the time the suite spends making the session ready (leaving a world, starting a
	// run, joining the member), not the time a case spends driving the game. The cases instantiate
	// their own SessionDriver, so this measures per-case setup; the shared pair setup is measured
	// separately by Program. Nested calls (RestartRun -> EnsureInWorld -> EnsureMember) count once.
	private readonly Stopwatch _setup = new();
	private int _setupDepth;

	public double SetupMilliseconds => _setup.Elapsed.TotalMilliseconds;

	/// <summary>The suite's timeline, called at the phase boundaries that are otherwise only a duration.</summary>
	public Action<string>? Timeline { get; init; }

	private void BeginSetup()
	{
		if (_setupDepth++ == 0)
		{
			_setup.Start();
		}
	}

	private void EndSetup()
	{
		if (--_setupDepth == 0)
		{
			_setup.Stop();
		}
	}

	private async Task<T> MeasureSetupAsync<T>(Func<Task<T>> work)
	{
		BeginSetup();
		try
		{
			return await work().ConfigureAwait(false);
		}
		finally
		{
			EndSetup();
		}
	}

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
		BeginSetup();
		try
		{
			// Never leave a world that is still loading: the host's start gate would wait for a member
			// that has just been sent back to the menu, and the window the player sees is a frozen
			// loading line nobody can explain. Wait until the world is up and the gate has released.
			// A host already at the menu (the ending/run-end cases leave it there) has no world to
			// leave; waiting 30 s for inWorld would be a dead wait, so read first.
			var state = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			if (IsTrue(state, "inWorld"))
			{
				await WaitUntilAsync(_options.HostUrl, candidate => IsTrue(candidate, "inWorld") && !IsTrue(candidate, "gateWaiting"), TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
				await ActionAsync(_options.HostUrl, "leave-world", cancellationToken).ConfigureAwait(false);
				// Never block the client's own thread on a state wait: the in-process wait-state loop sleeps
				// on the game thread and turns a slow scene transition into a "not responding" window.
				// Poll from the suite side instead; each state read is a short eval.
				await WaitWorldExitAsync(_options.HostUrl, cancellationToken).ConfigureAwait(false);
			}

			var started = await EnsureInWorldAsync(cancellationToken).ConfigureAwait(false);
			// A restart is a full leave + generation + member world; the normal join budget is deliberately
			// short so a stuck join fails fast, but the measured restart path is 41.7 s even when it succeeds.
			var joined = await EnsureMemberAsync(cancellationToken, TimeSpan.FromSeconds(50)).ConfigureAwait(false);
			return "fresh run: " + started + "; " + joined;
		}
		finally
		{
			EndSetup();
		}
	}
	/// <summary>Creates the host's IP session and leaves any older world; the run start is separate.</summary>
	public async Task<string> EnsureHostSessionAsync(CancellationToken cancellationToken)
	{
		var state = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		if (IsTrue(state, "inWorld"))
		{
			// A world that is already running belongs to an earlier session: its roster may be empty
			// (a run whose members never joined), and the cases would then read a world nobody
			// shares. The suite starts its own run every time; leaving before the first case costs
			// one load and is never done mid-case.
			await TryActionAsync(_options.HostUrl, "leave-world", cancellationToken).ConfigureAwait(false);
			await WaitWorldExitAsync(_options.HostUrl, cancellationToken).ConfigureAwait(false);
			state = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		}

		// A run needs the host's IP session first: at the menu, switch the transport and create
		// the host, then start the run. The home page answers only while its window is up, and a
		// client loading a scene can refuse the first click, so it retries with a fresh open.
		await ActionAsync(_options.HostUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
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
		await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
		try
		{
			// The guest's join sets its port from the host's own value below; pin the host to the
			// suite's port so a config left at another port cannot
			// make every join miss the listener and toggle the transport until the budget expires.
			await TryActionAsync(_options.HostUrl, "set-text", cancellationToken, ("-Control", "ip.listen_port"), ("-Text", "7777")).ConfigureAwait(false);
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

		return "the suite created the host's IP session; the run has not started yet";
	}

	/// <summary>
	/// The host log offset immediately before the current run's world load. A custody line before this
	/// offset belongs to an earlier world and must not satisfy a wait in this one.
	/// </summary>
	public long WorldStartLogOffset { get; private set; }

	/// <summary>Starts the run on the host's existing IP session and waits for the host's world.</summary>
	public async Task<string> StartHostRunAsync(CancellationToken cancellationToken)
	{
		WorldStartLogOffset = await LogOffsetAsync(_options.HostUrl, cancellationToken).ConfigureAwait(false);
		var startDispatch = Stopwatch.StartNew();
		await ActionAsync(_options.HostUrl, "start-run", cancellationToken).ConfigureAwait(false);
		startDispatch.Stop();

		// Poll from the suite side. The in-client wait-state loop sleeps on the game thread, which is
		// what made a normal 20 s scene transition look like a frozen host (2026-11-15).
		var startDeadline = Stopwatch.StartNew();
		while (startDeadline.Elapsed < StartBudget)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
			var state = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			if (IsTrue(state, "inWorld"))
			{
				return "the suite started a fresh run (start-run dispatch " + startDispatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + " ms; host in-world after " + startDeadline.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s)";
			}
		}

		throw new InvalidOperationException("the suite could not start a world for the cases");
	}

	/// <summary>Starts a run when no world is up and returns the note for the report.</summary>
	public async Task<string> EnsureInWorldAsync(CancellationToken cancellationToken)
	{
		_ = await EnsureHostSessionAsync(cancellationToken).ConfigureAwait(false);
		return await StartHostRunAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Joins the member to the host's IP session when it is not in the world.</summary>
	public Task<string> EnsureMemberAsync(CancellationToken cancellationToken, TimeSpan? budget = null) =>
		MeasureSetupAsync(() => JoinMemberAsync(cancellationToken, allowRelaunch: true, budget));

	/// <summary>
	/// The third client's endpoint. It is only up when the launch asked for it (<c>run-suite -All</c>);
	/// a row that needs two members states that in its report and fails on the alt's own state when it is
	/// not running, rather than silently testing one member twice.
	/// </summary>
	public string AltUrl => _options.AltUrl;

	/// <summary>
	/// Joins the second member (the alt client) to the host's current run. The join loop is the member's
	/// own: the home page offers the transport switch, a notice, or the join controls depending on how far
	/// the client has loaded, so the loop acts on what the state offers instead of clicking a control that
	/// may not be built.
	/// </summary>
	public async Task<string> EnsureSecondMemberAsync(CancellationToken cancellationToken)
	{
		var url = _options.AltUrl;
		var state = await ActionAsync(url, "state", cancellationToken).ConfigureAwait(false);
		if (IsTrue(state, "inWorld") && await IsInHostRunAsync(url, cancellationToken).ConfigureAwait(false))
		{
			return "the second member was already in the host's run";
		}

		if (IsTrue(state, "inWorld"))
		{
			await TryActionAsync(url, "leave-world", cancellationToken).ConfigureAwait(false);
			await WaitWorldExitAsync(url, cancellationToken).ConfigureAwait(false);
		}

		await ActionAsync(url, "open-window", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
		await TryActionAsync(url, "click", cancellationToken, ("-Control", "tab.home")).ConfigureAwait(false);

		var deadline = Stopwatch.StartNew();
		var lastJoinClick = DateTimeOffset.MinValue;
		while (deadline.Elapsed < TimeSpan.FromSeconds(50))
		{
			state = await ActionAsync(url, "state", cancellationToken).ConfigureAwait(false);
			if (IsTrue(state, "inWorld") && await IsInHostRunAsync(url, cancellationToken).ConfigureAwait(false))
			{
				return $"the second member joined the host's run after {deadline.Elapsed.TotalSeconds:F1}s";
			}

			var controls = Controls(state);
			if (controls.Contains("home.leave") && !string.Equals(StringValue(state, "role"), "Guest", StringComparison.Ordinal))
			{
				await TryActionAsync(url, "click", cancellationToken, ("-Control", "home.leave")).ConfigureAwait(false);
				await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
			}
			else if (controls.Contains("ip.notice_confirm"))
			{
				await TryActionAsync(url, "click", cancellationToken, ("-Control", "ip.notice_confirm")).ConfigureAwait(false);
			}
			else if (controls.Contains("ip.join"))
			{
				if (lastJoinClick == DateTimeOffset.MinValue || (DateTimeOffset.UtcNow - lastJoinClick).TotalSeconds >= 2)
				{
					await TryActionAsync(url, "set-text", cancellationToken, ("-Control", "ip.address"), ("-Text", "127.0.0.1")).ConfigureAwait(false);
					await TryActionAsync(url, "set-text", cancellationToken, ("-Control", "ip.port"), ("-Text", "7777")).ConfigureAwait(false);
					await TryActionAsync(url, "click", cancellationToken, ("-Control", "ip.join")).ConfigureAwait(false);
					lastJoinClick = DateTimeOffset.UtcNow;
				}
			}
			else if (controls.Contains("home.transport.ip"))
			{
				await TryActionAsync(url, "click", cancellationToken, ("-Control", "home.transport.ip")).ConfigureAwait(false);
			}

			await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException("the second member did not join the host's run; last state: " + state.GetRawText());
	}

	/// <summary>Both sides name the same kernel run, and the host's roster names this client.</summary>
	private async Task<bool> IsInHostRunAsync(string url, CancellationToken cancellationToken)
	{
		var state = await ActionAsync(url, "state", cancellationToken).ConfigureAwait(false);
		var localId = StringValue(state, "steamId");
		if (localId.Length == 0 || !IsTrue(state, "inWorld"))
		{
			return false;
		}

		var host = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		var hostRun = StateUInt64(host, "runId");
		if (hostRun == 0 || StateUInt64(state, "runId") != hostRun)
		{
			return false;
		}

		var members = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		return members.Contains(localId, StringComparer.Ordinal);
	}

	/// <summary>Join the host's IP session while both clients are still at the menu.</summary>
	public Task<string> EnsureMemberSessionAsync(CancellationToken cancellationToken) =>
		JoinMemberSessionAsync(cancellationToken);

	/// <summary>
	/// A host that starts a fresh run while the member is already in the IP session: the member's
	/// own client follows the host's world join. Do not re-run the join handshake from here; it
	/// would leave the old world first and can time out exactly as the member is arriving.
	/// </summary>
	public async Task WaitForMemberInCurrentRunAsync(CancellationToken cancellationToken)
	{
		var deadline = Stopwatch.StartNew();
		var lastHost = default(JsonElement?);
		var lastMember = default(JsonElement?);
		while (deadline.Elapsed < TimeSpan.FromSeconds(30))
		{
			lastHost = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			lastMember = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
			if (lastHost is { ValueKind: JsonValueKind.Object } host
				&& lastMember is { ValueKind: JsonValueKind.Object } member
				&& string.Equals(StringValue(member, "role"), "Guest", StringComparison.Ordinal)
				&& IsTrue(member, "inWorld")
				&& ReadInt(host, "runId") != 0
				&& ReadInt(host, "runId") == ReadInt(member, "runId"))
			{
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException(
			"the member did not join the host's current run: host=" + (lastHost?.GetRawText() ?? "null")
			+ " member=" + (lastMember?.GetRawText() ?? "null"));
	}

	/// <summary>
	/// Poll the client's state from the suite side. The in-process wait-state command sleeps on the
	/// game thread; using it here froze the host for the whole scene transition (2026-11-15).
	/// </summary>
	public async Task<bool> WaitWorldExitAsync(string url, CancellationToken cancellationToken)
	{
		var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < TimeSpan.FromSeconds(5))
		{
			var state = await ActionAsync(url, "state", cancellationToken).ConfigureAwait(false);
			if (!IsTrue(state, "inWorld"))
			{
				return true;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
		}

		return false;
	}

	/// <summary>
	/// Clears a Guest session left behind by the previous run. Leaving the world is not enough: the
	/// role and the active flag stay until the player uses Home > Leave, and while they stay the
	/// host's fresh run does not know the member. The suite starts a fresh pair, so this is its own
	/// cleanup, not a restart.
	/// </summary>
	public async Task ClearMemberSessionAsync(CancellationToken cancellationToken)
	{
		var state = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
		if (!IsTrue(state, "active") && !string.Equals(StringValue(state, "role"), "Guest", StringComparison.Ordinal))
		{
			return;
		}

		// Inside a world the session-leave control is not the world-leave edge: ask the product's
		// own leave intent first, then end the session from the menu. A bare home.leave click while
		// inWorld leaves the presence behind (the ghost measured after remote-removal).
		if (IsTrue(state, "inWorld"))
		{
			await TryActionAsync(_options.MemberUrl, "leave-world-intent", cancellationToken).ConfigureAwait(false);
			await WaitUntilAsync(
				_options.MemberUrl,
				probe => !IsTrue(probe, "inWorld"),
				TimeSpan.FromSeconds(5),
				cancellationToken).ConfigureAwait(false);
			state = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
		}

		if (Controls(state).Contains("home.leave"))
		{
			await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "home.leave")).ConfigureAwait(false);
		}

		await WaitUntilAsync(
			_options.MemberUrl,
			probe => !IsTrue(probe, "active") && !string.Equals(StringValue(probe, "role"), "Guest", StringComparison.Ordinal),
			TimeSpan.FromSeconds(5),
			cancellationToken).ConfigureAwait(false);
	}
	/// <summary>
	/// Joins the IP-direct session before the host opens its world. The host's start message is sent
	/// to session members, but a member that arrives after the start has already passed the gate has
	/// to wait for the host's 30 s start-gate timeout (measured 2026-11-15: host "waiting for 1
	/// member", member "waiting for the host to start", both already loaded). Handshaking the
	/// session first removes that wait; the world generation still overlaps the host's start.
	/// </summary>
	private async Task<string> JoinMemberSessionAsync(CancellationToken cancellationToken)
	{
		var state = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
		if (IsTrue(state, "active") && string.Equals(StringValue(state, "role"), "Guest", StringComparison.Ordinal))
		{
			return "the member's session was already connected before the host started";
		}

		await ActionAsync(_options.MemberUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
		await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "tab.home")).ConfigureAwait(false);
		var deadline = Stopwatch.StartNew();
		var lastJoinClick = DateTimeOffset.MinValue;
		while (deadline.Elapsed < TimeSpan.FromSeconds(20))
		{
			state = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
			if (IsTrue(state, "active") && string.Equals(StringValue(state, "role"), "Guest", StringComparison.Ordinal))
			{
				return $"the member's session connected before the host started ({deadline.Elapsed.TotalSeconds:F1}s)";
			}

			var controls = Controls(state);
			if (controls.Contains("home.leave") && !string.Equals(StringValue(state, "role"), "Guest", StringComparison.Ordinal))
			{
				await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "home.leave")).ConfigureAwait(false);
				await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
			}
			else if (controls.Contains("ip.notice_confirm"))
			{
				await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "ip.notice_confirm")).ConfigureAwait(false);
			}
			else if (controls.Contains("ip.join"))
			{
				if (lastJoinClick == DateTimeOffset.MinValue || (DateTimeOffset.UtcNow - lastJoinClick).TotalSeconds >= 2)
				{
					await TryActionAsync(_options.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.address"), ("-Text", "127.0.0.1")).ConfigureAwait(false);
					await TryActionAsync(_options.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.port"), ("-Text", "7777")).ConfigureAwait(false);
					await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "ip.join")).ConfigureAwait(false);
					lastJoinClick = DateTimeOffset.UtcNow;
				}
			}
			else if (controls.Contains("home.transport.ip"))
			{
				await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "home.transport.ip")).ConfigureAwait(false);
			}

			await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException("the member's session did not connect before the host's run start");
	}

	private async Task<string> WaitMemberWorldAsync(string sessionNote, CancellationToken cancellationToken)
	{
		var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < JoinBudget)
		{
			var state = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
			if (IsTrue(state, "inWorld"))
			{
				var localId = StringValue(state, "steamId");
				var hostMembers = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
				var listed = localId.Length > 0
					? hostMembers.Contains(localId, StringComparer.Ordinal)
					: hostMembers.Length > 0;
				if (listed)
				{
					return sessionNote + $"; the member entered the world after {deadline.Elapsed.TotalSeconds:F1}s";
				}
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException("the member's world was not ready after the host started");
	}

	private async Task<string> JoinMemberAsync(CancellationToken cancellationToken, bool allowRelaunch, TimeSpan? budget = null)
	{
		var state = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
		var hostState = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		var hostMembers = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		// A non-empty host roster is not proof that THIS member is in it. After a run restart the host
		// can still list the previous session's member while the new guest sits in the world unlisted;
		// that produced a pod refusal that named an old member id on 2026-11-15.
		var localId = StringValue(state, "steamId");
		var hostCarriesLocal = localId.Length > 0
			? hostMembers.Contains(localId, StringComparer.Ordinal)
			: hostMembers.Length > 0;
		var hostRunId = StateUInt64(hostState, "runId");
		var memberRunId = StateUInt64(state, "runId");
		var sameRun = hostRunId != 0 && memberRunId == hostRunId;
		if (IsTrue(state, "inWorld") && hostCarriesLocal && sameRun)
		{
			return "the member was already in the host's run";
		}

		if (IsTrue(state, "inWorld") && sameRun)
		{
			// The member is in the host's current run, but the host's roster does not carry it yet:
			// it is still generating its world (measured 2026-11-20: the guest's scene stayed
			// InMenu (PreGen) for 12-17 s while the host counted a stale second connection).
			// Leaving here destroys the session the world join needs, so wait for the roster.
			await WaitForMemberListedAsync(localId, TimeSpan.FromSeconds(50), cancellationToken).ConfigureAwait(false);
			return $"the suite waited for the member to appear in the host's run ({localId})";
		}

		if (IsTrue(state, "inWorld"))
		{
			// The member still sits in an earlier session; the host's fresh run does not list it,
			// so it leaves and joins the host's current one. The leave races the scene, and the
			// joiner refuses while THIS client is still generating a world ("IP-direct start
			// refused: a world is running or generating"), so the wait is the judge.
			await TryActionAsync(_options.MemberUrl, "leave-world", cancellationToken).ConfigureAwait(false);
			await WaitWorldExitAsync(_options.MemberUrl, cancellationToken).ConfigureAwait(false);
			state = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
		}
		// The window can be closed (a case pressed Escape) or the client can still be loading after
		// a restart; the home page's controls answer only while the page is up. A fresh offline
		// client shows the notice INSTEAD of the join controls, so the loop acts on what the state
		// offers instead of clicking a control that may not be built yet.
		await ActionAsync(_options.MemberUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
		await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "tab.home")).ConfigureAwait(false);

		var deadline = Stopwatch.StartNew();
		var activeSince = Stopwatch.StartNew();
		var joinBudget = budget ?? JoinBudget;
		while (deadline.Elapsed < joinBudget)
		{
			state = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
			// The IP-direct identity is assigned by the transport when the session connects; a fresh
			// client reads 0 until then, so the roster check must use the id from THIS probe rather
			// than the id captured before the join began.
			localId = StringValue(state, "steamId");
			if (!string.Equals(StringValue(state, "role"), "Guest", StringComparison.Ordinal) || IsTrue(state, "inWorld"))
			{
				activeSince.Restart();
			}

			if (IsTrue(state, "inWorld"))
			{
				// A member can be in the previous run's world while the host has already started a new
				// one. Presence plus roster is not enough: both sides must name the same kernel run.
				// A member still in the old world leaves and joins the host's current run.
				hostMembers = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
				hostState = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
				var listed = localId.Length > 0
					? hostMembers.Contains(localId, StringComparer.Ordinal)
					: hostMembers.Length > 0;
				var hostRun = StateUInt64(hostState, "runId");
				var memberRun = StateUInt64(state, "runId");
				if (listed && hostRun != 0 && memberRun == hostRun)
				{
					return $"the suite joined the member to the host's run after {deadline.Elapsed.TotalSeconds:F1}s";
				}

				if (memberRun != hostRun)
				{
					await TryActionAsync(_options.MemberUrl, "leave-world", cancellationToken).ConfigureAwait(false);
					await WaitWorldExitAsync(_options.MemberUrl, cancellationToken).ConfigureAwait(false);
					continue;
				}

				// The window can be up over a world the roster does not carry; the driver's own leave
				// is the only way out of a world the window does not show.
				var ghostControls = Controls(state);
				if (!ghostControls.Contains("home.leave") && string.Equals(StringValue(state, "page"), "Home", StringComparison.Ordinal))
				{
					await TryActionAsync(_options.MemberUrl, "leave-world", cancellationToken).ConfigureAwait(false);
					await Task.Delay(TimeSpan.FromMilliseconds(1200), cancellationToken).ConfigureAwait(false);
					continue;
				}
			}

			var controls = Controls(state);
			if (string.Equals(StringValue(state, "role"), "Guest", StringComparison.Ordinal) && !IsTrue(state, "inWorld"))
			{
				// Two different states wear the same role. An INACTIVE Guest is the leftover of a
				// session whose host went away: the client's own watchdog ends it, but until then
				// every join is refused or ignored (measured 2026-11-13: role=Guest, inWorld empty,
				// host roster empty), so the driver's own leave is the way out. An ACTIVE Guest is
				// the session the host has just restarted: its world join is on the way, and leaving
				// it every 800 ms destroys the very session that join needs. Wait instead; the
				// product-side phase reset is what lets the join land (measured 2026-11-15:
				// role=Guest, active=true, inWorld=false, page=Home, gateText=waiting for the host).
				if (!IsTrue(state, "active"))
				{
					await TryActionAsync(_options.MemberUrl, "leave-world", cancellationToken).ConfigureAwait(false);
					await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken).ConfigureAwait(false);
				}
				else
				{
					// An ACTIVE Guest can also be a stale session from the previous run: the host's
					// new world does not name it and no join is coming. Give the real join its measured
					// 12 s, then clear the stale session through Home > Leave and rejoin. Clearing
					// earlier destroys the session the join needs (measured 2026-11-15).
					if (activeSince.Elapsed >= TimeSpan.FromSeconds(20))
					{
						await ClearMemberSessionAsync(cancellationToken).ConfigureAwait(false);
						activeSince.Restart();
					}
					else
					{
						await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
					}
				}
			}

			var page = StringValue(state, "page");
			if (!string.Equals(page, "Home", StringComparison.Ordinal) && controls.Contains("tab.home"))
			{
				// The join controls live on the home page; a client parked on another tab has no
				// ip.join and no transport switch. Measured A1: the cleared guest sat on page=Network
				// with only the tabs and the ping, so clicking tab.network again never left it.
				await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "tab.home")).ConfigureAwait(false);
			}
			else if (controls.Contains("home.leave") && !string.Equals(StringValue(state, "role"), "Guest", StringComparison.Ordinal))
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
		// This is a FALLBACK, not a repair: the leave-world branch above is what should clear the
		// ghost world, so every trigger is a finding and is named in the report.
		// The member is never restarted to make a join succeed: a restart would throw away the session
		// being measured and hide whatever left the client unable to join (the user's rule,
		// 2026-11-14). The failure carries the client's own state so the cause can be fixed.

		throw new InvalidOperationException(
			"the suite could not join the member to the host's session: " + DescribeMemberState(state));
	}

	private async Task WaitForMemberListedAsync(string localId, TimeSpan budget, CancellationToken cancellationToken)
	{
		var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < budget)
		{
			var members = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
			var listed = localId.Length > 0
				? members.Contains(localId, StringComparer.Ordinal)
				: members.Length > 0;
			if (listed)
			{
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException(
			$"the member {localId} never appeared in the host's roster after {budget.TotalSeconds:F0}s");
	}

	private static string DescribeMemberState(JsonElement state) =>
		$"role={StringValue(state, "role")}, active={(IsTrue(state, "active") ? "true" : "false")}, "
		+ $"inWorld={(IsTrue(state, "inWorld") ? "true" : "false")}, "
		+ $"gateWaiting={(IsTrue(state, "gateWaiting") ? "true" : "false")}, "
		+ $"page={StringValue(state, "page")}, controls={string.Join(",", Controls(state))}";

	/// <summary>How many times the member had to be recreated to join (0 is the healthy reading; every trigger is a finding).</summary>
	public int MemberRelaunches { get; private set; }

	/// <summary>A restart is a deploy or a source change, never a fixture repair (the user's rule, 2026-11-14).</summary>
	private Task RelaunchMemberAsync(CancellationToken cancellationToken) => RelaunchAsync(_options.MemberUrl, "guest", cancellationToken);

	private Task RelaunchHostAsync(CancellationToken cancellationToken) => RelaunchAsync(_options.HostUrl, "host", cancellationToken);

	/// <summary>
	/// Opens the host's IP session over the world this process already has — the crash row's restored
	/// world. Unlike <see cref="EnsureHostSessionAsync"/> it never leaves that world: the restored
	/// world is the artefact under test, and leaving it would throw the reading away. The transport
	/// switch is the same one the setup path uses, because the Home page offers no `ip.create_host`
	/// until the IP transport is selected (measured A3, 2026-11-20: the row clicked create_host on a
	/// Steam-transport page and the state kept `role=None`).
	/// </summary>
	public async Task<string> OpenHostSessionOverWorldAsync(CancellationToken cancellationToken)
	{
		var state = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		if (!IsTrue(state, "inWorld"))
		{
			throw new InvalidOperationException("the host is not in a world, so there is no restored world to open a session over: " + DescribeMemberState(state));
		}

		if (string.Equals(StringValue(state, "role"), "Host", StringComparison.Ordinal))
		{
			return "the host already holds its session over the restored world";
		}

		await ActionAsync(_options.HostUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
		await TryActionAsync(_options.HostUrl, "click", cancellationToken, ("-Control", "tab.home")).ConfigureAwait(false);
		await TryActionAsync(_options.HostUrl, "click", cancellationToken, ("-Control", "ip.notice_confirm")).ConfigureAwait(false);
		await TryActionAsync(_options.HostUrl, "click", cancellationToken, ("-Control", "home.transport.ip")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
		// The member's join reads this port; pin it the same way the setup path does.
		await TryActionAsync(_options.HostUrl, "set-text", cancellationToken, ("-Control", "ip.listen_port"), ("-Text", "7777")).ConfigureAwait(false);

		var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < TimeSpan.FromSeconds(20))
		{
			await TryActionAsync(_options.HostUrl, "click", cancellationToken, ("-Control", "ip.create_host")).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
			state = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			if (string.Equals(StringValue(state, "role"), "Host", StringComparison.Ordinal) && IsTrue(state, "active"))
			{
				return "the host opened its IP session over the restored world";
			}
		}

		throw new InvalidOperationException("the restarted host could not open a session over its restored world: " + DescribeMemberState(state));
	}

	/// <summary>
	/// Starts the host after a case has confirmed its process is down (the host-crash row). The
	/// existing refusal for an answering client still applies: a crash case kills the host first,
	/// and only a down client is started.
	/// </summary>
	public Task RestartHostAsync(CancellationToken cancellationToken) => RelaunchAsync(_options.HostUrl, "host", cancellationToken);

	private async Task RelaunchAsync(string url, string only, CancellationToken cancellationToken)
	{
		// Only a client that is DOWN is started. A client that answers but cannot join is a finding,
		// never a restart (the user's rule, 2026-11-14): a restart would throw away the session being
		// measured and hide the cause.
		try
		{
			await ActionAsync(url, "state", cancellationToken).ConfigureAwait(false);
			throw new InvalidOperationException(
				$"the {only} client answers but cannot join. The suite does not restart a client: a restart is a deploy or a "
				+ "source change (the user's rule, 2026-11-14). Read its state and fix the cause.");
		}
		catch (InvalidOperationException)
		{
			throw;
		}
		catch (Exception)
		{
			// Not answering: the client is not running. Start it (a start, not a restart).
		}

		var hostProject = Path.Combine(RepositoryPaths.Root, "tools", "acceptance", "host", "CasualtiesUnknownOnline.AcceptanceHost.csproj");
		// The cached socket belongs to the process that just died. Dropping it before the new host
		// starts keeps the first state read from hitting the dead connection.
		_drivers.TryRemove(url, out _);
		var start = new ProcessStartInfo("dotnet")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			// A console program started with redirected pipes from a caller that has no console gets a
			// fresh one: a terminal window pops over the reader's desktop on every relaunch (2026-11-20).
			CreateNoWindow = true,
		};
		start.ArgumentList.Add("run");
		start.ArgumentList.Add("--no-build");
		start.ArgumentList.Add("--project");
		start.ArgumentList.Add(hostProject);
		start.ArgumentList.Add("--");
		start.ArgumentList.Add("launch");
		start.ArgumentList.Add("--only");
		start.ArgumentList.Add(only);
		// The host the case killed can leave a process that exists but never answers (measured
		// A3, 2026-11-20: "host process exists; letting it come up" sat on the launcher's 300 s
		// budget). The case has already refused an answering client, so this start stops any
		// leftover and uses the same measured 120 s startup budget.
		start.ArgumentList.Add("--restart");
		start.ArgumentList.Add("--ready-timeout");
		start.ArgumentList.Add("120");
		if (!string.IsNullOrEmpty(_options.GameDir))
		{
			start.ArgumentList.Add("--game-dir");
			start.ArgumentList.Add(_options.GameDir!);
		}

		if (!string.IsNullOrEmpty(_options.SaveRoot))
		{
			start.ArgumentList.Add("--save-root");
			start.ArgumentList.Add(_options.SaveRoot!);
		}

		using var process = Process.Start(start) ?? throw new InvalidOperationException($"could not start the acceptance launcher for the {only}");
		// A launcher's stdout is a pipe its own child can inherit: a game that outlives the launcher
		// keeps the write end open, so awaiting the end of that stream hangs this case past every
		// budget. Measured A3, 2026-11-20: no launcher process was left, the restarted host answered
		// on its port, and the case had been silent for minutes with no verdict and no report. The
		// exit code decides whether the launch worked; the text is a courtesy, taken with a cap.
		var output = ReadAll(process.StandardOutput);
		var errors = ReadAll(process.StandardError);
		await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
		var text = await TakeAsync(output).ConfigureAwait(false);
		_ = await TakeAsync(errors).ConfigureAwait(false);
		// The launcher's own readiness wait already says whether the host came up; polling
		// another 120 s behind a launcher that exited 6 would hide its named failure.
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"the acceptance launcher could not start the {only} (exit {process.ExitCode}): " + text.Trim());
		}

		var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < TimeSpan.FromSeconds(120))
		{
			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			try
			{
				await ActionAsync(url, "state", cancellationToken).ConfigureAwait(false);
				// The launcher rewrote pids.json for the process it just started: hold that handle now,
				// while the process is alive, or its exit code is lost with it.
				ClientExits?.Refresh();
				return;
			}
			catch (Exception)
			{
			}
		}

		throw new InvalidOperationException($"the suite started the {only} but its driver never answered: " + text.Trim());
	}

	private static Task<string> ReadAll(StreamReader reader) => Task.Run(async () =>
	{
		try
		{
			return await reader.ReadToEndAsync().ConfigureAwait(false);
		}
		catch (Exception)
		{
			// A client still holds the write end and the stream was closed under the reader; the
			// launcher's text is a courtesy for the error message, never the verdict.
			return string.Empty;
		}
	});

	/// <summary>Takes the reader's text if it arrived shortly after the launcher exited; otherwise none.</summary>
	private static async Task<string> TakeAsync(Task<string> reading)
	{
		var finished = await Task.WhenAny(reading, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
		return finished == reading ? await reading.ConfigureAwait(false) : string.Empty;
	}

	/// <summary>
	/// The pair a run is judged on. When the host stops listening (its transport logs a disconnect
	/// loop, its roster stays empty and no member can join it), clicking cannot restore the session —
	/// the host process itself has to be recreated. The suite owns both clients, so it recreates the
	/// host once and builds a fresh session before the cases start; the reading is on
	/// `docs/backlog/in-progress/1.0-acceptance-suite.md`.
	/// </summary>
	public async Task<(string HostNote, string MemberNote)> ReusePairAsync(CancellationToken cancellationToken)
	{
		// Fast-group entry: the caller just set the pair up and wants the InRun cases without paying
		// another cold world load. Reuse is explicit (--reuse) and only accepted when both clients are
		// already in a world and the host names a member; anything else falls back to a fresh pair.
		var host = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		var member = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
		if (!IsTrue(host, "inWorld") || !IsTrue(member, "inWorld"))
		{
			throw new InvalidOperationException("reuse requested but the pair is not both in a world");
		}

		var members = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		if (members.Length == 0)
		{
			throw new InvalidOperationException("reuse requested but the host names no member");
		}

		return ("reused the running run", $"reused the member already in the host's world ({members[0]})");
	}

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
		if (memberBefore is { } memberState)
		{
			if (IsTrue(memberState, "inWorld"))
			{
				// A member left in an older world is asked to leave it through its own game path. The
				// client is NEVER restarted for this: a restart throws away the session being measured
				// and hides the defect that left it there (the user's rule, 2026-11-14).
				await TryActionAsync(_options.MemberUrl, "leave-world", cancellationToken).ConfigureAwait(false);
				await WaitWorldExitAsync(_options.MemberUrl, cancellationToken).ConfigureAwait(false);
			}

			// Leaving the world does not clear a Guest session: the role and `active` stay behind while
			// the host's new run knows nothing of that member. The suite starts a fresh pair, so it
			// clears the stale session through the game's own Home > Leave control before joining.
			if (IsTrue(memberState, "active") || string.Equals(StringValue(memberState, "role"), "Guest", StringComparison.Ordinal))
			{
				await ClearMemberSessionAsync(cancellationToken).ConfigureAwait(false);
			}
		}

		// The default overlaps the host's generation with the member's join. Serial pairing made the
		// member sit in the lobby for the host's whole generation before its own world load began
		// (the player sees "joined the lobby" and then tens of seconds of nothing). Overlapping
		// measured 40.9 s from endpoints to a ready pair on 2026-11-15, serial 47.8 s; the earlier
		// 71.8 s/99.1 s readings were the same overlap on a stale pair and are superseded.
		var pairClock = Stopwatch.StartNew();
		var hostClock = Stopwatch.StartNew();
		string hostNote;
		try
		{
			hostNote = await EnsureHostSessionAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// No host is running at all (a previous run reclaimed it): the launcher starts one.
			await RelaunchHostAsync(cancellationToken).ConfigureAwait(false);
			hostNote = await EnsureHostSessionAsync(cancellationToken).ConfigureAwait(false);
		}
		hostClock.Stop();
		Timeline?.Invoke("host IP session ready");

		var sessionNote = string.Empty;
		try
		{
			sessionNote = await JoinMemberSessionAsync(cancellationToken).ConfigureAwait(false);
			Timeline?.Invoke(sessionNote);
		}
		catch (Exception exception) when (exception is DriverException or InvalidOperationException)
		{
			// Fall back to the old post-start join: a session handshake that the client refuses is
			// a reading, not a reason to abandon the pair.
			sessionNote = "session-first join failed: " + exception.Message;
			Timeline?.Invoke(sessionNote);
		}

		var startClock = Stopwatch.StartNew();
		var memberClock = Stopwatch.StartNew();
		string startNote;
		string memberNote;
		if (_options.ParallelSetup)
		{
			var startTask = StartHostRunAsync(cancellationToken);
			var memberTask = sessionNote.StartsWith("session-first join failed", StringComparison.Ordinal)
				? EnsureMemberAsync(cancellationToken)
				: WaitMemberWorldAsync(sessionNote, cancellationToken);
			await Task.WhenAll(startTask, memberTask).ConfigureAwait(false);
			startNote = startTask.Result;
			memberNote = memberTask.Result;
		}
		else
		{
			startNote = await StartHostRunAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				memberNote = sessionNote.StartsWith("session-first join failed", StringComparison.Ordinal)
					? await EnsureMemberAsync(cancellationToken).ConfigureAwait(false)
					: await WaitMemberWorldAsync(sessionNote, cancellationToken).ConfigureAwait(false);
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
		}
		startClock.Stop();
		memberClock.Stop();
		Timeline?.Invoke("host run start dispatched and member world wait finished");

		// The member can answer inWorld while the host's start gate still waits for its world-ready
		// report. Every InRun case depends on the native update loop being live: a pause written
		// during the gate is skipped by PauseHandlerUpdatePatch, so the first early case can read
		// isPaused=true with timeScale=1 and leave the pause on for the next row (measured
		// 2026-11-15: pause-clock failed after a 86.9 s pair setup, while the same pair passed at
		// 42.3 s). Wait for the host itself to report the gate clear before the batch starts.
		var gateClock = Stopwatch.StartNew();
		await WaitUntilAsync(
			_options.HostUrl,
			candidate => IsTrue(candidate, "inWorld") && !IsTrue(candidate, "gateWaiting"),
			TimeSpan.FromSeconds(30),
			cancellationToken).ConfigureAwait(false);
		Timeline?.Invoke("host start gate clear");
		gateClock.Stop();

		hostNote = hostNote + "; " + startNote
			+ $"; pair ready in {pairClock.Elapsed.TotalSeconds:F1}s"
			+ $" (host session {hostClock.Elapsed.TotalSeconds:F1}s, run start {startClock.Elapsed.TotalSeconds:F1}s,"
			+ $" member world {memberClock.Elapsed.TotalSeconds:F1}s, gate clear {gateClock.Elapsed.TotalSeconds:F1}s; serial)";

		var members = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		if (members.Length > 0)
		{
			// EnsureMemberAsync already waited for this roster read; the handshake log tail is a
			// fallback, not a second confirmation on the normal path (measured 2026-11-15: the
			// log-tail read added 23.6 s after the pair was ready).
			return (hostNote, memberNote);
		}

		var confirmed = await ConfirmedMemberIdAsync(cancellationToken).ConfigureAwait(false);
		if (confirmed is not null)
		{
			return (hostNote, memberNote);
		}

		// The roster can carry a row from a session that is gone; a member whose id the host never
		// confirmed is not in this world, whatever its own client believes (measured 2026-11-12).
		memberNote = await RecoverPairAsync(cancellationToken).ConfigureAwait(false);
		return (hostNote, memberNote);
	}

	/// <summary>
	/// The runner's guarantee for a case that verifies inside a world: the pair is live when the case
	/// starts, whoever left the world last. The 2026-11-21 all-case run measured what a case-owned
	/// guarantee costs: `character-solo-lock` left the world as its own verdict, and the nine rows
	/// after it that expected a world — <c>player-deaths</c>, the four medical rows, `crafting-outcome`,
	/// `two-member-continue`, `late-join` — each waited out its join budget against a host that was
	/// not hosting, while the sound rows read a body with no position. A live pair costs two state
	/// reads; a pair whose world is gone is rebuilt here, and the rebuild is charged to the case's
	/// setup, never hidden.
	/// </summary>
	public async Task<string> EnsureLiveWorldAsync(CancellationToken cancellationToken)
	{
		BeginSetup();
		var clock = Stopwatch.StartNew();
		try
		{
			var hostState = await TryStateAsync(_options.HostUrl, cancellationToken).ConfigureAwait(false);
			var hostInWorld = hostState is { } host
				&& IsTrue(host, "inWorld")
				&& !IsTrue(host, "gateWaiting");
			if (hostInWorld)
			{
				var memberState = await TryStateAsync(_options.MemberUrl, cancellationToken).ConfigureAwait(false);
				if (memberState is { } member && IsTrue(member, "inWorld"))
				{
					// A member in its own world is not the host's member: the roster is the host's
					// own account of who shares this run, and an empty one means the pair is not
					// live however each client describes itself (measured 2026-11-12).
					var members = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
					if (members.Length > 0
						&& await HasBreakableGroundAsync(cancellationToken).ConfigureAwait(false)
						&& await HasCarrySlotAsync(cancellationToken).ConfigureAwait(false))
					{
						return "the pair was already live; " + await EnsureSingleMemberAsync(cancellationToken).ConfigureAwait(false) + "; " + await ClearCarriedAsync(cancellationToken).ConfigureAwait(false);
					}
				}

				// The host still holds the run: the member comes back to it rather than the run being
				// thrown away (a rebuild costs a world load and a new world answers different rows).
				try
				{
					var memberNote = await EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
					var roster = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
					if (roster.Length > 0
						&& await HasBreakableGroundAsync(cancellationToken).ConfigureAwait(false)
						&& await HasCarrySlotAsync(cancellationToken).ConfigureAwait(false))
					{
						return "the host kept its run; the member rejoined it: " + memberNote
							+ "; " + await EnsureSingleMemberAsync(cancellationToken).ConfigureAwait(false)
							+ "; " + await ClearCarriedAsync(cancellationToken).ConfigureAwait(false);
					}
				}
				catch (Exception exception) when (exception is DriverException or InvalidOperationException or IOException)
				{
					Timeline?.Invoke("the member could not rejoin the host's run: " + exception.Message);
				}
			}

			Timeline?.Invoke("no live pair before the case; rebuilding it");
			var pair = await EnsurePairAsync(cancellationToken).ConfigureAwait(false);
			return "the pair was rebuilt before the case: " + pair.HostNote + "; " + pair.MemberNote
				+ "; " + await EnsureSingleMemberAsync(cancellationToken).ConfigureAwait(false)
				+ "; " + await ClearCarriedAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			EndSetup();
			// The run's own wall time is a product number here: the twenty-minute reading is mostly
			// this guarantee, so every case records what it cost (measured 2026-11-22: 461 s of setup
			// across 67 rows, most of it sixteen world loads and the per-row body waits).
			Timeline?.Invoke($"world guarantee: {clock.Elapsed.TotalSeconds:F1}s");
		}
	}

	/// <summary>
	/// The batch's model is one guest in the run: a member row left behind by an earlier case — or a second
	/// client still connected — makes every row that addresses "the member" answer with two ids and fail on
	/// its own precondition (measured 2026-11-21: `the run does not have exactly one member: 2`, and the
	/// host's roster naming two ids in `net-conditions` and `host-rule-toggles`). The extras are removed
	/// through the host's own kick, and the roster is re-read until only this guest is named. Cases that
	/// bring a second client in (two-member-continue) do it after this guarantee, as their own step.
	/// </summary>
	/// <summary>
	/// True when the host stands near ground the game will break.
	/// </summary>
	/// <remarks>
	/// The rows that stage terrain read the cells around the host, and a world kept from an earlier case can leave it
	/// standing where nothing breaks (measured 2026-11-22: block-sync, block-reconcile, mine-blast and save-continue all
	/// read "no breakable terrain cell", and the four passed once the pair was new). A pair whose ground cannot answer
	/// them is rebuilt instead of the row failing on a world it did not choose.
	/// </remarks>
	private async Task<bool> HasBreakableGroundAsync(CancellationToken cancellationToken)
	{
		var reading = await RecipeAtAsync(_options.HostUrl, "block-find-breakable",
			new Dictionary<string, string> { ["count"] = "1" }, cancellationToken).ConfigureAwait(false);
		if (reading is not { } answer
			|| !answer.TryGetProperty("count", out var count)
			|| count.ValueKind != JsonValueKind.Number)
		{
			return false;
		}

		return count.GetInt32() > 0;
	}

	/// <summary>
	/// Empties the member's inventory so a row that stages an item into the bag has room for it.
	/// </summary>
	/// <remarks>
	/// A run keeps what the rows before it left behind, and five rows stage an item into the member's bag (measured
	/// 2026-11-22: give-item, give-container-sound, crafting-outcome, drop-mid-transfer and two-member-continue all read
	/// "no-free-slot" late in the batch while each passed alone). The drop goes through the game's own
	/// `Body.DropItem(int)`, so it is the path the player's own key runs.
	/// </remarks>
	/// <summary>
	/// Whether the member's own body can take an item: one slot that exists, accepts a pickup and is not holding.
	/// </summary>
	/// <remarks>
	/// The medical rows amputate limbs, and an amputated slot answers `canPickUp` false, so five rows that stage an item
	/// into the member's bag read "no-free-slot" later in the batch while each passed alone (measured 2026-11-22,
	/// `.acceptance/every-9`: give-container-sound, crafting-outcome, two-member-continue, drop-mid-transfer and
	/// medical-splint). A rebuilt pair starts from the archive's intact body, so that is the answer when none qualifies.
	/// </remarks>
	private async Task<bool> HasCarrySlotAsync(CancellationToken cancellationToken)
	{
		JsonElement? answer;
		try
		{
			answer = await RecipeAtAsync(_options.MemberUrl, "item-provide",
				new Dictionary<string, string> { ["mode"] = "slots", ["type"] = string.Empty }, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception exception) when (exception is DriverException or InvalidOperationException or IOException)
		{
			// A probe that cannot answer must not stop the row: the pair as it stands is what runs, and the row's own
			// reading will say whether the body had room.
			Timeline?.Invoke("the member's slots could not be read: " + exception.Message);
			return true;
		}
		if (answer is not { } reading || !IsTrue(reading, "ok"))
		{
			return false;
		}

		if (!reading.TryGetProperty("slots", out var slots) || slots.ValueKind != JsonValueKind.Array)
		{
			return false;
		}

		foreach (var slot in slots.EnumerateArray())
		{
			if (IsTrue(slot, "canPickUp") && !IsTrue(slot, "holding"))
			{
				return true;
			}
		}

		return false;
	}

	private async Task<string> ClearCarriedAsync(CancellationToken cancellationToken)
	{
		var reading = await RecipeAtAsync(_options.MemberUrl, "clear-carried",
			new Dictionary<string, string> { ["slots"] = "6" }, cancellationToken).ConfigureAwait(false);
		if (reading is not { } answer || !IsTrue(answer, "ok"))
		{
			return "the member's carried set could not be cleared";
		}

		return "the member's carried set was cleared"
			+ (answer.TryGetProperty("dropped", out var dropped) ? " (" + dropped.GetInt32().ToString(CultureInfo.InvariantCulture) + " dropped)" : string.Empty);
	}

	public async Task<string> EnsureSingleMemberAsync(CancellationToken cancellationToken)
	{
		var state = await TryStateAsync(_options.MemberUrl, cancellationToken).ConfigureAwait(false);
		var memberId = state is { } member ? StringValue(member, "steamId") : string.Empty;
		if (memberId.Length == 0)
		{
			return "the member's own id was not readable; the roster was left as it is";
		}

		for (var attempt = 0; attempt < 12; attempt++)
		{
			var members = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
			var extras = members.Where(id => !string.Equals(id, memberId, StringComparison.Ordinal)).ToArray();
			if (extras.Length == 0)
			{
				return members.Length == 1
					? "the run names exactly this guest"
					: $"the run names no member yet ({members.Length}); the pair's own handshake owns the roster";
			}

			foreach (var extra in extras)
			{
				await RecipeAtAsync(_options.HostUrl, "kick-member", new Dictionary<string, string>
				{
					["steamId"] = extra,
					["reason"] = "the batch keeps one member",
				}, cancellationToken).ConfigureAwait(false);
				Timeline?.Invoke($"the roster named {extra}; the host kicked it before the case");
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException($"the host's roster never fell back to the one guest ({memberId})");
	}

	private async Task<JsonElement?> TryStateAsync(string url, CancellationToken cancellationToken)
	{
		try
		{
			return await ActionAsync(url, "state", cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exception) when (exception is DriverException or InvalidOperationException or IOException)
		{
			// A client that does not answer is not a live pair; the rebuild path names it.
			Timeline?.Invoke($"state read failed at {url}: {exception.Message}");
			return null;
		}
	}

	private Task<string> RecoverPairAsync(CancellationToken cancellationToken)

	{
		// The pair is never recreated to make a run work: a restart throws away the session being
		// measured and hides the reason the pair stopped answering (the user's rule, 2026-11-14).
		// The failure names the client state instead, and the cause is fixed in the product.
		throw new InvalidOperationException(
			"the pair is not answering: the suite does not restart clients (a restart is a deploy or a source change). "
			+ "Read the host's and the member's state and fix the cause.");
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

	/// <summary>One unsigned number of a state answer, at the top level or under the older result wrapper.</summary>
	private static ulong StateUInt64(JsonElement state, string name)
	{
		if (state.ValueKind != JsonValueKind.Object)
		{
			return 0;
		}

		if (state.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out var direct))
		{
			return direct;
		}

		return state.TryGetProperty("result", out var result)
			&& result.TryGetProperty(name, out value)
			&& value.ValueKind == JsonValueKind.Number
			&& value.TryGetUInt64(out var nested)
			? nested
			: 0;
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

	// The suite reads the host and the member log concurrently: they are two processes, and the
	// per-case marks/counts start both calls together. A plain Dictionary corrupts under the two
	// lazy creations that race on the first case, so the cache is concurrent by construction.
	private readonly ConcurrentDictionary<string, DriverClient> _drivers = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// One Online UI action, answered in-process over the client's own socket: the driver template that
	/// <c>drive-in-process.ps1</c> used to send is expanded here and evaluated by the game's own evaluator.
	/// No process is started per action.
	/// </summary>
	public async Task<JsonElement?> LogTailAsync(string url, string match, int count, CancellationToken cancellationToken) =>
		// The last lines of a client's own BepInEx log that contain the match: a case judges the
		// target's side of a relayed effect from the target's own file, not from the host's answer.
		await ActionAsync(url, "log-tail", cancellationToken, ("-Text", count.ToString(CultureInfo.InvariantCulture)), ("-Argument", match)).ConfigureAwait(false);

	/// <summary>
	/// The byte offset a case marks before it acts. Every warning/error written after the mark is
	/// attributed to that case; a missing log throws instead of becoming a silent zero.
	/// </summary>
	public async Task<long> LogOffsetAsync(string url, CancellationToken cancellationToken)
	{
		var answer = await ActionAsync(url, "log-offset", cancellationToken).ConfigureAwait(false);
		if (answer is { ValueKind: JsonValueKind.Object } value
			&& value.TryGetProperty("offset", out var offset)
			&& offset.TryGetInt64(out var result))
		{
			return result;
		}

		throw new InvalidOperationException("the client did not report a log offset");
	}

	/// <summary>
	/// Count this client's Warning and Error lines since the mark, and return the first line of each
	/// kind. The suite fails a case on an unexplained error even when its behavioural readings pass.
	/// </summary>
	public async Task<LogCounts> LogSinceAsync(string url, long offset, CancellationToken cancellationToken)
	{
		var answer = await ActionAsync(url, "log-since", cancellationToken, ("-Text", offset.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
		if (answer is not { ValueKind: JsonValueKind.Object } value)
		{
			throw new InvalidOperationException("the client did not answer the log count");
		}

		return new LogCounts(
			ReadInt(value, "warnings"),
			ReadInt(value, "errors"),
			ReadString(value, "firstWarning"),
			ReadString(value, "firstError"));
	}

	private static int ReadInt(JsonElement value, string name) =>
		value.TryGetProperty(name, out var property) && property.TryGetInt32(out var number) ? number : 0;

	private static string ReadString(JsonElement value, string name) =>
		value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : string.Empty;

	/// <summary>
	/// Wait on the target's log as a file event, not by sleeping on the game's main thread and not by
	/// polling: the acceptance driver never blocks Unity waiting for a line. The OS watcher wakes the
	/// suite when the target flushes; only the timeout is a timer. Returns the last matching line
	/// written after the watch began, or null when the budget ran out.
	/// </summary>
	public async Task<string?> WaitForLogAsync(string url, string match, int budgetMs, CancellationToken cancellationToken)
	{
		var answer = await ActionAsync(url, "log-path", cancellationToken).ConfigureAwait(false);
		if (answer is not { ValueKind: JsonValueKind.Object } value)
		{
			return null;
		}

		var path = ReadString(value, "path");
		if (string.IsNullOrEmpty(path) || !File.Exists(path))
		{
			return null;
		}

		var directory = Path.GetDirectoryName(path);
		var fileName = Path.GetFileName(path);
		if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
		{
			return null;
		}

		var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
		var start = new FileInfo(path).Length;
		var gate = new object();
		string? found = null;

		void Inspect()
		{
			lock (gate)
			{
				if (found is not null || completion.Task.IsCompleted)
				{
					return;
				}

				using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
				if (start > stream.Length)
				{
					return;
				}

				stream.Seek(start, SeekOrigin.Begin);
				using var reader = new StreamReader(stream);
				var content = reader.ReadToEnd();
				if (content.Length == 0)
				{
					return;
				}

				// The writer flushes a line in pieces; the unterminated tail is a prefix, and a match on
				// it returns half a line. Only newline-terminated lines count.
				var complete = content.EndsWith('\n') ? content.Length : content.LastIndexOf('\n') + 1;
				var lines = content.Substring(0, complete).Split('\n');
				foreach (var line in lines)
				{
					if (line.IndexOf(match, StringComparison.Ordinal) >= 0)
					{
						found = line.TrimEnd('\r');
					}
				}

				if (found is not null)
				{
					completion.TrySetResult(found);
				}
			}
		}

		using var watcher = new FileSystemWatcher(directory, fileName)
		{
			NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
			EnableRaisingEvents = true,
		};
		FileSystemEventHandler onChange = (_, _) =>
		{
			try
			{
				Inspect();
			}
			catch (IOException)
			{
				// The log writer may still hold the file; the next notification reads it.
			}
			catch (UnauthorizedAccessException)
			{
				completion.TrySetResult(null);
			}
		};
		watcher.Changed += onChange;
		watcher.Created += onChange;
		Inspect();

		var timeout = Task.Delay(budgetMs, cancellationToken);
		await Task.WhenAny(completion.Task, timeout).ConfigureAwait(false);
		watcher.EnableRaisingEvents = false;
		return completion.Task.IsCompletedSuccessfully ? await completion.Task.ConfigureAwait(false) : null;
	}

	/// <summary>
	/// The client processes the suite is driving, held open for their exit codes. A refused socket is
	/// the symptom; the row fails with the code that caused it (see <see cref="ClientExitLedger"/>).
	/// </summary>
	public ClientExitLedger? ClientExits => _options.ClientExits;

	/// <summary>
	/// Takes the Online UI window down on one client, so a row that drives the game's own pointer has the
	/// game under it, not our overlay. The window keeps the press; a staged bandage gesture that lands on the
	/// window never reaches the minigame (measured 2026-11-22: the roll did not turn for eighteen attempts
	/// while the window was up, and the row's own answer was that the pointer hit the panel).
	/// </summary>
	public Task<JsonElement> CloseWindowAsync(string url, CancellationToken cancellationToken) =>
		ActionAsync(url, "close-window", cancellationToken);

	public async Task<JsonElement> ActionAsync(string url, string action, CancellationToken cancellationToken, params (string Name, string Value)[] extraArguments)
	{
		try
		{
			return await ActionCoreAsync(url, action, cancellationToken, extraArguments).ConfigureAwait(false);
		}
		catch (Exception exception)
		{
			var ended = ClientExits?.Describe(url);
			if (ended is null)
			{
				throw;
			}

			throw new ClientEndedException($"{exception.Message} — {ended}", exception);
		}
	}

	private async Task<JsonElement> ActionCoreAsync(string url, string action, CancellationToken cancellationToken, (string Name, string Value)[] extraArguments)
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
			driver = new DriverClient(url, _options.DriverEvalTimeoutMs);
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

	/// <summary>
	/// Runs a recipe on any client endpoint (the second member has no CaseContext helper of its own).
	/// The driver is cached per URL by <see cref="ActionAsync"/>, so state reads and recipes share one socket.
	/// </summary>
	public async Task<JsonElement?> RecipeAtAsync(string url, string name, IReadOnlyDictionary<string, string>? args, CancellationToken cancellationToken)
	{
		if (!_drivers.TryGetValue(url, out var driver))
		{
			driver = new DriverClient(url, _options.DriverEvalTimeoutMs);
			await driver.ConnectAsync(cancellationToken).ConfigureAwait(false);
			_drivers[url] = driver;
		}

		return await driver.RecipeAsync(name, args, cancellationToken).ConfigureAwait(false);
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
		_drivers.TryRemove(url, out _);
		try
		{
			await driver.DisposeAsync().ConfigureAwait(false);
		}
		catch (Exception)
		{
		}

		var fresh = new DriverClient(url, _options.DriverEvalTimeoutMs);
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

/// <summary>The Warning/Error lines one client wrote during one case, and the first line of each kind.</summary>
internal sealed record LogCounts(int Warnings, int Errors, string FirstWarning, string FirstError);


