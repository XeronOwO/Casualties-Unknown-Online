using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A cut taken after a real trap blast restores its world-entity rows: the host stages a mine and a
/// crate on real soil beside its body, fires it (the explosion's building-health diff is what the
/// kernel records), saves, leaves and continues, and the restore audit must read every entity row
/// applied. The 2026-11-15 merged run saved after <c>mine-blast</c> and the host reported
/// <c>the live world did NOT take 2 of 3 restored world-entity row(s)</c>; the suite hid the row by
/// taking its save before the damage cases. This case is that reading: the save now comes after the
/// damage, and a requireGround building the blast changed must come back with the same health.
/// </summary>
internal sealed class SaveAfterDamageCase : AcceptanceCase
{
	public override string Name => "save-after-damage";

	public override CaseCost Cost => CaseCost.InRun;

	/// <summary>The host log's length when this case started. The waits read only what this run
	/// appended: an earlier case's "restored the live world" line is that case's reading, and a
	/// later one must never be reported as this case's.</summary>
	private long _logOffset;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		_logOffset = LogLength(context.HostLogPath);
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForCustodyAsync(cancellationToken).ConfigureAwait(false);

		var body = await context.RecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
		var x = NumberIn(body, "localBody", "positionX");
		var y = NumberIn(body, "localBody", "positionY");
		Require(x is not null && y is not null, "the host's own position is unknown: " + Raw(body));

		var before = await AreaAsync(context, x!.Value, y!.Value, 26, cancellationToken).ConfigureAwait(false);
		context.Read("building health around the host before", before);
		Require(Boolean(before, "ok"), "the host could not read its buildings: " + Raw(before));
		context.Note("buildings inside the blast radius: " + (Number(before, "total") ?? 0));

		var target = await context.FindBreakableCellAsync((int)Math.Round(x!.Value), (int)Math.Round(y!.Value), cancellationToken).ConfigureAwait(false);
		Require(target is not null, "no breakable terrain cell was found near the host: (" + x + ", " + y + ")");
		var (cellX, cellY, _) = target!.Value;

		var staged = await context.RecipeAsync("trap-stage", cancellationToken,
			("x", cellX.ToString(CultureInfo.InvariantCulture)),
			("y", cellY.ToString(CultureInfo.InvariantCulture)),
			("dx", "2"),
			("crate", "containercrate")).ConfigureAwait(false);
		context.Read("staged", staged);
		Require(Boolean(staged, "ok"), "the mine was not staged: " + Raw(staged));
		var mineX = Number(staged, "mineX");
		var mineY = Number(staged, "mineY");
		Require(mineX is not null && mineY is not null, "the staged mine names no position: " + Raw(staged));

		var fired = await context.RecipeAsync("trap-trigger", cancellationToken,
			("x", mineX!.Value.ToString("0.##", CultureInfo.InvariantCulture)),
			("y", mineY!.Value.ToString("0.##", CultureInfo.InvariantCulture))).ConfigureAwait(false);
		context.Read("trigger", fired);
		Require(Boolean(fired, "ok"), "the mine did not fire: " + Raw(fired));

		// The explosion reports its building-health diff in the same frame the blast lands; the
		// damaged entity's own health is what the applier writes back after the continue.
		var after = await PollAreaAsync(context, x.Value, y.Value, before, cancellationToken).ConfigureAwait(false);
		context.Read("building health around the host after", after);
		var (damagedX, damagedY, damagedHealth) = FirstHealthChange(before, after);
		context.Note(damagedX is null
			? "the blast changed no requireGround building's health in this world; only the blast's own entity rows are read"
			: "the blast changed a requireGround building at (" + damagedX + "," + damagedY + ") to health " + damagedHealth);

		var save = await context.Session.ActionAsync(context.Session.HostUrl, "console", cancellationToken, ("-Text", "/save")).ConfigureAwait(false);
		context.Read("save", save);
		var committed = await WaitForHostLogAsync(context, "Cut Command committed", TimeSpan.FromSeconds(25), cancellationToken).ConfigureAwait(false);
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
		if (damagedX is not null)
		{
			// The restore refuses a health row when the regenerated layer has no entity at the cut's
			// position; this timeline says WHICH of the two shapes it is — the entity never appears
			// (the layer will not have it) or it is there and goes before the replay writes it.
			context.Note("damaged building after continue: " + await SampleAsync(context, damagedX!.Value, damagedY!.Value, cancellationToken).ConfigureAwait(false));
		}

