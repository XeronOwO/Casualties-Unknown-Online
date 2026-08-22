using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The local mod UI surface (Phase 4 Mod API). A mod registers one or more
/// immediate-mode windows that CUO draws on the game's UI layer; the mod then
/// renders control calls through <see cref="IModUiWindow"/> each frame.
///
/// This surface is deliberately LOCAL-ONLY: it draws on the local client and
/// never touches network, session, or game-authoritative state, so every mod is
/// allowed to use it (there is no corresponding permission flag). A mod that
/// needs a shared UI state must coordinate through
/// <see cref="IModNetwork"/> or <see cref="IModCommands"/> and then project the
/// result into its local window.
/// </summary>
public interface IModUi
{
	/// <summary>
	/// Register a window for this mod. Returns false (with a framework log) when
	/// the id/title is empty, the draw handler is null, or the id is already
	/// registered by this mod. Register during <see cref="ICuoMod.Bind"/>.
	/// </summary>
	bool Register(string id, string title, Action<IModUiWindow> draw);

	/// <summary>Remove a previously registered window by id. Returns false when no such id exists.</summary>
	bool Unregister(string id);

	/// <summary>True when a window with this exact id is registered by this mod.</summary>
	bool IsRegistered(string id);

	/// <summary>A snapshot of the registered window ids for this mod (copy — safe to hold).</summary>
	IReadOnlyCollection<string> WindowIds { get; }
}
