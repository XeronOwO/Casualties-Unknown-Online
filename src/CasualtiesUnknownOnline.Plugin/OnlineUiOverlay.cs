using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
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

	private string _lobbyIdInput = "";
	private string? _inlineError;

	internal void Draw(SteamService steam, SessionService session, EntitySyncService entities, RemoteVitalsService vitals, RemoteInventoryService inventory, IPlayerInteractionControl playerInteraction, string? lastJoinError)
	{
		DrawStatusPanel(steam, session, entities, vitals, inventory, playerInteraction, lastJoinError ?? _inlineError);
		DrawNameplatesAndArrows(steam, session, entities, vitals);
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
			var rowY = y;
			GUI.Label(new Rect(10f, rowY, 900f, 20f),
				$"  {name} [{lobbyMember:X}] {(isHost ? "HOST" : "guest")} — {status}{vitalsText}{inventoryText}");

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

			y += 20f;

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
				}
			}
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
