using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using MultiCountryFxImporter.Core.Interfaces;
using MultiCountryFxImporter.Core.Models;
using MultiCountryFxImporter.MnbClient;
using System.Globalization;

namespace MultiCountryFxImporter.Infrastructure
{
    public class MnbImporter : IBankCurrencyImporter
    {
        private readonly MNBArfolyamServiceSoapClient _client;

        public MnbImporter(MNBArfolyamServiceSoapClient client)
        {
            _client = client;
        }

        public BankModuleDefinition ModuleDefinition { get; } = new()
        {
            Code = BankModuleCatalog.MnbCode,
            DisplayName = "Magyar Nemzeti Bank",
            DefaultRefCurrencyCode = "HUF"
        };

        public async Task<IEnumerable<FxRate>> ImportAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? currencyNames = null)
        {
            var doc = await LoadRatesXmlAsync(startDate, endDate, currencyNames);
            WriteXmlSnapshot(doc);

            return ParseRates(doc);
        }

        private async Task<XDocument> LoadRatesXmlAsync(DateTime? startDate, DateTime? endDate, string? currencyNames)
        {
            if (startDate.HasValue || endDate.HasValue)
            {
                if (!startDate.HasValue)
                {
                    startDate = endDate;
                }

                if (!endDate.HasValue)
                {
                    endDate = startDate;
                }

                var requestBody = new GetExchangeRatesRequestBody
                {
                    startDate = startDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    endDate = endDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    currencyNames = string.IsNullOrWhiteSpace(currencyNames) ? null : currencyNames
                };
                var response = await _client.GetExchangeRatesAsync(requestBody);
                var xml = response.GetExchangeRatesResponse1.GetExchangeRatesResult;
                if (string.IsNullOrWhiteSpace(xml))
                {
                    return new XDocument();
                }
                return XDocument.Parse(xml!);
            }

            var currentResponse = await _client.GetCurrentExchangeRatesAsync(new GetCurrentExchangeRatesRequestBody());
            var currentXml = currentResponse.GetCurrentExchangeRatesResponse1.GetCurrentExchangeRatesResult;
            return XDocument.Parse(currentXml!);
        }

        private static IReadOnlyList<FxRate> ParseRates(XDocument doc)
        {
            var list = new List<FxRate>();
            foreach (var day in doc.Descendants().Where(node => node.Name.LocalName == "Day"))
            {
                var dateAttr = day.Attribute("date")?.Value ?? DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var date = DateTime.Parse(dateAttr, CultureInfo.InvariantCulture);
                foreach (var rateNode in day.Descendants().Where(node => node.Name.LocalName == "Rate"))
                {
                    var currency = rateNode.Attribute("curr")?.Value ?? string.Empty;
                    var unitStr = (rateNode.Attribute("unit")?.Value ?? "1").Trim().Replace(',', '.');
                    var rateStr = (rateNode.Value ?? string.Empty).Trim().Replace(',', '.');

                    var rateUnit = decimal.Parse(unitStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                    var rate = decimal.Parse(rateStr, NumberStyles.Any, CultureInfo.InvariantCulture);

                    list.Add(new FxRate
                    {
                        Date = date,
                        Bank = "MNB",
                        Currency = currency,
                        RateUnit = rateUnit,
                        Rate = rate,
                    });
                }
            }

            return list;
        }

        private static void WriteXmlSnapshot(XDocument doc)
        {
            var logsDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDirectory);

            var fileName = $"mnb-response-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xml";
            var filePath = Path.Combine(logsDirectory, fileName);

            doc.Save(filePath, SaveOptions.None);
        }
    }
}
