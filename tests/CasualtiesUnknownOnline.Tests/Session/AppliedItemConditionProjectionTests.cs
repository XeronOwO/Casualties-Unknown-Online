using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The ONE writer of an applied item row's durability after the apply: the body's own
/// 1 Hz character capture, which already reads the limb component's live condition.
/// Without it the row keeps whatever the apply wrote, so a mod reading the item and the
/// archive that stores it would see a splint frozen at the condition it was installed with.
/// </summary>
public class AppliedItemConditionProjectionTests
{
	private const ulong Owner = 1001;

	[Fact]
	public void AComponentThatDecayed_MovesTheDurableRowToTheSameCondition()
	{
		var (authority, session, projection) = Fixture();
		Apply(authority, session, 42, condition: 1f);

		projection.SyncFromCharacterData(Owner, Splinted(0.62f));

		Assert.Equal(0.62f, authority.FindItem(42)!.Value.Data.Condition, 3);
	}

	[Fact]
	public void AComponentThatBarelyMoved_LeavesTheRowAloneUntilTheStepIsReached()
	{
		// The row follows the component in STEPS rather than mirroring every report: a splint
		// decays by 0.015 a minute, and one command per second per limb would buy nothing the
		// step does not.
		var (authority, session, projection) = Fixture();
		Apply(authority, session, 42, condition: 1f);

		projection.SyncFromCharacterData(Owner, Splinted(0.999f));
		Assert.Equal(1f, authority.FindItem(42)!.Value.Data.Condition, 3);

		projection.SyncFromCharacterData(Owner, Splinted(0.98f));
		Assert.Equal(0.98f, authority.FindItem(42)!.Value.Data.Condition, 3);
	}

	[Fact]
	public void ALimbWithNoAppliedRow_ChangesNothing()
	{
		var (authority, session, projection) = Fixture();
		Apply(authority, session, 42, condition: 1f);

		// The same body, one limb over: the component is there and the row is not, which is the
		// divergence the restore names — this projection is not the place that repairs it.
		projection.SyncFromCharacterData(Owner, Splinted(0.2f, limbIndex: 5));

		Assert.Equal(1f, authority.FindItem(42)!.Value.Data.Condition, 3);
	}

	[Fact]
	public void AComponentOutsideTheFamily_IsNotARow()
	{
		// The ice pack's ChilledLimb is the family's own example of a component an item does NOT
		// turn into, so nothing about it may move an item row.
		var (authority, session, projection) = Fixture();
		Apply(authority, session, 42, condition: 1f);

		projection.SyncFromCharacterData(Owner, Components(new ComponentStateMsg
		{
			TypeName = "ChilledLimb",
			Fields = [new ComponentFieldMsg { Name = "condition", Kind = SaveableFieldKind.Float, FloatValue = 0.1f }],
		}));

		Assert.Equal(1f, authority.FindItem(42)!.Value.Data.Condition, 3);
	}

	[Fact]
	public void ASecondBody_DoesNotMoveTheFirstBodysRow()
	{
		var (authority, session, projection) = Fixture();
		Apply(authority, session, 42, condition: 1f);

		projection.SyncFromCharacterData(Owner + 1, Splinted(0.1f));

		Assert.Equal(1f, authority.FindItem(42)!.Value.Data.Condition, 3);
	}

	private static void Apply(ItemKernelAuthority authority, FakeSessionControl session, ulong instanceId, float condition)
	{
		Assert.True(authority.TrySpawnCarried(
			Owner,
			instanceId,
			"splint",
			new CharacterItemMsg { InstanceId = instanceId, ItemId = "splint", Condition = condition },
			out _,
			out _));
		Assert.True(authority.TryApplyLimb(Owner, instanceId, new ActorId(Owner), 3, null, out _, out _));
		Assert.Equal(ItemLocationKind.Applied, authority.FindItem(instanceId)!.Value.Location.Kind);
		Assert.Equal(condition, authority.FindItem(instanceId)!.Value.Data.Condition, 3);
	}

	private static (ItemKernelAuthority Authority, FakeSessionControl Session, AppliedItemConditionProjection Projection) Fixture()
	{
		var authority = new ItemKernelAuthority(new RecordingLogger<ItemKernelAuthority>());
		var session = new FakeSessionControl { Role = SessionRole.Host, SessionActive = true, LocalSteamId = 1UL };
		var projection = new AppliedItemConditionProjection(session, authority, new RecordingLogger<AppliedItemConditionProjection>());
		return (authority, session, projection);
	}

	private static CharacterDataMsg Splinted(float condition, int limbIndex = 3) =>
		Components(limbIndex, new ComponentStateMsg
		{
			TypeName = "SplintLimb",
			Fields =
			[
				new ComponentFieldMsg { Name = "condition", Kind = SaveableFieldKind.Float, FloatValue = condition },
				new ComponentFieldMsg { Name = "item", Kind = SaveableFieldKind.String, StringValue = "splint" },
			],
		});

	private static CharacterDataMsg Components(params ComponentStateMsg[] components) => Components(3, components);

	private static CharacterDataMsg Components(int limbIndex, params ComponentStateMsg[] components) => new()
	{
		OwnerSteamId = Owner,
		Limbs = [new CharacterLimbMsg { Index = limbIndex, Components = [.. components] }],
	};
}