		var restored = await WaitForHostLogAsync(context, "Continue restored world", TimeSpan.FromSeconds(40), cancellationToken).ConfigureAwait(false);
		Require(restored.Contains("Continue restored world", StringComparison.Ordinal), "the continue never restored the world");

		var inWorld = false;
		for (var wait = 0; wait < 30 && !inWorld; wait++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			var state = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			inWorld = IsTrue(state, "inWorld");
		}

		Require(inWorld, "the host did not enter the restored world");

		// The audit lands after the host is back in the world (the live-world replay waits for the
		// generated objects); the world-entity clause names applied and refused rows.
		var audit = await WaitForHostLogAsync(context, "restored the live world", TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
		context.Read("restore audit", JsonDocument.Parse("\"" + Escape(audit) + "\"").RootElement.Clone());
		Require(audit.Contains("world-entity row(s)", StringComparison.Ordinal), "the restore carried no world-entity rows: " + audit);
		Require(!audit.Contains("not carried", StringComparison.Ordinal), "the restore did not carry the world-entity rows: " + audit);
		Require(audit.Contains(" applied / 0 refused world-entity row(s)", StringComparison.Ordinal), "the restore refused a world-entity row after the trap damage: " + audit);
		Require(audit.Contains("0 row(s) not taken", StringComparison.Ordinal), "the restore refused a row after the trap damage: " + audit);
		Require(!audit.Contains("INCOMPLETE", StringComparison.Ordinal), "the restore reported an incomplete world after the trap damage: " + audit);

		if (damagedX is not null)
		{
			var probe = await PollProbeAsync(context, damagedX.Value, damagedY!.Value, damagedHealth!.Value, cancellationToken).ConfigureAwait(false);
			context.Read("damaged building after continue", probe);
			if (damagedHealth.Value > 0.5f)
			{
				Require(Boolean(probe, "found"), "the damaged building was not in the restored world at (" + damagedX + "," + damagedY + "): " + Raw(probe));
				var health = Number(probe, "health");
				Require(health is not null && Math.Abs(health.Value - damagedHealth.Value) <= 0.5f,
					"the restored world lost the building's health: saved " + damagedHealth + ", live " + health + " (" + Raw(probe) + ")");
			}
			else
			{
				Require(!Boolean(probe, "found") || (Number(probe, "health") ?? 1f) <= 0.5f,
					"the destroyed building came back alive after the continue: " + Raw(probe));
			}
		}

		// The member followed the host out of the world; the cases behind this row need the pair back
		// in the world, and the continued world hands the member a new transport id.
		await context.Session.ClearMemberSessionAsync(cancellationToken).ConfigureAwait(false);
		var rejoined = await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		context.Note("member after continue: " + rejoined);
		var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		if (members.Length > 0)
		{
			context.UseMember(members[0]);
			context.Note("member id after continue: " + members[0]);
		}
	}

	private static async Task<JsonElement?> AreaAsync(CaseContext context, double x, double y, int radius, CancellationToken cancellationToken) =>
		await context.RecipeAsync("building-health-area", cancellationToken,
			("x", x.ToString("0.###", CultureInfo.InvariantCulture)),
			("y", y.ToString("0.###", CultureInfo.InvariantCulture)),
			("r", radius.ToString(CultureInfo.InvariantCulture)),
			("all", "1")).ConfigureAwait(false);

