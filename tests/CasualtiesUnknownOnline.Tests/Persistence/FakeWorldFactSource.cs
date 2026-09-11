using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// In-memory stand-in for the host's world-fact tables. It records how it was
/// used, so the save suites can prove the ORDER the restore depends on — reset
/// first, then an absolute apply — instead of only checking the final values.
/// </summary>
internal sealed class FakeWorldFactSource : IWorldFactSource
{
	private readonly List<BlockStateEntryMsg> _blocks = [];
	private readonly List<BlockDamageEntryMsg> _damages = [];

	/// <summary>Every call this source received, in order (<c>capture-blocks</c>, <c>apply</c>, …).</summary>
	internal List<string> Calls { get; } = [];

	internal RadiationLineStateMsg? RadiationLine { get; set; }

	/// <summary>The tables as they stand (after any apply or reset).</summary>
	internal IReadOnlyList<BlockStateEntryMsg> Blocks => _blocks;

	internal IReadOnlyList<BlockDamageEntryMsg> Damages => _damages;

	/// <summary>The bounded tables' verdict this fake reports: rows the caller should see refused (the production caps).</summary>
	internal int RefusedBlockStates { get; set; }

	internal int RefusedBlockDamages { get; set; }

	/// <summary>Set by <see cref="ApplyFacts"/> when the cut carried a fact, cleared by <see cref="ClearPendingLiveReplay"/>.</summary>
	public bool HasPendingLiveReplay { get; private set; }

	internal void SeedBlockState(int x, int y, ushort block) => _blocks.Add(new BlockStateEntryMsg { X = x, Y = y, Block = block });

	internal void SeedBlockDamage(int x, int y, float damage) => _damages.Add(new BlockDamageEntryMsg { X = x, Y = y, Damage = damage });

	public IReadOnlyList<BlockStateEntryMsg> CaptureBlockStates()
	{
		Calls.Add("capture-blocks");
		return [.. _blocks];
	}

	public IReadOnlyList<BlockDamageEntryMsg> CaptureBlockDamages()
	{
		Calls.Add("capture-damages");
		return [.. _damages];
	}

	public RadiationLineStateMsg? CaptureRadiationLine()
	{
		Calls.Add("capture-radiation");
		return RadiationLine;
	}

	public WorldFactApplyReport ApplyFacts(
		IReadOnlyList<BlockStateEntryMsg> blockStates,
		IReadOnlyList<BlockDamageEntryMsg> blockDamages,
		RadiationLineStateMsg? radiationLine)
	{
		// The port's contract is an absolute replace, and the fake models it as ONE
		// call: the production lifecycle resets through WorldStateMessageService
		// before applying, which is exactly the behavior under test.
		Calls.Add("apply");
		_blocks.Clear();
		_blocks.AddRange(blockStates);
		_damages.Clear();
		_damages.AddRange(blockDamages);
		RadiationLine = radiationLine;

		// The pending flag mirrors the production lifecycle: a cut that carried a
		// fact owns the next generation's cache state.
		HasPendingLiveReplay = blockStates.Count > 0 || blockDamages.Count > 0 || radiationLine is not null;
		return new WorldFactApplyReport(
			blockStates.Count - RefusedBlockStates, RefusedBlockStates,
			blockDamages.Count - RefusedBlockDamages, RefusedBlockDamages,
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
