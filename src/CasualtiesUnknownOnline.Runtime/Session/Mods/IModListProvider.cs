using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The narrow surface the session needs from the mod domain: the currently
/// discovered mods as wire infos, to fill the handshake (HandshakeMsg.Mods —
/// Phase 4 Mod API consistency check). Implemented by <see cref="ModRegistry"/>.
/// The session depends on this interface, never on the registry itself — the
/// same abstract-extraction pattern as the other control surfaces (the
/// dependency graph stays one-way).
/// </summary>
public interface IModListProvider
{
	/// <summary>
	/// The discovered mods as handshake infos (empty while discovery has not
	/// run yet — a guest's first handshake may carry an empty list and the 1 s
	/// retry then carries the real one).
	/// </summary>
	List<ModInfoMsg> CurrentModInfos();
}
