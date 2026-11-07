using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>One reported observation from the mod comparison: what differed, on which mod, and the two values.</summary>
public sealed record ModAdmissionNote(ModAdmissionNoteKind Kind, string ModId, string MemberValue, string HostValue, NetworkMode Mode);
