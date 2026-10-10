using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The materialized content set as a value another layer can compare: one
/// fingerprint per mod for the handshake, one for the whole registry for a cut's
/// manifest. It is a separate surface from <see cref="IModContentControl"/> on
/// purpose — its two readers are the session's handshake list and the save layer,
/// and neither may resolve the mod domain's facade (the facade depends on the
/// session), so what they read is a value rather than the registry itself.
/// </summary>
public interface IModContentFingerprints
{
	/// <summary>
	/// The fingerprint of EVERY registered entry of every mod — the value a cut records
	/// as <c>contentFingerprint</c> and a load compares against the live set. Defined for
	/// an empty set too: a process that registered no content still has a value, so
	/// "unknown" (the empty string an older manifest carries) stays distinct from
	/// "nothing was registered".
	/// </summary>
	string Fingerprint { get; }

	/// <summary>
	/// The fingerprint of each mod that registered content, by mod id, computed in ONE
	/// pass over the registry. A mod that registered NOTHING is absent, and a reader must
	/// read that as "no content" rather than as "unknown" — that is the wire's own
	/// encoding (a null <c>ModInfoMsg.ContentFingerprint</c>), so two peers agree on what
	/// a content-less mod carries.
	/// </summary>
	IReadOnlyDictionary<string, string> ByMod { get; }
}
