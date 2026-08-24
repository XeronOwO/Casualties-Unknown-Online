namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 43; // v43: Nap variant + dog-shake intensity ride the 20 Hz player entity stream (EntityStateMsg.NapVariant / DogShakeIntensity)

}
