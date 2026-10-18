using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The vitals row's sender and identity rules: the wire direction check proves only this client's
/// ROLE, so a row whose sender is not this session's host is a CLAIM and is dropped by name — the
/// same rule every host-addressed instruction follows. A peer that could deliver such a frame would
/// otherwise state a guest's blood volume and per-limb bleed (an output for another body). A row
/// that names someone else is dropped too: the row belongs to the player whose body it describes,
/// and the handler's refusal edge is this client's own.
/// </summary>
public class PlayerVitalsForgeryTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong ThirdId = 3001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void ARowFromANonHostSender_IsDroppedAndTheHostsRowLands()
	{
		var log = new RecordingLogger<PlayerVitalsHandler>();
		var (host, guest, network) = TestNode.CreatePairWithNetwork(HostId, GuestId, LobbyId,
			guestRegistrations: services => services.Replace(ServiceDescriptor.Singleton<ILogger<PlayerVitalsHandler>>(log)));
		using (host)
		using (guest)
		{
			var received = new List<PlayerVitalsMsg>();
			guest.Services.GetRequiredService<ICharacterDataControl>().PlayerVitalsReceived += received.Add;

			var third = TestNode.Create(ThirdId, network,
				new FakeSteamService(ThirdId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId, ThirdId] },
				host.Clock, pumpFirstFrame: true);
			try
			{
				host.Steam.LobbyMembers = [HostId, GuestId, ThirdId];
				third.Steam.FireLobbyEntered(LobbyId);
				new SimulationDriver(host.Clock, network, host, third).TickUntil(
					() => host.Session.Members.Any(member => member.SteamId == ThirdId && member.Handshaken),
					maxMs: 5000);

				// Forged: a third peer sends a row naming the guest straight to it.
				third.Services.GetRequiredService<PacketSender>().Send(GuestId, NetMsg.PlayerVitals, new PlayerVitalsMsg
				{
					SteamId = GuestId,
					BloodVolume = -100f,
				});
				guest.Update();

				Assert.Empty(received);
				Assert.Contains(log.Entries, entry => entry.Message.Contains("not this session's host"));

				// The real host's row still goes through the same handler.
				host.Services.GetRequiredService<PacketSender>().Send(GuestId, NetMsg.PlayerVitals, new PlayerVitalsMsg
				{
					SteamId = GuestId,
					BloodVolume = 55f,
				});
				guest.Update();

				var row = Assert.Single(received);
				Assert.Equal(55f, row.BloodVolume, 3);
			}
			finally
			{
				third.Dispose();
			}
		}
	}

	[Fact]
	public void ARowNamingAnotherPlayer_IsDropped_AndARefusalIsLoggedOncePerEdge()
	{
		var log = new RecordingLogger<PlayerVitalsHandler>();
		var (host, guest, _) = TestNode.CreatePairWithNetwork(HostId, GuestId, LobbyId,
			guestRegistrations: services => services.Replace(ServiceDescriptor.Singleton<ILogger<PlayerVitalsHandler>>(log)));
		using (host)
		using (guest)
		{
			var received = new List<PlayerVitalsMsg>();
			guest.Services.GetRequiredService<ICharacterDataControl>().PlayerVitalsReceived += received.Add;
			var sender = host.Services.GetRequiredService<PacketSender>();

			// A row naming a third member on the guest's client is a misroute: no apply, no edge.
			sender.Send(GuestId, NetMsg.PlayerVitals, new PlayerVitalsMsg
			{
				SteamId = ThirdId,
				BloodVolume = 1f,
				RefusedField = "health.BloodViscosity",
			});
			guest.Update();

			Assert.Empty(received);
			Assert.Contains(log.Entries, entry => entry.Message.Contains("naming"));

			// The guest's own refusal: one warning on the edge, none on its repeat.
			sender.Send(GuestId, NetMsg.PlayerVitals, new PlayerVitalsMsg
			{
				SteamId = GuestId,
				BloodVolume = 50f,
				RefusedField = "health.BloodViscosity",
			});
			guest.Update();
			sender.Send(GuestId, NetMsg.PlayerVitals, new PlayerVitalsMsg
			{
				SteamId = GuestId,
				BloodVolume = 51f,
				RefusedField = "health.BloodViscosity",
			});
			guest.Update();

			Assert.Equal(2, received.Count);
			Assert.Single(log.Entries.Where(entry => entry.Level == LogLevel.Warning
				&& entry.Message.Contains("non-finite")));

			// The refusal clearing is the other edge: one information line.
			sender.Send(GuestId, NetMsg.PlayerVitals, new PlayerVitalsMsg { SteamId = GuestId, BloodVolume = 52f });
			guest.Update();
			Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Information && entry.Message.Contains("accepts"));
		}
	}
}
