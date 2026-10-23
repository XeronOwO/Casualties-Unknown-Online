namespace CasualtiesUnknownOnline.BulkTransfer.Core;

/// <summary>The receiver's verdict on one chunk: taken, a duplicate it already had, or refused with its reason.</summary>
public enum BulkChunkVerdict
{
	/// <summary>The chunk was new and is now part of the file.</summary>
	Accepted,

	/// <summary>The chunk was already held; it is not applied twice.</summary>
	Duplicate,

	/// <summary>The sender announced this transfer with a different length, hash or chunk size.</summary>
	IdentityMismatch,

	/// <summary>The index is outside the announced file, or its length is not the announced slice's.</summary>
	Malformed,
}
