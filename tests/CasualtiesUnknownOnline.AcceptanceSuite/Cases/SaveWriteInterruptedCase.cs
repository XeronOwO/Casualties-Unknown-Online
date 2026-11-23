using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The interrupted-write row: the host saves twice, leaves the world (which takes its own MenuReturn
/// cut), the live archive is truncated after that cut, and Continue has to open the newest intact
/// backup. The newest backup is the second save's own cut (the writer copies the save it just took),
/// so the stored character is the readable proof that the world the player gets is the LAST completed
/// save: the member's item is coloured 5 before the first save and 7 before the second, and the
/// restored character must carry 7, not the older 5.
///
/// The red run moves the backup folder aside before the truncation: the same damaged live has no
/// fallback, so the continue must refuse and the case must fail. The backup move is test-only and is
/// undone in a finally; the live file is the batch's own scratch archive.
/// </summary>
internal sealed class SaveWriteInterruptedCase : AcceptanceCase
{
	public override string Name => "save-write-interrupted";

	public override CaseCost Cost => CaseCost.InRun;


	/// <summary>
	/// The damaged-live path reports itself loudly twice: the unreadable live archive and the backup
	/// that replaced it. Those two errors are the row's premise; any other error still fails the gate.
	/// </summary>
	public override int AllowedErrors => 2;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var saveRoot = context.SaveRoot ?? throw new AssertionException("the case needs --save-root: a real save library must never be the fixture");
		Require(!string.IsNullOrWhiteSpace(saveRoot), "the case needs --save-root: a real save library must never be the fixture");
		Require(Directory.Exists(saveRoot), "the batch's save root does not exist: " + saveRoot);

		await context.Session.EnsureInWorldAsync(cancellationToken).ConfigureAwait(false);
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);

		var memberItem = await FindItemAsync(context, member: true, cancellationToken).ConfigureAwait(false);
		var memberSlot = Text(memberItem, "slot");
		Require(memberSlot.Length > 0, "the member has no starting item to colour: " + Raw(memberItem));
		var first = await context.MemberRecipeAsync("item-customize-native", cancellationToken, ("slot", memberSlot), ("label", "MemberColor"), ("swatch", "5")).ConfigureAwait(false);
		Require(Boolean(first, "ok"), "the member's first colour did not apply: " + Raw(first));

		await SaveContinueCycle.SaveAsync(context, cancellationToken).ConfigureAwait(false);
		var storedFirst = await SaveContinueCycle.ReadStoredAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("stored after first save", storedFirst);
		Require(HasItem(storedFirst, "MemberColor", 5), "the first save did not store the member's colour 5: " + Raw(storedFirst));

		var second = await context.MemberRecipeAsync("item-customize-native", cancellationToken, ("slot", memberSlot), ("label", "MemberColor"), ("swatch", "7")).ConfigureAwait(false);
		Require(Boolean(second, "ok"), "the member's second colour did not apply: " + Raw(second));
		var secondCut = await SaveContinueCycle.SaveAndNameAsync(context, cancellationToken).ConfigureAwait(false);
		Require(secondCut.Length > 0, "the second save cut did not commit");
		var secondBackup = BackupPathOf(secondCut);
		Require(secondBackup.Length > 0, "the second save cut did not name the archive it wrote: " + secondCut);
		Require(File.Exists(secondBackup), "the second save named an archive that is not on disk: " + secondBackup);
		var storedSecond = await SaveContinueCycle.ReadStoredAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("stored after second save", storedSecond);
		Require(HasItem(storedSecond, "MemberColor", 7), "the second save did not store the member's colour 7: " + Raw(storedSecond));

		var live = NewestLive(saveRoot);
		Require(live is not null, "no live archive was written under the batch's save root: " + saveRoot);
		var backupFolder = Path.Combine(Path.GetDirectoryName(live!)!, "backups");
		var moved = Path.Combine(Path.GetDirectoryName(live!)!, "backups.red");
		var red = string.Equals(Environment.GetEnvironmentVariable("CUO_ACCEPTANCE_RED"), Name, StringComparison.Ordinal);
		context.Note("live archive: " + live + " (" + new FileInfo(live!).Length + " bytes)");

		// The leave takes a MenuReturn cut of its own, and that cut rewrites live.cuoz. The interrupted
		// write must be staged after it, or the leave replaces the truncation with a healthy archive
		// (measured 2026-11-20: the cut at 03:42:25 overwrote the 0-byte file before the 03:42:26
		// continue, which restored "from the live snapshot").
		var leaveCut = await SaveContinueCycle.LeaveWorldAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("leave cut", JsonDocument.Parse("\"" + Escape(leaveCut) + "\"").RootElement.Clone());
		var leaveBackup = BackupPathOf(leaveCut);
		Require(leaveBackup.Length > 0, "the leave cut did not name its backup: " + leaveCut);
		Require(File.Exists(leaveBackup), "the leave cut named a backup that is not on disk: " + leaveBackup);
		var aside = leaveBackup + ".menu-return";
		try
		{
			if (red)
			{
				Require(Directory.Exists(backupFolder), "the red run expected a backup folder beside the live archive: " + backupFolder);
				Directory.Move(backupFolder, moved);
				context.Note("red run: moved the backup folder aside");
			}
			else
			{
				// Both the second save's cut and the leave's cut hold the colour-7 save. Parking the
				// leave's backup makes the second save's cut the newest readable backup; the first
				// save (colour 5) is older and must not be picked.
				File.Move(leaveBackup, aside, overwrite: true);
				context.Note("parked the leave's own backup so the second save's cut is the newest backup");
			}

			using (var stream = new FileStream(live!, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite))
			{
				// The truncated live archive is the interrupted write the row stages.
			}

			context.Note("truncated the live archive to 0 bytes");
			var mark = SaveContinueCycle.LogMark(context.HostLogPath);
			var restoredLine = await SaveContinueCycle.ContinueAsync(context, cancellationToken).ConfigureAwait(false);
			context.Read("restored line", JsonDocument.Parse("\"" + Escape(restoredLine) + "\"").RootElement.Clone());
			var recovered = await SaveContinueCycle.WaitForLogAsync(context.HostLogPath, mark, "from backup", TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
			context.Read("backup recovery line", JsonDocument.Parse("\"" + Escape(recovered) + "\"").RootElement.Clone());
			Require(recovered.Length > 0, "the continue never logged a backup recovery after the live archive was truncated");

			// Which archive came back is the whole point: the recovery must name the SECOND save's
			// cut, not an older backup that happens to carry a colour as well.
			Require(recovered.Contains(Path.GetFileName(secondBackup), StringComparison.Ordinal), "the continue recovered from a different archive than the last completed save: expected " + Path.GetFileName(secondBackup) + ", the line says: " + recovered);

			await context.Session.ClearMemberSessionAsync(cancellationToken).ConfigureAwait(false);
			var rejoined = await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
			context.Note("member after the fallback: " + rejoined);
			var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
			if (members.Length > 0)
			{
				context.UseMember(members[0]);
			}

			var memberBag = await WaitForMemberBodyAsync(context, cancellationToken).ConfigureAwait(false);
			context.Read("member bag after the fallback", memberBag);
			Require(Boolean(memberBag, "ok"), "the member did not rejoin the restored world: " + Raw(memberBag));
			Require(MarkerCopies(memberBag, "MemberColor", 7) >= 1, "the fallback did not restore the last completed save's colour 7: " + Raw(memberBag));
			Require(MarkerCopies(memberBag, "MemberColor", 5) == 0, "the fallback restored the older first save's colour 5 instead of the last completed save: " + Raw(memberBag));
		}
		finally
		{
			if (Directory.Exists(moved) && !Directory.Exists(backupFolder))
			{
				Directory.Move(moved, backupFolder);
			}

			if (File.Exists(aside))
			{
				if (!Directory.Exists(backupFolder))
				{
					Directory.CreateDirectory(backupFolder);
				}

				File.Move(aside, leaveBackup, overwrite: true);
			}
		}
	}

	private static async Task<JsonElement?> FindItemAsync(CaseContext context, bool member, CancellationToken cancellationToken)
	{
		foreach (var type in new[] { "bandage", "lantern", "trashbag", "dogfood", "waterbottle" })
		{
			var reading = member
				? await context.MemberRecipeAsync("item-provide", cancellationToken, ("type", type), ("mode", "find")).ConfigureAwait(false)
				: await context.RecipeAsync("item-provide", cancellationToken, ("type", type), ("mode", "find")).ConfigureAwait(false);
			if (Boolean(reading, "found") && Text(reading, "slot").Length > 0)
			{
				return reading;
			}
		}

		return member
			? await context.MemberRecipeAsync("item-provide", cancellationToken, ("type", "bandage"), ("mode", "create")).ConfigureAwait(false)
			: await context.RecipeAsync("item-provide", cancellationToken, ("type", "bandage"), ("mode", "create")).ConfigureAwait(false);
	}

	private static async Task<JsonElement?> WaitForMemberBodyAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
		var reading = default(JsonElement?);
		while (DateTime.UtcNow < deadline)
		{
			reading = await context.MemberRecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
			if (Boolean(reading, "ok"))
			{
				return reading;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return reading;
	}

	private static string? NewestLive(string saveRoot)
	{
		var lives = Directory.EnumerateFiles(saveRoot, "live.cuoz", SearchOption.AllDirectories).ToArray();
		return lives.Length == 0
			? null
			: lives.OrderByDescending(path => new FileInfo(path).LastWriteTimeUtc).First();
	}

	private static bool HasItem(JsonElement? reading, string label, int swatch)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
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

	/// <summary>The archive path named by a cut's own committed line (<c>...; backup &lt;path&gt;; ...</c>).</summary>
	private static string BackupPathOf(string cutLine)
	{
		var at = cutLine.LastIndexOf("backup ", StringComparison.Ordinal);
		if (at < 0)
		{
			return string.Empty;
		}

		var start = at + "backup ".Length;
		var end = cutLine.IndexOf(';', start);
		return (end < 0 ? cutLine[start..] : cutLine[start..end]).Trim();
	}

	/// <summary>How many items in this body carry the stored marker: label and swatch in the details string.</summary>
	private static int MarkerCopies(JsonElement? reading, string label, int swatch) =>
		Text(reading, "details").Split('|', StringSplitOptions.RemoveEmptyEntries)
			.Count(entry =>
			{
				var parts = entry.Split(':', 4);
				return parts.Length == 4
					&& string.Equals(parts[2], label, StringComparison.Ordinal)
					&& int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
					&& value == swatch;
			});

	private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()
			: string.Empty;

	private static string Raw(JsonElement? reading) => reading?.ToString() ?? "(null)";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
