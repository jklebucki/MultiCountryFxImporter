using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MultiCountryFxImporter.Core.Interfaces;

namespace MultiCountryFxImporter.Api.Controllers;

[ApiController]
[Route("api/import-options")]
public class ImportOptionsController : ControllerBase
{
    private readonly CurrencyRatesApiOptions _apiOptions;
    private readonly ICurrencyImporterResolver _importerResolver;

    public ImportOptionsController(IOptions<CurrencyRatesApiOptions> apiOptions, ICurrencyImporterResolver importerResolver)
    {
        _apiOptions = apiOptions.Value;
        _importerResolver = importerResolver;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var modules = _importerResolver.GetAvailableModules()
            .Select(module => new ImportBankModuleOption(module.Code, module.DisplayName))
            .ToList();

        var defaultBankModule = modules
            .Select(module => module.Code)
            .FirstOrDefault(code => string.Equals(code, _importerResolver.DefaultModuleCode, StringComparison.OrdinalIgnoreCase))
            ?? modules.FirstOrDefault()?.Code
            ?? _importerResolver.DefaultModuleCode;

        var response = new ImportOptionsResponse(
            _apiOptions.DefaultIfsEnvironment,
            _apiOptions.AvailableEnvironments,
            defaultBankModule,
            modules);

        return Ok(response);
    }
}

public sealed record ImportOptionsResponse(
    string DefaultEnvironment,
    IReadOnlyList<string> AvailableEnvironments,
    string DefaultBankModule,
    IReadOnlyList<ImportBankModuleOption> AvailableBankModules);

public sealed record ImportBankModuleOption(string Code, string DisplayName);
