using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The second-entry row: one save cut, then two continues. The restore is a repair, not a one-shot
/// hand-over: the second entry must find the same characters, items and world the first entry did.
/// The member's colour marks the stored character; the geyser type set marks the world facts.
///
/// The red run leaves the world, then removes the live archive and its backups before the second
/// continue: with no archive the second entry cannot restore, and the case fails. The move is undone
/// in a finally; only the batch's own scratch archive is touched.
/// </summary>
internal sealed class SaveContinueReentryCase : AcceptanceCase
{
	public override string Name => "save-continue-reentry";

	public override CaseCost Cost => CaseCost.NewWorld;

	public override bool RunsInBatch => false;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var saveRoot = context.SaveRoot;
		Require(!string.IsNullOrWhiteSpace(saveRoot), "the case needs --save-root: a real save library must never be the fixture");

		await context.Session.EnsureInWorldAsync(cancellationToken).ConfigureAwait(false);
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);

		var memberItem = await FindItemAsync(context, cancellationToken).ConfigureAwait(false);
		var memberSlot = Text(memberItem, "slot");
		Require(memberSlot.Length > 0, "the member has no starting item to colour: " + Raw(memberItem));
		var coloured = await context.MemberRecipeAsync("item-customize-native", cancellationToken, ("slot", memberSlot), ("label", "MemberColor"), ("swatch", "5")).ConfigureAwait(false);
		Require(Boolean(coloured, "ok"), "the member's colour did not apply: " + Raw(coloured));

		await SaveContinueCycle.SaveAsync(context, cancellationToken).ConfigureAwait(false);
		var first = await SaveContinueCycle.ContinueAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("first restored line", TextElement(first));
		var firstStored = await SaveContinueCycle.ReadStoredAsync(context, cancellationToken).ConfigureAwait(false);
		var firstGeysers = await ReadGeyserTableAsync(context, cancellationToken).ConfigureAwait(false);
		var firstBag = await RejoinAndReadMarkerAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("first entry stored", firstStored);
		context.Read("first entry geysers", firstGeysers);
		context.Read("first entry member bag", firstBag);
		Require(HasItem(firstStored, "MemberColor", 5), "the first entry lost the stored character: " + Raw(firstStored));
		Require(GeyserSignature(firstGeysers).Length > 0, "the first entry's geyser table is empty: " + Raw(firstGeysers));

		var live = NewestLive(saveRoot!);
		Require(live is not null, "no live archive was written under the batch's save root: " + saveRoot);
		var worldFolder = Path.GetDirectoryName(live!)!;
		var backupFolder = Path.Combine(worldFolder, "backups");
		var backupAside = Path.Combine(worldFolder, "backups.reentry-red");
		var aside = live + ".reentry-red";
		var red = string.Equals(Environment.GetEnvironmentVariable("CUO_ACCEPTANCE_RED"), Name, StringComparison.Ordinal);
		try
		{
			string second;
			if (red)
			{
				// The leave rewrites live.cuoz, so the archive is removed after that cut. The
				// backups go too: the reader would otherwise promote one and the red would pass.
				await SaveContinueCycle.LeaveWorldAsync(context, cancellationToken).ConfigureAwait(false);
				File.Move(live!, aside, overwrite: true);
				if (Directory.Exists(backupFolder))
				{
					Directory.Move(backupFolder, backupAside);
				}

				context.Note("red run: removed the live archive and its backups after the leave cut");
				second = await SaveContinueCycle.ContinueOnlyAsync(context, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				second = await SaveContinueCycle.ContinueAsync(context, cancellationToken).ConfigureAwait(false);
			}

			context.Read("second restored line", TextElement(second));
			var secondStored = await SaveContinueCycle.ReadStoredAsync(context, cancellationToken).ConfigureAwait(false);
			var secondGeysers = await ReadGeyserTableAsync(context, cancellationToken).ConfigureAwait(false);
			var secondBag = await RejoinAndReadMarkerAsync(context, cancellationToken).ConfigureAwait(false);
			context.Read("second entry stored", secondStored);
			context.Read("second entry geysers", secondGeysers);
			context.Read("second entry member bag", secondBag);

			Require(HasItem(secondStored, "MemberColor", 5), "the second entry lost the stored character: " + Raw(secondStored));
			Require(GeyserSignature(secondGeysers).Length > 0, "the second entry's geyser table is empty: " + Raw(secondGeysers));
			Require(SameStoredItems(firstStored, secondStored), "the second entry's stored character differs from the first: " + Raw(firstStored) + " then " + Raw(secondStored));
			Require(MarkerCopies(firstBag) == MarkerCopies(secondBag), "the second entry changed the member's stored marker copies: " + MarkerCopies(firstBag) + " then " + MarkerCopies(secondBag));
			Require(GeyserSignature(firstGeysers) == GeyserSignature(secondGeysers), "the second entry's geyser type set differs from the first: " + Raw(firstGeysers) + " then " + Raw(secondGeysers));
		}
		finally
		{
			if (File.Exists(aside) && !File.Exists(live!))
			{
				File.Move(aside, live!);
			}

			if (Directory.Exists(backupAside))
			{
				if (!Directory.Exists(backupFolder))
				{
					Directory.Move(backupAside, backupFolder);
				}
				else
				{
					foreach (var file in Directory.EnumerateFiles(backupAside))
					{
						File.Move(file, Path.Combine(backupFolder, Path.GetFileName(file)), overwrite: true);
					}

					Directory.Delete(backupAside);
				}
			}
		}
	}

	/// <summary>
	/// The host's geyser table, read only once the host is in the world and the table has rows: the
	/// window after a Continue opens before the regenerated geysers exist, and an early read returns
	/// <c>total 0</c> — which compares equal to the next entry's empty read and makes the row's
	/// world-fact assertion vacuous (measured 2026-11-20: both entries read `total 0`).
	/// </summary>
	private static async Task<JsonElement> ReadGeyserTableAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
		JsonElement? lastTable = null;
		var lastState = default(JsonElement);
		while (DateTime.UtcNow < deadline)
		{
			lastState = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			lastTable = await context.RecipeAsync("geyser-types", cancellationToken).ConfigureAwait(false);
			if (Integer(lastTable, "total") > 0)
			{
				return lastTable!.Value;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		throw new AssertionException(
			"the host's geyser table never reached the live world; last state " + Raw(lastState) + ", last table " + Raw(lastTable));
	}

	private static async Task<JsonElement> WaitForMarkerAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The character file is restored before the member's body is re-bound and the item is
		// materialized; a single body-items read right after the rejoin can see an empty
		// bag. Wait for the stored marker itself, then compare the two entries on equal footing.
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
		JsonElement? reading = null;
		while (DateTime.UtcNow < deadline)
		{
			var item = await context.MemberRecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
			reading = item;
			if (item is { } present && MarkerCopies(present) >= 1)
			{
				return present;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		throw new AssertionException("the member's stored marker never reached the rejoin: " + Raw(reading));
	}

	/// <summary>
	/// The host's leave ends the member's session, so the member does not "follow" a restore entered
	/// from the menu; it has to join the restored run as the player does. The stored marker is read
	/// only after that join: the archive character is what the entry has to hand over.
	/// </summary>
	private static async Task<JsonElement> RejoinAndReadMarkerAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.ClearMemberSessionAsync(cancellationToken).ConfigureAwait(false);
		var rejoined = await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		context.Note("member rejoined the restored run: " + rejoined);
		var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		if (members.Length > 0)
		{
			context.UseMember(members[0]);
		}

		return await WaitForMarkerAsync(context, cancellationToken).ConfigureAwait(false);
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

	private static string? NewestLive(string saveRoot)
	{
		var lives = Directory.EnumerateFiles(saveRoot, "live.cuoz", SearchOption.AllDirectories).ToArray();
		return lives.Length == 0
			? null
			: lives.OrderByDescending(path => new FileInfo(path).LastWriteTimeUtc).First();
	}

	/// <summary>The stored character's item list as a stable string: type/label/swatch, depth-first order.</summary>
	private static string StoredItems(JsonElement? reading)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
		{
			return string.Empty;
		}

		return string.Join("|", items.EnumerateArray().Select(item =>
		{
			var id = item.TryGetProperty("id", out var idValue) && idValue.ValueKind == JsonValueKind.String ? idValue.GetString() : string.Empty;
			var label = item.TryGetProperty("label", out var labelValue) && labelValue.ValueKind == JsonValueKind.String ? labelValue.GetString() : string.Empty;
			var swatch = item.TryGetProperty("swatch", out var swatchValue) && swatchValue.ValueKind == JsonValueKind.Number ? swatchValue.GetInt32() : -1;
			return id + "/" + label + "/" + swatch;
		}));
	}

	private static bool SameStoredItems(JsonElement? left, JsonElement? right) => string.Equals(StoredItems(left), StoredItems(right), StringComparison.Ordinal);

	/// <summary>How many items in this body carry the stored marker: label MemberColor, swatch 5.</summary>
	private static int MarkerCopies(JsonElement? reading) =>
		Text(reading, "details").Split('|', StringSplitOptions.RemoveEmptyEntries)
			.Count(entry =>
			{
				var parts = entry.Split(':', 4);
				return parts.Length == 4
					&& string.Equals(parts[2], "MemberColor", StringComparison.Ordinal)
					&& int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var swatch)
					&& swatch == 5;
			});

	/// <summary>The geyser rows' types as a sorted multiset: positions are allowed to shift a hair.</summary>
	private static string GeyserSignature(JsonElement? reading)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
		{
			return string.Empty;
		}

		return string.Join(",", rows.EnumerateArray()
			.Select(row => row.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.Number ? type.GetInt32() : -1)
			.OrderBy(type => type));
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

	private static JsonElement TextElement(string text) => JsonDocument.Parse("\"" + Escape(text) + "\"").RootElement.Clone();

	private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()
			: string.Empty;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static int Integer(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
			? value.GetInt32()
			: -1;

	private static string Raw(JsonElement? reading) => reading?.ToString() ?? "(null)";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
