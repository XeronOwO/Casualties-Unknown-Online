using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The save-and-continue row, read on the real game: the host asks for a save cut, the cut commits,
/// the host leaves the world and continues from the menu, and the world comes back. The host's own
/// log carries the cut and the continue, so the case judges the lines the product writes. The cut is
/// taken after a real trap blast, because the 2026-11-15 merged run showed that a world damaged
/// before the save is where the restore's world-entity rows go missing; the audit must name every
/// one of them applied.
/// </summary>
internal sealed class SaveContinueCase : AcceptanceCase
{
	public override string Name => "save-continue";

	public override CaseCost Cost => CaseCost.InRun;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var hostState = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		if (!IsTrue(hostState, "inWorld"))
		{
			await context.Session.EnsureInWorldAsync(cancellationToken).ConfigureAwait(false);
		}

		// Use the run's own starting items: a created item is not in the character record the
		// save cut writes, so it would test item creation rather than colour persistence.
		var hostProvided = await ExistingItemAsync(context, false, cancellationToken).ConfigureAwait(false);
		var hostSlot = StringValue(hostProvided, "slot");
		Require(hostSlot.Length > 0, "the host could not find a starting item: " + Raw(hostProvided));
		var hostCustomized = await context.RecipeAsync("item-customize-native", cancellationToken, ("slot", hostSlot), ("label", "HostColor"), ("swatch", "3")).ConfigureAwait(false);
		Require(Boolean(hostCustomized, "ok"), "the host could not colour its starting item: " + Raw(hostCustomized));

		var memberProvided = await ExistingItemAsync(context, true, cancellationToken).ConfigureAwait(false);
		var memberSlot = StringValue(memberProvided, "slot");
		Require(memberSlot.Length > 0, "the member could not find a starting item: " + Raw(memberProvided));
		var memberCustomized = await context.MemberRecipeAsync("item-customize-native", cancellationToken, ("slot", memberSlot), ("label", "MemberColor"), ("swatch", "5")).ConfigureAwait(false);
		Require(Boolean(memberCustomized, "ok"), "the member could not colour its starting item: " + Raw(memberCustomized));

		var hostSeesMemberBefore = await WaitForCloneAsync(
			() => context.RecipeAsync("clone-customization", cancellationToken),
			"MemberColor",
			5,
			cancellationToken).ConfigureAwait(false);
		Require(HasLabeledItem(hostSeesMemberBefore, "MemberColor", 5), "the host's displayed copy does not carry the member's colour: " + Raw(hostSeesMemberBefore));
		var memberSeesHostBefore = await WaitForCloneAsync(
			() => context.MemberRecipeAsync("clone-customization", cancellationToken),
			"HostColor",
			3,
			cancellationToken).ConfigureAwait(false);
		Require(HasLabeledItem(memberSeesHostBefore, "HostColor", 3), "the member's displayed copy does not carry the host's colour: " + Raw(memberSeesHostBefore));
		context.Read("host sees member colour", hostSeesMemberBefore);
		context.Read("member sees host colour", memberSeesHostBefore);
		var storedBefore = await SaveContinueCycle.ReadStoredAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("host stored character before save", storedBefore);

