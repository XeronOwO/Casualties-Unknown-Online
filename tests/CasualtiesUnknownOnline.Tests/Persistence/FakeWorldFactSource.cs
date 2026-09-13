using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// In-memory stand-in for the host's Runtime-owned world-fact tables. It records
/// how it was used, so the save suites can prove the ORDER the restore depends
/// on — reset first, then an absolute apply — instead of only checking the final
/// values. The GAME's own tables are the other half of the seam and have their
/// own fake (<see cref="FakeNativeWorldFacts"/>); the partial block damage lives
/// only there.
/// </summary>
internal sealed class FakeWorldFactSource : IWorldFactSource
{
	private readonly List<BlockStateEntryMsg> _blocks = [];

	/// <summary>Every call this source received, in order (<c>capture-blocks</c>, <c>apply</c>, …).</summary>
	internal List<string> Calls { get; } = [];

	internal RadiationLineStateMsg? RadiationLine { get; set; }

	/// <summary>The table as it stands (after any apply or reset).</summary>
	internal IReadOnlyList<BlockStateEntryMsg> Blocks => _blocks;

	/// <summary>The bounded table's verdict this fake reports: rows the caller should see refused (the production cap).</summary>
	internal int RefusedBlockStates { get; set; }

	/// <summary>Set by <see cref="ApplyFacts"/> when the cut carried a fact, cleared by <see cref="ClearPendingLiveReplay"/>.</summary>
	public bool HasPendingLiveReplay { get; private set; }

	/// <summary>The restore attempt the tables were last applied by (stamped on every apply, armed or not).</summary>
	public ulong AppliedRestoreSequence { get; private set; }

	internal void SeedBlockState(int x, int y, ushort block) => _blocks.Add(new BlockStateEntryMsg { X = x, Y = y, Block = block });

	public IReadOnlyList<BlockStateEntryMsg> CaptureBlockStates()
	{
		Calls.Add("capture-blocks");
		return [.. _blocks];
	}

	public RadiationLineStateMsg? CaptureRadiationLine()
	{
		Calls.Add("capture-radiation");
		return RadiationLine;
	}

	public WorldFactApplyReport ApplyFacts(
		IReadOnlyList<BlockStateEntryMsg> blockStates,
		RadiationLineStateMsg? radiationLine,
		ulong restoreSequence)
	{
		// The port's contract is an absolute replace, and the fake models it as ONE
		// call: the production lifecycle resets through WorldStateMessageService
		// before applying, which is exactly the behavior under test.
		Calls.Add("apply");
		AppliedRestoreSequence = restoreSequence;
		_blocks.Clear();
		_blocks.AddRange(blockStates);
		RadiationLine = radiationLine;

		// The pending flag mirrors the production lifecycle: a cut that carried a
		// fact owns the next generation's cache state.
		HasPendingLiveReplay = blockStates.Count > 0 || radiationLine is not null;
		return new WorldFactApplyReport(
			blockStates.Count - RefusedBlockStates, RefusedBlockStates,
			radiationLine is not null);
	}

	public void ClearPendingLiveReplay()
	{
		if (!HasPendingLiveReplay)
		{
			// Mirrors the production lifecycle: a clear with nothing pending is a
			// no-op that records nothing (the layer advance / run start calls it
			// unconditionally).
			return;
		}

		Calls.Add("clear-pending-replay");
		HasPendingLiveReplay = false;
	}

	/// <summary>The index of the first call with this name, or -1.</summary>
	internal int IndexOf(string call) => Calls.IndexOf(call);
}
