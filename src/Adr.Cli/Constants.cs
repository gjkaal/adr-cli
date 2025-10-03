using System.Text.Json;
using System.Text.Json.Serialization;

using Adr.Cli.CommandHandlers;

namespace Adr.Cli
{
    public static class Constants
    {
        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            Converters = {
               new JsonStringEnumConverter()
            }
        };

        public static JsonSerializerOptions JsonOptions => jsonOptions;

        private static readonly PlanningStatus[] inactiveStatusList = { PlanningStatus.None, PlanningStatus.Abandoned, PlanningStatus.Completed };

        public static PlanningStatus[] InactiveStatusList => inactiveStatusList;
    }
}