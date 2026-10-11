using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The restore seam's comparison of the applied family's two carriers. It is read-only
/// by construction and it only ever NAMES a disagreement: either carrier could be the
/// stale one, and picking a winner would silently rewrite a world the player saved.
/// </summary>
public class RestoredAppliedItemAuditTests
{
	private const ulong Owner = 1001;

	[Fact]
	public void AMatchingPair_IsReportedAsAgreeingAndNothingIsWarned()
	{
		var kernel = Kernel(out _);
		Assert.True(kernel.TrySpawnCarried(Owner, 42, "splint", Item(42, "splint"), out _, out _));
		Assert.True(kernel.TryApplyLimb(Owner, 42, new ActorId(Owner), 3, null, out _, out _));

		var (audit, logger) = Audit(kernel);
		audit.Audit(Bound(Splinted(3, "splint")));

		Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
		Assert.Contains(logger.Entries, entry => entry.Message.Contains("agree in both directions"));
	}

	[Fact]
	public void AComponentWithNoAppliedRow_IsNamed()
	{
		// The item row is the authority for WHICH item is installed, so a component no row
		// names has no id at all — the gesture that takes it off will have to create a new item.
		var (audit, logger) = Audit(Kernel(out _));
		audit.Audit(Bound(Splinted(3, "splint")));

		var error = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
		Assert.Contains("1 limb component(s) with no applied item row", error.Message);
		Assert.Contains("0 applied item row(s) with no limb component", error.Message);
		Assert.Contains(logger.Entries, entry => entry.Message.Contains("will create a NEW item"));
	}

	[Fact]
	public void AnAppliedRowWithNoComponent_IsNamed()
	{
		// The other direction, which nothing else in the tree can see: the world says the item
		// is installed on a limb that does not carry it, so nothing will ever take it off.
		var kernel = Kernel(out _);
		Assert.True(kernel.TrySpawnCarried(Owner, 42, "splint", Item(42, "splint"), out _, out _));
		Assert.True(kernel.TryApplyLimb(Owner, 42, new ActorId(Owner), 3, null, out _, out _));

		var (audit, logger) = Audit(kernel);
		audit.Audit(Bound(NoLimbs()));

		var error = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
		Assert.Contains("0 limb component(s) with no applied item row", error.Message);
		Assert.Contains("1 applied item row(s) with no limb component", error.Message);
	}

	[Fact]
	public void ADifferentLimbOrDefinitionOnTheSameBody_IsNotTheSamePair()
	{
		// The binding is (owner, limb, definition): one limb legally carries a splint and a
		// tourniquet at once, and a splint row does not answer for the tourniquet's component.
		var kernel = Kernel(out _);
		Assert.True(kernel.TrySpawnCarried(Owner, 42, "splint", Item(42, "splint"), out _, out _));
		Assert.True(kernel.TryApplyLimb(Owner, 42, new ActorId(Owner), 3, null, out _, out _));

		var (audit, logger) = Audit(kernel);
		audit.Audit(Bound(Splinted(3, "splint", tourniquetOnTheSameLimb: true)));

		// The tourniquet's component has no row, and the splint's row is answered for.
		var error = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
		Assert.Contains("1 limb component(s) with no applied item row", error.Message);
		Assert.Contains("0 applied item row(s) with no limb component", error.Message);
	}

	[Fact]
	public void ACharacterNobodyBound_IsNotAudited()
	{
		// A stored character no present peer claims restores nobody's body, so naming its
		// divergence would describe a degradation no player has.
		var (audit, logger) = Audit(Kernel(out _));
		audit.Audit(new Dictionary<ulong, CharacterDataMsg>());

		Assert.Empty(logger.Entries);
	}

	private static CharacterDataMsg Splinted(int limbIndex, string definitionId, bool tourniquetOnTheSameLimb = false) => new()
	{
		OwnerSteamId = Owner,
		Limbs =
		[
			new CharacterLimbMsg
			{
				Index = limbIndex,
				Components =
				[
					new ComponentStateMsg
					{
						TypeName = "SplintLimb",
						Fields =
						[
							new ComponentFieldMsg { Name = "item", Kind = SaveableFieldKind.String, StringValue = definitionId },
						],
					},
					.. tourniquetOnTheSameLimb
						? new[] { new ComponentStateMsg { TypeName = "TourniquetScript" } }
						: [],
				],
			},
		],
	};

	private static CharacterDataMsg NoLimbs() => new() { OwnerSteamId = Owner };

	private static Dictionary<ulong, CharacterDataMsg> Bound(params CharacterDataMsg[] characters) =>
		characters.ToDictionary(character => character.OwnerSteamId, character => character);

	private static CharacterItemMsg Item(ulong instanceId, string definitionId) =>
		new() { InstanceId = instanceId, ItemId = definitionId, Condition = 0.75f };

	private static (RestoredAppliedItemAudit Audit, RecordingLogger<RestoredAppliedItemAudit> Logger) Audit(ItemKernelAuthority kernel)
	{
		var logger = new RecordingLogger<RestoredAppliedItemAudit>();
		return (new RestoredAppliedItemAudit(kernel, logger), logger);
	}

	private static ItemKernelAuthority Kernel(out RecordingLogger<ItemKernelAuthority> logger)
	{
		logger = new RecordingLogger<ItemKernelAuthority>();
		return new ItemKernelAuthority(logger);
	}
}
