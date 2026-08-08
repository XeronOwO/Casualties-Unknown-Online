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

	public static void Bind(IPatchBridge impl) => _bound = impl;

	public static void Unbind(IPatchBridge impl)
	{
		if (ReferenceEquals(_bound, impl))
		{
			_bound = null;
		}
	}
}
