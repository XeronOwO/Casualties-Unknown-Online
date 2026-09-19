using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The shrapnel session's operator/item bookkeeping: who is in a session, which item each
/// operator committed, and the release of both when an operator leaves or the session ends.
/// It was four private methods of <see cref="ShrapnelOperationSessionService"/> whose only
/// inputs were the session object and the shared claim book, which is what makes it a
/// collaborator rather than part of the service's own state machine: the service decides WHEN
/// an operator joins or leaves (the answer from the target, an update, a timeout), this type
/// decides what that does to the session's operator table and to the claims.
/// </summary>
internal static class ShrapnelOperatorBookkeeping
{
	internal static void Join(
		ShrapnelOperationSession shrapnel,
		ulong operatorId,
		ulong itemInstanceId,
		CharacterDataMsg userData,
		MedicalOperationClaims claims,
		ShrapnelSessionStateWriter writer,
		ITimeSource time,
		ILogger log)
	{
		shrapnel.Operators.Add(operatorId);
		claims.TryReserveOperator(operatorId);
		if (itemInstanceId != 0)
		{
			claims.TryReserveItem(itemInstanceId);
			shrapnel.OperatorItems[operatorId] = itemInstanceId;
			writer.DrainTweezers(operatorId, itemInstanceId, userData);
		}

		shrapnel.LastUpdateMs = time.NowMs;
		log.LogInformation("[Shrapnel] operator {Operator} joined session {OperationId}.", operatorId, shrapnel.OperationId);
	}

	/// <summary>Frees every piece the operator was holding (the pieces stay in the session — they are the limb's, not the operator's).</summary>
	internal static void ReleasePieces(ShrapnelOperationSession shrapnel, ulong operatorId)
	{
		foreach (var piece in shrapnel.Pieces.Values)
		{
			if (piece.Owner == operatorId)
			{
				piece.Owner = 0;
			}
		}
	}

	internal static void ReleaseItem(ShrapnelOperationSession shrapnel, ulong operatorId, MedicalOperationClaims claims)
	{
		if (shrapnel.OperatorItems.TryGetValue(operatorId, out var itemId))
		{
			shrapnel.OperatorItems.Remove(operatorId);
			claims.ReleaseItem(itemId);
		}
	}

	internal static void ReleaseItems(ShrapnelOperationSession shrapnel, MedicalOperationClaims claims)
	{
		foreach (var itemId in shrapnel.OperatorItems.Values)
		{
			claims.ReleaseItem(itemId);
		}

		shrapnel.OperatorItems.Clear();
	}
}
