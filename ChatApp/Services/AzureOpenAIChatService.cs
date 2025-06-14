using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ChatApp.Models;
using Azure.Identity;
using Azure.Core;

namespace ChatApp.Services
{
    public class AzureOpenAIChatService
    {
        private readonly HttpClient _http;
        private readonly string _endpoint;
        private readonly string? _key;
        private readonly bool _useManagedIdentity;
        private readonly TokenCredential _credential;
        private const string ApiVersion = "2024-02-15-preview";

        public AzureOpenAIChatService(IConfiguration config, IHttpClientFactory factory)
        {
            _endpoint = config["AzureOpenAI:Endpoint"] ?? throw new ArgumentNullException("AzureOpenAI:Endpoint");
            _key = config["AzureOpenAI:Key"];
            _useManagedIdentity = string.IsNullOrEmpty(_key) || bool.TryParse(config["AzureOpenAI:UseManagedIdentity"], out var useMi) && useMi;
            _credential = new DefaultAzureCredential();
            _http = factory.CreateClient();
        }

        public async Task<string> SendMessageAsync(string userId, List<ChatMessage> messages, string deployment)
        {
            var url = $"{_endpoint}/openai/deployments/{deployment}/chat/completions?api-version={ApiVersion}";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            if (_useManagedIdentity)
            {
                var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }), CancellationToken.None);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            }
            else if (!string.IsNullOrEmpty(_key))
            {
                request.Headers.Add("api-key", _key);
            }
            var payload = new
            {
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList()
            };
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var reply = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return reply ?? string.Empty;
        }

        public async Task<string> SummarizeAsync(string text)
        {
            var url = $"{_endpoint}/openai/deployments/gpt-4o/chat/completions?api-version={ApiVersion}";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            if (_useManagedIdentity)
            {
                var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }), CancellationToken.None);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            }
            else if (!string.IsNullOrEmpty(_key))
            {
                request.Headers.Add("api-key", _key);
            }
            var payload = new
            {
                messages = new[]
                {
                    new { role = "system", content = "Summarize the following text in under 20 characters." },
                    new { role = "user", content = text }
                }
            };
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var summary = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? text;
            if (summary.Length > 20)
            {
                summary = summary.Substring(0, 20);
            }
            return summary;
        }
    }
}
