namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The one static seam Harmony patches are allowed to touch. Static patch
/// classes cannot receive constructor injection, so the DI-owned GameAdapter
/// binds this bridge once at construction; patches read the narrow
/// <see cref="IPatchBridge"/> surface instead of the service itself.
/// Bind/Unbind are the only writes and happen at construction/disposal.
/// </summary>
internal static class PatchBridge
{
	private static IPatchBridge? _bound;

	public static IPatchBridge? Impl => _bound;

	/// <summary>The mod-content half of the same bound bridge (template/drop-source/tile resolution) — a patch that needs only that surface reads it here rather than widening <see cref="IPatchBridge"/>.</summary>
	public static IModContentPatchBridge? ModContent => _bound?.ModContent;

	/// <summary>The session-surface half of the same bound bridge (CUO modal / non-modal ESC surfaces).</summary>
	public static ISessionSurfacePatchBridge? SessionSurface => _bound?.SessionSurface;

	public static void Bind(IPatchBridge impl) => _bound = impl;

	public static void Unbind(IPatchBridge impl)
	{
		if (ReferenceEquals(_bound, impl))
		{
			_bound = null;
		}
	}
}
