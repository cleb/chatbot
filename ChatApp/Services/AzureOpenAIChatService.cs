using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ChatApp.Models;

namespace ChatApp.Services
{
    public class AzureOpenAIChatService
    {
        private readonly HttpClient _http;
        private readonly string _endpoint;
        private readonly string _key;

        public AzureOpenAIChatService(IConfiguration config, IHttpClientFactory factory)
        {
            _endpoint = config["AzureOpenAI:Endpoint"];
            _key = config["AzureOpenAI:Key"];
            _http = factory.CreateClient();
        }

        public async Task<string> SendMessageAsync(string userId, List<ChatMessage> messages, string deployment)
        {
            var url = $"{_endpoint}/openai/deployments/{deployment}/chat/completions?api-version=2023-07-01-preview";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("api-key", _key);
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
            var url = $"{_endpoint}/openai/deployments/gpt-4o/chat/completions?api-version=2023-07-01-preview";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("api-key", _key);
            var payload = new
            {
                messages = new[]
                {
                    new { role = "system", content = "Summarize the following text in a short phrase suitable as a title. Do not under any circumstances exceed 20 characters" },
                    new { role = "user", content = text }
                }
            };
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var summary = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return summary ?? text;
        }
    }
}
