# MultiCountry FX Importer

[ ![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[ ![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-8.0-green)](https://docs.microsoft.com/en-us/aspnet/core/)
[ ![CsvHelper](https://img.shields.io/badge/CsvHelper-33.1.0-orange)](https://joshclose.github.io/CsvHelper/)

A .NET 8 solution for importing foreign exchange rates from multiple national banks (starting with MNB – Hungary) using SOAP/REST APIs and exporting them to CSV files and API endpoints.

## Features

* **ASP.NET Core Web API**: Exposes latest FX rates via REST endpoints
* **Background Worker**: Scheduled imports using HostedService
* **MNB SOAP Integration**: XML parsing with unit normalization
* **CSV Export**: Using CsvHelper for formatted output
* **Extensible Architecture**: Easy to add importers for additional countries

## Tech Stack

* **Framework**: .NET 8.0
* **Web Framework**: ASP.NET Core
* **CSV Library**: CsvHelper
* **Integration**: SOAP/REST APIs, XML Parsing
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

## Notes

* All rates are normalized to HUF per 1 unit of currency
* CSV files are generated with semicolon delimiter and specific formatting
* The solution follows clean architecture principles for maintainability


