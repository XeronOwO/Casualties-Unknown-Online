using CasualtiesUnknownOnline.Runtime.Networking;
using Steamworks;
using Xunit;
using System;

namespace CasualtiesUnknownOnline.Tests.Networking;

/// <summary>
/// L0 tests for the Steam P2P send-failure classification added during the
/// #118 investigation. The mapping is purely mechanical: the transport logs a
/// compact family + actionable remediation instead of only the raw Steamworks
/// enum, so a fresh log line can point at the local-proxy/Steam-client causes
/// without requiring an interactive Steam session.
/// </summary>
public class SteamSendFailureClassifierTests
{
	[Fact]
	public void BadCert_EndReason_IsClassifiedAsBadCert()
	{
		Assert.Equal(
			SteamSendFailureKind.BadCert,
			SteamSendFailureClassifier.Classify(
				EResult.k_EResultOK,
				ESteamNetConnectionEnd.k_ESteamNetConnectionEnd_Remote_BadCert));
	}

	[Fact]
	public void Rendezvous_EndReason_IsClassifiedAsRendezvous()
	{
		Assert.Equal(
			SteamSendFailureKind.Rendezvous,
			SteamSendFailureClassifier.Classify(
				EResult.k_EResultOK,
				ESteamNetConnectionEnd.k_ESteamNetConnectionEnd_Misc_P2P_Rendezvous));
	}

	[Fact]
	public void ManyRelayConnectivity_IsClassifiedAsRendezvous()
	{
		Assert.Equal(
			SteamSendFailureKind.Rendezvous,
			SteamSendFailureClassifier.Classify(
				EResult.k_EResultOK,
				ESteamNetConnectionEnd.k_ESteamNetConnectionEnd_Local_ManyRelayConnectivity));
	}

	[Fact]
	public void ConnectFailed_Result_IsClassifiedAsConnectFailed()
	{
		Assert.Equal(
			SteamSendFailureKind.ConnectFailed,
			SteamSendFailureClassifier.Classify(EResult.k_EResultConnectFailed, ESteamNetConnectionEnd.k_ESteamNetConnectionEnd_Invalid));
	}

	[Fact]
	public void NoConnection_Result_IsClassifiedAsNoConnection()
	{
		Assert.Equal(
			SteamSendFailureKind.NoConnection,
			SteamSendFailureClassifier.Classify(EResult.k_EResultNoConnection, ESteamNetConnectionEnd.k_ESteamNetConnectionEnd_Invalid));
	}

	[Fact]
	public void Timeout_Result_IsClassifiedAsTimeout()
	{
		Assert.Equal(
			SteamSendFailureKind.Timeout,
			SteamSendFailureClassifier.Classify(EResult.k_EResultTimeout, ESteamNetConnectionEnd.k_ESteamNetConnectionEnd_Invalid));
	}

	[Fact]
	public void UnknownResultAndEndReason_AreClassifiedAsOther()
	{
		Assert.Equal(
			SteamSendFailureKind.Other,
			SteamSendFailureClassifier.Classify(EResult.k_EResultOK, ESteamNetConnectionEnd.k_ESteamNetConnectionEnd_Invalid));
	}

	[Fact]
	public void BadCertRemediation_NamesTheKnownLocalProxyFirst()
	{
		var remediation = SteamSendFailureClassifier.Remediation(SteamSendFailureKind.BadCert);
		Assert.Contains("Clash", remediation);
		Assert.Contains("localhost:7890", remediation);
		Assert.Contains("Steam", remediation);
	}

	[Fact]
	public void EveryKind_HasARemediation()
	{
		foreach (SteamSendFailureKind kind in Enum.GetValues(typeof(SteamSendFailureKind)))
		{
			Assert.False(string.IsNullOrWhiteSpace(SteamSendFailureClassifier.Remediation(kind)),
				$"missing remediation for {kind}");
		}
	}
}
