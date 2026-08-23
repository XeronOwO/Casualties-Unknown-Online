namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 35; // v35: TraderRecruitRequest/TraderRecruitResult (NetMsg 107/108) — a v34 peer has no trader-recruit revive flow

}
