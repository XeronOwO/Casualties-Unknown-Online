using System.Diagnostics;
using System.Text.Json;

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
			return "the host was already in a world";
		}

		// A run needs the host's IP session first: at the menu, switch the transport and create
		// the host, then start the run. The home page answers only while its window is up, and a
		// client loading a scene can refuse the first click, so it retries with a fresh open.
		await ActionAsync(_options.HostUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);
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
		await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

		await ActionAsync(_options.HostUrl, "start-run", cancellationToken).ConfigureAwait(false);
		var deadline = Stopwatch.StartNew();
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
	public async Task<string> EnsureMemberAsync(CancellationToken cancellationToken)
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
			// so it leaves and joins the host's current one.
			await ActionAsync(_options.MemberUrl, "leave-world", cancellationToken).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken).ConfigureAwait(false);
		}

		// The window can be closed (a case pressed Escape) or the client can still be loading
		// after a restart; the home page's controls answer only while the page is up, so the
		// first click retries with a fresh open instead of failing the case.
		await ActionAsync(_options.MemberUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);
		// A member that still holds a session shows its leave control instead of the join fields;
		// leaving first is best-effort, because a member at the menu has nothing to leave.
		await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "home.leave")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken).ConfigureAwait(false);
		try
		{
			// A member that still holds an IP session offers no transport switch; its join uses
			// the transport it already has.
			await ActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "home.transport.ip")).ConfigureAwait(false);
		}
		catch (Exception)
		{
		}
		await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
		await TryActionAsync(_options.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.address"), ("-Text", "127.0.0.1")).ConfigureAwait(false);
		await TryActionAsync(_options.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.port"), ("-Text", "7777")).ConfigureAwait(false);
		await TryActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "ip.join")).ConfigureAwait(false);

		var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < JoinBudget)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);
			state = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
			if (IsTrue(state, "inWorld"))
			{
				return "the suite joined the member to the host's session";
			}
		}

		throw new InvalidOperationException("the suite could not join the member to the host's session");
	}

	/// <summary>Reads the host's in-world members' session ids — the ids the cases address a member by.</summary>
	public async Task<string[]> ReadMembersAsync(CancellationToken cancellationToken)
	{
		var state = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		if (state.ValueKind != JsonValueKind.Object
			|| !state.TryGetProperty("result", out var result)
			|| !result.TryGetProperty("members", out var members)
			|| members.ValueKind != JsonValueKind.Array)
		{
			return [];
		}

		return [.. members.EnumerateArray().Select(member => member.ValueKind == JsonValueKind.String ? member.GetString() ?? string.Empty : member.ToString())];
	}

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

	public async Task<JsonElement> ActionAsync(string url, string action, CancellationToken cancellationToken, params (string Name, string Value)[] extraArguments)
	{
		var script = Path.Combine(RepositoryPaths.Root, "tools", "acceptance", "drive-in-process.ps1");
		var start = new ProcessStartInfo("pwsh")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		start.ArgumentList.Add("-NoProfile");
		start.ArgumentList.Add("-File");
		start.ArgumentList.Add(script);
		start.ArgumentList.Add("-Url");
		start.ArgumentList.Add(url);
		start.ArgumentList.Add("-Action");
		start.ArgumentList.Add(action);
		foreach (var (name, value) in extraArguments)
		{
			start.ArgumentList.Add(name);
			start.ArgumentList.Add(value);
		}

		using var process = Process.Start(start) ?? throw new InvalidOperationException($"could not start the acceptance driver for '{action}'");
		var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
		var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
		await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"the acceptance driver answered '{action}' with exit {process.ExitCode}: {error.Trim()}");
		}

		var startIndex = output.IndexOf('{', StringComparison.Ordinal);
		if (startIndex < 0)
		{
			throw new InvalidOperationException($"the acceptance driver answered '{action}' without a JSON body: {output.Trim()}");
		}

		using var document = JsonDocument.Parse(output[startIndex..]);
		return document.RootElement.Clone();
	}

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

