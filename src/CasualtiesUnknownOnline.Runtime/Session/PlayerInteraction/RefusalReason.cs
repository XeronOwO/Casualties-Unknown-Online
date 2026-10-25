namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Why a cross-player use was refused, as a KEY plus at most one argument — never as a sentence.
///
/// <para>
/// The user's 2026-10-25 ruling ("不只是命令，涉及到多语言的都要改") and its visible case: a Chinese client read
/// <c>使用被拒绝：injectable medicine goes through the medical view。</c> — a Chinese frame wrapped around the English sentence the
/// host had logged. A sentence crosses a language boundary only once, so what crosses now is the key and its values, and the
/// client that READS the line renders it in its own language. The host's log keeps an English line of its own, built from the
/// same key rather than from a second copy of the wording.
/// </para>
/// <para>
/// The KEY is produced by a refusal site and the catalogue owns its wording, so a site never carries a sentence of its own
/// language: the census treats a literal passed at a <c>labelKey</c> position as a READ, which is why the producers here are
/// the <c>Refuse</c> helpers on each side rather than a bare constructor call.
/// </para>
/// </summary>
public readonly record struct RefusalReason(string Key, string? Argument = null);
