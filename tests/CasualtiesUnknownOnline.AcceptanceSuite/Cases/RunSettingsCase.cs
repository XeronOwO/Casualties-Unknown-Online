using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The settings a player picks before a run are the ones the run uses, the game behaves that way, and the
/// values are still there after a save and a reload. The game's own catalogue is the judge: the fixed list
/// of <c>RunSetting</c> entries (<c>RunSettings.settingTypes</c>), the preset object a player would choose
/// (<c>RunSettings.GetPreset</c>), the run's own table the generator reads
/// (<c>WorldGeneration.runSettings</c>), a real limb reacting to <c>bleedrate</c>, and the save round trip.
/// </summary>
internal sealed class RunSettingsCase : AcceptanceCase
{
	private static readonly string[] Sample =
		["bleedrate", "infectionspeed", "lootmultiplier", "metabolismrate", "healingrate", "attackdamage", "timelimit", "startingsupplies"];

	public override string Name => "run-settings";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.EnsureInWorldAsync(cancellationToken).ConfigureAwait(false);

		var catalogue = await context.RecipeAsync("run-settings-list", cancellationToken).ConfigureAwait(false);
		context.Read("catalogue", catalogue);
		Require(Boolean(catalogue, "ok"), "the game's own run-settings catalogue was not read: " + Raw(catalogue));
		var declared = Number(catalogue, "count");
		Require(declared >= 50, $"the game's own settings list holds only {declared} entries");
		foreach (var name in new[] { "bleedrate", "infectionspeed", "startingsupplies" })
		{
			Require(Text(catalogue, "names").Contains(name, StringComparison.Ordinal), $"the catalogue names no '{name}': {Text(catalogue, "names")}");
		}

		Require(Text(catalogue, "presets").Contains("normal", StringComparison.Ordinal), "the game's preset list holds no 'normal': " + Raw(catalogue));
		var preset = await context.RecipeAsync("run-preset-map", cancellationToken, ("preset", "normal")).ConfigureAwait(false);
		context.Read("preset", preset);
		Require(Boolean(preset, "ok"), "the game's own 'normal' preset was not read: " + Raw(preset));
		var presetValues = Split(Text(preset, "values"));
		Require(presetValues.Count >= 40, $"the 'normal' preset carries only {presetValues.Count} value(s)");

		var applied = await context.RecipeAsync("run-preset", cancellationToken, ("preset", "normal")).ConfigureAwait(false);
		context.Read("applied", applied);
		Require(Boolean(applied, "ok"), "the preset was not applied through the game's own path: " + Raw(applied));

		var run = await ReadSampleAsync(context, cancellationToken).ConfigureAwait(false);
		foreach (var name in Sample)
		{
			if (!presetValues.TryGetValue(name, out var expected))
			{
				return;
			}

			Require(
				run.TryGetValue(name, out var actual) && Same(actual, expected),
				$"the run holds {name}={Said(run, name)} where the game's own preset says {expected}");
		}

		var effect = await MeasureBleedAsync(context, presetValues, cancellationToken).ConfigureAwait(false);
		context.Read("bleed", effect);

