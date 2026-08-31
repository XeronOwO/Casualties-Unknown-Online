using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class HostRulesJsonApplierTests
{
	[Fact]
	public void TryApply_ValidObject_AppliesEachPair()
	{
		var editor = new StubEditor();
		var ok = HostRulesJsonApplier.TryApply(
			"{\"AllowLateJoin\": false, \"PiggybackWeightMultiplier\": 2.5}",
			editor,
			out var updated,
			out var error);

		Assert.True(ok);
		Assert.Null(error);
		Assert.Equal(2, updated);
		Assert.Contains(("AllowLateJoin", "false"), editor.Applied);
		Assert.Contains(("PiggybackWeightMultiplier", "2.5"), editor.Applied);
	}

	[Fact]
	public void TryApply_EditorRejects_ReturnsErrorAndNoCount()
	{
		var editor = new StubEditor(("AllowLateJoin", "notabool"));
		var ok = HostRulesJsonApplier.TryApply(
			"{\"AllowLateJoin\": \"notabool\"}",
			editor,
			out var updated,
			out var error);

		Assert.False(ok);
		Assert.NotNull(error);
		Assert.Equal(0, updated);
	}

	[Fact]
	public void TryApply_MalformedJson_ReturnsParserError()
	{
		var editor = new StubEditor();
		var ok = HostRulesJsonApplier.TryApply(
			"{\"AllowLateJoin\": false",
			editor,
			out var updated,
			out var error);

		Assert.False(ok);
		Assert.NotNull(error);
		Assert.Equal(0, updated);
	}

	private sealed class StubEditor : IHostRulesEditor
	{
		private readonly (string Property, string Value)? _reject;

		internal StubEditor((string Property, string Value)? reject = null)
		{
			_reject = reject;
		}

		internal List<(string Property, string Value)> Applied { get; } = [];

		public bool TrySet(string property, string value, out string? error)
		{
			if (_reject is { } reject && reject.Property == property)
			{
				error = $"Invalid value for {property}.";
				return false;
			}

			Applied.Add((property, value));
			error = null;
			return true;
		}
	}
}
