namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>One piece slot in a shared shrapnel session.</summary>
internal sealed class ShrapnelPieceState
{
	internal int PieceIndex;
	internal float X;
	internal float Y;
	internal ulong Owner;
	internal long LeaseExpiryMs;
	internal bool Removed;
}
