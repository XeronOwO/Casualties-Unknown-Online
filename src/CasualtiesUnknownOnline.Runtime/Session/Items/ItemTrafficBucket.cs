namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>One item-label bucket in an <see cref="ItemTrafficWindow"/>.</summary>
internal sealed record ItemTrafficBucket(string ItemId, int Count);
