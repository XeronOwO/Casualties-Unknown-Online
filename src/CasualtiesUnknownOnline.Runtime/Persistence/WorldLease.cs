using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The world folder's writer lease (§5, the concurrent-host case of S4 scope 5).
///
/// The repository is the only writer of a world (decision 164), and that rule holds
/// per FOLDER, not per process: two CUO instances pointed at one world — the same
/// machine twice, a folder copied between machines, a folder opened over a share —
/// would otherwise interleave the §5 transaction, and the transaction is not written
/// to survive that. Both would stage into the one <c>.staging/</c> and both would
/// rename <c>live/</c> aside, so the loser's snapshot is deleted by a commit it never
/// saw. Nothing in the archive would say what happened; the player would simply find
/// an older world.
///
/// So the folder has a lease: the writing process records itself and the instant of
/// its last write, refreshes it on every write, and REFUSES to write while a lease
/// another process refreshed recently is in place. The staleness window is what keeps
/// a crash from locking the world forever: a lease nobody has refreshed for
/// <see cref="StaleAfter"/> is taken over, loudly, because a host that has been dead
/// that long cannot be writing.
///
/// The window is deliberately far longer than the interval autosave (ten minutes by
/// default): a live host refreshes its lease on every cut, so thirty minutes means
/// three missed autosaves, which no running session produces.
/// </summary>
internal static class WorldLease
{
	/// <summary>How long a lease may go unrefreshed before another process may take the world over.</summary>
	internal static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

	/// <summary>
	/// This process's writer identity. Machine and pid, because that is what makes two
	/// game instances distinguishable; the same process re-entering a world it already
	/// holds is the same owner by construction, so a menu return and a later Continue
	/// never look like a conflict.
	/// </summary>
	internal static string Owner { get; } = string.Format(
		CultureInfo.InvariantCulture, "{0}:{1}", Environment.MachineName, Process.GetCurrentProcess().Id);

	/// <summary>The lease file inside a world folder.</summary>
	internal static string PathOf(string worldDirectory) => Path.Combine(worldDirectory, SaveArchiveFormat.LeaseFileName);

	/// <summary>
	/// Records this process as the world's writer, or answers false with the holder
	/// named. A lease this process already holds is refreshed; a lease nobody refreshed
	/// inside <see cref="StaleAfter"/> is taken over with a warning; an unreadable lease
	/// file is treated as stale, because it cannot name a live writer.
	///
	/// A lease file that cannot be WRITTEN is not a refusal: the folder being unwritable
	/// is the write transaction's own failure to report, with its own step name, and
	/// turning it into a lease refusal here would name the wrong cause.
	/// </summary>
	internal static bool TryAcquire(string worldDirectory, DateTime nowUtc, ILogger log, out string refusal)
	{
		refusal = string.Empty;
		var path = PathOf(worldDirectory);
		var held = Read(worldDirectory, log);
		if (held is not null && !string.Equals(held.Owner, Owner, StringComparison.Ordinal))
		{
			var age = nowUtc - HeartbeatOf(held);
			if (age < StaleAfter && IsOwnerAlive(held.Owner))
			{
				refusal = $"another CUO instance ({held.Owner}) is writing this world; its last write was {Describe(age)} ago";
				log.LogWarning("World {Directory} is leased to {Owner} (last write {Age} ago); this instance will not write into it.", worldDirectory, held.Owner, Describe(age));
				return false;
			}

			// A lease nobody refreshed for the whole window — or one whose process is provably
			// gone — belongs to a writer that cannot be writing: taking it over is what keeps a
			// crashed host from locking the world, and it is never silent.
			log.LogWarning(
				"Taking world {Directory} over from {Owner}: {Reason} (the staleness window is {Stale}).",
				worldDirectory, held.Owner, DescribeTakeover(held.Owner, age), StaleAfter);
		}

		try
		{
			Directory.CreateDirectory(worldDirectory);
			SaveArchiveJson.WriteFile(path, new WorldLeaseEntry(Owner, SaveArchiveFormat.FormatUtc(nowUtc)));
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
		{
			log.LogWarning(ex, "The world writer lease {Path} could not be recorded; the write continues and reports its own failure if the folder is unusable.", path);
			return true;
		}
	}

	/// <summary>Removes the lease when THIS process owns it; another process's lease is left alone.</summary>
	internal static void Release(string worldDirectory, ILogger log)
	{
		var path = PathOf(worldDirectory);
		try
		{
			if (File.Exists(path) && Read(worldDirectory, log) is { } held && string.Equals(held.Owner, Owner, StringComparison.Ordinal))
			{
				File.Delete(path);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// A lease that outlives the process is harmless — it goes stale — but it must
			// not vanish silently, because the next instance's takeover line is the only
			// trace of it.
			log.LogDebug(ex, "The world writer lease {Path} could not be removed.", path);
		}
	}

	/// <summary>The lease a world folder carries, or null when it carries none (or one that cannot be read).</summary>
	internal static WorldLeaseEntry? Read(string worldDirectory, ILogger log)
	{
		var path = PathOf(worldDirectory);
		if (!File.Exists(path))
		{
			return null;
		}

		try
		{
			var entry = SaveArchiveJson.Deserialize<WorldLeaseEntry>(File.ReadAllBytes(path));
			if (entry is null || string.IsNullOrEmpty(entry.Owner) || !SaveArchiveFormat.TryParseUtc(entry.HeartbeatUtc, out _))
			{
				log.LogWarning("The world writer lease {Path} does not name a writer and a time; it is treated as stale.", path);
				return null;
			}

			return entry;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
		{
			log.LogWarning(ex, "The world writer lease {Path} is unreadable; it is treated as stale.", path);
			return null;
		}
	}

	private static DateTime HeartbeatOf(WorldLeaseEntry entry) =>
		SaveArchiveFormat.TryParseUtc(entry.HeartbeatUtc, out var utc) ? utc : DateTime.MinValue;

	/// <summary>Why a lease is being taken over, in the words the warning carries.</summary>
	private static string DescribeTakeover(string owner, TimeSpan age) =>
		IsOwnerAlive(owner)
			? $"its lease has not been refreshed for {Describe(age)}"
			: $"the process that held it is gone (last write {Describe(age)} ago)";

	/// <summary>
	/// True = the lease's owner is a process on THIS machine that is still running. A lease
	/// whose process is gone is stale whatever its heartbeat says: the alternative is a host
	/// that crashed a minute ago locking the world for the rest of the window. An owner on
	/// another machine cannot be checked at all, so there its heartbeat is the only evidence
	/// — and anything this machine cannot inspect counts as alive, because taking a world
	/// over from a writer that IS running is the one outcome the lease exists to prevent.
	/// </summary>
	private static bool IsOwnerAlive(string owner)
	{
		var separator = owner.LastIndexOf(':');
		if (separator <= 0
			|| !string.Equals(owner.Substring(0, separator), Environment.MachineName, StringComparison.OrdinalIgnoreCase)
			|| !int.TryParse(owner.Substring(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
		{
			return true;
		}

		try
		{
			using var process = Process.GetProcessById(pid);
			return !process.HasExited;
		}
		catch (ArgumentException)
		{
			// No process with that id: the writer is gone.
			return false;
		}
		catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
		{
			// "It exited while we looked" is a dead writer; "we may not look" is a live one.
			return ex is Win32Exception;
		}
	}

	private static string Describe(TimeSpan age) =>
		age <= TimeSpan.Zero
			? "less than a second"
			: string.Format(CultureInfo.InvariantCulture, "{0:F0} minute(s)", Math.Max(1.0, age.TotalMinutes));
}
