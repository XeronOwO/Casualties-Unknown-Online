using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// Batch 1's shrapnel session row: a remote shrapnel removal starts the host-authoritative
/// operation for the patient's limb, the operator's client drives the game's own
/// <c>ShrapnelMinigame</c>, and the piece that leaves the limb applies the game's own break damage
/// on the patient's client.
///
/// The chain, read before the fixture: the wound view's special dispatch
/// (<c>remote-wound-special</c>, the production <c>GameAdapterBridge.TryStartRemoteWoundSpecial</c>)
/// asks the host to start the medical operation; the host's dispatch and the patient's own client
/// run the native removal (<c>ShrapnelMinigame.BreakGrasp</c> → the limb's shrapnel write and its
/// skin/bleed/pain damage), and the session ends on its own terminal. The earlier stage-2 ticket
/// measured the same path (`break-grasp applied to … skin -5.199 bleed +0.886 pain +12.38`,
/// `session 2 timed out after 15016 ms idle`), and this row re-reads it on the release build.
///
/// The seeded shrapnel is a setup substitution (`seed-shrapnel` — the game's own surfaces grant no
/// shrapnel on demand); the special, the session and the damage are the real ones.
/// </summary>
internal sealed class MedicalShrapnelCase : AcceptanceCase
{
	public override string Name => "medical-shrapnel";

	public override CaseCost Cost => CaseCost.NewWorld;

