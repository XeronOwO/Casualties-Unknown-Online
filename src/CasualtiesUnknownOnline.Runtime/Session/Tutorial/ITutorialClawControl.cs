using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Tutorial;

/// <summary>
/// The tutorial-claw stream surface packet handlers and the Game Adapter use —
/// implemented by <see cref="TutorialClawService"/>. The host publishes the
/// authoritative claw presentation state; the service broadcasts it at the
/// configured state-stream cadence; the guest applies received snapshots
/// (seq-gated) for its remote render driver.
/// </summary>
public interface ITutorialClawControl
{
	/// <summary>Host only: publish the latest tutorial-claw presentation state.</summary>
	void PublishTutorialClawState(TutorialClawStateMsg msg);

	/// <summary>Host only: clear the last published state (leaving the tutorial world).</summary>
	void ClearTutorialClawState();

	/// <summary>Guest only: apply an arrived tutorial-claw snapshot (seq-gated).</summary>
	void ApplyTutorialClawState(TutorialClawStateMsg msg);

	/// <summary>Raised after a guest applies a new tutorial-claw snapshot — the Game Adapter drives the local render claw.</summary>
	event Action<TutorialClawStateMsg>? TutorialClawStateReceived;
}
