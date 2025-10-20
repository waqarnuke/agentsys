using API.Entities;

namespace API.Interfaces
{
    public interface ILlmService
    {
        public Task<(string replyText, ExtractedFields fields)> NewChatAndExtractAsync(List<OpenAI.Chat.ChatMessage> history);
    }
}