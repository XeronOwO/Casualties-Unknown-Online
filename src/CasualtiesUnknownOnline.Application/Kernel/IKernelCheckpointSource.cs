using CasualtiesUnknownOnline.GameState;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The host's checkpoint production surface plus the two identities a peer
/// validates a frame against: the global revision a batch sequence must follow
/// and the run epoch a peer must be serving. Read-only by design — producing a
/// checkpoint does not change the kernel.
/// </summary>
public interface IKernelCheckpointSource
{
	/// <summary>Snapshot the kernel's current state.</summary>
	GameCheckpoint CreateCheckpoint();

	/// <summary>The revision every currently committed fact sits at.</summary>
	ulong CurrentGlobalRevision { get; }

	/// <summary>The run identity this side is serving.</summary>
	RunEpoch CurrentRunEpoch { get; }
}
