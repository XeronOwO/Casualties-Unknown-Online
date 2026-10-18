using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Character;

/// <summary>
/// The host's continuous-vitals pump: the 10 Hz cadence, the unreliable channel, the authority
/// seam (an event the host arbitrated re-seeds the model; a report never does), the world/session
/// lifecycle, and the refusal of a non-finite report. The model's own rules are pinned in
/// <see cref="PlayerBleedingStateTests"/>; the report/event seam against the real store is pinned
/// in <see cref="PlayerVitalsOwnershipTests"/>.
/// </summary>
public class PlayerVitalsServiceTests
{
	private const ulong HostId = 1;
	private const ulong GuestId = 2;

	[Fact]
	public void Tick_AdvancesTheModelByWallTimeNotByFrame()
	{
		var harness = Create();
		var snapshot = Snapshot(bloodVolume: 100f, bleed: 10f);
		harness.Characters.SaveCharacterData(GuestId, snapshot);

		// A 60 fps pump for about one virtual second. The model must consume that whole second; a
		// per-frame delta would advance only ~0.14 s and leave the blood near its start.
		for (var frame = 0; frame < 64; frame++)
		{
			harness.Clock.Advance(16);
			harness.Service.Tick(harness.Clock.NowMs);
		}

		// The native one-second drain: 10 bleed * 0.01 bleedingSpeedMultiplier = 0.1 blood, no
		// regeneration (hunger 0). A per-frame dt leaves 99.98; the wall-time step lands near 99.90.
		Assert.InRange(snapshot.Health!.BloodVolume, 99.85f, 99.95f);
	}

