namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// Result of replacing kernel state from a checkpoint.
/// </summary>
public sealed record RestoreResult(bool Success, string? Error = null)
{
	public static RestoreResult Ok() => new(true);

	public static RestoreResult Failed(string error) => new(false, error);
}
