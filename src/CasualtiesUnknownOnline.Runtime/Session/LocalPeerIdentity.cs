namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The composition-bound identity value; see <see cref="ILocalPeerIdentity"/>.</summary>
public sealed record LocalPeerIdentity(string GameBuild, string CuoBuild, int SaveSchema) : ILocalPeerIdentity;
