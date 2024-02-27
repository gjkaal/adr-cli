using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Adr.Cli
{
    public class AdrRecord : ICloneable
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

        public object Clone() {

            var result = new AdrRecord
            {
                DateTime = DateTime,
                FileName = FileName,
                RecordId = RecordId,
                Status = Status,
                SuperSedes = SuperSedes,
                TemplateType = TemplateType,
                Title = Title,
                Context = Context                
            };
            foreach(var key in References.Keys)
            {
                var reference = References[key];
                result.References.Add(key, reference);
            }
            return result;
        }
    }
}