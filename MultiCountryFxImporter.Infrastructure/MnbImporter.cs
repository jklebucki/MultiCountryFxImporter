using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using MultiCountryFxImporter.Core.Models;
using MultiCountryFxImporter.MnbClient;

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
                var date = DateTime.Parse(day.Attribute("date")?.Value ?? DateTime.Now.ToString());
                foreach (var rateNode in day.Descendants("Rate"))
                {
                    list.Add(new FxRate
                    {
                        Date = date,
                        Bank = "MNB",
                        Currency = rateNode.Attribute("curr")?.Value ?? "",
                        RateUnit = decimal.Parse(rateNode.Attribute("unit")?.Value ?? "1"),
                        Rate = decimal.Parse(rateNode.Value.Replace(',', '.')),
                    });
                }
            }

            return list;
        }
    }
}
