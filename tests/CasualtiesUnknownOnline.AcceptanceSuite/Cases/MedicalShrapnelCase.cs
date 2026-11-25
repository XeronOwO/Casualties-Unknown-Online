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

	/// <summary>The seeded pieces, the pain the broken one applied, and the place the member was put at.</summary>
	public override CaseLeftovers Leaves => CaseLeftovers.Body | CaseLeftovers.Position;

	public override CaseCost Cost => CaseCost.NewWorld;


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

		// The host's own view of the member is a remote body: it opens only while the member is in the host's
		// sight (the same gate the push and carry gestures read). A batch leaves the two bodies far apart, so
		// the row puts the member beside the host the way the other remote-body rows do, then lets it stand
		// still — a body dropped on a tile keeps falling and the view refuses mid-fall (measured 2026-11-25).
		context.Note("member placed: " + await context.Session.BringTheMemberBesideTheHostAsync("the remote view", cancellationToken).ConfigureAwait(false));
		await SettleAsync(context, cancellationToken).ConfigureAwait(false);

		// The chart only opens while the LOCAL body is awake: the game's own wound view refuses on a
		// sleeping observer (`MedicalChartCase` measured the refusal the same way). This row used to walk
		// straight to the open, so in a batch it read `opened:false, nativeOpen:false` with the member
		// found — the member was there and the host was asleep. Read it first and name it if it is asleep.
		var viewer = await context.RecipeAsync("body-effects", cancellationToken).ConfigureAwait(false);
		context.Read("viewer", viewer);
		Require(Boolean(viewer, "alive"), "the host's body is not alive, so the game's wound view cannot open: " + Raw(viewer));
		Require(NumberValue(viewer, "consciousness") >= 60,
			"the host's body stayed asleep, so the game's own wound view refuses to open: " + Raw(viewer));

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
		// The log is buffered: a line's own timestamp can precede the moment its bytes reach the file (measured
		// 2026-11-25: `break-grasp applied` was stamped 20.650 while the file still ended before it at 21.407, so a
		// single read failed a row that had just watched the piece leave the limb). Poll the file, bounded.
		var applied = string.Empty;
		for (var wait = 0; wait < 50; wait++)
		{
			applied = FindLine(context.HostLogPath, "break-grasp applied", logStart);
			if (applied.Length > 0)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
		}

		context.Read("host-break-line", JsonSerializer.SerializeToElement(new { line = applied, pain }));
		Require(applied.Length > 0,
			"the host's log names no break-grasp application for this session (offset " + logStart.ToString(CultureInfo.InvariantCulture) +
			", length now " + LogLength(context.HostLogPath).ToString(CultureInfo.InvariantCulture) + "); the host's own tail says: " +
			Tail(context.HostLogPath, 1200));
		Require(pain > 0f, "the removed shrapnel piece applied no pain on the member's limb: " + Raw(after));
	}

	private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

	/// <summary>The host's log from an offset (the console channel is buffered; the line is polled above).</summary>
	/// <summary>The last part of a file, so a failed log read carries the text it could not find.</summary>
	private static string Tail(string path, int characters)
	{
		try
		{
			var bytes = ReadShared(path);
			var text = Encoding.UTF8.GetString(bytes);
			return text.Length <= characters ? text : text[^characters..];
		}
		catch (IOException exception)
		{
			return "(could not read the log: " + exception.Message + ")";
		}
	}

	/// <summary>
	/// The log's own bytes, read without taking a write lock: the host keeps writing it, and a plain
	/// <c>File.ReadAllText</c> fails while it does (measured 2026-11-25: "The process cannot access the file").
	/// </summary>
	private static byte[] ReadShared(string path)
	{
		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		using var memory = new MemoryStream();
		stream.CopyTo(memory);
		return memory.ToArray();
	}

	/// <summary>
	/// Finds the last <paramref name="needle"/> written at or after <paramref name="start"/>. The start is a
	/// byte count (what <c>LogLength</c> measures) while the needle is a string, so the offset is converted
	/// through the text's own encoding before they are compared — the log carries non-ASCII text, where a
	/// byte offset and a character index are not the same number.
	/// </summary>
	private static string FindLine(string path, string needle, long start)
	{
		try
		{
			if (!File.Exists(path))
			{
				return string.Empty;
			}

			var bytes = ReadShared(path);
			var text = Encoding.UTF8.GetString(bytes);
			var clipped = (int)Math.Min(start, bytes.Length);
			var charStart = Encoding.UTF8.GetCharCount(bytes, 0, clipped);
			var index = text.LastIndexOf(needle, StringComparison.Ordinal);
			return index >= 0 && index >= charStart ? text[index..Math.Min(text.Length, index + 240)].Trim() : string.Empty;
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

	/// <summary>The local body's world point, from <c>body-read</c>'s own <c>localBody</c> object.</summary>
	private static (float X, float Y) BodyPosition(JsonElement? reading)
	{
		if (reading is { ValueKind: JsonValueKind.Object } state
			&& state.TryGetProperty("localBody", out var body)
			&& body.ValueKind == JsonValueKind.Object
			&& body.TryGetProperty("positionX", out var x)
			&& body.TryGetProperty("positionY", out var y))
		{
			return ((float)x.GetDouble(), (float)y.GetDouble());
		}

		return (float.NaN, float.NaN);
	}

	/// <summary>Waits until the member's body stops moving (two readings within 0.1), so a fall is not read as a refusal.</summary>
	private static async Task<JsonElement?> SettleAsync(CaseContext context, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		var (lastX, lastY) = (double.NaN, double.NaN);
		for (var attempt = 0; attempt < 20; attempt++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
			last = await context.MemberRecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
			var (x, y) = BodyPosition(last);
			if (!double.IsNaN(lastX) && Math.Abs(x - lastX) < 0.1 && Math.Abs(y - lastY) < 0.1)
			{
				return last;
			}

			(lastX, lastY) = (x, y);
		}

		return last;
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
