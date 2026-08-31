using CasualtiesUnknownOnline.Runtime.Session.Commands;
using UnityEngine;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The in-game command/chat console page. It projects the Runtime command
/// buffer and the single input field; the actual parsing, permission gates and
/// chat send live in <see cref="CommandConsoleService"/> so this drawer stays a
/// pure presentation shell.
/// </summary>
internal static class OnlineUiConsoleDrawer
{
	internal static void Draw(OnlineUiContext ctx)
	{
		GUILayout.Label(ctx.T("console.title"), OnlineUiTheme.Section());
		GUILayout.Label(ctx.T("console.hint"), OnlineUiTheme.MutedLabel());
		GUILayout.Space(6f);

		foreach (var line in ctx.Commands.Lines)
		{
			DrawLine(line);
		}

		GUILayout.Space(8f);
		GUILayout.BeginHorizontal();
		ctx.State.ConsoleInput = GUILayout.TextField(ctx.State.ConsoleInput, GUILayout.ExpandWidth(true));
		if (GUILayout.Button(ctx.T("console.send"), OnlineUiTheme.Button(), GUILayout.Width(70f)) || TryEnter(ctx))
		{
			var input = ctx.State.ConsoleInput;
			if (!string.IsNullOrWhiteSpace(input) && ctx.Commands.TryExecute(input))
			{
				ctx.State.ConsoleInput = "";
			}
		}

		GUILayout.EndHorizontal();
	}

	private static void DrawLine(ConsoleLine line)
	{
		var previous = GUI.color;
		GUI.color = line.Kind switch
		{
			ConsoleLineKind.Success => OnlineUiTheme.Positive,
			ConsoleLineKind.Error => OnlineUiTheme.Error,
			_ => OnlineUiTheme.Text,
		};
		GUILayout.Label(line.Text, OnlineUiTheme.MutedLabel());
		GUI.color = previous;
	}

	private static bool TryEnter(OnlineUiContext ctx)
	{
		var evt = Event.current;
		return evt != null && evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Return;
	}
}
