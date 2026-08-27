using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Coordinates a native game operation that may cross several Harmony hooks and
/// delayed callbacks before its terminal state is known. The coordinator owns
/// operation identity/trace, same-frame or cross-frame waits, deferred destroy
/// claims, and the "one native operation -> one NativeObservation" invariant.
///
/// It does not contain Unity or kernel code: patches call Begin/Observe/
/// Complete/Abort; a controller/later phase translates the single observation
/// into an ItemKernelAuthority command. Remote-apply scopes are represented by
/// <c>origin</c> at Begin and are not allowed to complete a real observation.
/// </summary>
public sealed class NativeOperationCoordinator(ILogger<NativeOperationCoordinator> log)
{
	private readonly ILogger<NativeOperationCoordinator> _log = log;
	private readonly Dictionary<ulong, PendingOperation> _operations = [];
	private ulong _nextToken = 1;
	private bool _aborted;

	/// <summary>Begin a native operation for a subject. Returns a handle for
	/// subsequent Observe/Complete calls, or default when the operation is
	/// aborted/remote-apply and must stay silent.</summary>
	public NativeOperationHandle Begin(NativeOperationKind kind, ulong subject, string before, bool remoteApply = false)
	{
		if (_aborted)
		{
			_log.LogDebug("Native operation begin ignored: coordinator aborted ({Kind}, subject {Subject}).", kind, subject);
			return default;
		}

		if (remoteApply)
		{
			_log.LogDebug("Native operation begin suppressed: remote apply ({Kind}, subject {Subject}).", kind, subject);
			return default;
		}

		var token = _nextToken++;
		_operations[token] = new PendingOperation(kind, subject, before, token);
		_log.LogDebug("Native operation {Op} begun: {Kind} subject {Subject}.", token, kind, subject);
		return new NativeOperationHandle(token);
	}

	/// <summary>Record an intermediate fragment. Fragments are diagnostics; they
	/// do not produce separate observations. Unknown/aborted operations are ignored.</summary>
	public void Observe(NativeOperationHandle handle, string fragment)
	{
		if (handle.Token == 0 || !_operations.TryGetValue(handle.Token, out var op))
		{
			return;
		}

		op.Fragments.Add(fragment);
	}

	/// <summary>
	/// Complete the operation and return exactly one observation. A second
	/// Complete for the same handle returns null and is logged as a duplicate;
	/// an aborted or unknown operation returns null.
	/// </summary>
	public NativeObservation? Complete(NativeOperationHandle handle, string after)
	{
		if (handle.Token == 0 || !TryTake(handle.Token, out var op))
		{
			_log.LogWarning("Native operation complete ignored: unknown/aborted handle {Handle}.", handle.Token);
			return null;
		}

		var observation = new NativeObservation(op.Token, op.Kind, op.Subject, op.Before, [.. op.Fragments], after);
		_log.LogInformation("Native operation {Op} completed: {Kind} subject {Subject}.", op.Token, op.Kind, op.Subject);
		return observation;
	}

	/// <summary>Abort one operation without producing an observation.</summary>
	public void Abort(NativeOperationHandle handle, string reason)
	{
		if (handle.Token == 0)
		{
			return;
		}

		if (TryTake(handle.Token, out var op))
		{
			_log.LogWarning("Native operation {Op} aborted: {Reason}.", op.Token, reason);
		}
	}

	/// <summary>Abort all in-flight operations at scene/run end.</summary>
	public void AbortAll(string reason)
	{
		_aborted = true;
		foreach (var op in _operations.Values)
		{
			_log.LogWarning("Native operation {Op} aborted by {Reason}.", op.Token, reason);
		}

		_operations.Clear();
	}

	/// <summary>Re-arm after a scene/run boundary (the next run may begin again).</summary>
	public void ResetForRun()
	{
		_aborted = false;
		_operations.Clear();
		_log.LogDebug("Native operation coordinator reset for a new run.");
	}

	/// <summary>Number of currently in-flight operations (diagnostics/tests).</summary>
	public int InFlightCount => _operations.Count;

	private bool TryTake(ulong token, out PendingOperation op)
	{
		if (_operations.TryGetValue(token, out op!))
		{
			_operations.Remove(token);
			return true;
		}

		op = null!;
		return false;
	}

	private sealed class PendingOperation(
		NativeOperationKind kind,
		ulong subject,
		string before,
		ulong operationToken)
	{
		internal ulong Token { get; } = operationToken;
		internal NativeOperationKind Kind { get; } = kind;
		internal ulong Subject { get; } = subject;
		internal string Before { get; } = before;
		internal List<string> Fragments { get; } = [];
	}
}
