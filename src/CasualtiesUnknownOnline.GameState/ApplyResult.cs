namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// Result of applying a remote/replay committed batch on a non-authoritative or
/// replay side.
/// </summary>
public sealed record ApplyResult(bool Success, string? Error = null)
{
	public static ApplyResult Ok() => new(true);

	public static ApplyResult Failed(string error) => new(false, error);
}
