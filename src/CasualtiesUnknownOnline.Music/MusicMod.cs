using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Music;

/// <summary>
/// The CUO half of the music-share mod: it registers the sharing packets, the local console verb and the frame
/// tick that drives the transfer, and it wires the packet chains to <see cref="MusicShareService"/>. The mod is
/// <see cref="NetworkMode.ClientOnly"/> — it adds its own messages, never a game or session wire — but it needs
/// <see cref="ModPermission.SendNetworkMessage"/> for those messages and
/// <see cref="ModPermission.RegisterLocalCommand"/> for <c>/music</c>.
/// </summary>
[CuoMod("cuo.music", "Music Share", "0.1.0", NetworkMode = NetworkMode.ClientOnly,
	Permissions = ModPermission.RegisterLocalCommand | ModPermission.SendNetworkMessage,
	Description = "Plays a local WAV file (/music play) and shares a whole track with the session's members: the "
		+ "file is transferred in chunks first, then everyone plays it at the same announced instant.")]
public sealed class MusicMod : ICuoMod
{
	private IModContext? _context;
	private MusicShareService? _share;

	/// <inheritdoc />
	public void Bind(IModContext context)
	{
		_context = context;
		MusicGate.Bind(context.Logger);
		_share = new MusicShareService(context, MusicCommand.Device);
		MusicCommand.Bind(_share);

		var registered = context.Packets.Register(new ModPacket(
				MusicWire.Announce, ModPacketSender.AnyMember, ModPacketDelivery.EveryOtherMember,
				new ModPacketHandler(ModPacketStage.Apply, ctx => _share.OnAnnounce(ctx))))
			&& context.Packets.Register(new ModPacket(
				MusicWire.Chunk, ModPacketSender.AnyMember, ModPacketDelivery.EveryOtherMember,
				new ModPacketHandler(ModPacketStage.Apply, ctx => _share.OnChunk(ctx))))
			&& context.Packets.Register(new ModPacket(
				MusicWire.Ack, ModPacketSender.AnyMember, ModPacketDelivery.EveryOtherMember,
				new ModPacketHandler(ModPacketStage.Apply, ctx => _share.OnAck(ctx))))
			&& context.Packets.Register(new ModPacket(
				MusicWire.Play, ModPacketSender.AnyMember, ModPacketDelivery.EveryOtherMember,
				new ModPacketHandler(ModPacketStage.Apply, ctx => _share.OnPlay(ctx))));
		if (!registered)
		{
			context.Logger.LogWarning("[Music] the packet declarations were refused; sharing is unavailable (local playback still works).");
		}

		if (context.ConsoleCommands.Register(new ConsoleCommandDefinition(
			"music",
			"Play a local WAV file, or share it with the session",
			MusicCommand.Execute,
			parameters:
			[
				new ConsoleCommandParameter("action", new FixedChoiceCompleter("play", "share", "stop", "status")),
				new ConsoleCommandParameter("path"),
			],
			usage: "/music play <path> | share <path> | stop | status")))
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

	/// <summary>The transfer's clock: one chunk per interval while a share runs, and the agreed start instant.</summary>
	public void Update() => _share?.Tick();

	/// <inheritdoc />
	public void Stop() => _share?.Stop();

	/// <inheritdoc />
	public void Dispose()
	{
		_share?.Stop();
		_context = null;
	}
}
