namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Opaque handle returned by <see cref="NativeOperationCoordinator.Begin"/>.
/// The handle owns no state; the coordinator keeps the operation by token.
/// </summary>
public readonly record struct NativeOperationHandle(ulong Token);
