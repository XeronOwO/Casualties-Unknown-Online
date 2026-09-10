namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>How a loader must treat the manifest's <c>files</c> digests (§3.2).</summary>
public enum ChecksumPolicy
{
	/// <summary>A file whose digest does not match is reported as damaged (§6).</summary>
	Required,

	/// <summary>Digests are recorded but not enforced (reserved for a future import path).</summary>
	None,
}
