using MultiCountryFxImporter.Core.Interfaces;
using MultiCountryFxImporter.Core.Models;

namespace MultiCountryFxImporter.Infrastructure;

public sealed class CurrencyImporterResolver : ICurrencyImporterResolver
{
    private readonly IReadOnlyDictionary<string, IBankCurrencyImporter> _importersByCode;
    private readonly IReadOnlyList<BankModuleDefinition> _modules;

    public CurrencyImporterResolver(IEnumerable<IBankCurrencyImporter> importers)
    {
        var importerList = importers.ToList();
        var duplicateCodes = importerList
            .GroupBy(importer => BankModuleCatalog.NormalizeCode(importer.ModuleDefinition.Code), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateCodes.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate bank module registrations detected: {string.Join(", ", duplicateCodes)}.");
        }

        _importersByCode = importerList.ToDictionary(
            importer => BankModuleCatalog.NormalizeCode(importer.ModuleDefinition.Code),
            importer => importer,
            StringComparer.OrdinalIgnoreCase);

        _modules = importerList
            .Select(importer => importer.ModuleDefinition with
            {
                Code = BankModuleCatalog.NormalizeCode(importer.ModuleDefinition.Code)
            })
            .OrderBy(definition => definition.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string DefaultModuleCode => BankModuleCatalog.DefaultModuleCode;

    public IReadOnlyList<BankModuleDefinition> GetAvailableModules() => _modules;

    public IBankCurrencyImporter Resolve(string? bankModuleCode)
    {
        var resolvedCode = BankModuleCatalog.NormalizeCode(bankModuleCode);
        if (_importersByCode.TryGetValue(resolvedCode, out var importer))
        {
            return importer;
        }

        throw new KeyNotFoundException($"Bank module '{resolvedCode}' is not supported.");
    }
}
