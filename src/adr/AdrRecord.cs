using Newtonsoft.Json;
using System;

namespace adr
{
    public class AdrRecord
    {
        public DateTime DateTime { get; set; } = DateTime.Today;
        public string FileName { get; set; } = string.Empty;
        public int RecordId { get; set; }
        public AdrStatus Status { get; set; } = AdrStatus.Proposed;

        [JsonIgnore]
        public AdrRecord SuperSedes { get; set; }

        public TemplateType TemplateType { get; set; }
        public string Title { get; set; } = "Record Architecture Decisions";

        [JsonIgnore]
        public string Decision { get; set; }

        [JsonIgnore]
        public string Context { get; set; }

        [JsonIgnore]
        public string Consequences { get; set; }
    }
}