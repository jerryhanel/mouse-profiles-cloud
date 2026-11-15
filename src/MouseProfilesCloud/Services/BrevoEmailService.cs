using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MouseProfilesCloud.Options;

namespace MouseProfilesCloud.Services;

public class BrevoEmailService : IEmailService
{
    private const string BrevoApiUrl = "https://api.brevo.com/v3/smtp/email";

    private readonly BrevoOptions _options;
    private readonly IHttpClientFactory _http;

    public BrevoEmailService(IOptions<BrevoOptions> options, IHttpClientFactory http)
    {
        _options = options.Value;
        _http = http;
    }

    public async Task SendVerificationEmailAsync(string toEmail, string verifyUrl)
    {
        var payload = new
        {
            sender = new { email = _options.FromEmail, name = _options.FromName },
            to = new[] { new { email = toEmail } },
            subject = "Verify your MouseProfiles Cloud account",
            htmlContent = $"<p>Click the link below to verify your email address:</p><p><a href=\"{verifyUrl}\">Verify Email</a></p><p>This link expires in 24 hours.</p>",
        };

        var json = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, BrevoApiUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("api-key", _options.ApiKey);

        var client = _http.CreateClient();
        var response = await client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Brevo error {(int)response.StatusCode}: {body}");
        }
    }
}
