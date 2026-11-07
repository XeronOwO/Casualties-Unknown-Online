using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The mod comparison's answer: one refusal (or none) plus the notes the host logs.</summary>
public sealed record ModAdmissionResult(HandshakeRefusal? Refusal, IReadOnlyList<ModAdmissionNote> Notes);
