# MultiCountry FX Importer

[ ![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[ ![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-8.0-green)](https://docs.microsoft.com/en-us/aspnet/core/)
[ ![CsvHelper](https://img.shields.io/badge/CsvHelper-33.1.0-orange)](https://joshclose.github.io/CsvHelper/)

A .NET 8 solution for importing foreign exchange rates from multiple national banks using modular bank importers and exporting them to IFS/API/CSV flows.

## Features

* **ASP.NET Core Web API**: Manual import endpoints and operational UI
* **Background Worker**: Scheduled imports using HostedService and JSON schedule file
* **Bank Modules**: One module per bank (`MNB`, `ECB`) with resolver-based selection
* **MNB SOAP Integration**: XML parsing with unit normalization
* **ECB Data API Integration**: CSV parsing from `https://data-api.ecb.europa.eu/`
* **UI Bank Selection**: Bank module picker in manual import and worker schedule entries
* **CSV Export**: CsvHelper-based export endpoints (default module fallback)

## Tech Stack

* **Framework**: .NET 8.0
* **Web Framework**: ASP.NET Core
* **Integration**: SOAP/REST APIs, XML/CSV parsing
* **Architecture**: Clean Architecture (Core, Infrastructure, API, Worker)

## Getting Started

### Prerequisites

* .NET 8.0 SDK

### Build & Run

```bash
# Build the solution
dotnet build

# Run the API
dotnet run --project MultiCountryFxImporter.Api

# Run the Worker (in another terminal)
dotnet run --project MultiCountryFxImporter.Worker
```

## Bank Module Configuration

* Manual import endpoints accept optional `bankModule`:
  * `POST /api/CurrencyRatesImport/current?company=...&environment=...&bankModule=...`
  * `POST /api/CurrencyRatesImport/date?company=...&date=yyyy-MM-dd&environment=...&bankModule=...`
* UI configuration endpoint:
  * `GET /api/import-options`
* Worker schedule file supports:
  * `workerSchedule.environments[].bankModule`
* Backward compatibility:
  * missing `bankModule` defaults to `MNB`

## How To Add Another Bank Module

1. Add an importer class implementing `IBankCurrencyImporter` in `MultiCountryFxImporter.Infrastructure`.
2. Define `ModuleDefinition` (`Code`, `DisplayName`, `DefaultRefCurrencyCode`).
3. Register importer in API and Worker DI as `IBankCurrencyImporter`.
4. Add and bind module API options (if required) in both `Program.cs` files.
5. Verify the module appears in `/api/import-options` and UI dropdowns.
6. Validate schedule entries and worker runs with the new `bankModule` code.

## Notes

* Reference currency defaults are bank-specific (`MNB=HUF`, `ECB=EUR`) with global fallback.
* The worker deduplicates daily runs by `Environment + Company + BankModule`.
* The solution follows clean architecture principles for maintainability.
