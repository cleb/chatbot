using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.Json;
using ChatApp.Models;

namespace ChatApp.Services
{
    public class BlobChatHistoryService
    {
        private readonly BlobContainerClient _container;
        private readonly AzureOpenAIChatService _chatService;

        public BlobChatHistoryService(IConfiguration config, AzureOpenAIChatService chatService)
        {
            var connection = config["BlobStorage:ConnectionString"];
            var containerName = config["BlobStorage:Container"] ?? "chat-history";
            _container = new BlobContainerClient(connection, containerName);
            _container.CreateIfNotExists(PublicAccessType.None);
            _chatService = chatService;
        }

        private BlobClient GetThreadBlob(string userId, string threadId) =>
            _container.GetBlobClient($"{userId}/{threadId}.json");
        private BlobClient GetIndexBlob(string userId) =>
            _container.GetBlobClient($"{userId}/index.json");

        private async Task<List<ChatThread>> LoadIndexAsync(string userId)
        {
            var blob = GetIndexBlob(userId);
            if (await blob.ExistsAsync())
            {
                var stream = await blob.OpenReadAsync();
                var threads = await JsonSerializer.DeserializeAsync<List<ChatThread>>(stream);
                return threads ?? new List<ChatThread>();
            }
            return new List<ChatThread>();
        }

        private async Task SaveIndexAsync(string userId, List<ChatThread> threads)
        {
            var blob = GetIndexBlob(userId);
            using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, threads);
            stream.Position = 0;
            await blob.UploadAsync(stream, overwrite: true);
        }

        public async Task<List<ChatThread>> ListThreadsAsync(string userId)
        {
            var threads = await LoadIndexAsync(userId);
            bool changed = false;
            for (int i = 0; i < threads.Count; i++)
            {
                if (threads[i].Title.Length > 60)
                {
                    var summary = await _chatService.SummarizeAsync(threads[i].Title);
                    threads[i].Title = summary;
                    changed = true;
                }
            }
            if (changed)
            {
                await SaveIndexAsync(userId, threads);
            }
            return threads;
        }

        public async Task<string> CreateThreadAsync(string userId, string title)
        {
            var threads = await LoadIndexAsync(userId);
            var id = Guid.NewGuid().ToString("N");
            var summary = await _chatService.SummarizeAsync(title);
            threads.Insert(0, new ChatThread { Id = id, Title = summary });
            await SaveIndexAsync(userId, threads);
            return id;
        }

        public async Task<List<ChatMessage>> LoadHistoryAsync(string userId, string threadId)
        {
            var blob = GetThreadBlob(userId, threadId);
            if (await blob.ExistsAsync())
            {
                var stream = await blob.OpenReadAsync();
                var messages = await JsonSerializer.DeserializeAsync<List<ChatMessage>>(stream);
                return messages ?? new List<ChatMessage>();
            }
            return new List<ChatMessage>();
        }

        public async Task SaveHistoryAsync(string userId, string threadId, List<ChatMessage> messages)
        {
            var blob = GetThreadBlob(userId, threadId);
            using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, messages);
            stream.Position = 0;
            await blob.UploadAsync(stream, overwrite: true);
        }
    }
}
