using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One host-owned Stage 3 medical operation session. Unlike the shared
/// shrapnel session, these actions are exclusive to a single operator; the
/// shared reservation sets still arbitrate item/limb conflicts across all
/// medical operation kinds.
/// </summary>
internal sealed class OtherMedicalOperationSession
{
	internal ulong OperationId;
	internal ulong Operator;
	internal ulong Target;
	internal ulong ItemInstanceId;
	internal string ItemId = "";
	internal int LimbIndex = -1;
	internal MedicalOperationKind Kind;
	internal long StartedMs;
	internal long LastUpdateMs;
	internal int Sequence;

	/// <summary>Bandage consumed fraction or amputation cut progress (0..1).</summary>
	internal float Progress;

	/// <summary>Manual defib elapsed-drain already committed (seconds).</summary>
	internal float ManualDefibSeconds;

	internal bool AedStartDrained;
	internal bool AedAnalyzeDrained;
	internal bool AedShockApplied;

	/// <summary>Instance id of the item awarded by a removal, if any.</summary>
	internal ulong AwardedItemId;
}
