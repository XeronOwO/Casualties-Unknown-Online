namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The three answers a definition's native-use head can give
/// (<see cref="IKernelItemUseFacts.NativeUse"/>). The middle value is not a failure
/// of the caller: it is the honest "this host holds no fact about that definition",
/// which the seam must not read as either verdict.
/// </summary>
public enum ItemUsePermission
{
	/// <summary>The item's own data says a native use runs: <c>ItemInfo.usable</c> is set.</summary>
	Permitted,

	/// <summary>The item's own data says no native use exists: <c>ItemInfo.usable</c> is clear, so a use report about it is impossible.</summary>
	Forbidden,

	/// <summary>This host cannot answer for that definition — no registry, or an id it does not carry. Nothing to refuse on.</summary>
	Unknown,
}
