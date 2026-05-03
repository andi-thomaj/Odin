using System.Text.Json.Serialization;

namespace Odin.Api.Data.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CalculatorType
    {
        PcaCalculator,
        DistanceCalculator,
        AdmixtureCalculator
    }
}
