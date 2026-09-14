namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// What the frame-end seam does with a pending menu-return request.
/// </summary>
public enum RunMenuReturnFlush
{
	/// <summary>Nothing is pending.</summary>
	None = 0,

	/// <summary>The request is done with and the world is NOT leavable (already gone, or a teardown a new session superseded): drop it. The armed-cut path may still run this frame.</summary>
	Clear = 1,

	/// <summary>Leave the world now — after the seam has taken the cut the mode asks for.</summary>
	Leave = 2,
}
