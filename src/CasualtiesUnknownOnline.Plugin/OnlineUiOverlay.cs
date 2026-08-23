using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Chat;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Steam;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The Online UI overlay (IMGUI — the same low-ceremony surface the Phase-1
/// HUD used, now split out so Plugin.cs stays a thin lifecycle driver). It
/// owns the lobby create/join panel, the per-member status list, and the
/// world-space nameplates / off-screen arrows. Pure geometry lives in
/// <see cref="OffScreenArrowGeometry"/> (Runtime) so the edge math is covered
/// by L0 tests; this class only projects Unity state and draws.
/// </summary>
internal sealed class OnlineUiOverlay
{
	/// <summary>Invoked when the user clicks Join with a numeric lobby id.</summary>
	internal Func<string, bool>? JoinLobby;

	/// <summary>Invoked when the user clicks Create Lobby.</summary>
	internal Func<bool>? CreateLobby;

	/// <summary>Invoked when the user clicks Take on one of a remote player's inventory lines.</summary>
	internal Func<ulong, ulong, bool>? TakeItem;

	/// <summary>Invoked when the user clicks Carry on an unconscious/dead remote player.</summary>
	internal Func<ulong, bool>? CarryRemote;

	/// <summary>Invoked when the user clicks Drop on the currently carried remote player.</summary>
	internal Func<ulong, bool>? DropCarried;

	/// <summary>Invoked when the user clicks Heal on an in-world remote player.</summary>
	internal Func<ulong, bool>? HealRemote;

	/// <summary>Read-only UI check: does the local body currently carry a heal-profile medical item?</summary>
	internal Func<bool>? HasHealItem;

	/// <summary>Invoked when the user clicks one of the explicit local heal items (instance id must be non-zero).</summary>
	internal Func<ulong, ulong, bool>? HealWithItem;

	/// <summary>Explicit local heal items for the Online UI selector (slot items with wire ids only).</summary>
	internal Func<IReadOnlyList<LocalHealItem>>? GetLocalHealItems;

	/// <summary>Invoked when the user clicks Recruit on a dead in-world teammate (trader-recruit co-op revive).</summary>
	internal Func<ulong, bool>? RecruitPlayer;

	/// <summary>Invoked when the host clicks Kick on a non-local lobby member.</summary>
	internal Func<ulong, bool>? KickMember;

	/// <summary>Invoked when the host clicks Ban on a non-local lobby member.</summary>
	internal Func<ulong, bool>? BanMember;

	private string _lobbyIdInput = "";
	private string _chatInput = "";
	private string? _inlineError;

	internal void Draw(SteamService steam, SessionService session, EntitySyncService entities, RemoteVitalsService vitals, RemoteInventoryService inventory, IPlayerInteractionControl playerInteraction, IChatControl chat, string? lastJoinError)
	{
		DrawStatusPanel(steam, session, entities, vitals, inventory, playerInteraction, lastJoinError ?? _inlineError);
		DrawNameplatesAndArrows(steam, session, entities, vitals);
		DrawChatPanel(steam, session, chat);
	}

	private void DrawStatusPanel(SteamService steam, SessionService session, EntitySyncService entities, RemoteVitalsService vitals, RemoteInventoryService inventory, IPlayerInteractionControl playerInteraction, string? error)
	{
		var y = 10f;
		Line("CUO — Steam: " + (steam.IsInitialized ? "initialized" : "not initialized"));
		if (steam.IsInitialized)
		{
			Line($"SteamID: {steam.LocalSteamId}  Persona: {DisplayName(steam, steam.LocalSteamId)}");
			Line($"Lobby: {steam.CurrentLobbyId}  Members: {steam.GetLobbyMembers().Length}");
		}

		var role = session.Role == SessionRole.Host ? "HOST"
			: session.Role == SessionRole.Guest ? "GUEST"
			: "—";
		Line($"Session: {role}  handshake: {(session.SessionActive ? "yes" : "no")}  "
			+ $"entity sync: {(entities.EntitySyncActive ? "ON" : "off")}");
		Line(session.LastRttMs >= 0f ? $"Last RTT: {session.LastRttMs:F1} ms" : "No ping yet");

		y = DrawLobbyControls(y);
		y = DrawMemberStatus(steam, session, vitals, inventory, playerInteraction, y);

		if (!string.IsNullOrEmpty(error))
		{
			Line(error!);
		}

		Line("Hotkeys: F8 create lobby / F9 join from config / F7 ping peer");

		void Line(string text)
		{
			GUI.Label(new Rect(10f, y, 900f, 20f), text);
			y += 20f;
		}
	}

	/// <summary>
	/// Small bottom-right text-chat panel. It is intentionally IMGUI-simple:
	/// a bounded recent-line list and one input + Send button. The Runtime
	/// ChatService owns the buffer and the wire send; the overlay only projects
	/// persona names for display.
	/// </summary>
	private void DrawChatPanel(SteamService steam, SessionService session, IChatControl chat)
	{
		if (!session.SessionActive)
		{
			return;
		}

		const float width = 360f;
		const float height = 180f;
		var x = Screen.width - width - 12f;
		var y = Screen.height - height - 12f;
		GUI.Box(new Rect(x, y, width, height), GUIContent.none);

		const int maxVisible = 7;
		var lines = chat.Recent;
		var start = Math.Max(0, lines.Count - maxVisible);
		var lineY = y + 8f;
		for (var i = start; i < lines.Count; i++)
		{
			var line = lines[i];
			var name = DisplayName(steam, line.SenderSteamId);
			GUI.Label(new Rect(x + 8f, lineY, width - 16f, 18f), $"{name}: {line.Text}");
			lineY += 18f;
		}

		var inputY = y + height - 30f;
		_chatInput = GUI.TextField(new Rect(x + 8f, inputY, width - 70f, 22f), _chatInput, 200);
		if (GUI.Button(new Rect(x + width - 58f, inputY, 50f, 22f), "Send"))
		{
			if (chat.TrySend(_chatInput))
			{
				_chatInput = "";
			}
		}
	}

	private float DrawLobbyControls(float y)
	{
		GUI.Label(new Rect(10f, y, 90f, 22f), "Lobby ID:");
		_lobbyIdInput = GUI.TextField(new Rect(100f, y, 180f, 22f), _lobbyIdInput, 20);

		if (GUI.Button(new Rect(290f, y, 70f, 22f), "Join"))
		{
			var trimmed = _lobbyIdInput.Trim();
			if (ulong.TryParse(trimmed, out _))
			{
				_inlineError = null;
				JoinLobby?.Invoke(trimmed);
			}
			else
			{
				_inlineError = "Lobby ID must be a number.";
			}
		}

		if (GUI.Button(new Rect(370f, y, 80f, 22f), "Create"))
		{
			_inlineError = null;
			CreateLobby?.Invoke();
		}

		return y + 26f;
	}

	private float DrawMemberStatus(SteamService steam, SessionService session, RemoteVitalsService vitals, RemoteInventoryService inventory, IPlayerInteractionControl playerInteraction, float y)
	{
		if (steam.CurrentLobbyId == 0)
		{
			return y;
		}

		foreach (var lobbyMember in steam.GetLobbyMembers())
		{
			var name = DisplayName(steam, lobbyMember);
			var isHost = lobbyMember == steam.GetLobbyOwner();
			var member = session.Members.FirstOrDefault(m => m.SteamId == lobbyMember);
			var status = member is null ? "not handshaken"
				: (member.Handshaken ? "handshake" : "no handshake")
				+ (member.InWorld ? ", in world" : ", menu");
			var vitalsText = member is { InWorld: true } && vitals.TryGet(lobbyMember, out var snapshot)
				? $" — {snapshot.ToShortString()}"
				: "";
			var inventoryText = member is { InWorld: true } && inventory.TryGet(lobbyMember, out _)
				? " — items"
				: "";
			var rttText = member is { RttMs: >= 0f } ? $", {member.RttMs:F0} ms" : "";
			var rowY = y;
			GUI.Label(new Rect(10f, rowY, 900f, 20f),
				$"  {name} [{lobbyMember:X}] {(isHost ? "HOST" : "guest")} — {status}{rttText}{vitalsText}{inventoryText}");

			// The kick slice: the host can remove any non-local session member.
			// The target receives a dedicated Kicked message before the host
			// drops its presence, so the guest tears down instead of hanging.
			if (session.Role == SessionRole.Host && lobbyMember != steam.LocalSteamId && member is not null)
			{
				if (GUI.Button(new Rect(580f, rowY, 52f, 16f), "Kick"))
				{
					KickMember?.Invoke(lobbyMember);
				}

				// The ban slice: the host can also persist a permanent ban for
				// the same member; the target receives a dedicated Banned
				// message and the host rejects its future handshakes.
				if (GUI.Button(new Rect(636f, rowY, 52f, 16f), "Ban"))
				{
					BanMember?.Invoke(lobbyMember);
				}
			}

			// The carry slice: an in-world remote who is unconscious/dead can be
			// carried by the local player; the host re-checks the same rule. If
			// the local player is already carrying this member, the button turns
			// into a Drop action.
			var isCarryingThis = playerInteraction.TryGetCarried(steam.LocalSteamId, out var currentCarried)
				&& currentCarried == lobbyMember;
			var canCarry = lobbyMember != steam.LocalSteamId
				&& member is { InWorld: true }
				&& vitals.TryGet(lobbyMember, out var carryVitals)
				&& (!carryVitals.Conscious || !carryVitals.Alive)
				&& !playerInteraction.TryGetCarrier(lobbyMember, out _)
				&& !playerInteraction.TryGetCarried(steam.LocalSteamId, out _);
			if (isCarryingThis)
			{
				if (GUI.Button(new Rect(760f, rowY, 58f, 16f), "Drop"))
				{
					DropCarried?.Invoke(lobbyMember);
				}
			}
			else if (canCarry)
			{
				if (GUI.Button(new Rect(760f, rowY, 58f, 16f), "Carry"))
				{
					CarryRemote?.Invoke(lobbyMember);
				}
			}

			// The heal slice: show a Heal button for any in-world remote who is
			// alive and the local body has a known medical item. The host is the
			// authority — it re-checks availability/state from its snapshots.
			if (lobbyMember != steam.LocalSteamId
				&& member is { InWorld: true }
				&& session.LocalInWorld
				&& vitals.TryGet(lobbyMember, out var healVitals)
				&& healVitals.Alive
				&& (HasHealItem?.Invoke() ?? false))
			{
				if (GUI.Button(new Rect(700f, rowY, 58f, 16f), "Heal"))
				{
					HealRemote?.Invoke(lobbyMember);
				}
			}

			y += 20f;

			// Explicit heal-item selector: the wire already accepts a concrete
			// instance id, so list the local slot-held medical items. The host
			// remains the authority and re-validates the requested id.
			if (lobbyMember != steam.LocalSteamId
				&& member is { InWorld: true }
				&& session.LocalInWorld
				&& vitals.TryGet(lobbyMember, out var healSelectorVitals)
				&& healSelectorVitals.Alive
				&& (HasHealItem?.Invoke() ?? false)
				&& (GetLocalHealItems?.Invoke() ?? []) is { Count: > 0 } healItems)
			{
				foreach (var healItem in healItems)
				{
					if (GUI.Button(new Rect(30f, y, 190f, 16f), $"Heal {healItem.ItemId}"))
					{
						HealWithItem?.Invoke(lobbyMember, healItem.InstanceId);
					}

					y += 16f;
				}
			}


			// The trader-recruit slice: offer a Recruit button for a dead
			// in-world teammate. The adapter only dispatches the request when a
			// trader is within range; the host remains the authority for the
			// trade gates and the revive result.
			if (lobbyMember != steam.LocalSteamId
				&& member is { InWorld: true }
				&& session.LocalInWorld
				&& vitals.TryGet(lobbyMember, out var recruitVitals)
				&& !recruitVitals.Alive)
			{
				if (GUI.Button(new Rect(700f, rowY, 58f, 16f), "Recruit"))
				{
					RecruitPlayer?.Invoke(lobbyMember);
				}
			}

			// The "view items" slice: expand the in-world member's carried and
			// worn inventory under its status line. The clone already shows the
			// visuals; this gives a readable item/slot list from the same 1 Hz
			// character snapshot. The "take" slice adds one button per
			// backpack/hand-slot item on an unconscious/dead remote body — the
			// host re-checks the same rule from its authoritative snapshot.
			if (member is { InWorld: true } && inventory.TryGet(lobbyMember, out var inv))
			{
				var canTake = lobbyMember != steam.LocalSteamId
					&& vitals.TryGet(lobbyMember, out var targetVitals)
					&& (!targetVitals.Conscious || !targetVitals.Alive);
				foreach (var entry in inv.Items)
				{
					var slot = entry.SlotIndex >= 0 ? $"slot {entry.SlotIndex}" : "worn";
					var suffix = entry.ContentsCount > 0 ? $" (+{entry.ContentsCount} inside)" : "";
					var favourite = entry.Favourited ? " ★" : "";
					var line = $"{slot}: {entry.ItemId}{suffix}{favourite}";
					GUI.Label(new Rect(30f, y, 880f, 16f), $"    {line}");

					if (canTake && entry.SlotIndex >= 0 && entry.InstanceId != 0)
					{
						if (GUI.Button(new Rect(820f, y, 58f, 16f), "Take"))
						{
							TakeItem?.Invoke(lobbyMember, entry.InstanceId);
						}
					}

					y += 16f;
					y = DrawContainerContents(entry, y);
				}
			}
		}

		return y;
	}

	private static float DrawContainerContents(RemoteInventoryEntry entry, float y)
	{
		foreach (var child in entry.Contents)
		{
			var suffix = child.ContentsCount > 0 ? $" ({child.ContentsCount} inside)" : "";
			var favourite = child.Favourited ? " ★" : "";
			GUI.Label(new Rect(50f, y, 860f, 16f), $"            ↳ {child.ItemId}{suffix}{favourite}");
			y += 16f;
			y = DrawContainerContents(child, y);
		}

		return y;
	}


	private static void DrawNameplatesAndArrows(SteamService steam, SessionService session, EntitySyncService entities, RemoteVitalsService vitals)
	{
		var camera = Camera.main;
		if (camera == null)
		{
			return;
		}

		const float margin = 28f;
		foreach (var remote in entities.RemotePlayers)
		{
			if (remote.IsLocal || !session.IsRemoteInWorld(remote.SteamId))
			{
				continue;
			}

			var worldPoint = new Vector3(remote.Position.X, remote.Position.Y, 0f);
			var projected = camera.WorldToScreenPoint(worldPoint);
			// GUI y grows DOWN; WorldToScreenPoint y grows UP.
			var gui = new Vector2(projected.x, Screen.height - projected.y);
			var placement = OffScreenArrowGeometry.Place(gui.x, gui.y, Screen.width, Screen.height, margin);

			if (placement.Direction == OffScreenArrowDirection.None)
			{
				DrawNameplate(placement.X, placement.Y, DisplayName(steam, remote.SteamId), remote, vitals);
			}
			else
			{
				DrawOffScreenArrow(placement, DisplayName(steam, remote.SteamId));
			}
		}
	}

	private static void DrawNameplate(float x, float y, string name, PlayerEntity remote, RemoteVitalsService vitals)
	{
		var style = new GUIStyle(GUI.skin.label)
		{
			fontSize = 12,
			alignment = TextAnchor.MiddleCenter,
		};
		style.normal.textColor = Color.white;

		var status = remote.Alive ? (remote.Conscious ? "" : " Zzz") : " \u271D"; // ✝
		GUI.Label(new Rect(x - 80f, y - 34f, 160f, 20f), name + status, style);

		if (vitals.TryGet(remote.SteamId, out var snapshot))
		{
			var vitalsStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 10,
				alignment = TextAnchor.MiddleCenter,
			};
			vitalsStyle.normal.textColor = Color.white;
			GUI.Label(new Rect(x - 80f, y - 12f, 160f, 16f), snapshot.ToShortString(), vitalsStyle);
		}
	}

	private static void DrawOffScreenArrow(OffScreenArrowPlacement placement, string name)
	{
		var arrowStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = 18,
			alignment = TextAnchor.MiddleCenter,
		};
		arrowStyle.normal.textColor = Color.yellow;

		var arrow = placement.Direction switch
		{
			OffScreenArrowDirection.Up => "\u25B2",   // ▲
			OffScreenArrowDirection.Down => "\u25BC", // ▼
			OffScreenArrowDirection.Left => "\u25C0", // ◄
			OffScreenArrowDirection.Right => "\u25B6", // ►
			_ => "\u2022",                            // •
		};
		GUI.Label(new Rect(placement.X - 12f, placement.Y - 12f, 24f, 24f), arrow, arrowStyle);

		var nameStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = 10,
			alignment = TextAnchor.MiddleCenter,
		};
		nameStyle.normal.textColor = Color.white;
		GUI.Label(new Rect(placement.X - 70f, placement.Y + 12f, 140f, 16f), name, nameStyle);
	}
	private static string DisplayName(SteamService steam, ulong steamId)
	{
		var name = steam.GetPersonaName(steamId);
		return string.IsNullOrWhiteSpace(name) ? $"player-{steamId:X}" : name;
	}
}
