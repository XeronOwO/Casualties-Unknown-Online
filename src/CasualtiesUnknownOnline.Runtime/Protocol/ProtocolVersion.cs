namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 30; // v30: DynamiteExplosionMsg (NetMsg 105) — player-item explosion replay; v29 peers do not apply the body/visual segment of a remote dynamite blast

}
