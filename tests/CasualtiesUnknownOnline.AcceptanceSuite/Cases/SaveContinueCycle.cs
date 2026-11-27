using System.Text;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The save/continue act shared by the rows that cut the world and restore it. The cut is the host's
/// own <c>/save</c> command and Continue is the host's own menu action; the log lines are the product's
/// account of both. A row that changes the archive between the two steps (an interrupted write, a
/// crashed host, a second entry) calls the halves itself; a row that needs the ordinary act calls
/// <see cref="SaveAndContinueAsync"/>.
///
/// Every wait starts at the log mark taken before its action. The host log is appended across runs,
/// so a bare last-match search can read the PREVIOUS save or continue and let the row pass on a line
/// it did not cause (2026-11-20: the interrupted-write row needed the leave's own cut named, which a
/// whole-file search cannot tell from the earlier one).
/// </summary>
internal static class SaveContinueCycle
{
	public static async Task SaveAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var committed = await SaveAndNameAsync(context, cancellationToken).ConfigureAwait(false);
		Require(committed.Length > 0, "the save cut did not commit");
	}

	/// <summary>
	/// The same save, but the caller gets the cut's own committed line: a row that later has to say
	/// WHICH archive came back needs the line the cut wrote, not the newest file on disk.
	/// </summary>
	public static async Task<string> SaveAndNameAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var mark = LogMark(context.HostLogPath);
		var save = await context.Session.ActionAsync(context.Session.HostUrl, "console", cancellationToken, ("-Text", "/save")).ConfigureAwait(false);
		context.Read("save", save);
		return await WaitForLogAsync(context.HostLogPath, mark, "Cut timing for world", TimeSpan.FromSeconds(25), cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Leaves the world and waits for the MenuReturn cut it takes. The cut is not incidental: leaving
	/// rewrites <c>live.cuoz</c>, so a row that stages a damaged archive must do it after this line,
	/// and a row that reads the leave's backup needs the line that names it.
	/// </summary>
	public static async Task<string> LeaveWorldAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var mark = LogMark(context.HostLogPath);
		await context.Session.ActionAsync(context.Session.HostUrl, "leave-world", cancellationToken).ConfigureAwait(false);
		var state = default(JsonElement);
		var left = false;
		for (var wait = 0; wait < 20 && !left; wait++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			state = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			left = !IsTrue(state, "inWorld");
		}

		Require(left, "the host did not leave the world: " + (state.ValueKind == JsonValueKind.Object ? state.GetRawText() : "(no state)"));
		var cut = await WaitForLogAsync(context.HostLogPath, mark, "Cut timing for world", TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
		Require(cut.Length > 0, "the leave did not commit its menu cut");
		return cut;
	}

	public static async Task<string> ContinueAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var state = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
		if (IsTrue(state, "inWorld"))
		{
			await LeaveWorldAsync(context, cancellationToken).ConfigureAwait(false);
		}

		return await ContinueOnlyAsync(context, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Clicks Continue and waits for the restore, without leaving first: a row that must change the
	/// archive between the leave cut and the click (the second-entry red run) calls this half.
	/// </summary>
	public static async Task<string> ContinueOnlyAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var mark = LogMark(context.HostLogPath);
		var continued = await context.Session.ActionAsync(context.Session.HostUrl, "continue-run", cancellationToken).ConfigureAwait(false);
		context.Read("continue", continued);
		var restored = await WaitForLogAsync(context.HostLogPath, mark, "Continue restored world", TimeSpan.FromSeconds(40), cancellationToken).ConfigureAwait(false);
		Require(restored.Length > 0, "the continue never restored the world");
		var state = default(JsonElement);
		var inWorld = false;
		for (var wait = 0; wait < 30 && !inWorld; wait++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			state = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			inWorld = IsTrue(state, "inWorld");
		}

		Require(inWorld, "the host did not enter the restored world");
		return restored;
	}

	public static async Task<string> SaveAndContinueAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await SaveAsync(context, cancellationToken).ConfigureAwait(false);
		return await ContinueAsync(context, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Reads the host's ARCHIVE character for the member, retrying until the cut has written it. The
	/// cut log line is committed before the character file lands; the in-memory
	/// <c>CharacterDataStore</c> is the live 1 Hz report and is cleared by the cut, so the read comes
	/// from <c>characters/steam-&lt;id&gt;.json</c> in the newest world of the save root.
	/// </summary>
	public static async Task<JsonElement> ReadStoredAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
		JsonElement? last = null;
		while (DateTime.UtcNow < deadline)
		{
			last = await context.RecipeAsync("saved-character-file", cancellationToken, ("guest", context.MemberId)).ConfigureAwait(false);
			if (last is { ValueKind: JsonValueKind.Object } element
				&& element.TryGetProperty("found", out var found)
				&& found.ValueKind == JsonValueKind.True)
			{
				return element;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException("the stored character was not written within 20 s: " + (last?.GetRawText() ?? "no reading"));
	}

	/// <summary>The character offset a caller marks before its action; waits only see lines after it.</summary>
	public static int LogMark(string path) => ReadLog(path).Length;

	public static async Task<string> WaitForLogAsync(string path, int mark, string needle, TimeSpan budget, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + budget;
		var lastLength = -1L;
		while (DateTime.UtcNow < deadline)
		{
			// A file that has not grown cannot hold the line yet: read its length first and pay the whole
			// file only when it changed. At 60 ms a poll this turns a fixed 300 ms tick into a wait that
			// returns on the write that carries the line (measured 2026-11-26: the continue rows poll a
			// multi-megabyte log and sat on the tick after the line had already landed).
			var length = -1L;
			try
			{
				length = new FileInfo(path).Length;
			}
			catch (Exception)
			{
				// A log that is being rotated is read on the next poll.
			}

			if (length == lastLength)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(60), cancellationToken).ConfigureAwait(false);
				continue;
			}

			lastLength = length;
			var text = ReadLog(path);
			var from = Math.Min(Math.Max(mark, 0), text.Length);
			var index = text.IndexOf(needle, from, StringComparison.Ordinal);
			if (index >= 0)
			{
				var lineStart = text.LastIndexOf('\n', index) + 1;
				var lineEnd = text.IndexOf('\n', index);
				return text[lineStart..(lineEnd >= 0 ? lineEnd : text.Length)].Trim();
			}

			await Task.Delay(TimeSpan.FromMilliseconds(60), cancellationToken).ConfigureAwait(false);
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

	private static bool IsTrue(JsonElement state, string property) =>
		state.ValueKind == JsonValueKind.Object
		&& state.TryGetProperty(property, out var value)
		&& value.ValueKind is JsonValueKind.True;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
