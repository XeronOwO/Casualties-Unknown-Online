using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The Runtime half of the cut's transient observation: the in-flight state the
/// CUO services hold at the cut instant, in <see cref="WorldTransientPolicy"/>'s
/// own vocabulary. It COUNTS — the verdicts and the decision they drive live in
/// the policy and <see cref="WorldSaveService"/>.
///
/// The Game Adapter adds its half at the seam (the block-break report, the trap
/// drop hold and the drop flush are live-world windows only it holds), so the
/// two halves meet as one list of counts handed to the cut.
///
/// Every property read here is a read-only view of state its owner already
/// keeps: the probe is a query, never a second copy (a shadow table beside its
/// owner is exactly the drift the block-damage decision removed).
/// </summary>
public sealed class WorldCutTransientProbe(
	KernelProtocolService protocol,
	IMedicalOperationControl medical,
	RuntimeEntityChannel entities) : IWorldCutTransientProbe
{
	private readonly KernelProtocolService _protocol = protocol;
	private readonly IMedicalOperationControl _medical = medical;
	private readonly RuntimeEntityChannel _entities = entities;

	/// <summary>The Runtime-owned in-flight classes, zero-count rows included (the policy decides what a zero means).</summary>
	public IReadOnlyList<WorldTransientCount> Capture()
	{
		var sessions = _medical.PendingCutSessions;
		return
		[
			new(WorldTransientPolicy.PickupQueueKey, _protocol.PendingPickupCount),
			new(WorldTransientPolicy.MedicalSessionKey, sessions.Medical),
			new(WorldTransientPolicy.ShrapnelSessionKey, sessions.Shrapnel),
			new(WorldTransientPolicy.OtherMedicalSessionKey, sessions.Other),
			new(WorldTransientPolicy.DeferredEntityReportKey, _entities.PendingEntityReportCount),
		];
	}
}
