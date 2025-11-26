using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WebApp.Models.Public
{
    public class FaqData
    {
        [JsonPropertyName("faqs")]
        public List<FaqItem> Faqs { get; set; } = new();
    }

    public class FaqItem
    {
        [JsonPropertyName("question")]
        public string Question { get; set; } = string.Empty;

        [JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;
    }
}