	[Fact]
	public void TheRow_GoesOutAtTenHertz()
	{
		var harness = Create();
		var guest = new FakeTransport(GuestId, harness.Network);
		var received = 0;
		guest.MessageReceived += (_, _) => received++;
		harness.Characters.SaveCharacterData(GuestId, Snapshot(bloodVolume: 100f, bleed: 10f));

		// One virtual second at 60 fps: ten 100 ms rows (the first tick counts as the seed/send).
		for (var frame = 0; frame < 64; frame++)
		{
			harness.Clock.Advance(16);
			harness.Service.Tick(harness.Clock.NowMs);
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
		harness.Characters.SaveCharacterData(GuestId, Snapshot(bloodVolume: 100f, bleed: 10f));

		// Every unreliable frame on this link is dropped; a reliable send would still arrive.
		harness.Network.SetFaults(HostId, GuestId, new LinkFaults { UnreliableDropRate = 1.0 });
		harness.Clock.Advance(16);
		harness.Service.Tick(harness.Clock.NowMs);

		Assert.Equal(0, received);

		// Positive control: with the link healed the next row lands — the pump really sends.
		harness.Network.ClearFaults(HostId, GuestId);
		harness.Clock.Advance(100);
		harness.Service.Tick(harness.Clock.NowMs);

		Assert.Equal(1, received);
	}

	[Fact]
	public void ATickPastTheStallCap_AdvancesOnlyTheCap()
	{
		var harness = Create();
		var snapshot = Snapshot(bloodVolume: 100f, bleed: 10f);
		harness.Characters.SaveCharacterData(GuestId, snapshot);
		harness.Clock.Advance(16);
		harness.Service.Tick(harness.Clock.NowMs);

		// A five-second stall must be worth 0.25 s of model time, not five.
		harness.Clock.Advance(5_000);
		harness.Service.Tick(harness.Clock.NowMs);

		Assert.InRange(snapshot.Health!.BloodVolume, 99.95f, 99.99f);
	}

	[Fact]
	public void AnAuthoritativeSnapshotWrite_ReseedsTheModelAndIsNotAdvancedAgain()
	{
		var harness = Create();
		var snapshot = Snapshot(bloodVolume: 100f, bleed: 10f);
		harness.Characters.SaveCharacterData(GuestId, snapshot);
		harness.Clock.Advance(16);
		harness.Service.Tick(harness.Clock.NowMs);

		// The host's own applier writes the victim's saved snapshot (SaveCharacterData, the
		// authoritative path). That is the only writer of the owned values, so the next tick must
		// ADOPT it rather than advance over it.
		snapshot.Health!.BloodVolume = 40f;
		harness.Clock.Advance(100);
		harness.Service.Tick(harness.Clock.NowMs);

		Assert.Equal(40f, snapshot.Health.BloodVolume, 3);
	}

	[Fact]
	public void AGuestPump_NeverAdvances()
	{
		var harness = Create();
		var snapshot = Snapshot(bloodVolume: 100f, bleed: 10f);
		harness.Characters.SaveCharacterData(GuestId, snapshot);
		harness.Session.Role = SessionRole.Guest;

		harness.Clock.Advance(16);
		harness.Service.Tick(harness.Clock.NowMs);

		Assert.Equal(100f, snapshot.Health!.BloodVolume, 3);
	}

	[Fact]
	public void AMemberOutOfTheWorld_IsNotAdvanced()
	{
		var harness = Create();
		var snapshot = Snapshot(bloodVolume: 100f, bleed: 10f);
		harness.Characters.SaveCharacterData(GuestId, snapshot);
		harness.Clock.Advance(16);
		harness.Service.Tick(harness.Clock.NowMs);
		var inWorld = snapshot.Health!.BloodVolume;

		// The guest went back to the menu: no Body exists natively, so the saved values freeze.
		Assert.True(harness.Session.TryGetMember(GuestId, out var member));
		member.InWorld = false;
		harness.Clock.Advance(5_000);
		harness.Service.Tick(harness.Clock.NowMs);

		Assert.Equal(inWorld, snapshot.Health.BloodVolume, 3);
	}

	[Fact]
	public void TheHostOutOfTheWorld_IsNotAdvanced()
	{
		var harness = Create();
		var snapshot = Snapshot(bloodVolume: 100f, bleed: 10f);
		harness.Characters.SaveCharacterData(GuestId, snapshot);
		harness.Session.LocalInWorld = false;

		harness.Clock.Advance(16);
		harness.Service.Tick(harness.Clock.NowMs);

		Assert.Equal(100f, snapshot.Health!.BloodVolume, 3);
	}

	[Fact]
	public void ClearingTheSavedCharacters_DropsTheModelledState()
	{
		var harness = Create();
		harness.Characters.SaveCharacterData(GuestId, Snapshot(bloodVolume: 100f, bleed: 10f));
		harness.Clock.Advance(16);
		harness.Service.Tick(harness.Clock.NowMs);
		Assert.Equal(1, harness.Service.TrackedPlayers);

		// A new run / world restore voids the previous values: the next report seeds fresh.
		harness.Characters.FireSavedCharactersCleared();

		Assert.Equal(0, harness.Service.TrackedPlayers);
	}

	[Fact]
	public void SessionEnd_DropsTheModelledState()
	{
		var harness = Create();
		harness.Characters.SaveCharacterData(GuestId, Snapshot(bloodVolume: 100f, bleed: 10f));
		harness.Clock.Advance(16);
		harness.Service.Tick(harness.Clock.NowMs);
		Assert.Equal(1, harness.Service.TrackedPlayers);

		harness.Session.FireSessionEnded();

		Assert.Equal(0, harness.Service.TrackedPlayers);
	}

	[Fact]
	public void ANonFiniteReport_IsRefusedAndTheModelStaysFinite()
	{
		var harness = Create();
		var snapshot = Snapshot(bloodVolume: 100f, bleed: 10f);
		harness.Characters.SaveCharacterData(GuestId, snapshot);
		harness.Clock.Advance(16);
		harness.Service.Tick(harness.Clock.NowMs);

		// A corrupt authoritative snapshot (an input the model would integrate): the row's owned
		// value is offered as 20, but the report is refused by name and the model keeps its own.
		var corrupt = Snapshot(bloodVolume: 20f, bleed: 10f);
		corrupt.Health!.BloodViscosity = float.NaN;
		harness.Characters.SaveCharacterData(GuestId, corrupt);
		harness.Clock.Advance(100);
		harness.Service.Tick(harness.Clock.NowMs);

		Assert.True(float.IsNaN(corrupt.Health.BloodViscosity), "the refusal leaves the corrupt input where it was");
		Assert.True(!float.IsNaN(corrupt.Health.BloodVolume) && !float.IsInfinity(corrupt.Health.BloodVolume),
			"the model's own value must stay finite");
		Assert.True(corrupt.Health.BloodVolume > 50f, $"the corrupt 20 must not be adopted, got {corrupt.Health.BloodVolume}.");
		Assert.True(harness.Log.HasWarning("non-finite"));
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
			new DefaultPlayerVitalsTuning(),
			log);
		((ICuoService)service).Initialize();
		((ICuoService)service).Start();
		return new Harness(clock, network, session, characters, service, log);
	}

	private static CharacterDataMsg Snapshot(float bloodVolume, float bleed) => new()
	{
		Health = new CharacterHealthMsg { BloodVolume = bloodVolume },
		Limbs = [new CharacterLimbMsg { Index = 0, BleedAmount = bleed, SkinHealth = 50f }],
	};

	private sealed record Harness(
		FakeClock Clock,
		FakeNetwork Network,
		FakeSessionControl Session,
		FakeCharacterDataControl Characters,
		PlayerVitalsService Service,
		RecordingLogger<PlayerVitalsService> Log);
}
