using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The spectator row: when the member leaves the world, the host's own scene removes the displayed copy
/// and says so, and the renderer reports no failure. The witness is the host's clone probe (the copy is
/// gone) plus its own log line. This is the last in-world row; the next row is a NewWorld row and owns
/// its own restart, so the member is not forced to rejoin here. A live member whose session died must be
/// named as a finding, not hidden by a restart.
/// </summary>
internal sealed class RemoteRemovalCase : AcceptanceCase
{
	public override string Name => "remote-removal";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForCustodyAsync(cancellationToken).ConfigureAwait(false);

		JsonElement? before = null;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			before = await context.RecipeAsync("remote-body-count", cancellationToken, ("mode", "removal")).ConfigureAwait(false);
			if (RemoteBodyCount(before) >= 1)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		context.Read("host clone probe before", before);
		Require(RemoteBodyCount(before) >= 1, "the host shows no displayed copy of the member before the leave: " + Raw(before));

		// The product's own leave intent, not a bare ToMainMenu scene load: the latter never tells
		// the run coordinator that the world ended, so its in-world flag can outlive the menu.
		var left = await context.Session.ActionAsync(context.Session.MemberUrl, "leave-world-intent", cancellationToken).ConfigureAwait(false);
		context.Read("member leave", left);

		JsonElement? after = null;
		for (var attempt = 0; attempt < 20; attempt++)
		{
			after = await context.RecipeAsync("remote-body-count", cancellationToken, ("mode", "removal")).ConfigureAwait(false);
			if (RemoteBodyCount(after) == 0)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		context.Read("host clone probe after", after);
		Require(RemoteBodyCount(after) == 0, "the host still shows the member's copy after it left: " + Raw(after));

		var trace = await context.Session.LogTailAsync(context.Session.HostUrl, "Remote not in world", 80, cancellationToken).ConfigureAwait(false);
		context.Read("host removal trace", trace);
		var traceText = trace?.GetRawText() ?? string.Empty;
		Require(traceText.Contains("clone destroyed", StringComparison.OrdinalIgnoreCase),
			"the host's own log does not say the copy was destroyed: " + traceText);
		Require(!traceText.Contains("ICuoService.Update failed", StringComparison.Ordinal),
			"the host's own log carries a renderer failure around the removal: " + traceText);
	}

	private static double RemoteBodyCount(JsonElement? reading)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } root || !root.TryGetProperty("remoteBodies", out var count))
		{
			return -1;
		}

		return count.ValueKind == JsonValueKind.Number ? count.GetDouble() : double.TryParse(count.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : -1;
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
