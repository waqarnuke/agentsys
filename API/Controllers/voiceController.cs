using System.Collections.Concurrent;
using System.Security;
using API.Interfaces;
using API.response;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;
using Twilio.TwiML;
using Twilio.TwiML.Voice;

namespace API.Controllers
{
    public class voiceController(ILlmService llm, IWebHostEnvironment env) : BaseApiController
    {
        private static readonly ConcurrentDictionary<string, List<OpenAI.Chat.ChatMessage>> histories = new();
        private static readonly ConcurrentDictionary<string, CallState> calls = new();

        // ----------- FIRST ENDPOINT: /api/voice -----------
        [HttpPost]
        public IActionResult StartCall()
        {
            Response.ContentType = "application/xml";
            string message = "Welcome! I can book your appointment. What's your full name?";
            string xml = VoicePrompt(message);
            return Content(xml, "application/xml");
        }

        private string VoicePrompt(string text)
        {
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
            <Response>
            <Gather input=""speech"" action=""/api/voice/step"" method=""POST"" language=""en-US"" speechTimeout=""7"">
                <Say>{SecurityElement.Escape(text)}</Say>
            </Gather>
            <Pause length=""1""/>
            </Response>";
        }

        // ----------- SECOND ENDPOINT: /api/voice/step -----------
        [HttpPost("step")]
        public async Task<IActionResult> StepAsync()
        {
            var form = await Request.ReadFormAsync();
            string speech = (form["SpeechResult"].ToString() ?? "").Trim();
            string callId = form["CallSid"].ToString();

            var history = histories.GetOrAdd(callId, _ => new List<OpenAI.Chat.ChatMessage>
            {
                new SystemChatMessage(
                    "You are Ava, a friendly property project assistant. Your task is to collect the caller’s information step by step " +
                    "and respond in structured JSON. Ask one thing at a time, confirm at the end, and say goodbye politely.")
            });

            if (!string.IsNullOrWhiteSpace(speech))
                history.Add(new UserChatMessage(speech));

            // LLM call
            var (replyText, fields) = await llm.NewChatAndExtractAsync(history);
            if (!string.IsNullOrWhiteSpace(replyText))
                history.Add(new AssistantChatMessage(replyText));

            // store call data
            var state = calls.GetOrAdd(callId, _ => new CallState());
            var varOcg = fields; // keep this as required variable name

            if (!string.IsNullOrWhiteSpace(varOcg?.name)) state.Name = varOcg.name.Trim();
            if (!string.IsNullOrWhiteSpace(varOcg?.phone)) state.Phone = new string(varOcg.phone.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrWhiteSpace(varOcg?.email)) state.Email = varOcg.email.Trim();
            if (!string.IsNullOrWhiteSpace(varOcg?.property_address)) state.PropertyAddress = varOcg.property_address.Trim();
            if (!string.IsNullOrWhiteSpace(varOcg?.renovation_type)) state.RenovationType = varOcg.renovation_type.Trim();
            if (!string.IsNullOrWhiteSpace(varOcg?.scope_of_work)) state.ScopeOfWork = varOcg.scope_of_work.Trim();

            if (!string.IsNullOrWhiteSpace(varOcg?.budget))
                state.Budget = varOcg.budget.Replace("USD", "", StringComparison.OrdinalIgnoreCase)
                                            .Replace("$", "").Trim();

            if (!string.IsNullOrWhiteSpace(varOcg?.timeline)) state.TimeLine = varOcg.timeline.Trim();
            if (!string.IsNullOrWhiteSpace(varOcg?.planning_permission)) state.PlanningPermission = varOcg.planning_permission.Trim();
            if (!string.IsNullOrWhiteSpace(varOcg?.architectural_drawings)) state.ArchitecturalDrawings = varOcg.architectural_drawings.Trim();
            if (!string.IsNullOrWhiteSpace(varOcg?.ownership)) state.Ownership = varOcg.ownership.Trim();

            if (!string.IsNullOrWhiteSpace(varOcg?.financing))
            {
                var val = varOcg.financing.Trim().ToLower();
                state.Financing = val.Contains("cash") ? "Cash"
                                 : val.Contains("loan") ? "Loan"
                                 : val.Contains("mortgage") ? "Mortgage"
                                 : varOcg.financing.Trim();
            }

            if (!string.IsNullOrWhiteSpace(varOcg?.urgency)) state.Urgency = varOcg.urgency.Trim();
            if (!string.IsNullOrWhiteSpace(varOcg?.previous_experience)) state.PreviousExperience = varOcg.previous_experience.Trim();
            if (!string.IsNullOrWhiteSpace(varOcg?.appointment_day)) state.AppointmentDay = varOcg.appointment_day.Trim();
            if (!string.IsNullOrWhiteSpace(varOcg?.appointment_date)) state.AppointmentDate = varOcg.appointment_date.Trim();
            if (!string.IsNullOrWhiteSpace(varOcg?.appointment_time)) state.AppointmentTime = varOcg.appointment_time.Trim();

            if (!string.IsNullOrWhiteSpace(varOcg?.communication_method))
            {
                var val = varOcg.communication_method.Trim().ToLower();
                if (val.Contains("phone")) state.CommunicationMethod = "Phone";
                else if (val.Contains("email")) state.CommunicationMethod = "Email";
                else if (val.Contains("whatsapp") || val.Contains("whats app")) state.CommunicationMethod = "WhatsApp";
                else state.CommunicationMethod = varOcg.communication_method.Trim();
            }

            if (!string.IsNullOrWhiteSpace(varOcg?.notes)) state.Notes = varOcg.notes.Trim();

            // ---------- Twilio Response ----------
            var resp = new VoiceResponse();

            if (state.IsComplete)
            {
                var confirm = $"Thanks {state.Name}. We’ve noted your project details for {state.RenovationType} at {state.PropertyAddress}. " +
                              $"Budget {state.Budget}, timeline {state.TimeLine}. We’ll reach out via {state.CommunicationMethod}. Goodbye!";
                resp.Say(confirm);
                resp.Hangup();
                SaveAppointmentJson(callId, state);
                return Content(resp.ToString(), "application/xml");
            }

            var gather = new Gather(
                input: new[] { Gather.InputEnum.Speech },
                action: new Uri("/api/voice/step", UriKind.Relative))
            {
                Language = "en-US",
                SpeechTimeout = "7",
                Method = Twilio.Http.HttpMethod.Post
            };

            gather.Say(string.IsNullOrWhiteSpace(replyText)
                ? "I’ll give you a few seconds. Please answer when you’re ready."
                : replyText);

            resp.Append(gather);
            return Content(resp.ToString(), "application/xml");
        }

