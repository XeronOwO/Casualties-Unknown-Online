namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// Idempotency key for one logical operation. Re-delivery of the same
/// OperationId returns the original decision and never commits twice.
/// </summary>
public readonly record struct OperationId(ulong Value);
