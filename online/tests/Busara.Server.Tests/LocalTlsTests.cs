using Xunit;

namespace Busara.Server.Tests;

public sealed class LocalTlsTests
{
    [Theory]
    [InlineData("https://127.0.0.1:55433/", true)]
    [InlineData("https://localhost:55433/", true)]
    [InlineData("wss://127.0.0.1:55433/events", true)]
    [InlineData("https://[::1]:55433/", true)]
    [InlineData("http://localhost:55433/", false)]
    [InlineData("ws://localhost:55433/events", false)]
    [InlineData("https://example.com/", false)]
    [InlineData("https://localhost.example.com/", false)]
    [InlineData("https://127.0.0.1.example.com/", false)]
    [InlineData("/relative", false)]
    public void Certificate_exception_is_limited_to_secure_loopback_targets(string value, bool expected)
    {
        Assert.Equal(expected, PostgresFixture.IsLocalTlsTarget(new Uri(value, UriKind.RelativeOrAbsolute)));
    }

    [Fact]
    public void Handler_rejects_remote_certificates_and_does_not_follow_redirects()
    {
        using var handler = PostgresFixture.NewHttpHandler();
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(PostgresFixture.IsLocalTlsTarget(null));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/");
        Assert.False(handler.ServerCertificateCustomValidationCallback!(request, null, null,
            System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors));
    }
}
