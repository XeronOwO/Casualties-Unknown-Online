using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CasualtiesUnknownOnline.Runtime.Session.Chat;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The local in-game command/chat console. Commands are registered once at
/// construction, parsed from a slash-prefixed line, gated by a role permission,
/// executed on this side, and answered into a bounded output buffer. Non-command
/// lines are forwarded to the existing text-chat domain so the same input
/// surface doubles as the reworked chat UI.
/// 
/// This is deliberately a local console: no new wire protocol, no packet handler,
/// no host relay. Host-only commands are enforced by role and use the existing
/// session/ban services on the host process.
/// </summary>
public sealed class CommandConsoleService : ICommandControl
{
	private const int MaxLines = 200;

	private readonly IChatControl _chat;
	private readonly ISessionControl _session;
	private readonly IHostBanService _hostBans;
	private readonly ILogger<CommandConsoleService> _log;
	private readonly List<ConsoleLine> _lines = [];
	private readonly List<CommandDefinition> _commands = [];

	public CommandConsoleService(
		IChatControl chat,
		ISessionControl session,
		IHostBanService hostBans,
		ILogger<CommandConsoleService> log)
	{
		_chat = chat;
		_session = session;
		_hostBans = hostBans;
		_log = log;
		_chat.MessageReceived += OnChatLine;
		_session.SessionEnded += OnSessionEnded;
		RegisterBuiltIns();
		AddLine("CUO command console ready. Type /help for available commands, or just type to chat.", ConsoleLineKind.Info);
	}

	public IReadOnlyList<ConsoleLine> Lines => _lines;

	public bool TryExecute(string input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return false;
		}

		var text = input.Trim();
		if (text.StartsWith("/", StringComparison.Ordinal))
		{
			return ExecuteCommand(text.Substring(1));
		}

		if (!_chat.TrySend(text))
		{
			AddLine("Message was not sent (session inactive or invalid text).", ConsoleLineKind.Error);
			return false;
		}

