using System;
using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The limb record crosses the wire once and is written back onto a live limb by the heal reset and
/// the restore. Mapster's convention map reads a <c>Limb</c>'s public fields as a SOURCE but does not
/// write them as a DESTINATION, so the write half has to be explicit — a commission the mapping once
/// left implicit (2026-11-28: `/heal @a` answered applied and the host's abdomen still read 63.926;
/// four rows then found no free slot in an empty bag, because the slot lives on a limb whose skin
/// never came back). This gate keeps every state field the wire carries in the explicit map.
/// </summary>
public class LimbStateWriteCompletenessGateTests
{
	private const string MapperPath = "src/CasualtiesUnknownOnline.GameAdapter/Character/CharacterDataMapper.cs";

	/// <summary>The reverse map must exist and name each state field, not lean on convention.</summary>
	private static readonly string[] RequiredMaps =
	[
		"TypeAdapterConfig<CharacterLimbMsg, Limb>.NewConfig()",
		".Map(d => d.skinHealth, s => s.SkinHealth)",
		".Map(d => d.muscleHealth, s => s.MuscleHealth)",
		".Map(d => d.bleedAmount, s => s.BleedAmount)",
		".Map(d => d.pain, s => s.Pain)",
		".Map(d => d.broken, s => s.Broken)",
		".Map(d => d.dislocated, s => s.Dislocated)",
		".Map(d => d.splinted, s => s.Splinted)",
		".Map(d => d.infectionAmount, s => s.InfectionAmount)",
		".Map(d => d.dismembered, s => s.Dismembered)",
		".Map(d => d.shrapnel, s => s.Shrapnel)",
	];

	[Fact]
	public void The_limb_record_has_an_explicit_write_half()
	{
		var source = File.ReadAllText(RepositoryPaths.File(MapperPath));
		foreach (var required in RequiredMaps)
		{
			Assert.Contains(required, source, StringComparison.Ordinal);
		}
	}
}