	public override bool RunsInBatch => false;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		var memberId = await SingleMemberAsync(context, cancellationToken).ConfigureAwait(false);
		context.UseMember(memberId);
		await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);

		var limb = -1;
		for (var candidate = 0; candidate < 8 && limb < 0; candidate++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			// A READ, not a seed: seed-shrapnel with count 0 is the game's clearing call and returns the
			// cleared limb, so using it as a probe both loses a count and reports one that never existed.
			var probe = await context.MemberRecipeAsync("limb-read", cancellationToken, ("source", "local"), ("limb", Number(candidate)), ("target", string.Empty)).ConfigureAwait(false);
			if (!Boolean(probe, "ok"))
			{
				continue;
			}

			if (!Boolean(probe, "hasShrapnel") && !Boolean(probe, "dismembered") && !Boolean(probe, "isHead") && !Boolean(probe, "isVital") && NumberValue(probe, "skinHealth") >= 60f)
			{
				limb = candidate;
			}
		}

		Require(limb >= 0, "no healthy limb of the member's body can take seeded shrapnel");

		const int pieces = 3;
		var seeded = await context.MemberRecipeAsync("seed-shrapnel", cancellationToken, ("limb", Number(limb)), ("count", Number(pieces))).ConfigureAwait(false);
		context.Read("seeded-shrapnel", seeded);
		Require(Boolean(seeded, "ok") && NumberValue(seeded, "shrapnel") >= pieces,
			"the member's limb did not take the seeded shrapnel: " + Raw(seeded));

		var opened = await context.RecipeAsync("medical-open", cancellationToken, ("steamId", memberId), ("name", string.Empty)).ConfigureAwait(false);
		Require(Boolean(opened, "opened"), "the member's medical view did not open on the host: " + Raw(opened));

		// The view's displayed limb is filled from the peer's own components on the coordinator's tick,
		// so the dispatch waits a beat after the open (the recipe's own header records the race).
		var view = await context.RecipeAsync("remote-wound-special", cancellationToken,
			("mode", "open"), ("target", memberId), ("limb", Number(limb))).ConfigureAwait(false);
		context.Read("remote-view", view);
		Require(Boolean(view, "ok") && Boolean(view, "opened"), "the remote wound view did not open: " + Raw(view));
		await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);

		// The display limb's piece count is a 1 Hz report fact, so the seeded pieces can reach the
		// operator's view after the chart opens; the dispatch is retried while it reports no pieces
		// (a refused dispatch starts no session, so a retry is safe).
		var logStart = LogLength(context.HostLogPath);
		JsonElement? special = null;
		var started = false;
		for (var attempt = 0; attempt < 20 && !started; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			special = await context.RecipeAsync("remote-wound-special", cancellationToken,
				("mode", "special"), ("target", memberId), ("limb", Number(limb))).ConfigureAwait(false);
			started = Boolean(special, "started");
			if (!started)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
			}
		}

		context.Read("shrapnel-special", special);
		Require(started, "the remote shrapnel special did not start the session: " + Raw(special));

		// The operator's hand is the game's own removal: the wound view starts ShrapnelMinigame, and a piece
		// leaves only when the hand grabs it, breaks it and lifts it above the game's line. The committed
		// declaration is that hand (tools/acceptance/driver/eval-declarations/shrapnel-break6.cs) — the same
		// path the stage-2 ticket measured, replayed here instead of asserted from a flag.
		var declarationPath = Path.Combine(RepositoryPaths.Root, "tools", "acceptance", "driver", "eval-declarations", "shrapnel-break6.cs");
		var source = await File.ReadAllTextAsync(declarationPath, cancellationToken).ConfigureAwait(false);
		var declared = await context.Session.ActionAsync(context.Session.HostUrl, "declare", cancellationToken, ("-Declare", "shrapnel-break6"), ("-Text", source)).ConfigureAwait(false);
		context.Note("declaration: " + Raw(declared));

		var driven = await context.RecipeAsync("shrapnel-break6", cancellationToken, ("count", Number(pieces))).ConfigureAwait(false);
		context.Read("shrapnel-break", driven);
		Require(Boolean(driven, "ok"), "the operator's shrapnel hand did not start: " + Raw(driven));

		// The patient's own client applies the break: a piece leaves the limb and its damage lands.
		JsonElement? after = null;
		var remaining = pieces;
		var pain = 0f;
		for (var attempt = 0; attempt < 60; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			after = await context.MemberRecipeAsync("limb-read", cancellationToken, ("source", "local"), ("limb", Number(limb)), ("target", string.Empty)).ConfigureAwait(false);
			remaining = (int)NumberValue(after, "shrapnel");
			pain = NumberValue(after, "pain");
			if (remaining < pieces || pain > 0f)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		context.Read("patient-limb-after", after);
		Require(remaining < pieces,
			"no shrapnel piece left the member's limb (seeded " + pieces.ToString(CultureInfo.InvariantCulture) +
			", remaining " + remaining.ToString(CultureInfo.InvariantCulture) + "): " + Raw(after));

		// The host's own line names the break and the damage it applied, which is the session doing the work.
		var applied = FindLine(context.HostLogPath, "break-grasp applied", logStart);
		context.Read("host-break-line", JsonSerializer.SerializeToElement(new { line = applied, pain }));
		Require(applied.Length > 0, "the host's log names no break-grasp application for this session");
		Require(pain > 0f, "the removed shrapnel piece applied no pain on the member's limb: " + Raw(after));
	}

	private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

	/// <summary>The host's log from an offset (the console channel is buffered; the line is polled above).</summary>
	private static string FindLine(string path, string needle, long start)
	{
		try
		{
			if (!File.Exists(path))
			{
				return string.Empty;
			}

			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using var reader = new StreamReader(stream, Encoding.UTF8);
			var text = reader.ReadToEnd();
			var index = text.LastIndexOf(needle, StringComparison.Ordinal);
			return index >= 0 && index >= start ? text[index..Math.Min(text.Length, index + 240)].Trim() : string.Empty;
		}
		catch (IOException)
		{
			return string.Empty;
		}
	}

	private static long LogLength(string path)
	{
		try
		{
			return File.Exists(path) ? new FileInfo(path).Length : 0;
		}
		catch (IOException)
		{
			return 0;
		}
	}

	/// <summary>The host's roster names the one member.</summary>
	private static async Task<string> SingleMemberAsync(CaseContext context, CancellationToken cancellationToken)
	{
		for (var wait = 0; wait < 20; wait++)
		{
			var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
			if (members.Length == 1 && members[0].Length > 0)
			{
				return members[0];
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		var final = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		Require(false, "the host's roster did not name exactly one member: " + string.Join(", ", final));
		return string.Empty;
	}

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.True;

	private static float NumberValue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(property, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.Number
			? (float)propertyValue.GetDouble()
			: float.NaN;

	private static string Raw(JsonElement? reading) => reading?.ToString() ?? "(null)";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
