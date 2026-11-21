using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The injection row of batch 1: a fentanyl injection started by the doctor's own release on the
/// member's remote medical view lands on the member's body — the effect is the game's own
/// `WaterContainerItem.Inject`, which for fentanyl raises the body's `Painkillers.opiateAmount`
/// by `ml * 0.1 * 420` (reversing/release-1.0.2/Assembly-CSharp/Liquids.cs:388-400).
///
/// The chain, read before the fixture: the release on the remote view
/// (`remote-medical-release`, the same path the doctor's drag uses) starts CUO's medical
/// operation (adapter `GameAdapterBridge.TryDivertRemoteInjection` → the operation session);
/// the game's syringe increments reach the operation as updates, and its end commits the ml;
/// the drained amounts travel to the target, whose own client runs the native `Inject`
/// (MedicalOperationInjectionApplier's own summary). The row stages the start with the player's
/// gesture and supplies the increments the minigame would send, which is a declared fixture
/// substitution — the effect is still applied by the member's own client.
/// </summary>
internal sealed class MedicalInjectionCase : AcceptanceCase
{
	public override string Name => "medical-injection";

	public override CaseCost Cost => CaseCost.NewWorld;


	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		var memberId = await SingleMemberAsync(context, cancellationToken).ConfigureAwait(false);
		context.UseMember(memberId);

