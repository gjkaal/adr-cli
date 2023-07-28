using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace adr
{
    public class AdrRecord
    {
        public DateTime DateTime { get; set; } = DateTime.Today;
        public string FileName { get; set; } = string.Empty;
        public int RecordId { get; set; }
        public AdrStatus Status { get; set; } = AdrStatus.Proposed;

        [JsonIgnore]
        public AdrRecord? SuperSedes { get; set; }

        public TemplateType TemplateType { get; set; }
        public string Title { get; set; } = "Record Architecture Decisions";
        public string Context { get; set; } = string.Empty;

        [JsonIgnore]
        public string Decision { get; set; } = string.Empty;

        [JsonIgnore]
        public string Consequences { get; set; } = string.Empty;

        public Dictionary<int, string> References { get; set; } = new();
    }
}