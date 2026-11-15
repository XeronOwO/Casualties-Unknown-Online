namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>One present peer as the reconnect adoption sees it.</summary>
internal sealed record ReconnectCandidate(ulong PeerId, string DisplayName, bool Handshaken, bool InWorld);
