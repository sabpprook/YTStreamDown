using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace YTStreamDown
{
    class Program
    {
        static string Url { get; set; }
        static string Cookie { get; set; }
        static string HTML { get; set; }
        static string VideoID { get; set; }
        static string VideoTitle { get; set; }
        static string DashManifest { get; set; }
        static string AudioUrl { get; set; }
        static string VideoUrl { get; set; }

        static string Config_Null = "ew0KICAidXJsIjogIiIsDQogICJjb29raWUiOiAiIg0KfQ==";
        static string Cover_JPG = "https://i.ytimg.com/vi/{0}/maxresdefault_live.jpg";

        static void Main(string[] args)
        {
            ServicePointManager.DefaultConnectionLimit = 500;

            LoadConfig();
            GetVideoInfo();
            GetRepresentation();

            var tasks = new List<Task>();
            tasks.Add(Task.Run(() => DownloadTask(AudioUrl, "audio")));
            tasks.Add(Task.Run(() => DownloadTask(VideoUrl, "video")));

            Task.WaitAll(tasks.ToArray());
        }

        static void LoadConfig()
        {
            if (!File.Exists("config.json"))
            {
                File.WriteAllText("config.json", Encoding.UTF8.GetString(Convert.FromBase64String(Config_Null)));
                Console.WriteLine("Please setup config.json!");
                Console.ReadKey();
                Environment.Exit(0);
            }

            var config = File.ReadAllText("config.json");
            var jss = new JavaScriptSerializer();
            var d = jss.Deserialize<dynamic>(config);

            Url = d["url"];
            Cookie = d["cookie"];

            if (string.IsNullOrEmpty(Url))
            {
                Console.WriteLine("Video url cannot be null or empty!");
                Console.ReadKey();
                Environment.Exit(0);
            }
        }

        static void GetVideoInfo()
        {
            try
            {
                using (var wc = new WebClientEx())
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers.Add(HttpRequestHeader.Cookie, Cookie);
                    HTML = wc.DownloadString(Url);

                    var pattern = "\"videoId\":\"([^\"]+)\",\"title\":\"(.+)\",\"lengthSeconds";
                    var match = Regex.Match(HTML, pattern);

                    VideoID = match.Groups[1].Value;
                    VideoTitle = match.Groups[2].Value;

                    var cover_url = string.Format(Cover_JPG, VideoID);
                    wc.DownloadFile(cover_url, "cover.jpg");

                    File.WriteAllText("title.txt", VideoTitle);

                    pattern = "\"dashManifestUrl\":\"([^\"]+)\"";
                    var dashManifestUrl = Regex.Match(HTML, pattern).Groups[1].Value;

                    DashManifest = wc.DownloadString(dashManifestUrl);
                }
            }
            catch
            {
                Console.WriteLine("Cannot get video info!");
                Console.ReadKey();
                Environment.Exit(0);
            }
        }

        static void GetRepresentation()
        {
            var audio_best = string.Empty;
            var audio_bandwidth = 0;
            var audio_best_url = string.Empty;

            var video_best = string.Empty;
            var video_bandwidth = 0;
            var video_best_url = string.Empty;

            var matches = Regex.Matches(DashManifest, "<Representation(.+?)><BaseURL>(.+?)<\\/BaseURL>");
            foreach (Match match in matches)
            {
                var info = match.Groups[1].Value;
                var baseUrl = match.Groups[2].Value;

                var id = Regex.Match(info, "id=\"(\\d+)\"").Groups[1].Value;
                var _bandwidth = Regex.Match(info, "bandwidth=\"(\\d+)\"").Groups[1].Value;
                var bandwidth = int.Parse(_bandwidth);

                if (info.Contains("mp4a"))
                {
                    if (bandwidth > audio_bandwidth)
                    {
                        audio_best = id;
                        audio_bandwidth = bandwidth;
                        audio_best_url = baseUrl;
                    }
                }

                if (info.Contains("avc1"))
                {
                    if (bandwidth > video_bandwidth)
                    {
                        video_best = id;
                        video_bandwidth = bandwidth;
                        video_best_url = baseUrl;
                    }
                }
            }

            AudioUrl = audio_best_url;
            VideoUrl = video_best_url;
        }

        static void DownloadTask(string url, string type)
        {
            var path = $"download\\{type}";
            Directory.CreateDirectory(path);

            var segment = 0;
            var retry = 0;

            while (true)
            {
                var fileUrl = $"{url}sq/{segment}/";
                var fileName = $"{path}\\media_{segment.ToString("D5")}.mp4";

                if (!File.Exists(fileName))
                {
                    try
                    {
                        using (var wc = new WebClientEx())
                        {
                            wc.DownloadFile(fileUrl, fileName);
                            Console.WriteLine(fileName);
                        }
                        segment++;
                        Thread.Sleep(10);
                    }
                    catch
                    {
                        Console.WriteLine($"{fileName} Retry:{retry}");
                        Thread.Sleep(100);
                        if (retry++ >= 10)
                        {
                            retry = 0;
                            segment++;
                        }
                    }
                }
                else
                {
                    segment++;
                }
            }
        }
    }

    class WebClientEx : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            HttpWebRequest request = base.GetWebRequest(address) as HttpWebRequest;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.UserAgent = "Mozilla/5.0";
            request.Timeout = 10000;
            return request;
        }
    }
}
