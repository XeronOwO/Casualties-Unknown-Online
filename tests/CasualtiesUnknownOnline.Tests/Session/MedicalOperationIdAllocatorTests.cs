using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class MedicalOperationIdAllocatorTests
{
	[Fact]
	public void Allocator_ReturnsMonotonicUniqueIds()
	{
		var allocator = new MedicalOperationIdAllocator();

		Assert.Equal(1UL, allocator.Next());
		Assert.Equal(2UL, allocator.Next());
		Assert.Equal(3UL, allocator.Next());
	}
}
