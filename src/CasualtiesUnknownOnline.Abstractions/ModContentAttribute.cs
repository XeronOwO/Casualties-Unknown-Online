using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Declares a class as one piece of its mod's content, so the declaration lives
/// next to the code that owns it and the framework finds it: at discovery the
/// class is instantiated and registered through
/// <see cref="IModContent.TryRegister"/> on the mod's behalf, with no
/// registration call written by the mod.
///
/// The attribute carries NO data — not even the id, because the address belongs
/// to the definition (decision 247) and this attribute only DISCOVERS it.
///
/// <b>The kind is the interface.</b> A declaration implements exactly one KIND
/// CONTRACT — <see cref="IModItemDefinition"/> and its eight siblings — and is
/// registered as that kind, never as one derived from the attribute or the
/// class name (<see cref="ModContentContract"/> holds the mapping). A class
/// that implements two kind contracts, none of them, or one whose
/// <see cref="IModContentDefinition.Kind"/> member disagrees with the contract
/// it implements is REFUSED at discovery with a log naming it: the framework
/// never guesses a kind.
///
/// <b>Ownership is nesting.</b> A declaration nested inside a mod class is that
/// mod's. A declaration that is not nested in a mod belongs to the mod its
/// assembly declares, which therefore has to be the only one; a top-level
/// declaration in an assembly that declares several mods has no owner and is
/// refused with a log naming it (the mods themselves still load, and the code
/// path stays available to them).
///
/// <b>What the scan requires and what it executes.</b> The declaration is a
/// public concrete class with a public parameterless constructor, and the
/// framework constructs it and reads its members at discovery — so a mod may
/// compute them, and the value the game later reads is the computed one. A
/// constructor that throws, and a member getter that throws, each refuse THAT
/// declaration with a log naming it and leave its siblings binding, which is the
/// per-declaration isolation the code path does not have.
/// Registration itself is <see cref="IModContent.TryRegister"/>: it needs
/// <see cref="ModPermission.RegisterContent"/>, obeys the id/kind/schema rails
/// and the per-mod cap, and the scan adds no rule of its own.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ModContentAttribute : Attribute
{
}
