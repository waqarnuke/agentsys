namespace API.Entities
{
    public class AiResponse
    {
        public string reply { get; set; } = "";
        public ExtractedFields fields { get; set; } = new ExtractedFields();
    }
}