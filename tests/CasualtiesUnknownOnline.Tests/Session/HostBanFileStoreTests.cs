using System;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The host ban file's pure store contract: round-trip, disabled/persistence,
/// corrupt-file degradation, and the atomic-write temp contract.
/// </summary>
public class HostBanFileStoreTests
{
	private static string NewPath() =>
		Path.Combine(Path.GetTempPath(), "cuo-tests", "host-ban-store", Guid.NewGuid().ToString("N"), "bans.bin");

	[Fact]
	public void Save_Load_RoundTripsAndLeavesNoTemp()
	{
		var path = NewPath();
		var store = new HostBanFileStore(path, NullLogger<HostBanFileStore>.Instance);

		Assert.True(store.Save([1001UL, 2001UL, 3001UL]));
		Assert.False(File.Exists(path + ".tmp"), "a successful atomic write must not leave its temp file");
		Assert.True(store.TryLoad(out var loaded));
		Assert.Equal(3, loaded.Count);
		Assert.Contains(1001UL, loaded);
		Assert.Contains(2001UL, loaded);
		Assert.Contains(3001UL, loaded);
	}

	[Fact]
	public void MissingFile_LoadsEmpty()
	{
		var store = new HostBanFileStore(NewPath(), NullLogger<HostBanFileStore>.Instance);
		Assert.True(store.TryLoad(out var loaded));
		Assert.Empty(loaded);
	}

	[Fact]
	public void CorruptFile_DegradesToEmpty()
	{
		var path = NewPath();
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8, 9]);

		var store = new HostBanFileStore(path, NullLogger<HostBanFileStore>.Instance);
		Assert.False(store.TryLoad(out var loaded), "a corrupt file must be reported as a failed schema load");
		Assert.Empty(loaded);
	}

	[Fact]
	public void DisabledStore_LoadsEmptyAndSaveIsNoop()
	{
		var store = new HostBanFileStore(null, NullLogger<HostBanFileStore>.Instance);
		Assert.True(store.TryLoad(out var loaded));
		Assert.Empty(loaded);
		Assert.True(store.Save([42UL]));
	}
}
