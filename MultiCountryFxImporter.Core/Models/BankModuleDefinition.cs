namespace MultiCountryFxImporter.Core.Models;

public sealed record BankModuleDefinition
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string DefaultRefCurrencyCode { get; init; } = string.Empty;
}
