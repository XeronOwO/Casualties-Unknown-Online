using System.Collections.Generic;
using System.IO;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// The simulation's OperationTrace-format trace: every replay action emits the
/// same line shapes the game's OperationTrace emits
/// ("[ItemTrace] op=N begin item=X origin=Y event=Z" / "op=N item=X origin=Y
/// result=R events=[..]" — OperationTrace.cs:31-37), so tools/extract-itemtrace.ps1
/// can normalize the simulated trace exactly like a real latest.log and the two
/// are diffable — the OperationTrace→replay loop's last link: run the same
/// gesture sequence in the game and in the simulation, and the result/events
/// sequences must line up (the ps1 normalization drops origin/item, which the
/// simulation cannot mirror — it has no hook chain; the RESULT per step is the
/// fidelity surface). begin-without-end is the leak fingerprint, the same
/// baseline semantic the production trace asserts (OperationTrace.cs:14-16):
/// the begin line is written BEFORE the action executes, the end line only
/// when the action resolved — an exception mid-action leaves a pending op
/// visible in the trace file.
/// </summary>
internal sealed class SimTrace
{
	private readonly List<string> _lines = [];
	private readonly Dictionary<long, string> _pending = [];
	private long _nextOp;

	/// <summary>An action's begin line + the op its end line must carry. Written BEFORE the action executes.</summary>
	internal long Begin(ulong itemId, string origin, string eventName)
	{
		var op = _nextOp++;
		_pending.Add(op, eventName);
		_lines.Add($"[ItemTrace] op={op} begin item={itemId} origin={origin} event={eventName}");
		return op;
	}

	/// <summary>An action's end line (the result/decision chain). No-op if the op is unknown (already ended).</summary>
	internal void End(long op, ulong itemId, string origin, string result, params string[] events)
	{
		if (!_pending.Remove(op))
		{
			return;
		}

		_lines.Add($"[ItemTrace] op={op} item={itemId} origin={origin} result={result} events=[{string.Join(", ", events)}]");
	}

	/// <summary>Any action resolved without its end line? (A leak — the OperationTrace baseline semantic.)</summary>
	internal bool HasPendingOps => _pending.Count > 0;

	/// <summary>The trace lines (for the format-contract assertion).</summary>
	internal IReadOnlyList<string> Lines => _lines;

	internal void WriteTo(string path)
	{
		var directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		File.WriteAllLines(path, _lines);
	}
}
