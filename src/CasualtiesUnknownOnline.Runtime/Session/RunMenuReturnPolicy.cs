namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Pure host/guest/solo decisions for the deliberate menu return. L0-testable so
/// neither the save-authority rule nor the "would the seam act" precondition can
/// be re-derived differently in the adapter — the scene-load interception and the
/// frame-end seam must agree, or a suppressed scene load would never be replayed.
/// </summary>
public static class RunMenuReturnPolicy
{
	public static RunMenuReturnMode Decide(SessionRole role, bool inWorld)
	{
		if (!inWorld)
		{
			return RunMenuReturnMode.None;
		}

		// Only a guest lacks the world-archive authority (decision 164). Solo play
		// carries NO session role (SessionRole.None — the enum's own contract), so
		// the rule is written against the guest rather than for the host: a solo
		// player leaving the world is its own save authority and takes the same
		// cut a host does. The save layer refuses the guest regardless (belt and
		// the seam's answer stays meaningful).
		return role == SessionRole.Guest ? RunMenuReturnMode.MenuOnly : RunMenuReturnMode.SaveAndMenu;
	}

	/// <summary>
	/// Would the frame-end seam act on a request of this shape right now?
	/// <c>true</c> = the seam WILL leave the world (taking the cut the mode asks
	/// for). Called by the scene-load interception BEFORE it suppresses the game's
	/// own load: an interception that suppresses a leave the seam then drops would
	/// consume the player's action and strand them in a world they asked to leave.
	///
	/// The session is deliberately NOT part of this question: only a TEARDOWN
	/// request is dropped for a session that took over (<see cref="DecideFlush"/>),
	/// and the interception only ever records the player's own leave.
	/// </summary>
	public static bool WouldLeaveWorld(
		RunMenuReturnMode mode,
		bool inWorld,
		bool cameraAvailable) =>
		mode != RunMenuReturnMode.None && inWorld && cameraAvailable;

	public static RunMenuReturnFlush DecideFlush(
		RunMenuReturnMode mode,
		RunMenuReturnOrigin origin,
		bool inWorld,
		bool sessionActive,
		bool cameraAvailable)
	{
		if (mode == RunMenuReturnMode.None)
		{
			return RunMenuReturnFlush.None;
		}

		// The world must be leavable: still in it (a request whose world went
		// without it is stale) and a camera to hand the scene load to. The state is
		// sampled by the seam ONE frame after the record — after generation can have
		// destroyed the world.
		if (!inWorld || !cameraAvailable)
		{
			return RunMenuReturnFlush.Clear;
		}

		// A live session only makes a TEARDOWN request stale: it belonged to the
		// session that is ending, so a session that took over in the meantime means
		// the teardown it described is over. A leave the PLAYER asked for — and a
		// guest's world ending because the host walked to the menu (the session is
		// still up then, which is exactly the pulled guest's case) — is never dropped
		// for that reason: dropping it would consume the action and strand the player
		// in a world they are done with.
		if (sessionActive && origin == RunMenuReturnOrigin.SessionTeardown)
		{
			return RunMenuReturnFlush.Clear;
		}

		return RunMenuReturnFlush.Leave;
	}
}
