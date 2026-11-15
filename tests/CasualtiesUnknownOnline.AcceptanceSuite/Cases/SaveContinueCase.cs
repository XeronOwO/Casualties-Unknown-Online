using System.Text;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The save-and-continue row, read on the real game: the host asks for a save cut, the cut commits,
/// the host leaves the world and continues from the menu, and the world comes back. The host's own
/// log carries the cut and the continue, so the case judges the lines the product writes.
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
		var storedBefore = await context.RecipeAsync("saved-color-read", cancellationToken, ("guest", context.MemberId)).ConfigureAwait(false);
		context.Read("host stored character before save", storedBefore);

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
		context.Read("restored line", JsonDocument.Parse("\"" + Escape(restored) + "\"").RootElement.Clone());

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

		return null;
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
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			var text = ReadLog(context.HostLogPath);
			if (text.Contains(needle, StringComparison.Ordinal))
			{
				var index = text.LastIndexOf(needle, StringComparison.Ordinal);
				return text[index..Math.Min(text.Length, index + 240)].Split('\n')[0].Trim();
			}
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
