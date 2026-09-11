using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The save command group: <c>/save</c> — the host's mid-run cut.
///
/// It only ARMS the cut. The command console runs inside the game's input
/// handling, and a cut taken there could read a half-applied frame (a command
/// batch mid-commit, a frame flush mid-send); the cut therefore happens at the
/// host pump's frame-end seam, and the console answers with what that seam
/// resolved to (<see cref="CommandConsoleService"/> renders the report).
/// </summary>
internal sealed class WorldSaveCommands(
	IWorldSaveControl saves,
	ILogger log)
{
	private readonly IWorldSaveControl _saves = saves;
	private readonly ILogger _log = log;

	[ConsoleCommand("save", "Host only: write a CUO world-archive cut at the next frame boundary.", CommandPermission.HostOnly, "/save")]
	internal string Save(IReadOnlyList<string> _)
	{
		if (!_saves.TryRequestCut(WorldCutReason.Command, out var refusal))
		{
			_log.LogWarning("[Command] /save refused: {Refusal}", refusal);
			return $"Cannot save: {refusal}.";
		}

		_log.LogInformation("[Command] /save armed a cut for the frame-end seam.");
		return "Save queued: the host takes the cut at the end of this frame, and the result appears here.";
	}
}
