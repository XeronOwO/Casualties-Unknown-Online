using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A host's kill reaches the member's own copy of the world: the host picks a live animal it can hit without
/// standing in the blast, kills it with the game's own explosion, and the member's own count of animals must fall.
/// The count is judged as a range (the scene gains and loses animals on its own), so the row proves the removal
/// arrived, not an exact arithmetic. The enemies domain's cross-client row.
/// </summary>
internal sealed class EnemyKillCase : AcceptanceCase
{
	private const double SafeBodyDistance = 2.0;

	public override string Name => "enemy-kill";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForCustodyAsync(cancellationToken).ConfigureAwait(false);

		var scene = await context.RecipeAsync("enemy-scene", cancellationToken).ConfigureAwait(false);
		context.Read("host scene", scene);

		var target = PickAnimal(scene);
		Require(target is not null,
			"no animal stands beyond the blast's body radius, so the row would hurt the host: " + Raw(scene));

		var memberBefore = await context.MemberRecipeAsync("enemy-count", cancellationToken).ConfigureAwait(false);
		context.Read("member count before", memberBefore);
		var before = Number(memberBefore, "count");

		var blast = await context.RecipeAsync("enemy-blast", cancellationToken,
			("prefab", target!.Value.Id),
			("damage", "1000"),
			("range", "0.5")).ConfigureAwait(false);
		context.Read("blast", blast);
		var healthAfter = Number(blast, "healthAfter");
		Require(healthAfter is <= 0, "the host's blast did not kill the animal: " + Raw(blast));

		double? after = null;
		for (var attempt = 0; attempt < 10; attempt++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			var counted = await context.MemberRecipeAsync("enemy-count", cancellationToken).ConfigureAwait(false);
			after = Number(counted, "count");
			if (before is not null && after is not null && after < before)
			{
				context.Read("member count after", counted);
				break;
			}
		}

		Require(before is not null && after is not null && after < before,
			"the member's animal count did not fall after the host's kill: before=" + before + " after=" + after);
	}

	/// <summary>The nearest animal that stands outside the blast's own body radius.</summary>
	private static (string Id, double Distance)? PickAnimal(JsonElement? scene)
	{
		if (scene is not { ValueKind: JsonValueKind.Object } root
			|| !root.TryGetProperty("animals", out var animals)
			|| animals.ValueKind != JsonValueKind.Array)
		{
			return null;
		}

		(string Id, double Distance)? best = null;
		foreach (var animal in animals.EnumerateArray())
		{
			if (animal.ValueKind != JsonValueKind.Object
				|| !animal.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String
				|| !animal.TryGetProperty("bodyDistance", out var distance) || distance.ValueKind != JsonValueKind.Number)
			{
				continue;
			}

			var idText = id.GetString() ?? string.Empty;
			// A trader is an animal row too, and the blast's answer for it is a refusal path: the row kills
			// a creature, not the merchant the member may need.
			if (idText.StartsWith("trader", StringComparison.Ordinal))
			{
				continue;
			}

			var d = distance.GetDouble();
			if (d < SafeBodyDistance)
			{
				continue;
			}

			if (best is null || d < best.Value.Distance)
			{
				best = (idText, d);
			}
		}

		return best;
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static double? Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: null;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
