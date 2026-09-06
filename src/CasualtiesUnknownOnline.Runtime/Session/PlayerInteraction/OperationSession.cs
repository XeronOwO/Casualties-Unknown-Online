using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One active host-side medical operation session. It is the owner of the
/// operation's identity, reservations and committed progress; the
/// <see cref="MedicalOperationSessionService"/> owns the registry and lifecycle,
/// and <see cref="MedicalOperationInjectionApplier"/> owns the pure delta/terminal
/// application.
/// </summary>
internal sealed class OperationSession
{
	internal ulong OperationId;
	internal ulong Operator;
	internal ulong Target;
	internal ulong ItemInstanceId;
	internal string ItemId = "";
	internal int LimbIndex = -1;
	internal MedicalOperationKind Kind;
	internal float AvailableMl;
	internal float CommittedMl;
	internal int Sequence;
	internal long LastUpdateMs;
	internal List<LiquidStackMsg> OriginalLiquids = [];
}
