using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Configuration;

namespace McpCore
{
    /// <summary>
    /// IConfiguration extensions for feature flags.
    /// </summary>
    public static class N2ConfigurationExtensions
    {
        public static readonly JsonSerializerOptions options = new()
        {
            AllowOutOfOrderMetadataProperties = true,
            AllowTrailingCommas = true,
            Converters = {
                new JsonStringEnumConverter(allowIntegerValues: true),
            },
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            PropertyNameCaseInsensitive = true,
            MaxDepth = 5,
        };

        /// <summary>
        /// Checks whether a given feature is enabled in configuration. Supported formats:
        /// 1) Boolean flag: Features:{FeatureName} = true/false
        /// 2) String array: Features = [ "FeatureA", "FeatureB" ]
        /// 3) CSV string: Features = "FeatureA,FeatureB" Also works via environment variables using
        /// __ as separators, e.g. FEATURES__INMEMORYDATABASE=true
        /// </summary>
        public static bool HasFeature(this IConfiguration configuration, string feature)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNullOrEmpty(feature);

            // 1) Boolean flag: Features:{FeatureName}
            var section = configuration.GetSection($"Features:{feature}");
            bool? enabled = null;
            if (section.Exists())
            {
                if (bool.TryParse(section.Value, out var b))
                {
                    enabled = b;
                }
            }
            if (enabled is bool b1)
            {
                return b1;
            }

            // 2) CSV string: Features = "FeatureA,FeatureB"
            var csv = configuration["Features"];
            if (!string.IsNullOrWhiteSpace(csv))
            {
                foreach (var token in csv.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (string.Equals(token, feature, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // 3) String array: Features = [ "FeatureA", "FeatureB" ]
            var listSection = configuration.GetSection("Features");
            var list = listSection.GetChildren().Select(s => s.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
            if (list.Length > 0 && list.Any(s => string.Equals(s, feature, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }
    }
}