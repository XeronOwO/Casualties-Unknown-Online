using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The tourniquet row of batch 1: the game's own limb tool stops the bleeding of the limb
/// it is on and of every limb further from the heart, and of nothing else.
///
/// The chain, read before the fixture: the player selects a wounded limb and releases the
/// carried tourniquet in the wound view; <c>PlayerCamera.ApplyWoundItem</c> runs
/// <c>item.Stats.useLimbAction(limb, item)</c>, which attaches <c>TourniquetScript</c>;
/// its <c>Start</c> sets <c>blockedBleeding = true</c> on <c>GetLowerLimbs(limb)</c> and
/// <c>TakeOff</c> clears the same set (reversing/release-1.0.2/Assembly-CSharp/
/// TourniquetScript.cs:10-32). The row reads the game's own <c>GetLowerLimbs</c> set from
/// the <c>limb-bleeding</c> recipe and asserts that set, never an anatomy guess
/// (docs/design/mechanics/medical-view.md:54-58).
/// </summary>
internal sealed class MedicalTourniquetCase : AcceptanceCase
{
	public override string Name => "medical-tourniquet";

	public override CaseCost Cost => CaseCost.NewWorld;


	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		var memberId = await SingleMemberAsync(context, cancellationToken).ConfigureAwait(false);
		context.UseMember(memberId);

