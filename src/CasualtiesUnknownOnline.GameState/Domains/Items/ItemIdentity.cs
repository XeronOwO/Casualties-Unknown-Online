namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// Stable item identity: the runtime instance id plus the game definition id.
/// Unity instance ids are never used as kernel identity.
/// </summary>
public readonly record struct ItemIdentity(ulong InstanceId, string DefinitionId);