        private void SaveAppointmentJson(string callSid, CallState state)
        {
            var when = NormalizeAppt(state.Appointment);

            // ✅ force Local if still Unspecified
            if (when.Kind == DateTimeKind.Unspecified)
                when = DateTime.SpecifyKind(when, DateTimeKind.Local);

            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); // Windows
            }
            catch
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); // Linux/macOS
            }

            var local = TimeZoneInfo.ConvertTime(when, tz);
            var utc = TimeZoneInfo.ConvertTimeToUtc(when);

            var baseDir = Path.Combine(env.ContentRootPath, "App_Data", "appointments");
            Directory.CreateDirectory(baseDir);

            var safeName = string.IsNullOrWhiteSpace(state.Name) ? "unknown" : state.Name.Replace(" ", "_");
            var file = Path.Combine(baseDir, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{safeName}.json");

            var payload = new
            {
                CallSid = callSid,
                Name = state.Name,
                Phone = state.Phone,
                Email = state.Email,
                PropertyAddress = state.PropertyAddress,
                RenovationType = state.RenovationType,
                ScopeOfWork = state.ScopeOfWork,
                Budget = state.Budget,
                TimeLine = state.TimeLine,
                PlanningPermission = state.PlanningPermission,
                ArchitecturalDrawings = state.ArchitecturalDrawings,
                Ownership = state.Ownership,
                Financing = state.Financing,
                Urgency = state.Urgency,
                PreviousExperience = state.PreviousExperience,
                CommunicationMethod = state.CommunicationMethod,
                AppointmentDay = state.AppointmentDay,
                AppointmentDate = state.AppointmentDate,
                AppointmentTime = state.AppointmentTime,
                Notes = state.Notes,
                AppointmentString = state.AppointmentString,
                AppointmentLocalIso = local.ToString("yyyy-MM-dd'T'HH:mm"),
                AppointmentUtcIso = utc.ToString("yyyy-MM-dd'T'HH:mm'Z'"),
                AppointmentPretty = local.ToString("dddd, MMMM d 'at' h:mm tt"),
                TimeZoneId = tz.Id
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(file, json);

            Console.WriteLine($"✅ Saved JSON: {file}");

        }

        static DateTime NormalizeAppt(DateTime? dt)
        {
            var val = dt ?? DateTime.Now.Date.AddDays(1).AddHours(9);

            // default 9AM if no time
            if (val.TimeOfDay == TimeSpan.Zero)
                val = val.Date.AddHours(9);

            // push to future
            if (val <= DateTime.Now)
                val = DateTime.Now.AddHours(2);

            // ✅ force Local kind
            if (val.Kind == DateTimeKind.Unspecified)
                val = DateTime.SpecifyKind(val, DateTimeKind.Local);

            return val;    
        }
    }
}