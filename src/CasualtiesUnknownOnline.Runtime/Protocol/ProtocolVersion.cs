namespace CasualtiesUnknownOnline.Runtime.Protocol;

public static class ProtocolVersion
{
	/// <summary>Bumped on any breaking wire change.</summary>
	public const int Current = 37; // v37: TraderRecruitResult carries bonus trader-stock items — a v36 peer would revive but not receive the gift

}
