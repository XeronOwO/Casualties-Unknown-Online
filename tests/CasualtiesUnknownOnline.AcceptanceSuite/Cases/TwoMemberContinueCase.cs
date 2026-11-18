using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The two-member save/continue row: a host and two members play one run, the host cuts a save, leaves
/// the world and continues it, and both members' characters and their carried items come back.
///
/// The chain read before the fixture is the one <c>save-continue</c> already reads for one member
/// (<c>docs/design/cross-cutting/saves-and-recovery.md</c>): the cut states what the live world has, the
/// continue rebuilds it, and each present member's character is bound to its peer and handed over before
/// the entry instruction. This row's difference is the second peer: the host's roster must carry both, the
/// cut's character section must carry both, and both clients must read their own bag after the continue.
///
/// It needs the third client, so it is by name and only runs when the launch asked for it
/// (<c>run-suite -All</c>). The marker item is a bandage each member puts in its own bag through the
/// game's grant path before the save; a character whose items were lost would read the starting kit only.
/// </summary>
internal sealed class TwoMemberContinueCase : AcceptanceCase
{
	public override string Name => "two-member-continue";

	public override CaseCost Cost => CaseCost.NewWorld;

	public override bool RunsInBatch => false;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		var second = await context.Session.EnsureSecondMemberAsync(cancellationToken).ConfigureAwait(false);
		context.Note(second);

		var memberId = await ReadSteamIdAsync(context, context.Session.MemberUrl, cancellationToken).ConfigureAwait(false);
		var secondId = await ReadSteamIdAsync(context, context.Session.AltUrl, cancellationToken).ConfigureAwait(false);
		context.UseMember(memberId);
		Require(memberId.Length > 0 && secondId.Length > 0 && !string.Equals(memberId, secondId, StringComparison.Ordinal),
			"the two members do not name two different peers: " + memberId + " / " + secondId);

		var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		context.Read("roster", JsonSerializer.SerializeToElement(new { members }));
		Require(members.Contains(memberId, StringComparer.Ordinal) && members.Contains(secondId, StringComparer.Ordinal),
			"the host's roster does not carry both members: " + string.Join(", ", members));

