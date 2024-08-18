using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Linq;

namespace NzbDrone.Plugin.Deezer
{
    public class DeezerAPI
    {
        public static DeezerAPI? Instance { get; private set; }

        internal DeezerAPI(string arl)
        {
            Instance = this;
            _apiToken = "null"; // this doesn't necessarily need to be null; used for deezer.getUserData
            _arl = arl;
            UpdateArl(arl);
        }

        internal string _arl;
        internal string _apiToken;
        private JToken? _activeUserData;

        internal void UpdateArl(string arl)
        {
            _arl = arl;
            if (string.IsNullOrEmpty(arl))
            {
                // TODO: this would be where to grab one from firehawk
                return;
            }

            var userData = GetUserData();
            _activeUserData = userData;
            _apiToken = userData["checkForm"]!.ToString();
        }

        private JToken GetUserData() => Call("deezer.getUserData", needsArl: true);

        private JToken Call(string method, JObject? args = null, Dictionary<string, string>? parameters = null, bool needsArl = false)
        {
            parameters ??= [];
            parameters["api_version"] = "1.0";
            parameters["api_token"] = _apiToken;
            parameters["input"] = "3";
            parameters["method"] = method;

            var body = args?.ToString() ?? "";
            StringContent stringContent = new(body);

            StringBuilder stringBuilder = new("https://www.deezer.com/ajax/gw-light.php");
            for (var i = 0; i < parameters.Count; i++)
            {
                var start = i == 0 ? "?" : "&";
                var key = WebUtility.UrlEncode(parameters.ElementAt(i).Key);
                var value = WebUtility.UrlEncode(parameters.ElementAt(i).Value);
                stringBuilder.Append(start + key + "=" + value);
            }

            var url = stringBuilder.ToString();

            using HttpRequestMessage request = new(HttpMethod.Post, url)
            {
                Content = stringContent
            };

            if (needsArl)
                request.Headers.Add("Cookie", "arl=" + _arl);

            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            var response = client.Send(request);

            var readTask = response.Content.ReadAsStringAsync();
            readTask.Wait();
            var resp = readTask.Result;
            var json = JObject.Parse(resp);

            var error = json["error"];
            if (error != null && error.Any())
            {
                if (error["VALID_TOKEN_REQUIRED"] != null || error["GATEWAY_ERROR"] != null)
                {
                    UpdateArl(_arl);
                    return Call(method, args, parameters, needsArl);
                }

                if (error["DATA_ERROR"] != null)
                    throw new System.Exception("The given ID is not valid. It either does not exist or is not for the requested content type.");

                throw new System.Exception(error.ToString());
            }

            return json["results"]!;
        }

        public string GetGWUrl(string method, Dictionary<string, string>? parameters = null)
        {
            parameters ??= [];
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

        public string GetPublicUrl(string method, Dictionary<string, string>? parameters = null)
        {
            parameters ??= [];

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
