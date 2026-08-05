namespace CasualtiesUnknownOnline.Core.Logging;

/// <summary>
/// Logging abstraction so CUO Core stays host-independent (BepInEx today,
/// dedicated server later). The plugin entry provides the implementation.
/// </summary>
public interface ILogger
{
	void Info(string message);

	void Warning(string message);

	void Error(string message);
}
