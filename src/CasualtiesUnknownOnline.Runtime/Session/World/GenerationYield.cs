namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// How the generation wrapper (<c>WorldGenRandomIsolation.Drive</c>) must drive the current yield
/// of a world-generation coroutine. The rule exists because a WAIT instruction is an enumerator
/// too: UnityEngine's <c>CustomYieldInstruction</c> (WaitUntil/WaitWhile) implements
/// <c>IEnumerator</c>, so a wrapper that drives every enumerator recursively drives the wait frame
/// by frame — every waited frame becomes a "generation segment" and moves the recorded segment
/// start away from the state the generation actually produced. The layer-modifier replay rewinds
/// to that recorded start, so the defect surfaces as a member deciding its modifier from a
/// different stream position than the host (batch 20261002-p: host 19 segments and decision entry
/// 4AFD152F…, the waiting member 41 segments and D70305E2…).
///
/// Not covered: a <c>Coroutine</c> handle is a <c>YieldInstruction</c>, not an <c>IEnumerator</c>,
/// so it still takes the plain branch and is counted once — the generation chain never yields one
/// (its yields are nulls, nested iterators and the two WaitUntils).
/// </summary>
internal static class GenerationYield
{
	/// <summary>What the wrapper does with one yield.</summary>
	internal enum Kind
	{
		/// <summary>A plain yield — seal the generation stream around it and count it as one segment.</summary>
		Plain,

		/// <summary>A nested generation coroutine — drive it recursively; its consumption continues the segment.</summary>
		Nested,

		/// <summary>A wait instruction — hand it to the engine unchanged; it is not a generation segment.</summary>
		Wait,
	}

	/// <summary>Classifies one yield from the two type facts the wrapper can observe. The wait check
	/// comes first by definition: a wait instruction is also an enumerator, and the nested branch
	/// must never claim it.</summary>
	internal static Kind Classify(bool isEnumerator, bool isWaitInstruction) => (isWaitInstruction, isEnumerator) switch
	{
		(true, _) => Kind.Wait,
		(_, true) => Kind.Nested,
		_ => Kind.Plain,
	};
}
