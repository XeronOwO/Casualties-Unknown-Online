using System.Collections;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Wraps a native coroutine so every STEP of its body runs inside a capture
/// scope. An iterator method only CREATES its state machine when the patched
/// method runs — the body executes later, inside the machine's <c>MoveNext</c>,
/// long after a Prefix/Postfix pair around the method has been disposed. The
/// wrapper therefore enters the scope around each <c>MoveNext</c> and never
/// keeps one open across a <c>yield</c> (that would leak the origin into the
/// caller's frame, and Unity keeps the enumerator alive for the whole routine).
/// The enumerator is handed back unchanged when the call belongs to a clone or
/// a remote application, so the native coroutine runs exactly as it did.
/// Precedent for driving a wrapped enumerator: <c>WorldGenRandomIsolation</c>.
/// </summary>
internal static class ScopedCoroutine
{
	/// <summary>The same coroutine, with each step running inside <paramref name="origin"/>.</summary>
	internal static IEnumerator Capture(IEnumerator body, CallContext.Origin origin) => Drive(body, origin);

	private static IEnumerator Drive(IEnumerator body, CallContext.Origin origin)
	{
		while (true)
		{
			bool moved;
			using (CallContext.Enter(origin))
			{
				moved = body.MoveNext();
			}

			if (!moved)
			{
				yield break;
			}

			yield return body.Current;
		}
	}
}
