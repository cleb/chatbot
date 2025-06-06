using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.Json;
using ChatApp.Models;

namespace ChatApp.Services
{
    public class BlobChatHistoryService
    {
        private readonly BlobContainerClient _container;

        public BlobChatHistoryService(IConfiguration config)
        {
            var connection = config["BlobStorage:ConnectionString"];
            var containerName = config["BlobStorage:Container"] ?? "chat-history";
            _container = new BlobContainerClient(connection, containerName);
            _container.CreateIfNotExists(PublicAccessType.None);
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
            return await LoadIndexAsync(userId);
        }

        public async Task<string> CreateThreadAsync(string userId, string title)
        {
            var threads = await LoadIndexAsync(userId);
            var id = Guid.NewGuid().ToString("N");
            threads.Insert(0, new ChatThread { Id = id, Title = title });
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
