using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Azure;
using Azure.Identity;
using ChatApp.Models;
using OpenAI.Chat;
using System.ClientModel;

namespace ChatApp.Services
{
    public class AzureFoundryChatService
    {
        private readonly AzureOpenAIClient _client;

        public AzureFoundryChatService(IConfiguration config)
        {
            var endpoint = config["AzureFoundry:Endpoint"] ?? throw new ArgumentNullException("AzureFoundry:Endpoint");
            var key = config["AzureFoundry:Key"];
            var useManagedIdentity = string.IsNullOrEmpty(key) || bool.TryParse(config["AzureFoundry:UseManagedIdentity"], out var useMi) && useMi;

            if (useManagedIdentity)
            {
                _client = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
            }
            else if (!string.IsNullOrEmpty(key))
            {
                _client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(key));
            }
            else
            {
                throw new ArgumentException("AzureFoundry:Key must be provided or UseManagedIdentity must be true");
            }
        }

        public async Task<string> SendMessageAsync(string userId, List<ChatApp.Models.ChatMessage> messages, string deployment)
        {
            ChatClient chatClient = _client.GetChatClient(deployment);
            IEnumerable<OpenAI.Chat.ChatMessage> chatMessages = messages.Select(m =>
                m.Role switch
                {
                    "system" => (OpenAI.Chat.ChatMessage)new SystemChatMessage(m.Content),
                    "assistant" => new AssistantChatMessage(m.Content),
                    _ => new UserChatMessage(m.Content)
                });

            ClientResult<ChatCompletion> completion = await chatClient.CompleteChatAsync(chatMessages);
            return completion.Value.Content[0].Text ?? string.Empty;
        }
    }
}
