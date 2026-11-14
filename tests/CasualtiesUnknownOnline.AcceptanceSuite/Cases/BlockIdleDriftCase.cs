using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The block tables of the two clients agree, and stay agreed while both bodies stand still: the case
/// reads the same band on both clients, waits with neither body moving, and reads it again. The band is
/// one row at a time so a failure names the row and the client, and the two readings answer the first
/// question of the divergence investigation — does the table drift on its own (the game's simulation on
/// one client), or only when a body moves (a writer that follows the local player)?
/// </summary>
internal sealed class BlockIdleDriftCase : AcceptanceCase
{
	private const int BandStart = 900;
	private const int BandEnd = 1024;
	private const int BandWidth = 1024;
	private static readonly TimeSpan Idle = TimeSpan.FromSeconds(45);

	public override string Name => "block-idle-drift";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var first = await ReadBandAsync(context, cancellationToken).ConfigureAwait(false);
		Require(first.Host.Length == first.Member.Length, "the two digests answer different row counts");
		Require(first.Host.Length == BandEnd - BandStart, $"the digest answered {first.Host.Length} rows for a {BandEnd - BandStart}-row band");
		context.Note($"the band read {first.Host.Length} rows on both clients");

		var atEntryHost = first.Host;
		var atEntryMember = first.Member;

		await Task.Delay(Idle, cancellationToken).ConfigureAwait(false);

		var second = await ReadBandAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("host-band-idle", JsonDocument.Parse("\"" + string.Join(",", second.Host) + "\"").RootElement.Clone());
		context.Read("member-band-idle", JsonDocument.Parse("\"" + string.Join(",", second.Member) + "\"").RootElement.Clone());

		var hostChanged = CountChanges(atEntryHost, second.Host);
		var memberChanged = CountChanges(atEntryMember, second.Member);
		context.Note($"over {Idle.TotalSeconds:0}s idle: host changed {hostChanged} row(s), member changed {memberChanged} row(s)");

		Require(hostChanged == 0 && memberChanged == 0,
			$"a block row changed while neither body moved: host {hostChanged}, member {memberChanged} (rows {FirstChanged(atEntryHost, second.Host, BandStart)}, "
			+ $"{FirstChanged(atEntryMember, second.Member, BandStart)})");

		var differences = Differences(atEntryHost, atEntryMember);
		context.Note($"the two clients differ on {differences} row(s) of the band");

		// The host reconciles a drifted table on its 60 s step (a difference, never a blind resend).
		// A member that entered before that write heals on the first step; the case waits for it
		// rather than asserting instant agreement, and records how long the heal took.
		var waited = TimeSpan.Zero;
		while (differences > 0 && waited < TimeSpan.FromSeconds(150))
		{
			await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
			waited += TimeSpan.FromSeconds(5);
			var polled = await ReadBandAsync(context, cancellationToken).ConfigureAwait(false);
			differences = Differences(polled.Host, polled.Member);
			context.Note($"after {waited.TotalSeconds:0}s the two clients differ on {differences} row(s)");
		}

		context.Note($"the reconcile healed the band in {waited.TotalSeconds:0}s");
		Require(differences == 0, $"the two clients' tables differ on {differences} row(s) after {waited.TotalSeconds:0}s; first at row {FirstChanged(atEntryHost, atEntryMember, BandStart)}");
	}

	private static async Task<(string[] Host, string[] Member)> ReadBandAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var host = await context.RecipeAsync("block-band-digest", cancellationToken, ("y0", BandStart.ToString()), ("y1", BandEnd.ToString()), ("width", BandWidth.ToString())).ConfigureAwait(false);
		context.Read("host-band", host);
		var member = await context.MemberRecipeAsync("block-band-digest", cancellationToken, ("y0", BandStart.ToString()), ("y1", BandEnd.ToString()), ("width", BandWidth.ToString())).ConfigureAwait(false);
		context.Read("member-band", member);

		return (Rows(host, "host"), Rows(member, "member"));
	}

	private static string[] Rows(JsonElement? answer, string side)
	{
		if (answer is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("rows", out var rows)
			|| rows.ValueKind != JsonValueKind.Array)
		{
			throw new AssertionException($"the {side} digest answered no rows: {answer?.GetRawText() ?? "null"}");
		}

		var values = new string[rows.GetArrayLength()];
		var index = 0;
		foreach (var row in rows.EnumerateArray())
		{
			values[index++] = row.ValueKind == JsonValueKind.String ? row.GetString() ?? string.Empty : row.GetRawText();
		}

		return values;
	}

	private static int CountChanges(string[] before, string[] after)
	{
		var changed = 0;
		for (var i = 0; i < Math.Min(before.Length, after.Length); i++)
		{
			if (!string.Equals(before[i], after[i], StringComparison.Ordinal))
			{
				changed++;
			}
		}

		return changed;
	}

	private static int Differences(string[] host, string[] member)
	{
		var differing = 0;
		for (var i = 0; i < Math.Min(host.Length, member.Length); i++)
		{
			if (!string.Equals(host[i], member[i], StringComparison.Ordinal))
			{
				differing++;
			}
		}

		return differing;
	}

	private static int FirstChanged(string[] first, string[] second, int firstRow)
	{
		for (var i = 0; i < Math.Min(first.Length, second.Length); i++)
		{
			if (!string.Equals(first[i], second[i], StringComparison.Ordinal))
			{
				return firstRow + i;
			}
		}

		return firstRow;
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
