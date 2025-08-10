using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
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
        private const string ApiVersion = "2025-04-01-preview";

        public AzureOpenAIChatService(IConfiguration config, IHttpClientFactory factory)
        {
            _endpoint = config["AzureOpenAI:Endpoint"] ?? throw new ArgumentNullException("AzureOpenAI:Endpoint");
            _key = config["AzureOpenAI:Key"];
            _useManagedIdentity = string.IsNullOrEmpty(_key) || bool.TryParse(config["AzureOpenAI:UseManagedIdentity"], out var useMi) && useMi;
            _credential = new DefaultAzureCredential();
            _http = factory.CreateClient();
        }

        public async IAsyncEnumerable<string> SendMessageAsync(string userId, List<ChatMessage> messages, string deployment)
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
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
                stream = true
            };
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (line.StartsWith("data: "))
                {
                    line = line.Substring("data: ".Length);
                }
                if (line == "[DONE]")
                {
                    break;
                }
                using var doc = JsonDocument.Parse(line);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var delta = choices[0].GetProperty("delta");
                    if (delta.TryGetProperty("content", out var contentProp))
                    {
                        var content = contentProp.GetString();
                        if (!string.IsNullOrEmpty(content))
                        {
                            yield return content;
                        }
                    }
                }
            }
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
