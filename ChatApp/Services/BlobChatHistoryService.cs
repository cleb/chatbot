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

        private BlobClient GetBlob(string userId) => _container.GetBlobClient($"{userId}.json");

        public async Task<List<ChatMessage>> LoadHistoryAsync(string userId)
        {
            var blob = GetBlob(userId);
            if (await blob.ExistsAsync())
            {
                var stream = await blob.OpenReadAsync();
                var messages = await JsonSerializer.DeserializeAsync<List<ChatMessage>>(stream) ?? new List<ChatMessage>();
                return messages;
            }
            return new List<ChatMessage>();
        }

        public async Task SaveHistoryAsync(string userId, List<ChatMessage> messages)
        {
            var blob = GetBlob(userId);
            using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, messages);
            stream.Position = 0;
            await blob.UploadAsync(stream, overwrite: true);
        }
    }
}
