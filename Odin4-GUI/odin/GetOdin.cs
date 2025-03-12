using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GetOdin
{
    public class GetOdin
    {
        public static async Task Download()
        {
            string url = "https://api.github.com/repos/Adrilaw/OdinV4/releases/latest";
            string zipPath = "odin.zip";
            string extractPath = "odin";
            string binPath = "/bin/odin";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("request");

                var response = await client.GetStringAsync(url);
                var json = JObject.Parse(response);
                var assets = json["assets"];
                if (assets == null || !assets.HasValues)
                {
                    throw new Exception("No assets found in the release.");
                }

                var asset = assets[0];
                if (asset == null)
                {
                    throw new Exception("No asset found.");
                }

                var browserDownloadUrl = asset["browser_download_url"];
                if (browserDownloadUrl == null)
                {
                    throw new Exception("No browser download URL found for the asset.");
                }

                var downloadUrl = browserDownloadUrl.ToString();

                using (var downloadStream = await client.GetStreamAsync(downloadUrl))
                using (var fileStream = new FileStream(zipPath, FileMode.Create))
                {
                    await downloadStream.CopyToAsync(fileStream);
                }
            }

            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }

            ZipFile.ExtractToDirectory(zipPath, extractPath);

            string? odinExecutablePath = null;
            foreach (var file in Directory.GetFiles(extractPath))
            {
                if (file.EndsWith("odin"))
                {
                    odinExecutablePath = file;
                }
                else
                {
                    File.Delete(file);
                }
            }

            if (odinExecutablePath == null)
            {
                throw new Exception("Odin executable not found.");
            }

            if (File.Exists(binPath))
            {
                File.Delete(binPath);
            }

            File.Move(odinExecutablePath, binPath);
            File.Delete(zipPath);

            Console.WriteLine("Odin downloaded and extracted to /bin.");
        }
    }
}