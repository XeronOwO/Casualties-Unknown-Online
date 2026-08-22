using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// Local mod UI half of <see cref="ModService"/> (Phase 4 Mod API remainder).
/// Each mod gets a per-id registry of immediate-mode windows; the Unity plugin
/// reads <see cref="IModUiControl.Windows"/> and draws every window with its
/// own IMGUI bridge. The surface is local-only — no permission is required
/// because a window cannot touch network, session, or game-authoritative state.
/// </summary>
public sealed partial class ModService : IModUiControl
{
	public IReadOnlyList<ModUiWindow> Windows =>
		[.. _mods.SelectMany(m => m.Context.UiAdapter.Windows.Select(w =>
			new ModUiWindow(m.Manifest.Id, w.Id, w.Title, w.Draw)))];

	// ---- Per-mod UI adapter ----

	/// <summary>
	/// The per-mod UI registry: a tiny immediate-mode window list. Register
	/// failures are logged and refused (empty id/title, null draw handler,
	/// duplicate id); the mod id is scoped by construction because the adapter
	/// belongs to exactly one mod context.
	/// </summary>
	private sealed class ModUiAdapter(ModService owner, ModManifest manifest) : IModUi
	{
		private readonly List<ModUiRegistration> _windows = [];

		public bool Register(string id, string title, Action<IModUiWindow> draw)
		{
			if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) || draw is null)
			{
				owner._log.LogWarning("[Mods] {ModId} tried to register an invalid mod UI window (empty id/title or null draw) — refused.",
					manifest.Id);
				return false;
			}

			if (_windows.Any(w => w.Id == id))
			{
				owner._log.LogWarning("[Mods] {ModId}/{Id} is already registered as a UI window — the duplicate is refused.",
					manifest.Id, id);
				return false;
			}

			_windows.Add(new ModUiRegistration(id, title, draw));
			return true;
		}

		public bool Unregister(string id)
		{
			var index = _windows.FindIndex(w => w.Id == id);
			if (index < 0)
			{
				return false;
			}

			_windows.RemoveAt(index);
			return true;
		}

		public bool IsRegistered(string id) => _windows.Any(w => w.Id == id);

		public IReadOnlyCollection<string> WindowIds => [.. _windows.Select(w => w.Id)];

		internal IReadOnlyList<ModUiRegistration> Windows => _windows;

		internal sealed record ModUiRegistration(string Id, string Title, Action<IModUiWindow> Draw);
	}
}
