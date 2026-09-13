using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// In-memory stand-in for the kernel's restored world-entity facts
/// (<see cref="IRestoredWorldEntitySource"/>): the host/solo half of a restore that
/// waits for the world-entry seam. The suite pins that the replay reads exactly
/// this shape and ends the pending write with the outcome the live world reported.
/// </summary>
internal sealed class FakeRestoredWorldEntitySource : IRestoredWorldEntitySource
{
	/// <summary>The facts a host/solo checkpoint restore armed — what the write must be handed.</summary>
	internal RestoredWorldEntityFacts Facts { get; set; } = RestoredWorldEntityFacts.Empty;

	/// <summary>Whether a restore is waiting for the world-entry seam.</summary>
	internal bool Armed { get; set; }

	/// <summary>
	/// The restore attempt the armed facts belong to (the kernel restore sequence).
	/// The suites set it to the sequence the account under test was opened for; a
	/// mismatched value models a half that outlived the restore it was armed by.
	/// </summary>
	internal ulong Sequence { get; set; }

	/// <summary>How many times the replay read the pending facts (more than one read would mean a take, not a read).</summary>
	internal int Reads { get; private set; }

	/// <summary>How many times the live world took the whole set.</summary>
	internal int Commits { get; private set; }

	/// <summary>Every cancellation reason, in order — an armed set must never disappear silently.</summary>
	internal List<string> Cancels { get; } = [];

	public bool HasPendingRestore => Armed;

	public ulong PendingRestoreSequence => Armed ? Sequence : 0;

	public RestoredWorldEntityFacts ReadPendingFacts()
	{
		Reads++;
		return Facts;
	}

	public void CommitPendingRestore()
	{
		Commits++;
		Armed = false;
	}

	public void CancelPendingRestore(string reason)
	{
		Cancels.Add(reason);
		Armed = false;
	}
}