		// The save must come after real world damage. The 2026-11-15 merged run saved after a trap
		// blast and the restore then refused the cut's building-health rows; the suite hid that by
		// taking its save before the damage cases. Staging the mine here puts the reading in the row
		// that already saves and continues, instead of a second save/continue cycle (the separate
		// save-after-damage row cost 36.8 s of the suite's budget).
		var blastBody = await context.RecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
		var blastX = NumberIn(blastBody, "localBody", "positionX");
		var blastY = NumberIn(blastBody, "localBody", "positionY");
		Require(blastX is not null && blastY is not null, "the host's own position is unknown: " + Raw(blastBody));
		var breakable = await context.FindBreakableCellAsync((int)Math.Round(blastX!.Value), (int)Math.Round(blastY!.Value), cancellationToken).ConfigureAwait(false);
		Require(breakable is not null, "no breakable terrain cell was found near the host, so a mine there could not be staged: (" + blastX + ", " + blastY + ")");
		var stagedMine = await context.RecipeAsync("trap-stage", cancellationToken,
			("x", breakable!.Value.X.ToString(CultureInfo.InvariantCulture)),
			("y", breakable.Value.Y.ToString(CultureInfo.InvariantCulture)),
			("dx", "2"),
			("crate", "containercrate")).ConfigureAwait(false);
		context.Read("staged blast", stagedMine);
		Require(Boolean(stagedMine, "ok"), "the mine was not staged: " + Raw(stagedMine));
		var mineX = Number(stagedMine, "mineX");
		var mineY = Number(stagedMine, "mineY");
		Require(mineX is not null && mineY is not null, "the staged mine names no position: " + Raw(stagedMine));
		var fired = await context.RecipeAsync("trap-trigger", cancellationToken,
			("x", mineX!.Value.ToString("0.##", CultureInfo.InvariantCulture)),
			("y", mineY!.Value.ToString("0.##", CultureInfo.InvariantCulture))).ConfigureAwait(false);
		context.Read("blast", fired);
		Require(Boolean(fired, "ok"), "the mine did not fire: " + Raw(fired));

		// The mine's fuse is 0.8 s of game time; the kernel records the explosion's rows when it
		// lands, and the cut must be taken after them. This is that duration, not a poll budget.
		await Task.Delay(TimeSpan.FromMilliseconds(1200), cancellationToken).ConfigureAwait(false);

		// The type table is read AFTER the blast, because the blast can destroy a geyser: the cut
		// is taken after the damage, so the pre-damage table is not what the archive will carry
		// (2026-11-17: 92 rows before the mine, 91 in the cut).
		var beforeGeysers = await context.RecipeAsync("geyser-types", cancellationToken).ConfigureAwait(false);
		Require(Boolean(beforeGeysers, "ok"), "the host could not read the geyser types before saving: " + Raw(beforeGeysers));
		var beforeTypes = GeyserRows(beforeGeysers);
		Require(beforeTypes.Count > 0, "the host's world held no geyser rows before saving: " + Raw(beforeGeysers));
		context.Read("geyser types before save", beforeGeysers);

		await SaveContinueCycle.SaveAsync(context, cancellationToken).ConfigureAwait(false);
		var restored = await SaveContinueCycle.ContinueAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("restored line", JsonDocument.Parse("\"" + Escape(restored) + "\"").RootElement.Clone());

