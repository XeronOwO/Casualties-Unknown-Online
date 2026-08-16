using System.Text;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.ModExample;

/// <summary>
/// The example CUO mod — the Phase 4 Mod API's runtime verification target:
/// its lifecycle and every message it receives are logged, and each received
/// payload is echoed (a guest's report reaches the host's copy; the host
/// broadcasts the echo to every member). Synchronized: both sides must run the
/// same version, so the handshake consistency check admits the pair and a
/// missing copy is refused — exactly what the two-process verification proves.
/// </summary>
[CuoMod("cuo.example", "CUO Example", "0.1.0", NetworkMode = NetworkMode.Synchronized,
	Permissions = ModPermission.SendNetworkMessage | ModPermission.RegisterCommand | ModPermission.ExecuteHostAction)]
public sealed class ExampleMod : ICuoMod
{
	private IModContext? _context;

	public void Bind(IModContext context)
	{
		_context = context;
		context.Network.MessageReceived += (sender, payload) =>
		{
			var text = Encoding.UTF8.GetString(payload);
			context.Logger.LogInformation("[Example] echo from {Sender}: {Text}", sender, text);
			if (context.Session.IsHost)
			{
				context.Network.Broadcast(Encoding.UTF8.GetBytes($"echo:{text}"));
			}
		};
		context.Commands.Register(new ModCommand("echo", c => $"echo:{string.Join(" ", c.Arguments)}"));
		context.Commands.Register(new ModCommand("whoami", c => $"requester:{c.RequesterSteamId}", isHostAction: true));
		context.PlayerJoined += id => context.Logger.LogInformation("[Example] player {Id} joined.", id);
		context.PlayerLeft += id => context.Logger.LogInformation("[Example] player {Id} left.", id);
		context.SessionEnded += () => context.Logger.LogInformation("[Example] session ended.");
		context.Logger.LogInformation("[Example] bound (session active: {Active}, host: {Host}).",
			context.Session.SessionActive, context.Session.HostSteamId);
	}

	public void Initialize() => _context?.Logger.LogInformation("[Example] initialized.");

	public void Start() => _context?.Logger.LogInformation("[Example] started.");

	public void Update()
	{
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}
}
