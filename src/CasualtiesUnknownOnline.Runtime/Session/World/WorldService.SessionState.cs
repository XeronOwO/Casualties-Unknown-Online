namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Session-ended resets for the world domain (partial split at the 600-line
/// gate): world-start params, the start gate and the block/entity registries
/// are session-scoped — a lobby switch or host exit must not leak any of them
/// into the next session. The host session survives a guest leaving, so
/// same-session reconnects keep their state.
/// </summary>
public sealed partial class WorldService
{
	/// <summary>
	/// Session ended (host exit, lobby switch, protocol mismatch): every
	/// world-level session state is void. WorldParams are re-published by the
	/// next host run; the damage table and registries are rebuilt by the next
	/// generation, so nothing of the old session may leak into the new one.
	/// </summary>
	public void ResetSessionState()
	{
		HostRunPending = false;
		_startGate = null;
		_startGateArmedMs = 0;
		_gateReleased = false;
		WorldParams = null;
		RadiationLineState = null;
		ResetDamagedBlocks();
	}

	private void OnSessionEnded() => ResetSessionState();

	public void Dispose() => _session.SessionEnded -= OnSessionEnded;
}
