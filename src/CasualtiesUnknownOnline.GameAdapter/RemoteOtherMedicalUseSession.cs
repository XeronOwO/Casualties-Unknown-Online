using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// One active Stage 3 remote-medical session on the operator side. It tracks
/// the native minigame, pending pre-ack semantic reports and the authoritative
/// item-condition markers used for cancel/restore decisions.
/// </summary>
internal sealed class RemoteOtherUseSession
{
	internal RemoteOtherUseSession(ulong target, ulong itemInstanceId, ulong localSteamId)
	{
		Target = target;
		ItemInstanceId = itemInstanceId;
		LocalSteamId = localSteamId;
	}

	internal ulong Target { get; }
	internal ulong ItemInstanceId { get; }
	internal ulong LocalSteamId { get; }
	internal ulong OperationId { get; set; }
	internal MedicalOperationKind Kind { get; init; }
	internal int LimbIndex { get; init; }
	internal Item? Item { get; init; }
	internal bool HasItem { get; init; }
	internal float OriginalCondition { get; init; }
	internal Minigame? Minigame { get; init; }
	internal bool Removal { get; init; }
	internal bool Wrench { get; init; }
	internal bool EngineEnded { get; set; }
	internal bool CancelledBeforeAck { get; set; }
	internal bool DislocationSucceeded { get; set; }
	internal float Progress { get; set; }
	internal int PendingWraps { get; set; }
	internal int PendingHits { get; set; }
	internal float PendingCut { get; set; }
	internal int PendingAedStart { get; set; }
	internal int PendingAedAnalyze { get; set; }
	internal int PendingAedShock { get; set; }
	internal float PendingManualShock { get; set; }
}
