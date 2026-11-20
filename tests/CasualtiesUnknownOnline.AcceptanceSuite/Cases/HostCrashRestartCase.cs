using System.Diagnostics;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The host-crash row: the host saves, the case kills the host process by the pid the launcher wrote,
/// the member's session ends on its own, the launcher starts the host again, and the host continues the
/// archived world and opens a session over it. The member rejoins; the stored character must be the
/// one the save cut. This is the only row allowed to kill a client: the process is the suite's own host,
/// named by pids.json, and the restart is the recovery the row reads — not a fixture convenience.
///
/// The red run skips the restart: the continue then has no process to answer it and the case fails.
/// </summary>
internal sealed class HostCrashRestartCase : AcceptanceCase
{
	public override string Name => "host-crash-restart";

	public override CaseCost Cost => CaseCost.ClientRestart;

	public override bool RunsInBatch => false;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var pidsPath = context.PidsPath;
		Require(!string.IsNullOrWhiteSpace(pidsPath) && File.Exists(pidsPath), "the case needs the launcher's --pids file to name the host process");
		var pid = HostPid(pidsPath!);
		Require(pid > 0, "the launcher's pid file names no host process: " + pidsPath);

		await context.Session.EnsureInWorldAsync(cancellationToken).ConfigureAwait(false);
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);

		var memberItem = await FindItemAsync(context, cancellationToken).ConfigureAwait(false);
		var memberSlot = Text(memberItem, "slot");
		Require(memberSlot.Length > 0, "the member has no starting item to colour: " + Raw(memberItem));
		var coloured = await context.MemberRecipeAsync("item-customize-native", cancellationToken, ("slot", memberSlot), ("label", "MemberColor"), ("swatch", "5")).ConfigureAwait(false);
		Require(Boolean(coloured, "ok"), "the member's colour did not apply: " + Raw(coloured));

		await SaveContinueCycle.SaveAsync(context, cancellationToken).ConfigureAwait(false);
		var stored = await SaveContinueCycle.ReadStoredAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("stored before the crash", stored);
		Require(HasItem(stored, "MemberColor", 5), "the save before the crash did not store the member's colour: " + Raw(stored));

		await KillHostAsync(pid, context, cancellationToken).ConfigureAwait(false);

		// The member's own watchdog is the product's answer to a silent host (the session's 15 s
		// budget). The row records how long the member kept its role; it must end, not hang.
		var ended = await WaitForMemberSessionEndAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("member after the host died", ended);
		Require(Text(ended, "role") == "None" || !IsTrue(ended, "active"), "the member's session did not end after the host died: " + Raw(ended));

		var red = string.Equals(Environment.GetEnvironmentVariable("CUO_ACCEPTANCE_RED"), Name, StringComparison.Ordinal);
		if (!red)
		{
			await context.Session.RestartHostAsync(cancellationToken).ConfigureAwait(false);
			context.Note("host restarted");
			var menu = await WaitForStateAsync(context, context.Session.HostUrl, state => !IsTrue(state, "inWorld"), TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
			Require(!IsTrue(menu, "inWorld"), "the restarted host did not settle outside a world: " + Raw(menu));
		}

		// With the product healthy the host continues the archived world; without the restart (the
		// red run) there is no process to answer, and this call is where the row fails.
		var restored = await SaveContinueCycle.ContinueAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("restored line", JsonDocument.Parse("\"" + Escape(restored) + "\"").RootElement.Clone());

		// The continue restores the world with no session (the host process is new). Open the
		// session over the running world through the driver's own transport sequence — the Home
		// page offers no ip.create_host until the IP transport is selected — and keep the world:
		// the restore is the reading, so unlike the setup path it is never left.
		context.Note(await context.Session.OpenHostSessionOverWorldAsync(cancellationToken).ConfigureAwait(false));
		var hosting = await WaitForStateAsync(
			context,
			context.Session.HostUrl,
			state => Text(state, "role") == "Host" && IsTrue(state, "inWorld"),
			TimeSpan.FromSeconds(20),
			cancellationToken).ConfigureAwait(false);
		context.Read("host session over the restored world", hosting);
		Require(Text(hosting, "role") == "Host" && IsTrue(hosting, "inWorld"), "the restarted host did not open a session over its restored world: " + Raw(hosting));

		await context.Session.ClearMemberSessionAsync(cancellationToken).ConfigureAwait(false);
		var rejoined = await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		context.Note("member after the host restart: " + rejoined);
		var member = await WaitForStateAsync(
			context,
			context.Session.MemberUrl,
			state => Text(state, "role") == "Guest" && IsTrue(state, "inWorld"),
			TimeSpan.FromSeconds(30),
			cancellationToken).ConfigureAwait(false);
		context.Read("member joined the restarted host", member);
		Require(Text(member, "role") == "Guest" && IsTrue(member, "inWorld"), "the member did not rejoin the restarted host: " + Raw(member));

		var storedAfter = await SaveContinueCycle.ReadStoredAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("stored after the restart", storedAfter);
		Require(HasItem(storedAfter, "MemberColor", 5), "the host restart lost the stored character's colour: " + Raw(storedAfter));
	}

	private static async Task KillHostAsync(int pid, CaseContext context, CancellationToken cancellationToken)
	{
		using var process = Process.GetProcessById(pid);
		var name = process.ProcessName;
		if (!string.Equals(name, "CasualtiesUnknown", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("the pid file names " + name + " (pid " + pid + "), not the game's host process");
		}

		// The stop is the case's own action: the ledger records it as expected, so a later read of this
		// client names the kill as the case's step instead of blaming it for the row's outcome.
		context.Session.ClientExits?.Expect(pid);
		process.Kill(entireProcessTree: true);
		context.Note("killed the host process " + pid);
		if (!process.WaitForExit(10_000))
		{
			throw new InvalidOperationException("the host process " + pid + " did not exit within 10 s");
		}

		await Task.Yield();
	}

	private static async Task<JsonElement> WaitForMemberSessionEndAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(25);
		var state = default(JsonElement);
		while (DateTime.UtcNow < deadline)
		{
			state = await context.Session.ActionAsync(context.Session.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
			if (Text(state, "role") == "None" || !IsTrue(state, "active"))
			{
				return state;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		return state;
	}

	private static async Task<JsonElement> WaitForStateAsync(
		CaseContext context,
		string url,
		Func<JsonElement, bool> until,
		TimeSpan budget,
		CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + budget;
		var state = default(JsonElement);
		while (DateTime.UtcNow < deadline)
		{
			state = await context.Session.ActionAsync(url, "state", cancellationToken).ConfigureAwait(false);
			if (until(state))
			{
				return state;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		return state;
	}

	private static int HostPid(string path)
	{
		using var document = JsonDocument.Parse(File.ReadAllText(path));
		return document.RootElement.TryGetProperty("host", out var host)
			&& host.TryGetProperty("pid", out var pid)
			&& pid.ValueKind == JsonValueKind.Number
			? pid.GetInt32()
			: 0;
	}

	private static async Task<JsonElement?> FindItemAsync(CaseContext context, CancellationToken cancellationToken)
	{
		foreach (var type in new[] { "bandage", "lantern", "trashbag", "dogfood", "waterbottle" })
		{
			var reading = await context.MemberRecipeAsync("item-provide", cancellationToken, ("type", type), ("mode", "find")).ConfigureAwait(false);
			if (Boolean(reading, "found") && Text(reading, "slot").Length > 0)
			{
				return reading;
			}
		}

		return await context.MemberRecipeAsync("item-provide", cancellationToken, ("type", "bandage"), ("mode", "create")).ConfigureAwait(false);
	}

	private static bool HasItem(JsonElement? reading, string label, int swatch)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
		{
			return false;
		}

		foreach (var item in items.EnumerateArray())
		{
			if (item.ValueKind != JsonValueKind.Object)
			{
				continue;
			}

			var itemLabel = item.TryGetProperty("label", out var labelValue) && labelValue.ValueKind == JsonValueKind.String ? labelValue.GetString() : null;
			var itemSwatch = item.TryGetProperty("swatch", out var swatchValue) && swatchValue.ValueKind == JsonValueKind.Number ? swatchValue.GetInt32() : -1;
			if (string.Equals(itemLabel, label, StringComparison.Ordinal) && itemSwatch == swatch)
			{
				return true;
			}
		}

		return false;
	}

	private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

	private static bool IsTrue(JsonElement state, string property) =>
		state.ValueKind == JsonValueKind.Object
		&& state.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static bool IsTrue(JsonElement? state, string property) => state is { } value && IsTrue(value, property);

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()
			: string.Empty;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static string Raw(JsonElement? reading) => reading?.ToString() ?? "(null)";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
