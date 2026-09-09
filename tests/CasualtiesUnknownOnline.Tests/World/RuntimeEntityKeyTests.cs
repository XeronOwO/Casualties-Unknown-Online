using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The runtime-creation identity: prefab id + the floored CREATION cell + the
/// creation-instance token (creator SteamId + monotonic sequence). The token is
/// what keeps two identical prefabs created inside one cell apart — the round-3
/// defect — and it must survive the snapshot's key-list round trip, because
/// that list is the only acknowledgement an accepted animal gets.
/// </summary>
public class RuntimeEntityKeyTests
{
	private static EntitySpawnedMsg Creation(string id, float x, float y, ulong creator = 0, uint sequence = 0) => new()
	{
		Id = id,
		Position = new NetVector2Msg(x, y),
		CreatorSteamId = creator,
		CreationSequence = sequence,
	};

	[Fact]
	public void From_FloorsTheCreationPosition() =>
		Assert.Equal(new RuntimeEntityKey("a", 12, -2, 0, 0), RuntimeEntityKey.From(Creation("a", 12.9f, -1.5f)));

	[Fact]
	public void From_TwoCreationsOfTheSamePrefabInOneCell_AreDistinctByToken()
	{
		Assert.NotEqual(
			RuntimeEntityKey.From(Creation("turret", 5.2f, 7.2f, creator: 2001, sequence: 1)),
			RuntimeEntityKey.From(Creation("turret", 5.9f, 7.6f, creator: 2001, sequence: 2)));
	}

	[Fact]
	public void From_ARepeatedReportOfOneCreation_IsTheSameKey()
	{
		// The fallback re-report and the host snapshot send the SAME record, so
		// a small position difference inside the creation cell must not fork
		// the identity as long as the token matches.
		Assert.Equal(
			RuntimeEntityKey.From(Creation("turret", 5.2f, 7.2f, creator: 2001, sequence: 1)),
			RuntimeEntityKey.From(Creation("turret", 5.4f, 7.3f, creator: 2001, sequence: 1)));
	}

	[Fact]
	public void ToKeyMsg_RoundTripsThroughFromKeyMsg()
	{
		var key = new RuntimeEntityKey("a", -3, 4, 2001, 7);

		Assert.Equal(key, RuntimeEntityKey.FromKeyMsg(key.ToKeyMsg()));
	}
}
