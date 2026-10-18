using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Character;

/// <summary>
/// The host's continuous-vitals wire pump: the 10 Hz cadence, the unreliable channel, the
/// world/session gates, the row's exact host-owned values, and the refusal of a non-finite stored
/// value. The model itself now runs in the Game Adapter on the host's per-player clones; its
/// authority round-trip is pinned in <c>ContinuousVitalsTests</c> and the report/event seam against
/// the real store in <see cref="PlayerVitalsOwnershipTests"/>.
/// </summary>
public class PlayerVitalsServiceTests
{
	private const ulong HostId = 1;
	private const ulong GuestId = 2;

	[Fact]
	public void TheRow_GoesOutAtTenHertz()
	{
		var harness = Create();
		var guest = new FakeTransport(GuestId, harness.Network);
		var received = 0;
		guest.MessageReceived += (_, _) => received++;
		harness.Characters.SaveCharacterData(GuestId, Snapshot(bloodVolume: 100f, totalBleedSpeed: 0.2f, bleed: 10f));

		// One virtual second at 60 fps: ten 100 ms rows.
		for (var frame = 0; frame < 64; frame++)
		{
			harness.Clock.Advance(16);
			harness.Tick();
		}

		Assert.Equal(10, received);
	}

	[Fact]
	public void TheRow_GoesOutUnreliable()
	{
		var harness = Create();
		var guest = new FakeTransport(GuestId, harness.Network);
		var received = 0;
		guest.MessageReceived += (_, _) => received++;
		harness.Characters.SaveCharacterData(GuestId, Snapshot(bloodVolume: 100f, totalBleedSpeed: 0.2f, bleed: 10f));

		// Every unreliable frame on this link is dropped; a reliable send would still arrive.
		harness.Network.SetFaults(HostId, GuestId, new LinkFaults { UnreliableDropRate = 1.0 });
		harness.Clock.Advance(16);
		harness.Tick();

		Assert.Equal(0, received);

		// Positive control: with the link healed the next row lands — the pump really sends.
		harness.Network.ClearFaults(HostId, GuestId);
		harness.Clock.Advance(100);
		harness.Tick();

		Assert.Equal(1, received);
	}

	[Fact]
	public void TheRow_CarriesTheStoredHostOwnedValues_AndSkipsADismemberedLimb()
	{
		var harness = Create();
		var guest = new FakeTransport(GuestId, harness.Network);
		PlayerVitalsMsg? row = null;
		guest.MessageReceived += (_, data) => row = NetPacket.DecodePayload<PlayerVitalsMsg>(data);
		var snapshot = Snapshot(bloodVolume: 42f, totalBleedSpeed: 0.42f, bleed: 4f);
		snapshot.Limbs.Add(new CharacterLimbMsg { Index = 1, BleedAmount = 9f, Dismembered = true });
		harness.Characters.SaveCharacterData(GuestId, snapshot);

		harness.Clock.Advance(16);
		harness.Tick();

		Assert.NotNull(row);
		Assert.Equal(GuestId, row!.SteamId);
		Assert.Equal(42f, row.BloodVolume, 3);
		Assert.Equal(0.42f, row.TotalBleedSpeed, 3);
		var limb = Assert.Single(row.Limbs);
		Assert.Equal(0, limb.Index);
		Assert.Equal(4f, limb.BleedAmount, 3);
	}

	[Fact]
	public void AGuestPump_SendsNothing()
	{
		var harness = Create();
		var guest = new FakeTransport(GuestId, harness.Network);
		var received = 0;
		guest.MessageReceived += (_, _) => received++;
		harness.Characters.SaveCharacterData(GuestId, Snapshot(bloodVolume: 100f, totalBleedSpeed: 0.2f, bleed: 10f));
		harness.Session.Role = SessionRole.Guest;

		harness.Clock.Advance(16);
		harness.Tick();

		Assert.Equal(0, received);
	}

	[Fact]
	public void TheHostOutOfTheWorld_SendsNothing()
	{
		var harness = Create();
		var guest = new FakeTransport(GuestId, harness.Network);
		var received = 0;
		guest.MessageReceived += (_, _) => received++;
		harness.Characters.SaveCharacterData(GuestId, Snapshot(bloodVolume: 100f, totalBleedSpeed: 0.2f, bleed: 10f));
		harness.Session.LocalInWorld = false;

		harness.Clock.Advance(16);
		harness.Tick();

		Assert.Equal(0, received);
	}

	[Fact]
	public void AMemberOutOfTheWorld_IsNotSent()
	{
		var harness = Create();
		var guest = new FakeTransport(GuestId, harness.Network);
		var received = 0;
		guest.MessageReceived += (_, _) => received++;
		harness.Characters.SaveCharacterData(GuestId, Snapshot(bloodVolume: 100f, totalBleedSpeed: 0.2f, bleed: 10f));
		Assert.True(harness.Session.TryGetMember(GuestId, out var member));
		member.InWorld = false;

		harness.Clock.Advance(16);
		harness.Tick();

		Assert.Equal(0, received);
	}

	[Fact]
	public void AMemberWithoutASnapshot_IsSkipped()
	{
		var harness = Create();
		var guest = new FakeTransport(GuestId, harness.Network);
		var received = 0;
		guest.MessageReceived += (_, _) => received++;

		harness.Clock.Advance(16);
		harness.Tick();

		Assert.Equal(0, received);
	}

