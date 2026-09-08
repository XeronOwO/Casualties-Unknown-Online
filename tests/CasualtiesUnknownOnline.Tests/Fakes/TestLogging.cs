using CasualtiesUnknownOnline.Runtime.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// Test-composition logging: keep the BepInEx sink, drop the rolling file sink.
/// A test process must not write a latest.log per node — it costs a file handle,
/// an AutoFlush write per log line and a temp directory per node, and two nodes
/// with the same Steam id would race on the same file name because xUnit runs
/// test classes in parallel. The file sink itself is covered directly by
/// <c>LoggingOptionsTests</c>; a test that needs it can re-register a provider
/// after this hook runs (the caller's registrations win).
/// </summary>
internal static class TestLogging
{
	internal static void RemoveFileSink(IServiceCollection services)
	{
		services.RemoveAll<RollingFileLoggerProvider>();
		services.RemoveAll<ILoggerProvider>();
		services.AddSingleton<ILoggerProvider>(p => p.GetRequiredService<BepInExLoggerProvider>());
	}
}