		await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);

		var limb = await PickLimbAsync(context, cancellationToken).ConfigureAwait(false);
		Require(limb >= 0, "no limb of the member's body can take an injection");

		// The doctor's kit is emptied through the player's own drop so the syringe is the item the
		// release finds, and the member's own client names the value the injection will move.
		await context.ClearBodyAsync(cancellationToken).ConfigureAwait(false);
		var provided = await context.RecipeAsync("item-provide", cancellationToken, ("type", "fentanyl"), ("mode", "create")).ConfigureAwait(false);
		Require(Boolean(provided, "ok"), "the host could not take a fentanyl syringe: " + Raw(provided));

		var before = await context.MemberRecipeAsync("body-effects", cancellationToken).ConfigureAwait(false);
		context.Read("member-effects-before", before);
		var beforeOpiate = Number(before, "opiate");

		var opened = await context.RecipeAsync("medical-open", cancellationToken, ("steamId", memberId), ("name", "")).ConfigureAwait(false);
		context.Read("medical-open", opened);
		Require(Boolean(opened, "opened"), "the member's medical view did not open: " + Raw(opened));

		var logStart = LogLength(context.HostLogPath);
		var operation = await ReleaseUntilStartedAsync(context, limb, logStart, cancellationToken).ConfigureAwait(false);
		Require(operation > 0, "the release did not start a medical operation on the host");

		// The game's syringe is a 100 ml container holding 90 ml water and 10 ml fentanyl
		// (Item.cs:1269-1284), and the minigame injects `mult * 100f` ml of the mixture. Twenty
		// millilitres therefore carry two millilitres of fentanyl, and the native effect is 42 opiate
		// per millilitre (Liquids.cs:392-400): about 84. The dose is driven as two ten-millilitre
		// increments, which is the protocol call the minigame makes.
		const double expectedRise = 84;
		var injected = await context.RecipeAsync("medical-inject", cancellationToken,
			("op", Text(operation)), ("ml", "10"), ("count", "2")).ConfigureAwait(false);
		context.Read("medical-inject", injected);
		Require(Boolean(injected, "ok"), "the operation could not be driven: " + Raw(injected));

		JsonElement? after = null;
		var afterOpiate = beforeOpiate;
		var committed = string.Empty;
		for (var attempt = 0; attempt < 60; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			after = await context.MemberRecipeAsync("body-effects", cancellationToken).ConfigureAwait(false);
			afterOpiate = Number(after, "opiate");
			// The host's console log is written through a buffered channel: the commit line can land
			// a moment after the value does (measured 2026-11-18), so both are polled together.
			committed = FindLine(context.HostLogPath, "operation " + Text(operation) + " committed");
			if (afterOpiate - beforeOpiate >= expectedRise * 0.8 && committed.Length > 0)
			{
				break;
			}

			await Task.Delay(250, cancellationToken).ConfigureAwait(false);
		}

		context.Read("member-effects-after", after);
		context.Note("host commit line: " + committed);

		Require(committed.Length > 0, "the host never committed the operation: " + Text(operation));
		Require(afterOpiate - beforeOpiate >= expectedRise * 0.6,
			"the member's body did not take the injection: opiate " + NumberText(beforeOpiate) + " -> " + NumberText(afterOpiate));
		Require(afterOpiate - beforeOpiate <= expectedRise * 1.4,
			"the member's body took more than the delivered dose: opiate " + NumberText(beforeOpiate) + " -> " + NumberText(afterOpiate));
		context.Note("the member received the injection: opiate " + NumberText(beforeOpiate) + " -> " + NumberText(afterOpiate));
	}

	/// <summary>Picks an intact, non-head, non-vital limb from the member's own reading.</summary>
	private static async Task<int> PickLimbAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var body = await context.MemberRecipeAsync("limb-bleeding", cancellationToken, ("limb", "-1")).ConfigureAwait(false);
		foreach (var row in LimbRows(body).OrderByDescending(row => Number(row, "i")))
		{
			var index = (int)Number(row, "i");
			if (index < 0 || Boolean(row, "head") || Boolean(row, "vital") || Boolean(row, "dismembered") || Number(row, "skin") < 60)
			{
				continue;
			}

			return index;
		}

		return -1;
	}

	/// <summary>
	/// Releases the syringe and waits for the host's own "[MedicalOps] started operation {id}" line.
	/// A created item reaches the host's item table with the carried-inventory report, so the first
	/// release can be refused with "Item not found" before that report lands (measured 2026-11-18:
	/// the refusal named the instance and the next attempt started the operation). The row retries
	/// the same player gesture instead of staging the start itself.
	/// </summary>
	private static async Task<ulong> ReleaseUntilStartedAsync(CaseContext context, int limb, long logStart, CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < 5; attempt++)
		{
			var released = await context.RecipeAsync("remote-medical-release", cancellationToken, ("item", "fentanyl"), ("limb", Text(limb))).ConfigureAwait(false);
			context.Read("release-" + Text(attempt), released);
			Require(Boolean(released, "ok"), "the release of the syringe answered not-ok: " + Raw(released));

			var operation = await WaitForStartedOperationAsync(context, logStart, cancellationToken, 32).ConfigureAwait(false);
			if (operation > 0)
			{
				return operation;
			}

			await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
		}

		return 0;
	}

	/// <summary>Waits for the host's own "[MedicalOps] started operation {id}" line written after the release.</summary>
	private static async Task<ulong> WaitForStartedOperationAsync(CaseContext context, long logStart, CancellationToken cancellationToken, int attempts)
	{
		for (var attempt = 0; attempt < attempts; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var text = ReadLog(context.HostLogPath);
			if (text.Length > logStart)
			{
				var tailText = text[(int)Math.Min(logStart, text.Length)..];
				var index = tailText.LastIndexOf("started operation ", StringComparison.Ordinal);
				if (index >= 0)
				{
					var start = index + "started operation ".Length;
					var end = start;
					while (end < tailText.Length && char.IsDigit(tailText[end]))
					{
						end++;
					}

					if (end > start && ulong.TryParse(tailText[start..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
					{
						return id;
					}
				}
			}

			await Task.Delay(250, cancellationToken).ConfigureAwait(false);
		}

		return 0;
	}

	/// <summary>The host's roster names the member the suite joined.</summary>
	private static async Task<string> SingleMemberAsync(CaseContext context, CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < 20; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
			var ids = members.Where(id => !string.IsNullOrEmpty(id)).ToList();
			if (ids.Count == 1)
			{
				return ids[0];
			}

			await Task.Delay(250, cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException("the host's roster does not name exactly one member");
	}

	private static IEnumerable<JsonElement> LimbRows(JsonElement? reading) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty("limbs", out var limbs) && limbs.ValueKind == JsonValueKind.Array
			? limbs.EnumerateArray()
			: [];

	private static long LogLength(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

	private static string ReadLog(string path)
	{
		if (!File.Exists(path))
		{
			return string.Empty;
		}

		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}

	private static string FindLine(string path, string needle)
	{
		var text = ReadLog(path);
		var index = text.LastIndexOf(needle, StringComparison.Ordinal);
		if (index < 0)
		{
			return string.Empty;
		}

		var start = text.LastIndexOf('\n', index) + 1;
		var end = text.IndexOf('\n', index);
		return end < 0 ? text[start..] : text[start..end];
	}

	private static double Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.Number
			? propertyValue.GetDouble()
			: double.NaN;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.True;

	private static string Text(ulong value) => value.ToString(CultureInfo.InvariantCulture);

	private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static string NumberText(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "(no answer)";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
