namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 18; // v18: CharacterSound (NetMsg 94) gains GunFire kind + RecoilDegrees — a v17 peer would silently miss the weapon recoil/shot event (presentation degradation, but the handshake refuses silent cross-version degradation by policy)
}
