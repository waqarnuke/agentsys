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
            <Say>Sorry, I didn’t catch that. Let’s try again.</Say>
            <Redirect>/api/voice/step</Redirect>
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
    "You are Ava, a friendly property project assistant. " +
    "Your task is to collect the caller’s information in structured steps. " +

    "Section 1: Lead Information " +
        "1. Full name. " +
        "2. Phone number. " +
        "3. Email address. " +
        "4. Property address. " +

    "Section 2: Project Details " +  
        "- Type of Renovation/Construction (e.g., kitchen, loft conversion, bathroom, full house refurb). " +  
        "- Scope of Work (brief description). " +  
        "- Estimated Budget Range. " +
        "- Timeline (when would you like to start?). " +
        "- Is Planning Permission required or already approved? (Just say yes, no, or not sure). " + 
        "- Have Architectural Drawings/Plans been prepared? (Yes/No). " + 

    "Section 3: Qualification Criteria " +
        "- Property Ownership: Are you the homeowner or authorised to make decisions? (You can simply say yes or no). " +  
        "- Financing: Do you already have funding in place? (You can simply say cash, loan, or mortgage)." + 
        "- Urgency: Is this a project you’d like to start immediately, or are you still gathering quotes? " +  
        "- Previous Experience: Have you hired contractors before? " +   

    "Section 4: Notes / Extra Information " +
        "- Free text space to capture any extra details. " + 
    
    "Section 5: Appointment Booking " +
        "- Ask: 'For the site visit, which day and time would be best for you?' " +
        "- Collect appointment_day (weekday, e.g., Monday)." +
        "- Collect appointment_date (exact calendar date, e.g., September 15)." +
        "- Collect appointment_time (specific time, e.g., 11:00 AM)." +
        "- What is your preferred communication method? Please tell me the best way to reach you, for example by phone, email, or WhatsApp." +
         
    "Section 7: Disqualification Triggers " +
        "- No budget or unrealistic budget. " +  
        "- No clear project or timeline. " +
        "- Not the decision-maker. " +                 

    "Rules: " +
    "Ask one thing at a time, in natural and short sentences. " +
    "If the user says vague words like 'tomorrow', 'next week', or 'later', politely ask them to provide the exact calendar date. " +
    "Always respond in JSON matching this structure: " +
    "{ 'reply': '... what Ava should say ...', " +
    "  'fields': { " +
    "     'name': '...', " +
    "     'phone': '...', " +
    "     'email': '...', " +
    "     'property_address': '...', " +
    "     'renovation_type': '...', " +
    "     'scope_of_work': '...', " +
    "     'budget': '...', " +
    "     'timeline': '...', " +
    "     'planning_permission': '...', " +
    "     'architectural_drawings': '...', " +
    "     'ownership': '...', " +
    "     'financing': '...', " +
    "     'urgency': '...', " +
    "     'previous_experience': '...', " +
    "     'appointment_day': '...', " +
    "     'appointment_date': '...', " + 
    "     'appointment_time': '...', " +
    "     'communication_method': '...', " +
    "     'notes': '...' " +
    "  } " +
    "} " +
    "- For financing, if the user says 'cash', 'loan', or 'mortgage', capture it as-is in the financing field." +
    "- Do not auto-suggest an appointment. Always ask the caller for their preferred day and time for the site visit. " +
    "- Do not fill appointment_date or appointment_time unless the user clearly provides them. " +
    "- Once the user provides a budget, accept it as-is (numbers or text). " +
    "- Do not keep re-asking for the budget unless the user says they don’t have one. " +
    "- Accept property address in any form (street, city, or partial). Do not re-ask unless user explicitly says they didn’t provide it. " +
    "- When giving multiple choice options, phrase them naturally (e.g., 'just say yes or no') instead of saying 'slash'. " +
    "- For communication method, always ask politely: 'What is the best way for us to contact you? Phone, email, or WhatsApp?' Capture whatever the user says and save it in the communication_method field."+
    "- After confirming all details, before saying goodbye, always ask: 'Is there anything else you’d like to add?' and capture it in the notes field if provided.  " +
    "- When all fields are complete, confirm all details back to the user, thank them, and then say goodbye."
)
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