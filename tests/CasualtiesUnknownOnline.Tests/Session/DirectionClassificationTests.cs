using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The classification-completeness guard: every NetMsg value must appear in
/// exactly one direction list (see the three direction-contract classes). The
/// receiver is fail-closed (an unregistered id is dropped), but this contract
/// still matters: a new message that is not listed in any family — or whose
/// handler attribute does not match that family — never gets its intended
/// direction enforced. This class needs no session and builds no nodes.
/// </summary>
public class DirectionClassificationTests
{
	[Fact]
	public void EveryNetMsg_IsExplicitlyClassified()
	{
		var all = Enum.GetValues(typeof(NetMsg)).Cast<NetMsg>().ToHashSet();
		var classified = ((IEnumerable<object[]>)GuestToHostDirectionTests.GuestToHostMessages)
			.Concat((IEnumerable<object[]>)HostToGuestDirectionTests.HostToGuestMessages)
			.Concat((IEnumerable<object[]>)BidirectionalDirectionTests.BidirectionalMessages)
			.Select(row => (NetMsg)row[0])
			.ToHashSet();

		var missing = all.Except(classified).ToList();
		Assert.True(missing.Count == 0,
			$"every NetMsg must be explicitly classified as g2h / h2g / bidirectional; missing: [{string.Join(", ", missing)}]");
	}
}