		await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);
		await ProvideMarkerAsync(context, context.Session.MemberUrl, cancellationToken).ConfigureAwait(false);
		await ProvideMarkerAsync(context, context.Session.AltUrl, cancellationToken).ConfigureAwait(false);

		var beforeMember = await ItemsAsync(context, context.Session.MemberUrl, cancellationToken).ConfigureAwait(false);
		var beforeSecond = await ItemsAsync(context, context.Session.AltUrl, cancellationToken).ConfigureAwait(false);
		context.Read("items-before-member", beforeMember);
		context.Read("items-before-second", beforeSecond);
		Require(Text(beforeMember, "ids").Contains("bandage", StringComparison.Ordinal),
			"the first member's bag does not carry the marker before the save: " + Raw(beforeMember));
		Require(Text(beforeSecond, "ids").Contains("bandage", StringComparison.Ordinal),
			"the second member's bag does not carry the marker before the save: " + Raw(beforeSecond));

		var logStart = LogLength(context.HostLogPath);
		var save = await context.Session.ActionAsync(context.Session.HostUrl, "console", cancellationToken, ("-Text", "/save")).ConfigureAwait(false);
		context.Read("save", save);
		var committed = await WaitForLogAsync(context, "Cut Command committed", TimeSpan.FromSeconds(25), cancellationToken).ConfigureAwait(false);
		Require(committed.Contains("Cut Command committed", StringComparison.Ordinal), "the save cut did not commit");

		await context.Session.ActionAsync(context.Session.HostUrl, "leave-world", cancellationToken).ConfigureAwait(false);
		var left = false;
		for (var wait = 0; wait < 20 && !left; wait++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			var state = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			left = !IsTrue(state, "inWorld");
		}

		Require(left, "the host did not leave the world before continuing");

		var continued = await context.Session.ActionAsync(context.Session.HostUrl, "continue-run", cancellationToken).ConfigureAwait(false);
		context.Read("continue", continued);
		var restored = await WaitForLogAsync(context, "Continue restored world", TimeSpan.FromSeconds(40), cancellationToken).ConfigureAwait(false);
		Require(restored.Contains("Continue restored world", StringComparison.Ordinal), "the continue never restored the world");

		var inWorld = false;
		for (var wait = 0; wait < 30 && !inWorld; wait++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			var state = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			inWorld = IsTrue(state, "inWorld");
		}

		Require(inWorld, "the host did not enter the restored world");

		// Both members follow the continue on their own; a rejoin is only the fallback when a client
		// stayed behind (the row then names which one and why in the failure).
		await WaitForFollowerAsync(context, context.Session.MemberUrl, "first", cancellationToken).ConfigureAwait(false);
		await WaitForFollowerAsync(context, context.Session.AltUrl, "second", cancellationToken).ConfigureAwait(false);

		var afterMember = await WaitForItemsAsync(context, context.Session.MemberUrl, cancellationToken).ConfigureAwait(false);
		var afterSecond = await WaitForItemsAsync(context, context.Session.AltUrl, cancellationToken).ConfigureAwait(false);
		context.Read("items-after-member", afterMember);
		context.Read("items-after-second", afterSecond);
		Require(Text(afterMember, "ids").Contains("bandage", StringComparison.Ordinal),
			"the first member's character lost its items across the continue: " + Raw(afterMember));
		Require(Text(afterSecond, "ids").Contains("bandage", StringComparison.Ordinal),
			"the second member's character lost its items across the continue: " + Raw(afterSecond));

		// The host's own view of both players must render the marker too: the continue's hand-over writes
		// each member's character, and the applied-item ledger is what the host shows.
		var hostView = await WaitForHostMarkersAsync(context, memberId, secondId, cancellationToken).ConfigureAwait(false);
		context.Read("host-view", hostView);
		Require(hostView is not null, "the host's view does not show the crafted-run markers for both members");
	}

	private static async Task ProvideMarkerAsync(CaseContext context, string url, CancellationToken cancellationToken)
	{
		var items = await ItemsAsync(context, url, cancellationToken).ConfigureAwait(false);
		if (Text(items, "ids").Contains("bandage", StringComparison.Ordinal))
		{
			return;
		}

		var create = await context.Session.RecipeAtAsync(url, "item-provide", new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["type"] = "bandage",
			["mode"] = "create",
		}, cancellationToken).ConfigureAwait(false);
		if (Boolean(create, "ok"))
		{
			return;
		}

		// A body has six slots and the starting-supply grant fills most of them; the fixture takes one
		// unwanted item out through the game's own drop path (the tourniquet row's rule), then grants.
		Require(Text(create, "error").Contains("no-free-slot", StringComparison.Ordinal),
			"the marker could not be granted on " + url + ": " + Raw(create));
		foreach (var material in new[] { "trashbag", "dogfood", "waterbottle" })
		{
			if (!Text(items, "ids").Contains(material, StringComparison.Ordinal))
			{
				continue;
			}

			await context.Session.RecipeAtAsync(url, "item-provide", new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["type"] = material,
				["mode"] = "drop",
			}, cancellationToken).ConfigureAwait(false);
			break;
		}

		var retried = await context.Session.RecipeAtAsync(url, "item-provide", new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["type"] = "bandage",
			["mode"] = "create",
		}, cancellationToken).ConfigureAwait(false);
		Require(Boolean(retried, "ok"), "the marker could not be granted on " + url + " even after freeing a slot: " + Raw(retried));
	}

	private static async Task<JsonElement?> ItemsAsync(CaseContext context, string url, CancellationToken cancellationToken) =>
		await context.Session.RecipeAtAsync(url, "body-items", args: null, cancellationToken).ConfigureAwait(false);

	private static async Task<JsonElement?> WaitForItemsAsync(CaseContext context, string url, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			last = await ItemsAsync(context, url, cancellationToken).ConfigureAwait(false);
			if (Text(last, "ids").Contains("bandage", StringComparison.Ordinal))
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	private static async Task WaitForFollowerAsync(CaseContext context, string url, string which, CancellationToken cancellationToken)
	{
		var deadline = Stopwatch.StartNew();
		JsonElement? last = null;
		while (deadline.Elapsed < TimeSpan.FromSeconds(60))
		{
			last = await context.Session.ActionAsync(url, "state", cancellationToken).ConfigureAwait(false);
			var host = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			if (IsTrue(last, "inWorld") && StateUInt64(last, "runId") == StateUInt64(host, "runId") && StateUInt64(host, "runId") != 0)
			{
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException("the " + which + " member did not follow the continue: " + Raw(last));
	}

	private static async Task<JsonElement?> WaitForHostMarkersAsync(CaseContext context, string memberId, string secondId, CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < 60; attempt++)
		{
			var clone = await context.RecipeAsync("container-read", cancellationToken, ("mode", "clone"), ("guest", memberId)).ConfigureAwait(false);
			if (ShowsMarker(clone, memberId) && ShowsMarker(clone, secondId))
			{
				return clone;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return null;
	}

	private static bool ShowsMarker(JsonElement? reading, string owner)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } clone || !clone.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
		{
			return false;
		}

		foreach (var entry in items.EnumerateArray())
		{
			if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("owner", out var ownerValue) || ownerValue.ValueKind != JsonValueKind.String || !string.Equals(ownerValue.GetString(), owner, StringComparison.Ordinal))
			{
				continue;
			}

			if (entry.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && string.Equals(type.GetString(), "bandage", StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private static async Task<string> ReadSteamIdAsync(CaseContext context, string url, CancellationToken cancellationToken)
	{
		var state = await context.Session.ActionAsync(url, "state", cancellationToken).ConfigureAwait(false);
		return Text(state, "steamId");
	}

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue)
			? propertyValue.ValueKind == JsonValueKind.String ? propertyValue.GetString() ?? string.Empty : propertyValue.ToString()
			: string.Empty;

	private static ulong StateUInt64(JsonElement? reading, string property)
	{
		var text = Text(reading, property);
		return ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0UL;
	}

	private static string Raw(JsonElement? reading) => reading?.ToString() ?? "(null)";

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.True;

	private static bool IsTrue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.True;

	private static long LogLength(string path)
	{
		try
		{
			return File.Exists(path) ? new FileInfo(path).Length : 0;
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
		catch (IOException)
		{
			return string.Empty;
		}
	}

	private static async Task<string> WaitForLogAsync(CaseContext context, string needle, TimeSpan budget, CancellationToken cancellationToken)
	{
		var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < budget)
		{
			var text = ReadLog(context.HostLogPath);
			var index = text.LastIndexOf(needle, StringComparison.Ordinal);
			if (index >= 0)
			{
				var end = Math.Min(text.Length, index + 260);
				return text[index..end];
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return string.Empty;
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
