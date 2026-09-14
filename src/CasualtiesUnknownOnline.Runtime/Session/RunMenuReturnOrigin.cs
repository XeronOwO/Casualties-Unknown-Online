namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Who asked for the menu return — the record's second half, and what makes the
/// seam's staleness rule expressible: a request recorded by a session TEARDOWN
/// is dropped when a new session has taken over, while a request that IS the
/// player's own leave action — or the host's world ending under a guest — must
/// never be dropped while a world is leavable. Dropping those would consume the
/// action that asked for the leave and strand the player in the world.
/// </summary>
public enum RunMenuReturnOrigin
{
	/// <summary>A session teardown recorded the return: it belongs to the session that is ending.</summary>
	SessionTeardown = 0,

	/// <summary>The player's own "leave the world" action (the game's <c>PlayerCamera.ToMainMenu</c> call site).</summary>
	PlayerLeave = 1,

	/// <summary>The host left the world, so a guest's world is over and the guest is pulled back to the menu.</summary>
	HostPull = 2,
}
