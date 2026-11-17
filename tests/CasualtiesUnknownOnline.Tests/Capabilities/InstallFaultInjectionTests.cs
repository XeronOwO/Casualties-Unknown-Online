using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Capabilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Capabilities;

/// <summary>
/// The fault seam the player-visible refusal row runs on: with
/// CUO_FAULT_CAPABILITY set, a REQUIRED capability reports one blocking failure
/// under its own player-facing title (so the install refuses and the mod's Home
/// page names the system), an OPTIONAL one reports the same way but degrades as
/// the real rule says, and an id the catalog does not declare is logged and
/// ignored — the seam must never invent a capability and never hide a real miss.
/// </summary>
public class InstallFaultInjectionTests
{
	private static readonly ILogger Log = NullLogger.Instance;

	[Fact]
	public void Unset_AddsNothing()
	{
		Assert.Empty(InstallFaultInjection.Applied(null, Log));
		Assert.Empty(InstallFaultInjection.Applied("  ", Log));
		Assert.Empty(InstallFaultInjection.Parse(null));
	}

	[Fact]
	public void ARequiredCapability_ReportsOneBlockingFailureUnderItsOwnTitle()
	{
		var failure = Assert.Single(InstallFaultInjection.Applied("medical", Log));

		Assert.True(failure.BlocksInstall);
		Assert.Contains("fault injection", failure.Detail);
		Assert.True(AdapterCapabilityCatalog.IsRequiredOwner(failure.PatchClassType));
		Assert.Equal(
			AdapterCapabilityCatalog.Find("medical")!.Title,
			AdapterCapabilityCatalog.TitleOfPatchClass(failure.PatchClassType));
	}

	[Fact]
	public void AnOptionalCapability_ReportsAFailureTheDegradationRuleReadsAsOptional()
	{
		var failure = Assert.Single(InstallFaultInjection.Applied("mods", Log));

		Assert.True(failure.BlocksInstall);
		Assert.False(AdapterCapabilityCatalog.IsRequiredOwner(failure.PatchClassType));
	}

	[Fact]
	public void AnUndeclaredId_IsIgnoredAndSaidSo()
	{
		var recording = new RecordingLogger();

		Assert.Empty(InstallFaultInjection.Applied("no-such-capability", recording));
		Assert.Contains(recording.Warnings, line => line.Contains("no-such-capability", StringComparison.Ordinal));
	}

	[Fact]
	public void TwoIds_SeparatedByEitherMark_AnswerInOrderAndOnce() =>
		Assert.Equal(["session", "mods"], InstallFaultInjection.Parse(" session , mods ; session "));

	private sealed class RecordingLogger : ILogger
	{
		internal List<string> Warnings { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (logLevel >= LogLevel.Warning)
			{
				Warnings.Add(formatter(state, exception));
			}
		}
	}
}
