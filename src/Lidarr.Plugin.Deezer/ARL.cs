using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.XPath;

namespace NzbDrone.Plugin.Deezer
{
    public class ARL
    {
        public ARL(string token, string country = "")
        {
            Token = token;
            Country = country;
        }

        private const string FIREHAWK_URL = "https://rentry.org/firehawk52";

        public static async Task<ARL[]> GetSortedARLs()
        {
            HttpClient client = new();
            var html = await client.GetStringAsync(FIREHAWK_URL);

            var parser = new HtmlParser();
            var document = await parser.ParseDocumentAsync(html);

            var tableNode = document.Body.SelectSingleNode("/html/body/div/div/div[2]/div[1]/div/div[2]/article/div/div[6]/table/tbody");

            if (tableNode == null)
            {
                return Array.Empty<ARL>();
            }

            List<ARL> arls = new();
            foreach (var row in tableNode.ChildNodes)
            {
                if (row is IElement elementRow)
                {
                    var countryElement = elementRow.QuerySelector("img");
                    var country = countryElement?.GetAttribute("title") ?? "Unknown";

                    var slashIdx = country.IndexOf('/');
                    if (slashIdx > 0)
                        country = country[..slashIdx];

                    var tokenElement = elementRow.QuerySelector("td:nth-child(4) code");
                    var token = tokenElement?.TextContent;
                    if (token != null)
                    {
                        arls.Add(new ARL(token, country));
                    }
                }
            }

            return arls.ToArray();
        }

        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(Token))
                return false;

            try
            {
                // calling this gets a checkForm/API token, it will always return one regardless of the arl being valid or not, requiring the additional check
                DeezerAPI.Instance.Client.SetARL(Token).Wait();

                if (DeezerAPI.Instance.Client.GWApi.ActiveUserData!["USER"]!.Value<long>("USER_ID") == 0)
                    return false;
            }
            catch
            {
                return false;
            }


            return true;
        }

        public string Token { get; init; }
        public string Country { get; init; }
    }
}
