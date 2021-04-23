using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using WingetAppsLibrary;
using System.IO.Compression;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Test().GetAwaiter().GetResult();
        }

        private static async Task<string> Test()
        {
            var cachePath = $"{AppContext.BaseDirectory}cache";

            if (!Directory.Exists(cachePath))
            {
                var extractedZipPath = $"{AppContext.BaseDirectory}winget-pkgs-master";
                var zipPath = $"{AppContext.BaseDirectory}master.zip";

                if (Directory.Exists(extractedZipPath))
                {
                    Directory.Delete(extractedZipPath, true);
                }

                var webClient = new WebClient();
                webClient.Headers.Add("User-Agent", "request");
                webClient.DownloadFile(new Uri("https://github.com/microsoft/winget-pkgs/archive/refs/heads/master.zip"), zipPath);

                ZipFile.ExtractToDirectory(zipPath, AppContext.BaseDirectory);

                if (!Directory.Exists(cachePath))
                {
                    Directory.Move($"{extractedZipPath}\\manifests", cachePath);
                }

                if (Directory.Exists(extractedZipPath))
                {
                    Directory.Delete(extractedZipPath, true);
                }

                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
            }

            var wingetApps = new WingetApps();

            var test = await wingetApps.GetWingetApps(60 * 24);
            var appCount = 0;

            // Getting disctinct items by Id with GroupBy
            foreach (var app in test.GroupBy(a => a.PackageId).Select(g => g.First()).ToList())
            {
                Console.WriteLine(app.Name);
                appCount++;
            }

            Console.WriteLine($"Total apps: {appCount}");
            Console.ReadKey();

            return "";
        }
    }
}
