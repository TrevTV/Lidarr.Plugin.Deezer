using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.XPath;

namespace NzbDrone.Plugin.Deezer
{
    public static class ARLUtilities
    {
        private const string FIREHAWK_URL = "https://rentry.org/firehawk52";

        public static async Task<string> GetFirstValidARL()
        {
            using HttpClient client = new();
            var html = await client.GetStringAsync(FIREHAWK_URL);

            var parser = new HtmlParser();
            var document = await parser.ParseDocumentAsync(html);

            var deezerTitleNode = (IElement)document.Body.SelectSingleNode("//*[@id=\"deezer-arls\"]");

            var tableNode = deezerTitleNode.NextElementSibling;
            while (tableNode != null && tableNode.GetAttribute("class") != "ntable-wrapper")
                tableNode = tableNode.NextElementSibling;

            if (tableNode == null)
                return "";
            else
                tableNode = (IElement)tableNode.SelectSingleNode("table/tbody");

            List<string> arls = new();
            foreach (var row in tableNode.ChildNodes)
            {
                if (row is IElement elementRow)
                {
                    var tokenElement = elementRow.QuerySelector("td:nth-child(4) code");
                    var token = tokenElement?.TextContent;
                    if (token != null)
                    {
                        if (IsValid(token))
                            return token;
                    }
                }
            }

            return "";
        }

        public static bool IsValid(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            try
            {
                // calling this gets a checkForm/API token, it will always return one regardless of the arl being valid or not, requiring the additional checks
                DeezerAPI.Instance.Client.SetARL(token).Wait();

                bool accountActive = DeezerAPI.Instance.Client.GWApi.ActiveUserData!["USER"]!.Value<long>("USER_ID") == 0;
                bool hasStreaming = DeezerAPI.Instance.Client.GWApi.ActiveUserData!["USER"]!["OPTIONS"]!.Value<bool>("web_streaming");
                if (accountActive && hasStreaming)
                    return false;
            }
            catch
            {
                return false;
            }


            return true;
        }
    }
}