		return true;
	}

	public void Clear()
	{
		_lines.Clear();
		_log.LogDebug("[Command] console output cleared.");
	}

	public void Dispose()
	{
		_chat.MessageReceived -= OnChatLine;
		_session.SessionEnded -= OnSessionEnded;
	}

	private void RegisterBuiltIns()
	{
		Register("help", "List available commands.", CommandPermission.Anyone, _ => HelpText());
		Register("clear", "Clear the console output.", CommandPermission.Anyone, _ =>
		{
			Clear();
			return string.Empty;
		});
		Register("players", "List current session members.", CommandPermission.Anyone, _ => PlayersText());
		Register("rtt", "Show the last measured round-trip time.", CommandPermission.Anyone, _ => RttText());
		Register("whoami", "Show local role, SteamId and session state.", CommandPermission.Anyone, _ => WhoAmIText());
		Register("kick", "Host only: kick a member by SteamId or display name.", CommandPermission.HostOnly, Kick);
		Register("ban", "Host only: ban a member by SteamId or display name.", CommandPermission.HostOnly, Ban);
		Register("unban", "Host only: unban a SteamId.", CommandPermission.HostOnly, Unban);
	}

	private void Register(string name, string description, CommandPermission permission, Func<IReadOnlyList<string>, string> handler) =>
		_commands.Add(new CommandDefinition(name, description, permission, handler));

	private bool ExecuteCommand(string commandLine)
	{
		var tokens = Split(commandLine);
		if (tokens.Count == 0)
		{
			AddLine("Empty command.", ConsoleLineKind.Error);
			return false;
		}

		var command = _commands.FirstOrDefault(c => string.Equals(c.Name, tokens[0], StringComparison.OrdinalIgnoreCase));
		if (command is null)
		{
			AddLine($"Unknown command '{tokens[0]}'. Type /help for available commands.", ConsoleLineKind.Error);
			return false;
		}

		if (command.Permission == CommandPermission.HostOnly && _session.Role != SessionRole.Host)
		{
			AddLine("Permission denied: this command is host-only.", ConsoleLineKind.Error);
			return false;
		}

		try
		{
			var output = command.Handler(tokens);
			if (!string.IsNullOrWhiteSpace(output))
			{
				AddLine(output, ConsoleLineKind.Success);
			}

			return true;
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "[Command] {Name} threw while executing.", command.Name);
			AddLine($"Command error: {ex.Message}", ConsoleLineKind.Error);
			return true;
		}
	}

	private string HelpText()
	{
		var names = new List<string>(_commands.Count);
		foreach (var command in _commands)
		{
			names.Add($"/{command.Name} — {command.Description}");
		}

		return string.Join("\n", names);
	}

	private string PlayersText()
	{
		var builder = new StringBuilder();
		builder.Append("Local: ").Append(_session.LocalSteamId).Append(" (").Append(_session.Role).Append(')');
		foreach (var member in _session.Members)
		{
			builder.Append('\n');
			builder.Append(member.SteamId);
			if (!string.IsNullOrWhiteSpace(member.DisplayName))
			{
				builder.Append(" [").Append(member.DisplayName).Append(']');
			}

			builder.Append(" handshake=").Append(member.Handshaken);
			builder.Append(" inWorld=").Append(member.InWorld);
			builder.Append(" rtt=").AppendFormat(CultureInfo.InvariantCulture, "{0:F0}", member.RttMs);
		}

		return builder.ToString();
	}

	private string RttText()
	{
		return _session.LastRttMs < 0f
			? "No ping measured yet."
			: $"Last RTT: {_session.LastRttMs:F0} ms";
	}

	private string WhoAmIText() =>
		$"Role={_session.Role} SteamId={_session.LocalSteamId} Host={_session.HostSteamId} SessionActive={_session.SessionActive}";

	private string Kick(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return "Usage: /kick <steamId|displayName>";
		}

		if (!TryResolveMember(args[1], out var steamId))
		{
			return $"Unknown member '{args[1]}'.";
		}

		if (!_session.KickMember(steamId, "kicked via console"))
		{
			return $"Could not kick {steamId} — not a removable guest, or already left.";
		}

		return $"Kicked member {steamId}.";
	}

	private string Ban(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return "Usage: /ban <steamId|displayName>";
		}

		if (!TryResolveMember(args[1], out var steamId))
		{
			return $"Unknown member '{args[1]}'.";
		}

		if (!_hostBans.Ban(steamId, "banned via console"))
		{
			return $"Could not ban {steamId} — not a removable guest, already banned, or not host.";
		}

		return $"Banned member {steamId}.";
	}

	private string Unban(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return "Usage: /unban <steamId>";
		}

		if (!ulong.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var steamId))
		{
			return $"SteamId '{args[1]}' is not a number.";
		}

		if (!_hostBans.Unban(steamId))
		{
			return $"Could not unban {steamId} — not in the ban list.";
		}

		return $"Unbanned {steamId}.";
	}

	private bool TryResolveMember(string text, out ulong steamId)
	{
		if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out steamId))
		{
			return true;
		}

		if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
			&& ulong.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out steamId))
		{
			return true;
		}

		foreach (var member in _session.Members)
		{
			if (!string.IsNullOrWhiteSpace(member.DisplayName)
				&& string.Equals(member.DisplayName.Trim(), text, StringComparison.OrdinalIgnoreCase))
			{
				steamId = member.SteamId;
				return true;
			}
		}

		steamId = 0;
		return false;
	}

	private void OnChatLine(ChatLine line) => AddLine(FormatChatLine(line), ConsoleLineKind.Info);

	private string FormatChatLine(ChatLine line)
	{
		if (line.SenderSteamId == _session.LocalSteamId)
		{
			return $"You: {line.Text}";
		}

		foreach (var member in _session.Members)
		{
			if (member.SteamId == line.SenderSteamId && !string.IsNullOrWhiteSpace(member.DisplayName))
			{
				return $"{member.DisplayName}: {line.Text}";
			}
		}

		return $"player-{line.SenderSteamId:X}: {line.Text}";
	}

	private void OnSessionEnded()
	{
		_lines.Clear();
		AddLine("Session ended.", ConsoleLineKind.Info);
	}

	private void AddLine(string text, ConsoleLineKind kind)
	{
		_lines.Add(new ConsoleLine(kind, text));
		if (_lines.Count > MaxLines)
		{
			_lines.RemoveAt(0);
		}
	}

	private static IReadOnlyList<string> Split(string text)
	{
		var tokens = new List<string>();
		var current = new StringBuilder();
		var inQuotes = false;
		foreach (var ch in text)
		{
			if (ch == '"')
			{
				inQuotes = !inQuotes;
			}
			else if (char.IsWhiteSpace(ch) && !inQuotes)
			{
				if (current.Length > 0)
				{
					tokens.Add(current.ToString());
					current.Clear();
				}
			}
			else
			{
				current.Append(ch);
			}
		}

		if (current.Length > 0)
		{
			tokens.Add(current.ToString());
		}

		return tokens;
	}

	private enum CommandPermission
	{
		Anyone,
		HostOnly,
	}

	private sealed record CommandDefinition(
		string Name,
		string Description,
		CommandPermission Permission,
		Func<IReadOnlyList<string>, string> Handler);
}
