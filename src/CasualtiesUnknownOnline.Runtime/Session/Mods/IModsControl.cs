using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The sixth control surface (alongside Session/Entities/CharacterData/World/
/// Items — the same abstract-extraction pattern): what the packet handlers and
/// the handshake need from the mod domain. Handlers keep their zero-constructor-
/// dependency contract; the mod domain is reached through this.
/// </summary>
public interface IModsControl
{
	/// <summary>A mod frame arrived — route it to the local mod with that id (unknown ids are dropped with a log).</summary>
	void FireModMessageReceived(ulong sender, ModMessageMsg msg);

	/// <summary>A guest's host-command request arrived — validate and execute on the host's mod copy (Phase 4b).</summary>
	void FireModCommandRequestReceived(ulong sender, ModCommandRequestMsg msg);

	/// <summary>A host's command result arrived — settle the guest's pending callback by ModId + RequestId.</summary>
	void FireModCommandResultReceived(ulong sender, ModCommandResultMsg msg);

	/// <summary>The discovered mods (empty before discovery ran — a "pending" handshake check refuses until this flips).</summary>
	IReadOnlyList<ModManifest> CurrentModManifests { get; }

	/// <summary>True once the first-frame discovery scan completed.</summary>
	bool IsDiscoveryComplete { get; }
}
