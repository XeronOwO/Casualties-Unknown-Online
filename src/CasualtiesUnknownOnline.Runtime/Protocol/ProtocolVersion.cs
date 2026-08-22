namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 31; // v31: EntityStateMsg carries LookTarget override gaze + eye-scare presentation; v30 peers cannot render a remote player's enemy gaze/scared face

}