		// The body has to exist before its limbs can be read, and a run that has already wounded it —
		// an earlier medical row bleeds a limb to the bone — leaves no limb this row can use. The
		// precondition is "an intact limb with something below it", so it is established, not assumed.
		await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);
		var (limb, chain) = await PickLimbAsync(context, cancellationToken).ConfigureAwait(false);
		if (limb < 0)
		{
			context.Note("no limb qualified on the member's shared body; healing it natively and re-probing");
			await context.MemberRecipeAsync("native-heal", cancellationToken).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
			(limb, chain) = await PickLimbAsync(context, cancellationToken).ConfigureAwait(false);
		}

		if (limb < 0)
		{
			// A severed limb does not heal back. Restarting the run to grow the member new legs is not something a
			// player can do, so the row says what it needs instead of manufacturing it (measured 2026-11-22,
			// `every-16`: the old fallback spent 57 s and then failed anyway; the runner's own world guarantee is
			// the place that decides what body a row gets).
			throw new AssertionException("the member's body has no lower-limb chain left to put a tourniquet on; "
				+ "an earlier row took the legs and a player cannot regrow them");
		}

		Require(limb >= 0, "no limb of the member's body has a lower-limb chain and can take a tourniquet");

		var seeded = await RecipeAsync(context, "wound-seed", cancellationToken, ("limb", limb.ToString(CultureInfo.InvariantCulture)), ("force", "200"), ("infection", "0")).ConfigureAwait(false);
		Require(Boolean(seeded, "ok"), "the wound seed failed on limb " + Text(limb) + ": " + Raw(seeded));
		Require(Number(seeded, "bleedAmount") > 0.001,
			"the seeded wound reads no bleed on limb " + Text(limb) + ": " + Raw(seeded));
		Require(Number(seeded, "skinHealth") > 0.5,
			"the seeded wound destroyed the limb instead of wounding it: " + Raw(seeded));

		// A created item needs a free slot, and the run may leave the bag either full or empty: ask for the item first,
		// and free a slot only when the game says there is none (measured 2026-11-22, `.acceptance/slots-1`: the bag was
		// empty, so dropping "the least interesting thing" had nothing to drop and the row failed on a slot it had).
		var provided = await context.MemberRecipeAsync("item-provide", cancellationToken, ("type", "tourniquet"), ("mode", "create")).ConfigureAwait(false);
		if (!Boolean(provided, "ok"))
		{
			if (!await TryFreeSlotAsync(context, cancellationToken).ConfigureAwait(false))
			{
				// No item to drop and no slot to take one: the body's slots are unusable, which is what an earlier
				// medical row leaves behind when it takes a limb. Healing restores what the game can restore, then the
				// item is asked for once more (measured 2026-11-22, `.acceptance/slots-2`: filled 0, every slot unusable).
				context.Note("no slot to free: healing the member's body natively and asking for the tourniquet again");
				await context.MemberRecipeAsync("native-heal", cancellationToken).ConfigureAwait(false);
				await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
			}

			provided = await context.MemberRecipeAsync("item-provide", cancellationToken, ("type", "tourniquet"), ("mode", "create")).ConfigureAwait(false);
		}

		Require(Boolean(provided, "ok"), "the member could not be given a tourniquet: " + Raw(provided));

		var selected = await context.MemberRecipeAsync("select-limb", cancellationToken, ("limb", Text(limb))).ConfigureAwait(false);
		Require(Boolean(selected, "ok"), "the member could not select limb " + Text(limb) + ": " + Raw(selected));

		var used = await context.MemberRecipeAsync("limb-use", cancellationToken, ("type", "tourniquet"), ("mode", "use")).ConfigureAwait(false);
		Require(Boolean(used, "ok"), "the wound view refused the tourniquet: " + Raw(used));
		Require(Boolean(used, "actuallyUsable"), "the game does not consider the tourniquet usable on the selected limb: " + Raw(used));

		var after = await WaitForTourniquetAsync(context, limb, cancellationToken).ConfigureAwait(false);
		var treated = LimbRow(after, limb);
		Require(treated.ValueKind == JsonValueKind.Object, "the treated limb is missing after the tourniquet: " + Raw(after));
		Require(Boolean(treated, "tourniquet"), "the treated limb carries no tourniquet: " + treated.GetRawText());
		Require(Boolean(treated, "blocked"), "the treated limb's bleeding is not blocked: " + treated.GetRawText());
		Require(Numbers(after, "lower").SequenceEqual(chain),
			"the blocked set changed between the probe and the tourniquet: " + Raw(after));

		foreach (var index in chain)
		{
			var row = LimbRow(after, index);
			Require(row.ValueKind == JsonValueKind.Object, "a lower limb is missing from the answer: " + Text(index));
			Require(Boolean(row, "blocked"), "limb " + Text(index) + " is further from the heart but not blocked: " + row.GetRawText());
		}

		foreach (var row in LimbRows(after))
		{
			if (Number(row, "i") == limb || chain.Contains((int)Number(row, "i")))
			{
				continue;
			}

			Require(!Boolean(row, "blocked"), "a limb outside the tourniquet's chain is blocked: " + row.GetRawText());
		}

		// What the player notices: the blood stops leaving. The body's own volume is read on the member's client
		// before and after three seconds with the tourniquet on; the chain being blocked must show up as a volume
		// that is no longer falling (the game keeps bleeding wounded limbs otherwise).
		var volumeBefore = Number(await context.MemberRecipeAsync("body-effects", cancellationToken).ConfigureAwait(false), "bloodVolume");
		await Task.Delay(TimeSpan.FromMilliseconds(3000), cancellationToken).ConfigureAwait(false);
		var volumeAfter = Number(await context.MemberRecipeAsync("body-effects", cancellationToken).ConfigureAwait(false), "bloodVolume");
		Require(volumeAfter >= volumeBefore - 0.05, $"the body kept losing blood with the tourniquet on: {volumeBefore:0.###} -> {volumeAfter:0.###}");

		// And the way back: the wound view's own removal button takes the tourniquet off, and the blocked set must
		// open again — a player who removes it gets the bleeding limb back.
		var removed = await context.MemberRecipeAsync("limb-use", cancellationToken, ("type", "tourniquet"), ("mode", "special")).ConfigureAwait(false);
		context.Read("removal", removed);
		Require(Boolean(removed, "ok"), "the wound view refused to take the tourniquet off: " + Raw(removed));
		var reopened = await context.MemberRecipeAsync("limb-bleeding", cancellationToken, ("limb", Text(limb))).ConfigureAwait(false);
		var reopenedRow = LimbRow(reopened, limb);
		Require(reopenedRow.ValueKind == JsonValueKind.Object, "the treated limb is missing after the removal: " + Raw(reopened));
		Require(!Boolean(reopenedRow, "blocked"), "the limb is still blocked after the tourniquet was taken off: " + reopenedRow.GetRawText());
	}

	/// <summary>Picks a treatable limb whose game chain reaches beyond itself, so the row proves the chain and not the item's own limb.</summary>
	private static async Task<(int Limb, List<int> Chain)> PickLimbAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var body = await context.MemberRecipeAsync("limb-bleeding", cancellationToken, ("limb", "-1")).ConfigureAwait(false);
		foreach (var row in LimbRows(body).OrderByDescending(row => Number(row, "i")))
		{
			var index = (int)Number(row, "i");
			if (index < 0 || Boolean(row, "dismembered") || Boolean(row, "tourniquet") || Number(row, "skin") < 60)
			{
				continue;
			}

			var probe = await context.MemberRecipeAsync("limb-bleeding", cancellationToken, ("limb", Text(index))).ConfigureAwait(false);
			var chain = Numbers(probe, "lower");
			// GetLowerLimbs starts with the limb itself; a foot's chain is therefore just itself and
			// would make "the chain is blocked" the item's own limb, not the rule. Require a further limb.
			if (chain.Count >= 2)
			{
				return (index, chain);
			}
		}

		return (-1, []);
	}

	/// <summary>Polls until the tourniquet's Start has run on the treated limb and its set is blocked.</summary>
	private static async Task<JsonElement?> WaitForTourniquetAsync(CaseContext context, int limb, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			last = await context.MemberRecipeAsync("limb-bleeding", cancellationToken, ("limb", Text(limb))).ConfigureAwait(false);
			var treated = LimbRow(last, limb);
			if (treated.ValueKind == JsonValueKind.Object && Boolean(treated, "tourniquet") && Boolean(treated, "blocked"))
			{
				return last;
			}

			await Task.Delay(250, cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	/// <summary>Drops one carried item of a known starting kit through the game's own drop, to open a slot for the tourniquet.</summary>
	private static async Task<bool> TryFreeSlotAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The row frees a slot from whatever the body actually holds: a fixed list of four starting-supply types was
		// enough only while the bag was untouched (measured 2026-11-22, `.acceptance/every-11`: the bag held a
		// trash bag and an emergency light, neither on the list, and the row read "every slot occupied or unusable").
		var slots = await context.MemberRecipeAsync("item-provide", cancellationToken, ("mode", "slots"), ("type", string.Empty)).ConfigureAwait(false);
		if (slots is { } slotReading && Boolean(slotReading, "ok")
			&& slotReading.TryGetProperty("ids", out var ids) && ids.GetString() is { Length: > 0 } listing)
		{
			foreach (var entry in listing.Split('|', StringSplitOptions.RemoveEmptyEntries))
			{
				var separator = entry.IndexOf(':');
				if (separator < 0 || separator + 1 >= entry.Length)
				{
					continue;
				}

				var type = entry[(separator + 1)..];
				var dropped = await context.MemberRecipeAsync("item-provide", cancellationToken, ("mode", "drop"), ("type", type)).ConfigureAwait(false);
				if (Boolean(dropped, "ok"))
				{
					return true;
				}
			}
		}

		foreach (var type in new[] { "trashbag", "dogfood", "waterbottle", "bandage" })
		{
			var dropped = await context.MemberRecipeAsync("item-provide", cancellationToken, ("mode", "drop"), ("type", type)).ConfigureAwait(false);
			if (Boolean(dropped, "ok"))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>The host's roster names the member the suite joined; the log tail is unreliable here.</summary>
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

	private static Task<JsonElement?> RecipeAsync(CaseContext context, string name, CancellationToken cancellationToken, params (string Key, string Value)[] arguments) =>
		context.MemberRecipeAsync(name, cancellationToken, arguments);

	private static IEnumerable<JsonElement> LimbRows(JsonElement? reading) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty("limbs", out var limbs) && limbs.ValueKind == JsonValueKind.Array
			? limbs.EnumerateArray()
			: [];

	private static JsonElement LimbRow(JsonElement? reading, int limb) =>
		LimbRows(reading).FirstOrDefault(row => Number(row, "i") == limb);

	private static List<int> Numbers(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } value || !value.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
		{
			return [];
		}

		return [.. array.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.Number ? item.GetInt32() : -1).Where(index => index >= 0)];
	}

	private static double Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.Number
			? propertyValue.GetDouble()
			: double.NaN;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.True;

	private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "(no answer)";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
