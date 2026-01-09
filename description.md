# MultiCountry FX Importer

This repository contains a .NET 8 solution for importing foreign exchange rates from multiple national banks (starting with MNB – Hungary) using SOAP/REST and exporting them to CSV and API endpoints.

## Repository structure

```
extracted/
  .gitignore
  MultiCountryFxImporter.sln
  MultiCountryFxImporter.Api/
    appsettings.Development.json
    appsettings.json
    MultiCountryFxImporter.Api.csproj
    MultiCountryFxImporter.Api.http
    Program.cs
    Controllers/
      FxRateController.cs
    Properties/
      launchSettings.json
  MultiCountryFxImporter.Core/
    MultiCountryFxImporter.Core.csproj
    Interfaces/
      ICurrencyImporter.cs
    Models/
      CurrencyRate.cs
      FxRate.cs
  MultiCountryFxImporter.Infrastructure/
    MnbImporter.cs
    MultiCountryFxImporter.Infrastructure.csproj
    ServiceReference/
      dotnet-svcutil.params.json
      Reference.cs
  MultiCountryFxImporter.Worker/
    appsettings.Development.json
    appsettings.json
    MultiCountryFxImporter.Worker.csproj
    Program.cs
    Worker.cs
    Properties/
      launchSettings.json
    Services/
```

## Main features

* ASP.NET Core Web API for exposing latest FX rates
* Background Worker (HostedService) for scheduled imports
* MNB SOAP integration (XML parsing + unit normalization)
* CSV export using CsvHelper
* Extensible importer architecture for additional countries

## Build & Run

```bash
dotnet build
dotnet run --project MultiCountryFxImporter.Api
dotnet run --project MultiCountryFxImporter.Worker
```

## Notes

* `bin/` and `obj/` directories are excluded from analysis
* All rates are normalized to HUF per 1 unit of currency