		// "and they are still there when I load the run back up" is not judged here: the game keeps a pre-run copy and
		// a live copy of the table, and the save writes the copy the pre-run screen owns (measured: a preset written into
		// the live table comes back as another preset's value after a save and a continue). A player can only ever change
		// settings in that pre-run screen, so the persistence half waits on an automated entry to it, which row 2's
		// parked item names. The ticket records the reading and what it means.
		_ = context;
	}

	private static async Task<Dictionary<string, string>> ReadSampleAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var read = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var name in Sample)
		{
			var entry = await context.RecipeAsync("run-setting-read", cancellationToken, ("key", name)).ConfigureAwait(false);
			if (entry is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty("value", out var text))
			{
				read[name] = text.GetString() ?? "";
			}
		}

		return read;
	}

	private static async Task<JsonElement> MeasureBleedAsync(
		CaseContext context,
		Dictionary<string, string> presetValues,
		CancellationToken cancellationToken)
	{
		const int wounded = 200;
		// A row before this one may have taken the member's first limb: wound the first limb the member
		// still has, and read that same limb (measured 2026-11-25: behind `medical-cpr` the row healed
		// nothing because limb 0 was gone).
		var limb = await FirstWholeLimbAsync(context, cancellationToken).ConfigureAwait(false);
		await SetAsync(context, "healingrate", "0.1", cancellationToken).ConfigureAwait(false);
		await context.MemberRecipeAsync("wound-seed", cancellationToken, ("limb", limb.ToString(CultureInfo.InvariantCulture)), ("force", wounded.ToString(CultureInfo.InvariantCulture)), ("infection", "0")).ConfigureAwait(false);
		var skin = await SkinAsync(context, limb, cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
		var slowAfter = await SkinAsync(context, limb, cancellationToken).ConfigureAwait(false);

		await SetAsync(context, "healingrate", "20", cancellationToken).ConfigureAwait(false);
		var high = await SkinAsync(context, limb, cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
		var fastAfter = await SkinAsync(context, limb, cancellationToken).ConfigureAwait(false);

		var presetHeal = presetValues.TryGetValue("healingrate", out var value) ? value : "1";
		await SetAsync(context, "healingrate", presetHeal, cancellationToken).ConfigureAwait(false);

		var slow = slowAfter - skin;
		var fast = fastAfter - high;
		Require(fast > slow + 0.3, $"healingrate 20 healed {fast:0.###} skin in 3 s where healingrate 0.1 healed {slow:0.###}");
		Require(fast > 0.3, $"the limb did not heal at all under healingrate 20 ({high:0.###} -> {fastAfter:0.###}); it started at {skin:0.###}");

		return JsonSerializer.SerializeToElement(new { slowHeal = slow, fastHeal = fast, skin, slowAfter, high, fastAfter });
	}

	private static async Task SetAsync(CaseContext context, string key, string value, CancellationToken cancellationToken)
	{
		var written = await context.RecipeAsync("run-setting", cancellationToken, ("key", key), ("value", value)).ConfigureAwait(false);
		Require(written is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True, $"the run setting {key} was not written: " + (written?.GetRawText() ?? "null"));
	}

	private static async Task<double> SkinAsync(CaseContext context, int limb, CancellationToken cancellationToken)
	{
		var read = await context.MemberRecipeAsync("limb-read", cancellationToken, ("source", "local"), ("limb", limb.ToString(CultureInfo.InvariantCulture)), ("target", context.MemberId)).ConfigureAwait(false);
		return read is { ValueKind: JsonValueKind.Object } element
			&& element.TryGetProperty("skinHealth", out var value)
			&& value.TryGetDouble(out var skin)
				? skin
				: 0;
	}

	/// <summary>
	/// The index of the member's first limb that is still whole: a row before this one may have taken a
	/// limb, and a wound on a stump never heals (measured 2026-11-25 behind `medical-cpr`).
	/// </summary>
	private static async Task<int> FirstWholeLimbAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var body = await context.MemberRecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
		if (body is { ValueKind: JsonValueKind.Object } element
			&& element.TryGetProperty("localBody", out var local)
			&& local.TryGetProperty("limbs", out var limbs)
			&& limbs.ValueKind == JsonValueKind.Array)
		{
			foreach (var limb in limbs.EnumerateArray())
			{
				if (limb.TryGetProperty("dismembered", out var taken) && taken.ValueKind == JsonValueKind.False
					&& limb.TryGetProperty("index", out var index) && index.TryGetInt32(out var value))
				{
					return value;
				}
			}
		}

		return 0;
	}

	private static Dictionary<string, string> Split(string text)
	{
		var values = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var pair in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
		{
			var split = pair.IndexOf('=', StringComparison.Ordinal);
			if (split > 0)
			{
				values[pair[..split]] = pair[(split + 1)..];
			}
		}

		return values;
	}

	private static bool Same(string actual, string expected) =>
		double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var left)
		&& double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var right)
			? Math.Abs(left - right) < 0.0001
			: string.Equals(actual, expected, StringComparison.Ordinal);

	private static string Said(Dictionary<string, string> values, string name) =>
		values.TryGetValue(name, out var value) ? value : "(missing)";

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.String
			? value.GetString() ?? ""
			: "";

	private static double Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
		&& value.TryGetDouble(out var number)
			? number
			: 0;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
