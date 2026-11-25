using System.Globalization;
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

	/// <summary>The wounds the kill test's own hits leave on the bodies it drives.</summary>
	public override CaseLeftovers Leaves => CaseLeftovers.Body;

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

		// The friend must be seeing the animal the host is about to kill. Instance ids are per client (a Unity
		// instance id says nothing on the other machine — measured 2026-11-24: the member has 47 animals, none
		// with the host's id), so the animal is matched by its prefab and by standing where the host saw it.
		var targetX = target!.Value.X;
		var targetY = target.Value.Y;
		var memberSceneBefore = await context.MemberRecipeAsync("enemy-scene", cancellationToken).ConfigureAwait(false);
		var seenBefore = HasAnimalNear(memberSceneBefore, target.Value.Id, targetX, targetY, 3.0);
		context.Read("member saw the target", JsonSerializer.SerializeToElement(new { id = target.Value.Id, x = targetX, y = targetY, seen = seenBefore }));
		Require(seenBefore, "the member's own scene never carried the animal the host kills (" + target.Value.Id + " at " + targetX + "," + targetY + "): " + Raw(memberSceneBefore));

		// The game's own explosion rolls `structuralDamage * Random.Range(0f, 2f)` once per
		// blast (WorldGeneration.cs:5449 in the 1.0.2 decompile), so a single 1000-damage blast
		// leaves a healthy animal alive whenever the roll is low. The row repeats the game's
		// own explosion until the animal dies, bounded so a blast that never connects still
		// fails; each call re-reads the animal by iid, so it hits wherever the animal now is.
		JsonElement? blast = null;
		double? healthAfter = null;
		var blasts = 0;
		for (var attempt = 0; attempt < 12 && healthAfter is not <= 0; attempt++)
		{
			blast = await context.RecipeAsync("enemy-blast", cancellationToken,
				("prefab", target!.Value.Id),
				("damage", "1000"),
				("range", "0.5"),
				("iid", target.Value.Iid.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
			Require(Boolean(blast, "ok"), "the host's blast could not be placed: " + Raw(blast));
			healthAfter = Number(blast, "healthAfter");
			blasts++;
		}

		context.Read("blast", blast);
		context.Note("blasts until the kill: " + blasts + " (health after the last: " + healthAfter + ")");
		Require(healthAfter is <= 0, "the host's blast did not kill the animal after " + blasts + " blast(s): " + Raw(blast));

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

		// The sharp half: the animal the host killed must leave the member's own scene — matched by prefab and
		// place, since an instance id says nothing on the other machine. A count that fell while a despawn
		// elsewhere paid for it would pass the range above and fail here.
		var stillSeen = true;
		for (var attempt = 0; attempt < 10 && stillSeen; attempt++)
		{
			var memberSceneAfter = await context.MemberRecipeAsync("enemy-scene", cancellationToken).ConfigureAwait(false);
			stillSeen = HasAnimalNear(memberSceneAfter, target.Value.Id, targetX, targetY, 3.0);
			if (!stillSeen)
			{
				context.Read("member scene after", memberSceneAfter);
			}

			if (stillSeen)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			}
		}

		Require(!stillSeen, "the member's own scene still carries the animal the host killed (" + target.Value.Id + " at " + targetX + "," + targetY + ")");
	}

	/// <summary>The nearest animal that stands outside the blast's own body radius.</summary>
	private static (string Id, int Iid, double Distance, double X, double Y)? PickAnimal(JsonElement? scene)
	{
		if (scene is not { ValueKind: JsonValueKind.Object } root
			|| !root.TryGetProperty("animals", out var animals)
			|| animals.ValueKind != JsonValueKind.Array)
		{
			return null;
		}

		(string Id, int Iid, double Distance, double X, double Y)? best = null;
		foreach (var animal in animals.EnumerateArray())
		{
			if (animal.ValueKind != JsonValueKind.Object
				|| !animal.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String
				|| !animal.TryGetProperty("iid", out var iid) || iid.ValueKind != JsonValueKind.Number
				|| !animal.TryGetProperty("x", out var ax) || ax.ValueKind != JsonValueKind.Number
				|| !animal.TryGetProperty("y", out var ay) || ay.ValueKind != JsonValueKind.Number
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
				best = (idText, iid.GetInt32(), d, ax.GetDouble(), ay.GetDouble());
			}
		}

		return best;
	}

	/// <summary>Whether a scene answer lists an animal of this prefab standing within `tolerance` of the point.</summary>
	private static bool HasAnimalNear(JsonElement? scene, string prefab, double x, double y, double tolerance)
	{
		if (scene is not { ValueKind: JsonValueKind.Object } root
			|| !root.TryGetProperty("animals", out var animals)
			|| animals.ValueKind != JsonValueKind.Array)
		{
			return false;
		}

		foreach (var animal in animals.EnumerateArray())
		{
			if (animal.ValueKind != JsonValueKind.Object
				|| !animal.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String
				|| !string.Equals(id.GetString(), prefab, StringComparison.Ordinal)
				|| !animal.TryGetProperty("x", out var ax) || ax.ValueKind != JsonValueKind.Number
				|| !animal.TryGetProperty("y", out var ay) || ay.ValueKind != JsonValueKind.Number)
			{
				continue;
			}

			if (Math.Abs(ax.GetDouble() - x) <= tolerance && Math.Abs(ay.GetDouble() - y) <= tolerance)
			{
				return true;
			}
		}

		return false;
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static double? Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: null;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind is JsonValueKind.True or JsonValueKind.False
		&& value.GetBoolean();

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
