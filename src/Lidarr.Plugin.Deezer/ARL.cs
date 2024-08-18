using System.Collections.Generic;
using HtmlAgilityPack;

namespace NzbDrone.Plugin.Deezer
{
    public class ARL(string token, string country = "")
    {
        private const string FIREHAWK_URL = "https://rentry.org/firehawk52";

        public static ARL[] GetSortedARLs()
        {
            HtmlWeb web = new();
            var loadTask = web.LoadFromWebAsync(FIREHAWK_URL);
            loadTask.Wait();
            var doc = loadTask.Result;

            var tableNode = doc.DocumentNode.SelectSingleNode("/html/body/div/div/div[2]/div[1]/div/div[2]/article/div/div[11]/table/tbody");

            if (tableNode == null)
            {
                return [];
            }

            List<ARL> arls = [];
            foreach (var row in tableNode.ChildNodes)
            {
                if (row is HtmlTextNode)
                    continue;

                var country = row.SelectSingleNode("td[1]/span/img").GetAttributeValue("title", "Unknown");

                // removes any additional names of countries like Netherlands/Nederland, simpler to just use the first
                var slashIdx = country.IndexOf('/');
                if (slashIdx > 0)
                    country = country[..slashIdx];

                var token = row.SelectSingleNode("td[4]/code").InnerText;
                arls.Add(new(token, country));
            }

            return [.. arls];
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

        public string Token { get; init; } = token;
        public string Country { get; init; } = country;
    }
}
