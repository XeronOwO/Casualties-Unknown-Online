using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A member that joins a run for the first time is a new player: the host's starting-supply
/// coordinator gives the new character the game's opening kit, and the member's own client
/// carries it. The case restarts the run with the member out, rejoins it, and reads the
/// member's carried items.
/// </summary>
internal sealed class StartingSuppliesCase : AcceptanceCase
{
	public override string Name => "starting-supplies";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var outOfWorld = false;
		for (var attempt = 0; attempt < 3 && !outOfWorld; attempt++)
		{
			var left = await context.Session.ActionAsync(context.Session.MemberUrl, "leave-world", cancellationToken).ConfigureAwait(false);
			context.Note("member left its world: " + Raw(left));
			for (var wait = 0; wait < 12 && !outOfWorld; wait++)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
				var state = await context.Session.ActionAsync(context.Session.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
				outOfWorld = state is { ValueKind: JsonValueKind.Object } element
					&& element.TryGetProperty("result", out var result)
					&& result.TryGetProperty("inWorld", out var inWorld)
					&& inWorld.ValueKind == JsonValueKind.False;
			}
		}

		Require(outOfWorld, "the member is still in its old world after leaving");
		var restarted = await context.Session.RestartRunAsync(cancellationToken).ConfigureAwait(false);
		context.Note("session: " + restarted);

		var joined = string.Empty;
		for (var attempt = 0; attempt < 20; attempt++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			joined = await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
			if (joined.Contains("joined", StringComparison.Ordinal))
			{
				break;
			}
		}

		Require(joined.Contains("joined", StringComparison.Ordinal), "the member did not rejoin the fresh run: " + joined);

		// The starting kit is granted by the host's coordinator once the new character stands in
		// the world; the member's own carried items are the marker.
		var carried = 0;
		for (var wait = 0; wait < 16 && carried < 4; wait++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			var items = await context.MemberRecipeAsync("container-read", cancellationToken, ("mode", "local"), ("guest", "")).ConfigureAwait(false);
			context.Read("member-items", items);
			carried = CountCarried(items);
		}

		Require(carried >= 4, $"the member carries {carried} item(s), not the game's starting kit");
	}

	private static int CountCarried(JsonElement? reading)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element)
		{
			return 0;
		}

		foreach (var property in new[] { "localCount", "count" })
		{
			if (element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number)
			{
				return value.GetInt32();
			}
		}

		return 0;
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";
}
