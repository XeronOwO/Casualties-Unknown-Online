using System;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// A mod that throws in Update — the exception-isolation test's victim. Its
/// presence in the test assembly means EVERY TestNode discovers it; the
/// ModService isolates the throw (per-mod catch-and-log), so no other test is
/// affected — the pump and the sibling mods continue.
/// </summary>
[CuoMod("test.throwing", "Throwing", "1.0.0", NetworkMode = NetworkMode.ClientOnly)]
public sealed class TestThrowingMod : ICuoMod
{
	public int UpdateAttempts { get; private set; }

	public void Bind(IModContext context)
	{
	}

	public void Initialize()
	{
	}

	public void Start()
	{
	}

	public void Update()
	{
		UpdateAttempts++;
		throw new InvalidOperationException("test.throwing always throws in Update");
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}
}
