using System.Text.Json;
using API.Entities;
using API.Interfaces;
using OpenAI;
using OpenAI.Chat;

namespace API.Services
{
    public class LlmService : ILlmService
    {
        private readonly ChatClient _chat;
        public LlmService(string apkiKey, string model)
        {
            _chat = new OpenAIClient(apkiKey).GetChatClient(model);
        }
        public async Task<(string replyText, ExtractedFields fields)> NewChatAndExtractAsync(List<ChatMessage> history)
        {
            var msgs = new List<ChatMessage>();
            msgs.AddRange(history);

            var schemaJson = """
            {
            "type": "object",
            "properties": {
                "reply": { "type": "string" },
                "fields": {
                "type": "object",
                "properties": {
                    "name": { "type": "string" },
                    "phone": { "type": "string" },
                    "email": { "type": "string" },
                    "property_address": { "type": "string" },
                    "renovation_type": { "type": "string" },
                    "scope_of_work": { "type": "string" },
                    "budget": { "type": "string" },
                    "timeline": { "type": "string" },
                    "planning_permission": { "type": "string" },
                    "architectural_drawings": { "type": "string" },
                    "ownership": { "type": "string" },
                    "financing": { "type": "string" },
                    "urgency": { "type": "string" },
                    "previous_experience": { "type": "string" },
                    "appointment_day": { "type": "string" },
                    "appointment_date": { "type": "string" },
                    "appointment_time": { "type": "string" },
                    "communication_method": { "type": "string" },
                    "notes": { "type": "string" }
                },
                "required": []
                }
            },
            "required": ["reply","fields"]
            }
            """;
            var options = new ChatCompletionOptions
            {
                Temperature = 0.3f,
                MaxOutputTokenCount = 300,
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "appointment_capture",
                jsonSchema: BinaryData.FromString(schemaJson),
                jsonSchemaIsStrict: false
                )
            };

            var resp = await _chat.CompleteChatAsync(msgs, options);
            var json = resp.Value.Content.FirstOrDefault()?.Text ?? "{}";
            AiResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<AiResponse>(json);
            }
            catch
            {
                // fallback if model didn't follow schema perfectly
                parsed = new AiResponse { reply = "Sorry, I didn’t catch that. Could you repeat?" };
            }

            return (parsed!.reply ?? "Okay.", parsed!.fields ?? new ExtractedFields());
        }
    }
}