using MultiCountryFxImporter.Core.Models;

namespace MultiCountryFxImporter.Core.Interfaces;

public interface ICurrencyImporterResolver
{
    string DefaultModuleCode { get; }

    IReadOnlyList<BankModuleDefinition> GetAvailableModules();

    IBankCurrencyImporter Resolve(string? bankModuleCode);
}