		// The live-world replay holds the native geyser set while the live count is still growing,
		// so the audit lands after the set stops filling, not at the entry seam. The wait is the
		// fixture's budget: the measured runs put it inside 20 s.
		var restoredAudit = await WaitForLogAsync(context, "restored the live world", TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
		Require(
			restoredAudit.Contains(beforeTypes.Count + " geyser type(s) applied (the archive carried " + beforeTypes.Count + ")", StringComparison.Ordinal),
			"the restore did not apply the " + beforeTypes.Count + " geyser types the live world held before the save: " + restoredAudit);
		Require(restoredAudit.Contains("0 row(s) not taken", StringComparison.Ordinal), "the restore still refused geyser rows: " + restoredAudit);
		Require(restoredAudit.Contains("/ 0 refused world-entity row(s)", StringComparison.Ordinal), "the restore refused a world-entity row after the trap blast: " + restoredAudit);
		Require(!restoredAudit.Contains("not carried world-entity row(s)", StringComparison.Ordinal), "the restore carried none of the cut's world-entity rows: " + restoredAudit);
		context.Read("geyser restore audit", JsonDocument.Parse("\"" + Escape(restoredAudit) + "\"").RootElement.Clone());

		var afterGeysers = await context.RecipeAsync("geyser-types", cancellationToken).ConfigureAwait(false);
		Require(Boolean(afterGeysers, "ok"), "the host could not read the geyser types after continuing: " + Raw(afterGeysers));
		context.Read("geyser types after continue", afterGeysers);
		Require(SameGeyserTypes(beforeTypes, GeyserRows(afterGeysers)), "the geyser types changed across the save/continue: " + Raw(afterGeysers));

		var forced = await context.RecipeAsync("geyser-start", cancellationToken).ConfigureAwait(false);
		Require(Boolean(forced, "ok"), "the acceptance could not force the geysers' Start: " + Raw(forced));
		var forcedCount = Double(forced, "started");
		context.Note(forcedCount >= 1
			? "forced the Start of " + forcedCount + " geyser(s) after the continue"
			: "no geyser was left unstarted after the continue; the Start re-apply path was not exercised in this world");
		var afterStart = await context.RecipeAsync("geyser-types", cancellationToken).ConfigureAwait(false);
		Require(Boolean(afterStart, "ok"), "the host could not read the geyser types after Start: " + Raw(afterStart));
		Require(SameGeyserTypes(beforeTypes, GeyserRows(afterStart)), "a geyser's own Start overwrote the restored type: " + Raw(afterStart));
		Require(Double(afterStart, "started") >= beforeTypes.Count, "not every geyser kept its started mark after Start: " + Raw(afterStart));
		context.Read("geyser types after Start", afterStart);

		// The member followed the host out of the world; the continued world needs it back before a
		// later case reads the pair, and the host's refreshed member id replaces the old one. Clear
		// the menu session first: RunEnded leaves it active but not in the world, and joining from
		// that stale session keeps the old kernel revision (a range loop) instead of a fresh checkpoint.
		await context.Session.ClearMemberSessionAsync(cancellationToken).ConfigureAwait(false);
		var rejoined = await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		context.Note("member after continue: " + rejoined);
		var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		if (members.Length > 0)
		{
			context.UseMember(members[0]);
			context.Note("member id after continue: " + members[0]);
		}

		var hostSeesMemberAfter = await WaitForCloneAsync(
			() => context.RecipeAsync("clone-customization", cancellationToken),
			"MemberColor",
			5,
			cancellationToken).ConfigureAwait(false);
		Require(HasLabeledItem(hostSeesMemberAfter, "MemberColor", 5), "the member's colour did not survive the save/continue: " + Raw(hostSeesMemberAfter));
		var memberSeesHostAfter = await WaitForCloneAsync(
			() => context.MemberRecipeAsync("clone-customization", cancellationToken),
			"HostColor",
			3,
			cancellationToken).ConfigureAwait(false);
		Require(HasLabeledItem(memberSeesHostAfter, "HostColor", 3), "the host's colour did not survive the save/continue: " + Raw(memberSeesHostAfter));
		context.Read("host sees member colour after continue", hostSeesMemberAfter);
		context.Read("member sees host colour after continue", memberSeesHostAfter);
	}

	private static async Task<JsonElement?> ExistingItemAsync(CaseContext context, bool member, CancellationToken cancellationToken)
	{
		foreach (var type in new[] { "bandage", "lantern", "trashbag", "dogfood", "waterbottle" })
		{
			var reading = member
				? await context.MemberRecipeAsync("item-provide", cancellationToken, ("type", type), ("mode", "find")).ConfigureAwait(false)
				: await context.RecipeAsync("item-provide", cancellationToken, ("type", type), ("mode", "find")).ConfigureAwait(false);
			if (Boolean(reading, "found") && StringValue(reading, "slot").Length > 0)
			{
				return reading;
			}
		}

		// A long-lived suite world may have consumed the starting kit. Create one
		// carried item instead; the customizer close re-reports the inventory, so
		// the new item is in the character record by the time the save runs.
		var created = member
			? await context.MemberRecipeAsync("item-provide", cancellationToken, ("type", "bandage"), ("mode", "create")).ConfigureAwait(false)
			: await context.RecipeAsync("item-provide", cancellationToken, ("type", "bandage"), ("mode", "create")).ConfigureAwait(false);
		return Boolean(created, "found") ? created : null;
	}

