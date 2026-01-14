using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using MultiCountryFxImporter.Core.Models;
using MultiCountryFxImporter.MnbClient;
using System.Globalization;

namespace MultiCountryFxImporter.Infrastructure
{
    public class MnbImporter : ICurrencyImporter
    {
        private readonly MNBArfolyamServiceSoapClient _client;

        public MnbImporter(MNBArfolyamServiceSoapClient client)
        {
            _client = client;
        }

        public async Task<IEnumerable<FxRate>> ImportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var response = await _client.GetCurrentExchangeRatesAsync(new GetCurrentExchangeRatesRequestBody());
            var xml = response.GetCurrentExchangeRatesResponse1.GetCurrentExchangeRatesResult;

            var doc = XDocument.Parse(xml!);
            var list = new List<FxRate>();

            foreach (var day in doc.Descendants("Day"))
            {
                var dateAttr = day.Attribute("date")?.Value ?? DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var date = DateTime.Parse(dateAttr, CultureInfo.InvariantCulture);
                foreach (var rateNode in day.Descendants("Rate"))
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
    }
}
