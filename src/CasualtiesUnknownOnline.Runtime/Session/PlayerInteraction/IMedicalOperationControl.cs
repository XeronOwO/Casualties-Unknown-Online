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

	/// <summary>Any role: request joining or starting the shared shrapnel session for a remote limb (guest → host on the wire; host handles locally).</summary>
	void SendShrapnelStartRequest(ulong targetSteamId, ulong itemInstanceId, int targetLimbIndex);

	/// <summary>Host only: a shrapnel session start/join request arrived.</summary>
	void HandleShrapnelStartRequest(ulong sender, MedicalOperationStartRequestMsg msg);

	/// <summary>Any role: send one shrapnel piece report (grab/move/release/break/remove) for an accepted shared session.</summary>
	void SendShrapnelUpdate(ulong operationId, ShrapnelPieceUpdate update);

	/// <summary>Host only: a shrapnel piece report arrived.</summary>
	void HandleShrapnelUpdate(ulong sender, MedicalOperationUpdateMsg msg);

	/// <summary>Any role: report that the local shrapnel minigame ended (operator leaves the shared session).</summary>
	void SendShrapnelEndRequest(ulong operationId);

	/// <summary>Host only: a shrapnel end/leave request arrived.</summary>
	void HandleShrapnelEndRequest(ulong sender, MedicalOperationEndRequestMsg msg);

	/// <summary>Any role: start a Stage 3 medical operation (bandage/removal/dislocation/defib/amputation).</summary>
	void SendOtherStartRequest(ulong targetSteamId, ulong itemInstanceId, int targetLimbIndex, MedicalOperationKind kind);

	/// <summary>Any role: send one Stage 3 semantic update for an accepted operation.</summary>
	void SendOtherUpdate(ulong operationId, MedicalOperationUpdateAction action, float value1 = 0f, float value2 = 0f, float value3 = 0f, bool flag1 = false);

	/// <summary>Any role: report the Stage 3 native minigame ended, with the final scalar (success/progress) where applicable.</summary>
	void SendOtherEndRequest(ulong operationId, float total = 0f);

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