	[Fact]
	public void ANonFiniteStoredValue_RefusesTheRowAndWarnsOnce()
	{
		var harness = Create();
		var guest = new FakeTransport(GuestId, harness.Network);
		var received = 0;
		guest.MessageReceived += (_, _) => received++;
		var snapshot = Snapshot(bloodVolume: float.NaN, totalBleedSpeed: 0.2f, bleed: 10f);
		harness.Characters.SaveCharacterData(GuestId, snapshot);

		for (var frame = 0; frame < 16; frame++)
		{
			harness.Clock.Advance(16);
			harness.Tick();
		}

		Assert.Equal(0, received);
		Assert.True(harness.Log.HasWarning("not finite"), "the refusal must be named");
		Assert.Equal(1, harness.Log.Entries.FindAll(entry => entry.Level == LogLevel.Warning).Count);
	}

	[Fact]
	public void TheRefusalWindow_ClearsWhenTheStoredValueIsFiniteAgain()
	{
		var harness = Create();
		var guest = new FakeTransport(GuestId, harness.Network);
		var received = 0;
		guest.MessageReceived += (_, _) => received++;
		var snapshot = Snapshot(bloodVolume: float.NaN, totalBleedSpeed: 0.2f, bleed: 10f);
		harness.Characters.SaveCharacterData(GuestId, snapshot);
		harness.Clock.Advance(16);
		harness.Tick();

		snapshot.Health!.BloodVolume = 80f;
		harness.Clock.Advance(100);
		harness.Tick();

		Assert.Equal(1, received);
		Assert.Contains(harness.Log.Entries, entry => entry.Level == LogLevel.Information);
	}

	[Fact]
	public void TheRow_AnswersARefusedReportWithTheFieldName()
	{
		var harness = Create();
		var guest = new FakeTransport(GuestId, harness.Network);
		PlayerVitalsMsg? row = null;
		guest.MessageReceived += (_, data) => row = NetPacket.DecodePayload<PlayerVitalsMsg>(data);
		harness.Characters.SaveCharacterData(GuestId, Snapshot(bloodVolume: 80f, totalBleedSpeed: 0.2f, bleed: 4f));
		var corrupt = Snapshot(bloodVolume: 80f, totalBleedSpeed: 0.2f, bleed: 4f);
		corrupt.Health!.BloodViscosity = float.NaN;
		Assert.False(harness.Characters.SaveReportedCharacterData(GuestId, corrupt));

		harness.Clock.Advance(16);
		harness.Tick();

		Assert.NotNull(row);
		Assert.Equal("health.BloodViscosity", row!.RefusedField);
	}

	[Fact]
	public void TheRow_StartsOnlyAfterTheModelAdvances()
	{
		var harness = Create();
		var guest = new FakeTransport(GuestId, harness.Network);
		var received = 0;
		guest.MessageReceived += (_, _) => received++;
		harness.Characters.SaveCharacterData(GuestId, Snapshot(bloodVolume: 100f, totalBleedSpeed: 0.2f, bleed: 10f));

		harness.Clock.Advance(16);
		harness.Service.Tick(harness.Clock.NowMs); // no adapter advance yet
		Assert.Equal(0, received);

		harness.Clock.Advance(100);
		harness.Tick(); // the fixed step advances the model
		Assert.Equal(1, received);
	}

	[Fact]
	public void TheRow_StopsWhenTheModelIsNotAdvancing()
	{
		var harness = Create();
		var guest = new FakeTransport(GuestId, harness.Network);
		var received = 0;
		guest.MessageReceived += (_, _) => received++;
		harness.Characters.SaveCharacterData(GuestId, Snapshot(bloodVolume: 100f, totalBleedSpeed: 0.2f, bleed: 10f));
		harness.Clock.Advance(16);
		harness.Tick();
		Assert.Equal(1, received);

		// The clone stops advancing (timeScale 0 / a clone that cannot be seeded): the stored mark
		// ages out and the row must stop, or it would keep pinning the owner to frozen host values.
		harness.Clock.Advance(1_000);
		harness.Service.Tick(harness.Clock.NowMs);
		Assert.Equal(1, received);

		harness.Clock.Advance(100);
		harness.Tick();
		Assert.Equal(2, received);
	}

	private static Harness Create()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var transport = new FakeTransport(HostId, network);
		var monitor = new NetworkTrafficMonitor(clock, NullLogger<NetworkTrafficMonitor>.Instance);
		var session = new FakeSessionControl
		{
			Role = SessionRole.Host,
			LocalSteamId = HostId,
			HostSteamId = HostId,
			SessionActive = true,
		};
		session.AddMember(GuestId, "guest");
		var characters = new FakeCharacterDataControl();
		var log = new RecordingLogger<PlayerVitalsService>();
		var service = new PlayerVitalsService(
			session,
			new PacketSender(transport, monitor),
			characters,
			clock,
			log);
		((ICuoService)service).Initialize();
		((ICuoService)service).Start();
		return new Harness(clock, network, session, characters, service, log);
	}

	private static CharacterDataMsg Snapshot(float bloodVolume, float totalBleedSpeed, float bleed) => new()
	{
		Health = new CharacterHealthMsg { BloodVolume = bloodVolume, TotalBleedSpeed = totalBleedSpeed },
		Limbs = [new CharacterLimbMsg { Index = 0, BleedAmount = bleed, SkinHealth = 50f }],
	};

	private sealed record Harness(
		FakeClock Clock,
		FakeNetwork Network,
		FakeSessionControl Session,
		FakeCharacterDataControl Characters,
		PlayerVitalsService Service,
		RecordingLogger<PlayerVitalsService> Log)
	{
		/// <summary>The adapter advances the model on its fixed step; the test marks it fresh right before the row pump reads it.</summary>
		internal void Tick()
		{
			Characters.MarkHostModelAdvancing(GuestId, Clock.NowMs);
			Service.Tick(Clock.NowMs);
		}
	}
}
