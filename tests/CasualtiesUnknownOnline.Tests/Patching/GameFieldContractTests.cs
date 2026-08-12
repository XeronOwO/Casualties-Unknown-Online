using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The Traverse-accessed game members' type contracts — the silent-failure
/// guard for the reflection reads/writes. Traverse.GetValue&lt;T&gt; requires the
/// EXACT field type (a byte field read as int threw InvalidCastException and
/// the geyser report died silently — 1545837; a SetValue with the wrong type
/// killed the peer's rumble+spout, 71a804a). Every field/property the adapter
/// touches through reflection is declared here with its exact game type — a
/// game update that retypes a member fails the test run BEFORE the game
/// launches, instead of killing the report at runtime.
/// </summary>
public class GameFieldContractTests
{
	private enum Kind
	{
		Field,
		Property,
	}

	/// <summary>The declared contracts — one row per Traverse-accessed member.
	/// The type is what the game assembly declares TODAY; the adapter's
	/// GetValue/SetValue calls must keep matching it exactly.</summary>
	private static readonly (string Type, string Member, Kind MemberKind, Type ExpectedType, string Why)[] Declared =
	[
		("GeyserScript", "liquidType", Kind.Field, typeof(byte), "read as byte — a GetValue<int> threw InvalidCastException and killed the geyser report (1545837)"),
		("GeyserScript", "rumbleTime", Kind.Field, typeof(float), "the TryRumble transition edge (TrapGeyserPatch)"),
		("TurretScript", "timeSinceFired", Kind.Property, typeof(float), "the fire skin/lamp timeline (TrapStateActions, #131)"),
		("TurretScript", "didShoot", Kind.Field, typeof(bool), "the reload latch (TrapStateActions)"),
		("TurretScript", "didBeep", Kind.Field, typeof(bool), "the discovery-branch latch (TrapStateActions)"),
	];

	[Fact]
	public void DeclaredTable_EveryContractResolvesWithExactType()
	{
		var violations = new List<string>();
		foreach (var (typeName, memberName, memberKind, expectedType, why) in Declared)
		{
			var type = GameAssemblyHost.Game.GetType(typeName);
			if (type == null)
			{
				violations.Add($"type {typeName} not found in the game assembly (renamed?)");
				continue;
			}

			var member = memberKind == Kind.Field
				? (MemberInfo?)type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
				: type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			if (member == null)
			{
				violations.Add($"{typeName}.{memberName} ({memberKind}) not found (renamed?)");
				continue;
			}

			var actualType = memberKind == Kind.Field ? ((FieldInfo)member).FieldType : ((PropertyInfo)member).PropertyType;
			if (actualType != expectedType)
			{
				violations.Add($"{typeName}.{memberName} is {actualType.Name}, the adapter reads/writes {expectedType.Name} — {why}");
			}
		}

		Assert.True(violations.Count == 0,
			$"game-field contracts broken against the game assembly ({violations.Count}):\n" + string.Join("\n", violations));
	}
}
