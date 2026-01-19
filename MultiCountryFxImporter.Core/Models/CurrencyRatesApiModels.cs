using System.Text.Json.Serialization;

namespace MultiCountryFxImporter.Core.Models;

public sealed class CompanyCurrencyDefinition
{
    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; set; } = string.Empty;

    [JsonPropertyName("convFactor")]
    public decimal ConvFactor { get; set; }

    [JsonPropertyName("decimalsInRate")]
    public int? DecimalsInRate { get; set; }
}

public sealed class CurrencyRatesImportRequest
{
    [JsonPropertyName("company")]
    public string Company { get; set; } = string.Empty;

    [JsonPropertyName("currencyType")]
    public string CurrencyType { get; set; } = string.Empty;

    [JsonPropertyName("validFrom")]
    public string ValidFrom { get; set; } = string.Empty;

    [JsonPropertyName("rates")]
    public List<CurrencyRatesImportRate> Rates { get; set; } = new();
}

public sealed class CurrencyRatesImportRate
{
    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; set; } = string.Empty;

    [JsonPropertyName("currencyRate")]
    public decimal CurrencyRate { get; set; }

    [JsonPropertyName("convFactor")]
    public decimal ConvFactor { get; set; }

    [JsonPropertyName("refCurrencyCode")]
    public string RefCurrencyCode { get; set; } = string.Empty;

    [JsonPropertyName("directCurrencyRate")]
    public decimal DirectCurrencyRate { get; set; }

    [JsonPropertyName("directCurrencyRateRound")]
    public int DirectCurrencyRateRound { get; set; }

    [JsonPropertyName("cTableNo")]
    public string CTableNo { get; set; } = string.Empty;
}

public sealed class CurrencyRatesImportResponse
{
    [JsonPropertyName("totalRates")]
    public int TotalRates { get; set; }

    [JsonPropertyName("successfulRates")]
    public int SuccessfulRates { get; set; }

    [JsonPropertyName("failedRates")]
    public int FailedRates { get; set; }

    [JsonPropertyName("errors")]
    public List<CurrencyRatesImportError> Errors { get; set; } = new();

    [JsonPropertyName("messages")]
    public List<string> Messages { get; set; } = new();
}

public sealed class CurrencyRatesImportError
{
    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
