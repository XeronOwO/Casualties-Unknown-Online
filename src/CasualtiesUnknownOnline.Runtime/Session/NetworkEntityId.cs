using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Network entity ID per architecture.md §3: never use Unity instance IDs (they
/// are process-local). Composed of a session epoch (host-chosen, unique per
/// session), a host allocation counter, and a generation (bumped on respawn).
/// Phase 1 only has player entities, but the type is built complete now.
/// </summary>
public readonly struct NetworkEntityId(ulong epoch, uint counter, byte generation) : IEquatable<NetworkEntityId>
{
	public readonly ulong Epoch = epoch;
	public readonly uint Counter = counter;
	public readonly byte Generation = generation;

	public bool Equals(NetworkEntityId other) =>
		Epoch == other.Epoch && Counter == other.Counter && Generation == other.Generation;

	public override bool Equals(object? obj) => obj is NetworkEntityId other && Equals(other);

	// net48 has no System.HashCode — hand-rolled combine.
	public override int GetHashCode()
	{
		var hash = (int)(Epoch ^ (Epoch >> 32));
		hash = (hash * 397) ^ (int)Counter;
		return (hash * 397) ^ Generation;
	}

	public static bool operator ==(NetworkEntityId left, NetworkEntityId right) => left.Equals(right);

	public static bool operator !=(NetworkEntityId left, NetworkEntityId right) => !left.Equals(right);

	/// <summary>Domain → wire; the reverse lives on <see cref="NetworkEntityIdMsg"/>.</summary>
	public NetworkEntityIdMsg ToNetworkEntityIdMsg() => new(Epoch, Counter, Generation);

	public override string ToString() => $"{Epoch:X}:{Counter}:{Generation}";
}
