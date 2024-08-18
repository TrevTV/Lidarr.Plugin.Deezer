using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Linq;
using DeezNET;

namespace NzbDrone.Plugin.Deezer
{
    public class DeezerAPI
    {
        public static DeezerAPI Instance { get; private set; } = new("");

        internal DeezerAPI(string arl)
        {
            Instance = this;
            _client = new();
            CheckAndSetARL(arl);
        }

        public DeezerClient Client => _client;

        private DeezerClient _client;
        private string _apiToken => _client.GWApi.ActiveUserData["checkForm"]?.ToString() ?? "null";

        internal bool CheckAndSetARL(string arl)
        {
            if (string.IsNullOrEmpty(arl))
                return string.IsNullOrEmpty(_client.ActiveARL) ? false : true;

            // prevent double hitting the Deezer API when there's no reason to
            if (_client.ActiveARL != arl)
                _client.SetARL(arl).Wait();

            return true;
        }

        public string GetGWUrl(string method, Dictionary<string, string> parameters = null)
        {
            parameters ??= new();
            parameters["api_version"] = "1.0";
            parameters["api_token"] = _apiToken;
            parameters["input"] = "3";
            parameters["method"] = method;

            StringBuilder stringBuilder = new("https://www.deezer.com/ajax/gw-light.php");
            for (var i = 0; i < parameters.Count; i++)
            {
                var start = i == 0 ? "?" : "&";
                var key = WebUtility.UrlEncode(parameters.ElementAt(i).Key);
                var value = WebUtility.UrlEncode(parameters.ElementAt(i).Value);
                stringBuilder.Append(start + key + "=" + value);
            }
            return stringBuilder.ToString();
        }

        public string GetPublicUrl(string method, Dictionary<string, string> parameters = null)
        {
            parameters ??= new();

            StringBuilder stringBuilder = new("https://api.deezer.com/" + method);
            for (var i = 0; i < parameters.Count; i++)
            {
                var start = i == 0 ? "?" : "&";
                var key = WebUtility.UrlEncode(parameters.ElementAt(i).Key);
                var value = WebUtility.UrlEncode(parameters.ElementAt(i).Value);
                stringBuilder.Append(start + key + "=" + value);
            }

            return stringBuilder.ToString();
        }
    }
}
