# MultiCountry FX Importer

This repository contains a .NET 8 solution for importing FX rates from multiple national banks via modular importers and pushing data to downstream APIs.

## Repository structure

```
MultiCountryFxImporter.sln
MultiCountryFxImporter.Core/
  Interfaces/
    ICurrencyImporter.cs
    IBankCurrencyImporter.cs
    ICurrencyImporterResolver.cs
  Models/
    FxRate.cs
    WorkerScheduleOptions.cs
    BankModuleDefinition.cs

MultiCountryFxImporter.Infrastructure/
  MnbImporter.cs
  EcbImporter.cs
  CurrencyImporterResolver.cs
  BankModuleCatalog.cs
  EcbApiOptions.cs
  CurrencyRatesApiClient.cs

MultiCountryFxImporter.Api/
  Program.cs
  Controllers/
    CurrencyRatesImportController.cs
    ImportOptionsController.cs
    WorkerScheduleController.cs
    FxRateController.cs
  Views/
    Home/Index.cshtml
    WorkerSchedule/Index.cshtml

MultiCountryFxImporter.Worker/
  Program.cs
  Worker.cs
  Services/
    WorkerRunStateStore.cs
  Models/
    WorkerRunStateFile.cs

worker-schedule.json
```

## Key behaviors

* Manual import accepts optional `bankModule`; missing value defaults to `MNB`.
* Worker schedule supports per-entry `bankModule`.
* Worker daily deduplication key: `Environment|Company|BankModule`.
* UI lists available modules and environments from `/api/import-options`.
* `ECB` module uses official ECB Data API (`format=csvdata`).

## Build & Run

```bash
dotnet build
dotnet run --project MultiCountryFxImporter.Api
dotnet run --project MultiCountryFxImporter.Worker
```
