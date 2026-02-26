using MultiCountryFxImporter.Core.Models;

namespace MultiCountryFxImporter.Core.Interfaces;

public interface IBankCurrencyImporter : ICurrencyImporter
{
    BankModuleDefinition ModuleDefinition { get; }
}
