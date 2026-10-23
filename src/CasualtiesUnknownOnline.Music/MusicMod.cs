using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Music;

/// <summary>
/// The CUO half of the music-share mod. This stage registers the LOCAL console verb that plays a WAV file from
/// this machine's disk — the decoder and the engine hand-off the sharing stage will drive from the network. It is
/// <see cref="NetworkMode.ClientOnly"/> with no wire surface yet: nothing here changes the session.
/// </summary>
[CuoMod("cuo.music", "Music Share", "0.1.0", NetworkMode = NetworkMode.ClientOnly,
	Permissions = ModPermission.RegisterLocalCommand,
	Description = "Plays a local WAV file for this client (/music), the first half of sharing a track with the "
		+ "session. The transfer and the shared start instant land with the sharing stage.")]
public sealed class MusicMod : ICuoMod
{
	private IModContext? _context;

	/// <inheritdoc />
	public void Bind(IModContext context)
	{
		_context = context;
		MusicGate.Bind(context.Logger);
		if (context.ConsoleCommands.Register(new ModConsoleCommand(
			"music",
			"Play a local WAV file",
			"/music play <path> | stop | status",
			CommandPermission.Anyone,
			[CommandArgumentKind.Text, CommandArgumentKind.Text],
			MusicCommand.Execute)))
		{
			context.Logger.LogInformation("[Music] console command /music registered.");
			return;
		}

		context.Logger.LogWarning("[Music] the console refused the /music registration; the mod has no active surface.");
	}

	/// <inheritdoc />
	public void Initialize() => _context?.Logger.LogInformation("[Music] initialized.");

	/// <inheritdoc />
	public void Start()
	{
	}

	/// <inheritdoc />
	public void Update()
	{
	}

	/// <inheritdoc />
	public void Stop()
	{
	}

	/// <inheritdoc />
	public void Dispose()
	{
	}
}
