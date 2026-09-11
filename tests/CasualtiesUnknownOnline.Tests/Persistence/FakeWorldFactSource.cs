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

	public void ApplyFacts(
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
	}

	/// <summary>The index of the first call with this name, or -1.</summary>
	internal int IndexOf(string call) => Calls.IndexOf(call);
}
