using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The session's handshake list: the discovered mods as wire infos, each carrying
/// the fingerprint of the content ITS mod registered. Two leaves compose it — the
/// discovery registry, which owns the declared facts, and the content store, which
/// owns what those declarations produced — because the session builds a handshake
/// before, and without, the mod domain's facade, and a peer has to be able to
/// compare what a declaration produced rather than only what it declared.
/// </summary>
internal sealed class ModHandshakeListProvider(ModRegistry registry, IModContentFingerprints content) : IModListProvider
{
	/// <inheritdoc />
	public List<ModInfoMsg> CurrentModInfos()
	{
		var infos = registry.CurrentModInfos();
		var fingerprints = content.ByMod;
		foreach (var info in infos)
		{
			// Absent = this mod registered no content: the wire carries null, which the host
			// reads as "none" and never as "unknown" (see IModContentFingerprints).
			info.ContentFingerprint = fingerprints.TryGetValue(info.Id, out var fingerprint) ? fingerprint : null;
		}

		return infos;
	}
}
