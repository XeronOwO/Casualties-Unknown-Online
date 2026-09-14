using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// What one encode pass produced: the payload files of a cut, and the per-domain row
/// counts the ARCHIVE will hold. They travel together because only the encoder knows
/// what its own filtering DID — a layer-end cut drops the in-layer rows (the
/// world-rooted items, the world-entity facts, the live enemies, the fluid chunks and
/// the payload's world facts), so a caller that counted the checkpoint it handed in
/// would report a different set than the archive holds, and than the restore reading
/// that archive can ever report (see <see cref="WorldSnapshotCounts"/>).
/// </summary>
internal sealed record EncodedSnapshot(IReadOnlyList<SavePayloadFile> Files, WorldSnapshotCounts Counts);
