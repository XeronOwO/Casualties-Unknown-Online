using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// Anti-rot gate for the xUnit v2 long pole: every test of one class runs
/// strictly serially, so a class with an unbounded number of cases becomes the
/// suite's critical path again (Stage 1/2 split the 135-case class and the
/// 33-row behaviour families for exactly this reason). The limit is the real
/// xUnit case count, including <c>MemberData</c> rows: a syntax-tree gate
/// cannot see those rows, and the original long pole was a <c>MemberData</c>
/// class, so this gate asks xUnit's own data attributes for the rows.
/// </summary>
public class TestClassSizeGateTests
{
	/// <summary>
	/// The agreed per-class limit. The measured maximum on the delivered tree is
	/// 35 cases, so a class that grows by six cases fails and must be split by
	/// behaviour family. Raising this limit requires the same measured evidence
	/// as a stage change; it is not a formatting escape hatch.
	/// </summary>
	internal const int MaxCasesPerClass = 40;

	[Fact]
	public void NoTestClass_ExceedsTheCaseLimit()
	{
		var measured = typeof(TestClassSizeGateTests).Assembly.GetTypes()
			.Where(type => type.IsClass && !type.IsGenericTypeDefinition
				&& (!type.IsAbstract || type.IsSealed)
				&& (type.IsPublic || type.IsNestedPublic))
			.Select(type => new ClassCaseCount(type, CountCases(type)))
			.ToList();
		var failures = FindOversizedClasses(measured);

		Assert.True(failures.Count == 0,
			"Test class size gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void CaseCounting_SeesMemberDataRowsAndFlagsTheLimit()
	{
		// The gate's own contract: a Theory's MemberData row count must be
		// visible (otherwise the exact long-pole mechanism would slip through),
		// inherited test methods and static test classes must be counted (xUnit
		// runs both), and the limit check must flag a class one case above the
		// agreed cap.
		Assert.Equal(3, CountCases(typeof(MemberDataSample)));
		Assert.Equal(1, CountCases(typeof(FactSample)));
		Assert.Equal(4, CountCases(typeof(DerivedTestSample)));
		Assert.Equal(1, CountCases(typeof(StaticTestSample)));
		Assert.Equal(3, CountCases(typeof(MultiDataSample)));

		var failures = FindOversizedClasses(
		[
			new ClassCaseCount(typeof(MemberDataSample), MaxCasesPerClass),
			new ClassCaseCount(typeof(FactSample), MaxCasesPerClass + 1),
		]);
		Assert.Single(failures);
		Assert.Contains(nameof(FactSample), failures[0], StringComparison.Ordinal);
	}

	private static IReadOnlyList<string> FindOversizedClasses(IEnumerable<ClassCaseCount> classes) =>
		classes
			.Where(entry => entry.Cases > MaxCasesPerClass)
			.Select(entry => $"{entry.Type.FullName} : {entry.Cases} test cases (max {MaxCasesPerClass}); "
				+ "split the class by behaviour family so xUnit does not serialize the whole family in one collection.")
			.ToList();

	private static int CountCases(Type type)
	{
		var methods = new HashSet<MethodInfo>();
		for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
		{
			foreach (var method in current.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
				| BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
			{
				methods.Add(method);
			}
		}

		return methods
			.GroupBy(method => method.GetBaseDefinition())
			.Select(group => group.First())
			.Sum(CountMethodCases);
	}

	private static int CountMethodCases(MethodInfo method)
	{
		var theories = method.GetCustomAttributes<TheoryAttribute>(inherit: true).ToList();
		if (theories.Count > 0)
		{
			return CountTheoryCases(method);
		}

		return method.GetCustomAttributes<FactAttribute>(inherit: true).Count();
	}

	private static int CountTheoryCases(MethodInfo method)
	{
		var dataAttributes = method.GetCustomAttributes(inherit: true).OfType<DataAttribute>().ToList();
		if (dataAttributes.Count == 0)
		{
			// xUnit rejects a Theory without a data source at discovery; count it
			// as one so the gate's message stays about class size.
			return 1;
		}

		var total = 0;
		foreach (var attribute in dataAttributes)
		{
			try
			{
				total += attribute.GetData(method)?.Count() ?? 0;
			}
			catch (Exception exception)
			{
				throw new InvalidOperationException(
					$"failed to enumerate {attribute.GetType().Name} data for "
					+ $"{method.DeclaringType?.FullName}.{method.Name}", exception);
			}
		}

		return total;
	}

	private sealed record ClassCaseCount(Type Type, int Cases);

	// These are private fixtures for the counting contract, not discoverable
	// xUnit classes; the analyzer cannot see that distinction.
#pragma warning disable xUnit1000
	private sealed class MemberDataSample
	{
		public static IEnumerable<object[]> Rows =>
		[
			[1],
			[2],
			[3],
		];

		[Theory]
		[MemberData(nameof(Rows))]
		public void Theory(int value) => Assert.InRange(value, 1, 3);
	}

	private sealed class FactSample
	{
		[Fact]
		public void Fact() => Assert.True(true);
	}

	private abstract class BaseTestSample
	{
		[Fact]
		public void BaseFact() => Assert.True(true);

		[Theory]
		[InlineData(1)]
		[InlineData(2)]
		public void BaseTheory(int value) => Assert.InRange(value, 1, 2);
	}

	private sealed class DerivedTestSample : BaseTestSample
	{
		[Fact]
		public void DerivedFact() => Assert.True(true);
	}

	private static class StaticTestSample
	{
		[Fact]
		public static void StaticFact() => Assert.True(true);
	}

	private sealed class MultiDataSample
	{
		public static IEnumerable<object[]> Rows =>
		[
			[1],
			[2],
		];

		[Theory]
		[InlineData(3)]
		[MemberData(nameof(Rows))]
		public void Theory(int value) => Assert.InRange(value, 1, 3);
	}
#pragma warning restore xUnit1000
}