	private static async Task<JsonElement?> WaitForCloneAsync(Func<Task<JsonElement?>> read, string label, int swatch, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
		while (DateTime.UtcNow < deadline)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var reading = await read().ConfigureAwait(false);
			if (HasLabeledItem(reading, label, swatch))
			{
				return reading;
			}

			await Task.Yield();
		}

		return await read().ConfigureAwait(false);
	}

	private static bool HasLabeledItem(JsonElement? reading, string label, int swatch)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("items", out var items)
			|| items.ValueKind != JsonValueKind.Array)
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

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static double Double(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: 0;

	private static double? Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: null;

	private static double? NumberIn(JsonElement? reading, string outer, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(outer, out var inner)
		&& inner.ValueKind == JsonValueKind.Object
		&& inner.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: null;

	/// <summary>A geyser row as the probe reports it: position plus the type the restore had to keep.</summary>
	private readonly record struct GeyserRow(double X, double Y, int Type);

	/// <summary>
	/// The probe's rows as positions and types. Positions are compared with a tolerance, never by exact key:
	/// a regenerated layer can place the same geyser a hair off (measured 2026-11-18: 263.601 against 263.6),
	/// and the row's claim is that each geyser kept its type, not that the float survived the round trip.
	/// </summary>
	private static List<GeyserRow> GeyserRows(JsonElement? reading)
	{
		var rows = new List<GeyserRow>();
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("rows", out var array)
			|| array.ValueKind != JsonValueKind.Array)
		{
			return rows;
		}

		foreach (var row in array.EnumerateArray())
		{
			if (row.ValueKind != JsonValueKind.Object)
			{
				continue;
			}

			rows.Add(new GeyserRow(row.GetProperty("x").GetDouble(), row.GetProperty("y").GetDouble(), row.GetProperty("type").GetInt32()));
		}

		return rows;
	}

	/// <summary>
	/// One-to-one within the restore's own six metres: every row the cut carried is matched by an unused live
	/// geyser of the same type nearby. A swap between two geysers, a lost row and a changed type all fail.
	/// </summary>
	private static bool SameGeyserTypes(List<GeyserRow> left, List<GeyserRow> right)
	{
		if (left.Count != right.Count)
		{
			return false;
		}

		var used = new bool[right.Count];
		foreach (var row in left)
		{
			var best = 6.0 * 6.0;
			var match = -1;
			for (var index = 0; index < right.Count; index++)
			{
				if (used[index] || right[index].Type != row.Type)
				{
					continue;
				}

				var dx = right[index].X - row.X;
				var dy = right[index].Y - row.Y;
				var distance = (dx * dx) + (dy * dy);
				if (distance < best)
				{
					best = distance;
					match = index;
				}
			}

			if (match < 0)
			{
				return false;
			}

			used[match] = true;
		}

		return true;
	}

	private static string StringValue(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty(property, out var value))
		{
			return string.Empty;
		}

		return value.ValueKind switch
		{
			JsonValueKind.String => value.GetString() ?? string.Empty,
			JsonValueKind.Number => value.GetRawText(),
			_ => string.Empty,
		};
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

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

		return false;
	}

	private static async Task<string> WaitForLogAsync(CaseContext context, string needle, TimeSpan budget, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + budget;
		while (DateTime.UtcNow < deadline)
		{
			var text = ReadLog(context.HostLogPath);
			var index = text.LastIndexOf(needle, StringComparison.Ordinal);
			if (index >= 0)
			{
				var lineStart = text.LastIndexOf('\n', index) + 1;
				var lineEnd = text.IndexOf('\n', index);
				return text[lineStart..(lineEnd >= 0 ? lineEnd : text.Length)].Trim();
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		return string.Empty;
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

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
