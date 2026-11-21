using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The splint row of batch 1: the game's own limb tool puts a splint on a fractured limb,
/// the splint marks the limb as splinted, and taking it off through the wound view's special
/// action clears the mark and hands the item back.
///
/// The chain, read before the fixture: the player selects a fractured limb and releases the
/// carried splint in the wound view; <c>PlayerCamera.ApplyWoundItem</c> runs
/// <c>item.Stats.useLimbAction(limb, item)</c>, which attaches <c>SplintLimb</c> and destroys
/// the item (reversing/release-1.0.2/Assembly-CSharp/Item.cs:1770-1791). <c>SplintLimb.Start</c>
/// sets <c>limb.splinted = true</c> and draws the limb's icon; <c>TakeOff</c> instantiates the
/// item, hands it to the body and clears <c>splinted</c> (SplintLimb.cs:11-30). The removal
/// entry is <c>PlayerCamera.WoundSpecialAction</c>, which checks the tourniquet, shrapnel and
/// splint components in that order. The fracture itself is the state the tool acts on, so the
/// row creates it through the game's own <c>Limb.BreakBone</c> (the declared setup substitution).
/// </summary>
internal sealed class MedicalSplintCase : AcceptanceCase
{
	public override string Name => "medical-splint";

	public override CaseCost Cost => CaseCost.NewWorld;


	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		var memberId = await SingleMemberAsync(context, cancellationToken).ConfigureAwait(false);
		context.UseMember(memberId);

		var limb = await PickLimbAsync(context, cancellationToken).ConfigureAwait(false);
		Require(limb >= 0, "no limb of the member's body can take a splint");

		var before = await context.MemberRecipeAsync("item-provide", cancellationToken, ("type", "splint"), ("mode", "find")).ConfigureAwait(false);
		Require(!Boolean(before, "found"),
			"the member's starting kit already holds a splint, so the row cannot tell the created one from it: " + Raw(before));

		var broken = await context.MemberRecipeAsync("limb-break", cancellationToken, ("limb", Text(limb))).ConfigureAwait(false);
		Require(Boolean(broken, "ok"), "the game refused to break limb " + Text(limb) + ": " + Raw(broken));
		Require(Boolean(broken, "broken"), "the game did not record the limb as broken: " + Raw(broken));

		// The created splint needs a slot, and a fresh member's kit fills them: take one out through the game's own drop.
		Require(await TryFreeSlotAsync(context, cancellationToken).ConfigureAwait(false),
			"the member's body has no slot to free for a splint (every slot occupied or unusable)");

		var provided = await context.MemberRecipeAsync("item-provide", cancellationToken, ("type", "splint"), ("mode", "create")).ConfigureAwait(false);
		Require(Boolean(provided, "ok"), "the member could not be given a splint: " + Raw(provided));

		var selected = await context.MemberRecipeAsync("select-limb", cancellationToken, ("limb", Text(limb))).ConfigureAwait(false);
		Require(Boolean(selected, "ok"), "the member could not select limb " + Text(limb) + ": " + Raw(selected));

		var used = await context.MemberRecipeAsync("limb-use", cancellationToken, ("type", "splint"), ("mode", "use")).ConfigureAwait(false);
		Require(Boolean(used, "ok"), "the wound view refused the splint: " + Raw(used));
		Require(Boolean(used, "actuallyUsable"), "the game does not consider the splint usable on the selected limb: " + Raw(used));

		var after = await WaitForSplintAsync(context, limb, cancellationToken).ConfigureAwait(false);
		var treated = LimbRow(after, limb);
		Require(treated.ValueKind == JsonValueKind.Object, "the treated limb is missing after the splint: " + Raw(after));
		Require(Boolean(treated, "splint"), "the treated limb carries no splint: " + treated.GetRawText());
		Require(Boolean(treated, "splinted"), "the game does not mark the treated limb as splinted: " + treated.GetRawText());

		var consumed = await context.MemberRecipeAsync("item-provide", cancellationToken, ("type", "splint"), ("mode", "find")).ConfigureAwait(false);
		Require(!Boolean(consumed, "found"),
			"the splint is still carried after it was applied, so the game did not consume it: " + Raw(consumed));

		var removed = await context.MemberRecipeAsync("limb-use", cancellationToken, ("type", "splint"), ("mode", "special")).ConfigureAwait(false);
		Require(Boolean(removed, "ok"), "the wound view refused to take the splint off: " + Raw(removed));

		var afterOff = await WaitForNoSplintAsync(context, limb, cancellationToken).ConfigureAwait(false);
		var off = LimbRow(afterOff, limb);
		Require(off.ValueKind == JsonValueKind.Object, "the treated limb is missing after the splint was taken off: " + Raw(afterOff));
		Require(!Boolean(off, "splint"), "the limb still carries the splint component after it was taken off: " + off.GetRawText());
		Require(!Boolean(off, "splinted"), "the game still marks the limb as splinted after the splint was taken off: " + off.GetRawText());

		var returned = await context.MemberRecipeAsync("item-provide", cancellationToken, ("type", "splint"), ("mode", "find")).ConfigureAwait(false);
		Require(Boolean(returned, "found"),
			"the splint did not come back to the body after it was taken off: " + Raw(returned));
	}

	/// <summary>Picks a limb the game's own splint action accepts: not head, not vital, not dismembered, healthy skin, no splint yet.</summary>
	private static async Task<int> PickLimbAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var body = await context.MemberRecipeAsync("limb-bleeding", cancellationToken, ("limb", "-1")).ConfigureAwait(false);
		foreach (var row in LimbRows(body).OrderByDescending(row => Number(row, "i")))
		{
			var index = (int)Number(row, "i");
			if (index < 0 || Boolean(row, "head") || Boolean(row, "vital") || Boolean(row, "dismembered") || Boolean(row, "splint") || Number(row, "skin") < 60)
			{
				continue;
			}

			return index;
		}

		return -1;
	}

	/// <summary>Polls until the splint's Start has run on the treated limb.</summary>
	private static async Task<JsonElement?> WaitForSplintAsync(CaseContext context, int limb, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			last = await context.MemberRecipeAsync("limb-bleeding", cancellationToken, ("limb", Text(limb))).ConfigureAwait(false);
			var treated = LimbRow(last, limb);
			if (treated.ValueKind == JsonValueKind.Object && Boolean(treated, "splint") && Boolean(treated, "splinted"))
			{
				return last;
			}

			await Task.Delay(250, cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	/// <summary>Polls until the splint's TakeOff has cleared the limb.</summary>
	private static async Task<JsonElement?> WaitForNoSplintAsync(CaseContext context, int limb, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			last = await context.MemberRecipeAsync("limb-bleeding", cancellationToken, ("limb", Text(limb))).ConfigureAwait(false);
			var row = LimbRow(last, limb);
			if (row.ValueKind == JsonValueKind.Object && !Boolean(row, "splint") && !Boolean(row, "splinted"))
			{
				return last;
			}

			await Task.Delay(250, cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	/// <summary>Drops one carried item of a known starting kit through the game's own drop, to open a slot for the splint.</summary>
	private static async Task<bool> TryFreeSlotAsync(CaseContext context, CancellationToken cancellationToken)
	{
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

	private static IEnumerable<JsonElement> LimbRows(JsonElement? reading) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty("limbs", out var limbs) && limbs.ValueKind == JsonValueKind.Array
			? limbs.EnumerateArray()
			: [];

	private static JsonElement LimbRow(JsonElement? reading, int limb) =>
		LimbRows(reading).FirstOrDefault(row => Number(row, "i") == limb);

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
