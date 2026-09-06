using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The remote medical operation control surface. It is intentionally generic:
/// start/update/end/cancel plus host-to-client state/terminal events carry the
/// per-operation payload, so later stages (shrapnel, bandage, splint removal)
/// can reuse the same session envelope.
/// </summary>
public interface IMedicalOperationControl
{
	/// <summary>Any role: request a medical operation (guest → host on the wire; host handles locally).</summary>
	void SendStartRequest(ulong targetSteamId, ulong itemInstanceId, int targetLimbIndex);

	/// <summary>Host only: a medical-operation start request arrived.</summary>
	void HandleStartRequest(ulong sender, MedicalOperationStartRequestMsg msg);

	/// <summary>Any role: send one incremental ml delta for an accepted session.</summary>
	void SendUpdate(ulong operationId, float deltaMl);

	/// <summary>Host only: a medical-operation incremental update arrived.</summary>
	void HandleUpdate(ulong sender, MedicalOperationUpdateMsg msg);

	/// <summary>Any role: report that the native minigame ended, with the exact total delivered ml.</summary>
	void SendEndRequest(ulong operationId, float totalMl);

	/// <summary>Host only: a medical-operation end request arrived.</summary>
	void HandleEndRequest(ulong sender, MedicalOperationEndRequestMsg msg);

	/// <summary>Any role: cancel an active medical operation (committed progress stays).</summary>
	void SendCancelRequest(ulong operationId);

	/// <summary>Host only: a medical-operation cancel request arrived.</summary>
	void HandleCancelRequest(ulong sender, MedicalOperationCancelMsg msg);

	/// <summary>Raise a received start ack for the Game Adapter.</summary>
	void FireStartAckReceived(MedicalOperationStartAckMsg msg);

	/// <summary>Raise a received non-terminal progress state for the Game Adapter.</summary>
	void FireStateReceived(MedicalOperationStateMsg msg);

	/// <summary>Raise the single received terminal result for the Game Adapter.</summary>
	void FireEndCommittedReceived(MedicalOperationEndCommittedMsg msg);

	event Action<MedicalOperationStartAckMsg>? StartAckReceived;

	event Action<MedicalOperationStateMsg>? StateReceived;

	event Action<MedicalOperationEndCommittedMsg>? EndCommittedReceived;
}
