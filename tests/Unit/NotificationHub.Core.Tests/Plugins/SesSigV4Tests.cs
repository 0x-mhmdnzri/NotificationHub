using FluentAssertions;
using NotificationHub.Plugins.Email.Ses;

namespace NotificationHub.Core.Tests.Plugins;

public class SesSigV4Tests
{
    [Fact]
    public void TC_F_SES_001_SignatureKey_Deterministic()
    {
        var k1 = SesEmailPlugin.GetSignatureKey("wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY", "20150830", "us-east-1", "ses");
        var k2 = SesEmailPlugin.GetSignatureKey("wJalrXUtnFEMI/K7MDENG+bPxRfiCYEXAMPLEKEY", "20150830", "us-east-1", "ses");
        SesEmailPlugin.ToHex(k1).Should().Be(SesEmailPlugin.ToHex(k2));
        SesEmailPlugin.ToHex(k1).Should().HaveLength(64);
    }
}
