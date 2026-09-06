using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One host-owned shared shrapnel session. Unlike the single-operator
/// injection session, this object represents a whole target limb and allows
/// multiple operators to join; per-piece ownership state lives in
/// <see cref="ShrapnelPieceState"/>.
/// </summary>
internal sealed class ShrapnelOperationSession
{
	internal ulong OperationId;
	internal ulong Target;
	internal int LimbIndex;
	internal int Sequence;
	internal long LastUpdateMs;
	internal readonly HashSet<ulong> Operators = [];
	internal readonly Dictionary<ulong, ulong> OperatorItems = [];
	internal readonly Dictionary<int, ShrapnelPieceState> Pieces = [];

	internal bool AllPiecesRemoved => Pieces.Values.All(p => p.Removed);
}