	/// <summary>Reads the same area again until a building's health differs from the first reading.</summary>
	private static async Task<JsonElement?> PollAreaAsync(CaseContext context, double x, double y, JsonElement? before, CancellationToken cancellationToken)
	{
		var last = await AreaAsync(context, x, y, 26, cancellationToken).ConfigureAwait(false);
		for (var attempt = 0; attempt < 16 && FirstHealthChange(before, last).X is null; attempt++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			last = await AreaAsync(context, x, y, 26, cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	/// <summary>
	/// Samples the small area around the damaged building while the continue regenerates the world: one
	/// compact entry per distinct reading, so the ticket can say whether the entity ever existed and when
	/// it went. Runs until the restore audit lands or the budget ends.
	/// </summary>
	private async Task<string> SampleAsync(CaseContext context, float x, float y, CancellationToken cancellationToken)
	{
		var samples = new StringBuilder();
		var previous = string.Empty;
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
		while (DateTime.UtcNow < deadline)
		{
			var area = await AreaAsync(context, x, y, 3, cancellationToken).ConfigureAwait(false);
			var rows = AreaRows(area);
			var current = rows.Count == 0
				? "-"
				: string.Join(";", rows.Select(row => row.Key + "=" + row.Value.ToString("0.#", CultureInfo.InvariantCulture)));
			if (!string.Equals(current, previous, StringComparison.Ordinal))
			{
				samples.Append(samples.Length == 0 ? string.Empty : " then ").Append(current);
				previous = current;
			}

			if (TryFindHostLogLine(context, "restored the live world") is not null)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
		}

		return samples.Length == 0 ? "-" : samples.ToString();
	}

	/// <summary>The first building whose health changed, named by its own position.</summary>
	private static (float? X, float? Y, float? Health) FirstHealthChange(JsonElement? before, JsonElement? after)
	{
		var previous = AreaRows(before);
		if (previous.Count == 0)
		{
			return (null, null, null);
		}

		foreach (var (key, health) in AreaRows(after))
		{
			if (!previous.TryGetValue(key, out var old) || Math.Abs(old - health) <= 0.5f)
			{
				continue;
			}

			var parts = key.Split(',');
			return (
				float.Parse(parts[0], CultureInfo.InvariantCulture),
				float.Parse(parts[1], CultureInfo.InvariantCulture),
				health);
		}

		return (null, null, null);
	}

	private static Dictionary<string, float> AreaRows(JsonElement? area)
	{
		var rows = new Dictionary<string, float>(StringComparer.Ordinal);
		if (area is not { ValueKind: JsonValueKind.Object } root
			|| !root.TryGetProperty("entries", out var entries)
			|| entries.ValueKind != JsonValueKind.Array)
		{
			return rows;
		}

		foreach (var entry in entries.EnumerateArray())
		{
			if (entry.ValueKind != JsonValueKind.Array || entry.GetArrayLength() < 3
				|| entry[0].ValueKind != JsonValueKind.Number
				|| entry[1].ValueKind != JsonValueKind.Number
				|| entry[2].ValueKind != JsonValueKind.Number)
			{
				continue;
			}

			rows[entry[0].GetDouble().ToString("0.###", CultureInfo.InvariantCulture) + "," + entry[1].GetDouble().ToString("0.###", CultureInfo.InvariantCulture)] = (float)entry[2].GetDouble();
		}

		return rows;
	}

	/// <summary>Reads the entity at the damaged position until the restored health settles or the entity goes.</summary>
	private static async Task<JsonElement?> PollProbeAsync(CaseContext context, float x, float y, float health, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var attempt = 0; attempt < 20; attempt++)
		{
			last = await context.RecipeAsync("entity-probe-at", cancellationToken,
				("x", x.ToString("0.###", CultureInfo.InvariantCulture)),
				("y", y.ToString("0.###", CultureInfo.InvariantCulture))).ConfigureAwait(false);
			var found = Boolean(last, "found");
			var live = Number(last, "health");
			if (health > 0.5f ? found && live is not null && Math.Abs(live.Value - health) <= 0.5f : !found || (live ?? 1f) <= 0.5f)
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	private async Task<string> WaitForHostLogAsync(CaseContext context, string needle, TimeSpan budget, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + budget;
		while (DateTime.UtcNow < deadline)
		{
			var line = TryFindHostLogLine(context, needle);
			if (line is not null)
			{
				return line;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		return string.Empty;
	}

	/// <summary>The whole line carrying <paramref name="needle"/> in what this case appended, or null
	/// while the host log does not carry it.</summary>
	private string? TryFindHostLogLine(CaseContext context, string needle)
	{
		var text = ReadLog(context.HostLogPath);
		var start = (int)Math.Min(_logOffset, text.Length);
		var index = text.LastIndexOf(needle, StringComparison.Ordinal);
		if (index < start)
		{
			return null;
		}

		var lineStart = text.LastIndexOf('\n', index) + 1;
		var lineEnd = text.IndexOf('\n', index);
		return text[lineStart..(lineEnd >= 0 ? lineEnd : text.Length)].Trim();
	}

	/// <summary>The host log's current length (0 while the file does not exist yet).</summary>
	private static long LogLength(string path)
	{
		try
		{
			return new FileInfo(path).Length;
		}
		catch (Exception)
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

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& (value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && value.GetString() == "true"));

	private static double? Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: null;

	private static double? NumberIn(JsonElement? reading, string outer, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(outer, out var inner) && inner.ValueKind == JsonValueKind.Object
		&& inner.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: null;

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

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
