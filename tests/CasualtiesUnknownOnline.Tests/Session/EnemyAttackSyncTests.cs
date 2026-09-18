using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The announced enemy attack chain (EnemyAttackMsg): the host's enemy
/// simulation performs an attack, the host BROADCASTS the announcement (which
/// enemy, which kind, the per-enemy identity) to every in-world guest, and each
/// guest judges on its own view whether it was hit and reports the post-attack
/// terminal state through the kernel combat-result events. The host names neither
/// a victim nor a limb — that decision belongs to the client the effect lands on.
/// </summary>
[Trait("Category", "Integration")]
public class EnemyAttackSyncTests
{
	private static readonly NetworkEntityId Enemy = new(7, 3, 0);

	[Fact]
	public void EnemyAttack_RoundTripsTheAnnouncement()
	{
		var source = new EnemyAttackMsg
		{
			EnemyId = Enemy.ToNetworkEntityIdMsg(),
			Kind = EnemyAttackKind.CrystalLunge,
			AttackSeq = 7,
		};

		var decoded = NetPacket.DecodePayload<EnemyAttackMsg>(
			NetPacket.Encode(NetMsg.EnemyAttack, source));

		Assert.Equal(Enemy, decoded.EnemyId.ToNetworkEntityId());
		Assert.Equal(EnemyAttackKind.CrystalLunge, decoded.Kind);
		Assert.Equal(7u, decoded.AttackSeq);
	}

	[Fact]
	public void Announcement_ReachesEveryInWorldGuest_WithAPerEnemyIdentity()
	{
		using var w = ItemSimWorld.Create();
		var hostEnemies = w.Host.Services.GetRequiredService<EnemySyncService>();
		foreach (var member in w.Host.Session.Members)
		{
			member.InWorld = true;
		}

		var firstGuestSeqs = new List<uint>();
		var secondGuestSeqs = new List<uint>();
		w.G1.Services.GetRequiredService<EnemySyncService>().EnemyAttackReceived += msg => firstGuestSeqs.Add(msg.AttackSeq);
		w.G2.Services.GetRequiredService<EnemySyncService>().EnemyAttackReceived += msg => secondGuestSeqs.Add(msg.AttackSeq);

		hostEnemies.SendEnemyAttack(Enemy, EnemyAttackKind.SpiderBite);
		hostEnemies.SendEnemyAttack(Enemy, EnemyAttackKind.SpiderBite);
		hostEnemies.SendEnemyAttack(new NetworkEntityId(7, 4, 0), EnemyAttackKind.CrystalLunge);

		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.EnemyAttack) == 3, "every in-world guest must receive every announcement");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.EnemyAttack) == 3, "the announcement is a broadcast, not a verdict for one victim");
		Assert.Equal(new uint[] { 1, 2, 1 }, firstGuestSeqs);
		Assert.Equal(firstGuestSeqs, secondGuestSeqs);
	}

	[Fact]
	public void Announcement_IsNotSentWhileNoGuestIsInWorld()
	{
		using var w = ItemSimWorld.Create();
		var hostEnemies = w.Host.Services.GetRequiredService<EnemySyncService>();

		hostEnemies.SendEnemyAttack(Enemy, EnemyAttackKind.SpiderBite);

		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.EnemyAttack) == 0, "a menu/loading guest cannot receive an in-world announcement");
	}
}
