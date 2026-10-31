namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The two registry spaces a relayed command request may name, carried on the wire as the request's <c>SourceKind</c>. They are separate on purpose: a mod's
/// own <c>IModCommands</c> API and its console command registry are different surfaces, so the same word may exist in both and the host must know which one
/// the member meant.
/// </summary>
internal static class CommandRelaySource
{
	/// <summary>The mod's own <c>IModCommands</c> API (the original host-command half).</summary>
	internal const int ModApi = 0;

	/// <summary>The console command registry, for a definition that declared <c>CommandScope.HostRelayed</c>.</summary>
	internal const int Console = 1;
}
