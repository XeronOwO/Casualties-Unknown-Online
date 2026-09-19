using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: the two absolute run/layer facts the game keeps OUTSIDE the
/// kernel's run baseline, read off the host's live world at one instant
/// (world entry and the 60 s repair group, in both cases right after the kernel
/// checkpoint that establishes the generation stamp this message is compared
/// against).
///
/// <see cref="RunClockBase"/> is the total run time the game itself would put in
/// its save slot: <c>SaveSystem.savedRunTime + WorldGeneration.world.realTimeElapsed</c>
/// (<c>SaveSystem.cs:165</c>), which is what <c>WorldGeneration.TotalRunTime()</c>
/// adds the current layer's elapsed time to (<c>WorldGeneration.cs:177-179</c>).
/// Without it a member that joined a run already in progress reads only the time
/// since it joined (its own static is 0 on a fresh launch), while the host reads
/// the run's total — the end screen's death-stats clock is the game's only
/// reader.
///
/// <see cref="LayerTimeSpent"/> / <see cref="MaxTimePerLayer"/> are the layer's
/// radiation-timer accounting (<c>WorldGeneration.cs:860-861</c>: the line
/// activates once <c>layerTimeSpent</c> passes <c>maxTimePerLayer</c>, which
/// <c>WorldGeneration.Start</c> derives from the run's <c>timelimit</c> setting
/// at <c>:258</c>). They are per LAYER, not per run, so they also travel here
/// rather than in the archive-shaped run facts.
///
/// <see cref="RunEpoch"/> / <see cref="LayerIndex"/> are the kernel run
/// baseline's own generation identity (<c>WorldReportGeneration</c>), the same
/// stamp the layer-relative world reports carry: the receiver REFUSES a message
/// whose stamp is not its own current generation, so a value captured in a
/// previous layer can never be written onto a freshly generated one.
///
/// Every member stays absolute: the receiver applies the clock only when it
/// ADVANCES, so a late duplicate can never rewind a clock that has been running,
/// and a layer time is applied as "the layer started no later than this".
/// </summary>
[ProtoContract]
public sealed class RunFactsMsg
{
	/// <summary>The kernel run baseline's epoch when these values were read.</summary>
	[ProtoMember(1)]
	public ulong RunEpoch { get; set; }

	/// <summary>The kernel run baseline's layer index when these values were read.</summary>
	[ProtoMember(2)]
	public int LayerIndex { get; set; }

	/// <summary>The host's total run time in seconds (<c>savedRunTime + realTimeElapsed</c>).</summary>
	[ProtoMember(3)]
	public float RunClockBase { get; set; }

	/// <summary>The host's <c>WorldGeneration.world.layerTimeSpent</c>, in seconds.</summary>
	[ProtoMember(4)]
	public float LayerTimeSpent { get; set; }

	/// <summary>The host's <c>WorldGeneration.world.maxTimePerLayer</c>, in seconds.</summary>
	[ProtoMember(5)]
	public float MaxTimePerLayer { get; set; }
}
