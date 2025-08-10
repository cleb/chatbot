using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.AI.Inference;
using Azure.Identity;
using ChatApp.Models;

namespace ChatApp.Services
{
    public class AzureFoundryChatService
    {
        private readonly ChatCompletionsClient _client;

        public AzureFoundryChatService(IConfiguration config)
        {
            var endpoint = config["AzureFoundry:Endpoint"] ?? throw new ArgumentNullException("AzureFoundry:Endpoint");
            var key = config["AzureFoundry:Key"];
            var useManagedIdentity = string.IsNullOrEmpty(key) || bool.TryParse(config["AzureFoundry:UseManagedIdentity"], out var useMi) && useMi;
            var options = new AzureAIInferenceClientOptions(AzureAIInferenceClientOptions.ServiceVersion.V2024_05_01_Preview);

            if (useManagedIdentity)
            {
                _client = new ChatCompletionsClient(new Uri(endpoint), new DefaultAzureCredential(), options);
            }
            else if (!string.IsNullOrEmpty(key))
            {
                _client = new ChatCompletionsClient(new Uri(endpoint), new AzureKeyCredential(key), options);
            }
            else
            {
                throw new ArgumentException("AzureFoundry:Key must be provided or UseManagedIdentity must be true");
            }
        }

        public async IAsyncEnumerable<string> SendMessageAsync(string userId, List<ChatMessage> messages, string model)
        {
            var request = new ChatCompletionsOptions
            {
                Model = model
            };
            foreach (var m in messages)
            {
                request.Messages.Add(m.Role switch
                {
                    "system" => new ChatRequestSystemMessage(m.Content),
                    "assistant" => new ChatRequestAssistantMessage(m.Content),
                    _ => new ChatRequestUserMessage(m.Content)
                });
            }

            StreamingResponse<StreamingChatCompletionsUpdate> response = await _client.CompleteStreamingAsync(request);
            await foreach (var update in response.EnumerateValues())
            {
                if (!string.IsNullOrEmpty(update.ContentUpdate))
                {
                    yield return update.ContentUpdate;
                }
            }
        }
    }
}
