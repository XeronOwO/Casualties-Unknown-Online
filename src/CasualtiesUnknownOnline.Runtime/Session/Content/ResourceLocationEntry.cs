using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Content;

/// <summary>
/// One addressable resource/content entry in the console's resource vocabulary.
/// <see cref="Id"/> is always canonical; <see cref="DisplayName"/> is whatever
/// the owning source knows (for vanilla items that is the game-localised item
/// name, so a Chinese client can complete by its own language).
/// </summary>
public sealed record ResourceLocationEntry(ContentId Id, string Kind, string DisplayName);
