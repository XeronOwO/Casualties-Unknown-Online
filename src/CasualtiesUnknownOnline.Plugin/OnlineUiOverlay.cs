using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Chat;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Steam;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The Online UI overlay. It owns the IMGUI composition (the new CUO Online
/// window, world nameplates/off-screen arrows and the bottom-right chat panel).
/// The old top-left status/lobby/member dump is gone: the same runtime facts
/// are now presented through the tabbed <see cref="OnlineUiWindow"/>.
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

	/// <summary>Invoked when the host clicks Unban on a banned SteamID.</summary>
	internal Func<ulong, bool>? UnbanMember;

	private readonly OnlineUiWindow _window = new();

	private string _chatInput = "";

	internal void Draw(
		SteamService steam,
		SessionService session,
		EntitySyncService entities,
		RemoteVitalsService vitals,
		RemoteInventoryService inventory,
		IPlayerInteractionControl playerInteraction,
		IChatControl chat,
		IHostBanService hostBan,
		IHostRules hostRules,
		IGameAdapter? adapter,
		string? lastJoinError)
	{
		var ctx = new OnlineUiContext
		{
			Steam = steam,
			Session = session,
			Entities = entities,
			Vitals = vitals,
			Inventory = inventory,
			PlayerInteraction = playerInteraction,
			Chat = chat,
			HostBan = hostBan,
			HostRules = hostRules,
			Adapter = adapter,
			LastJoinError = lastJoinError,
			State = _window.State,
			JoinLobby = JoinLobby,
			CreateLobby = CreateLobby,
			TakeItem = TakeItem,
			CarryRemote = CarryRemote,
			DropCarried = DropCarried,
			HealRemote = HealRemote,
			HealWithItem = HealWithItem,
			RecruitPlayer = RecruitPlayer,
			KickMember = KickMember,
			BanMember = BanMember,
			UnbanMember = UnbanMember,
			GetLocalHealItems = GetLocalHealItems,
			HasHealItem = HasHealItem,
		};

		_window.Draw(ctx);
		DrawNameplatesAndArrows(steam, session, entities, vitals);
		DrawChatPanel(steam, session, chat);
	}

	/// <summary>
	/// Small bottom-right text-chat panel. It is intentionally IMGUI-simple: a
	/// bounded recent-line list and one input + Send button. The Runtime
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
		OnlineUiTheme.DrawBackground(new Rect(x, y, width, height));

		const int maxVisible = 7;
		var lines = chat.Recent;
		var start = Math.Max(0, lines.Count - maxVisible);
		var lineY = y + 8f;
		for (var i = start; i < lines.Count; i++)
		{
			var line = lines[i];
			var name = DisplayName(steam, line.SenderSteamId);
			GUI.Label(new Rect(x + 8f, lineY, width - 16f, 18f), $"{name}: {line.Text}", OnlineUiTheme.MutedLabel());
			lineY += 18f;
		}

		var inputY = y + height - 30f;
		_chatInput = GUI.TextField(new Rect(x + 8f, inputY, width - 70f, 22f), _chatInput, 200);
		if (GUI.Button(new Rect(x + width - 58f, inputY, 50f, 22f), "Send", OnlineUiTheme.Button()))
		{
			if (chat.TrySend(_chatInput))
			{
				_chatInput = "";
			}
		}
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
