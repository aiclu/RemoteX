namespace Shawn.Utils
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    public static class HttpHelper
    {
        #region POST

        public static string Post(string url, Dictionary<string, string> dic, Encoding? encoding = null)
        {
            return PostAsync(url, dic, encoding).Result;
        }

        public static string Post(string url, string content, Encoding? encoding = null)
        {
            return PostAsync(url, content, encoding).Result;
        }

        public static async Task<string> PostAsync(string url, Dictionary<string, string> dic, Encoding? encoding = null)
        {
            var builder = new StringBuilder();
            int i = 0;
            foreach (var item in dic)
            {
                if (i > 0)
                    builder.Append("&");
                builder.AppendFormat("{0}={1}", item.Key, item.Value);
                i++;
            }
            return await PostAsync(url, builder.ToString(), encoding);
        }

        public static async Task<string> PostAsync(string url, string content, Encoding? encoding = null)
        {
            var client = new HttpClient();
            var response = await client.PostAsync(url, new StringContent(content, Encoding.UTF8));
            var ret = await response.Content.ReadAsByteArrayAsync();
            encoding ??= System.Text.Encoding.UTF8;
            var responseString = encoding.GetString(ret, 0, ret.Length - 1);
            return responseString;
        }

        #endregion POST

        #region GET

        public static string Get(string url, Encoding? encoding = null)
        {
            return GetAsync(url, encoding).Result;
        }
        public static string Get(string url, Dictionary<string, string> dic, Encoding? encoding = null)
        {
            return GetAsync(url, dic, encoding).Result;
        }

        public static async Task<string> GetAsync(string url, Encoding? encoding = null)
        {
            var client = new HttpClient();
            // GitHub's API rejects requests without a User-Agent (403); most other
            // hosts are fine with any reasonable agent string.
            client.DefaultRequestHeaders.UserAgent.TryParseAdd("RemoteX");
            var response = await client.GetByteArrayAsync(url);
            encoding ??= System.Text.Encoding.UTF8;
            var responseString = encoding.GetString(response, 0, response.Length - 1);
            return responseString;
        }

        public static async Task<string> GetAsync(string url, Dictionary<string, string> dic, Encoding? encoding = null)
        {
            var builder = new StringBuilder();
            builder.Append(url);
            if (dic.Count > 0)
            {
                builder.Append("?");
                int i = 0;
                foreach (var item in dic)
                {
                    if (i > 0)
                        builder.Append("&");
                    builder.AppendFormat("{0}={1}", item.Key, item.Value);
                    i++;
                }
            }
            var uri = builder.ToString();
            return await GetAsync(uri, encoding);
        }

        #endregion GET
    }

}
