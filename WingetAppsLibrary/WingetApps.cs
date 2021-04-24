using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace WingetAppsLibrary
{
    public class WingetApps
    {
        public static int GetWingetAppsEstimatedTotal;
        public static int GetWingetAppsItemsDone;
        public static string cacheDirectory;
        public static string appListCachePath;
        private const int monthExpireTimeInMinutes = 43200;

        public async Task<List<AppDto>> GetWingetApps(int cacheExpirationTimeInMinutes)
        {
            cacheDirectory = $"{AppContext.BaseDirectory}cache\\";
            appListCachePath = $"{cacheDirectory}AppListCache.json";

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            if (File.Exists(appListCachePath))
            {
                var cachedAppList = JsonSerializer.Deserialize<AppListDto>(File.ReadAllText(appListCachePath));

                if (cachedAppList?.CreatedAt.AddMinutes(cacheExpirationTimeInMinutes) > DateTime.UtcNow)
                {
                    GetWingetAppsEstimatedTotal = cachedAppList.Apps.Count;
                    GetWingetAppsItemsDone = GetWingetAppsEstimatedTotal;

                    stopwatch.Stop();
                    Console.WriteLine($"Time elapsed loading appList from cache: {stopwatch.ElapsedMilliseconds}ms");
                    
                    return cachedAppList.Apps;
                }
            }

            var fileTreeResponse = await GetOrLoadFromCache<TreeDto>("https://api.github.com/repos/microsoft/winget-pkgs/git/trees/master", $"{cacheDirectory}tree.json");
            var sha = fileTreeResponse.tree.FirstOrDefault(file => file.path == "manifests")?.sha;

            if (string.IsNullOrEmpty(sha))
            {
                throw new NullReferenceException(nameof(sha));
            }

            var appsResponse = await GetOrLoadFromCache<TreeDto>($"https://api.github.com/repos/microsoft/winget-pkgs/git/trees/{sha}?recursive=1", $"{cacheDirectory}treelist.json");

            var apps = new List<FileDto>();
            var packageNames = new List<string>();

            Console.WriteLine($"Time elapsed with loading file tree: {stopwatch.ElapsedMilliseconds}ms");
            stopwatch.Restart();

            foreach (var app in appsResponse.tree)
            {
                if (app.type != "blob")
                {
                    continue;
                }

                var path = app.path.Split("/");

                if (packageNames.Contains(path[2]))
                {
                    continue;
                }

                packageNames.Add(path[2]);
                apps.Add(app);
            }

            Console.WriteLine($"Time elapsed checking packageNames: {stopwatch.ElapsedMilliseconds}ms");
            stopwatch.Restart();

            GetWingetAppsEstimatedTotal = apps.Count;

            var appList = await CreateAppList(apps);

            Console.WriteLine($"Time elapsed creating appList: {stopwatch.ElapsedMilliseconds}ms");
            stopwatch.Restart();

            if (File.Exists(appListCachePath))
            {
                File.WriteAllText(appListCachePath, "");
            }

            File.WriteAllText(appListCachePath, JsonSerializer.Serialize(new AppListDto { Apps = appList, CreatedAt = DateTime.UtcNow }));

            stopwatch.Stop();
            Console.WriteLine($"Time elapsed: {stopwatch.ElapsedMilliseconds}ms");

            return appList;
        }

        private async Task<List<AppDto>> CreateAppList(List<FileDto> apps)
        {
            var appList = new List<AppDto>();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            foreach (var app in apps)
            {
                var yamlPath = $"{cacheDirectory}{app.path.Replace("/", "\\")}";
                var yamlResponse = await GetOrLoadFromCache<string>($"https://raw.githubusercontent.com/microsoft/winget-pkgs/master/manifests/{app.path}", yamlPath);

                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();

                var appData = deserializer.Deserialize<PackageYamlDto>(yamlResponse);

                appList.Add(
                            new AppDto
                            {
                                PackageId = appData.PackageIdentifier,
                                Name = appData.PackageName,
                                ShortDescription = appData.ShortDescription
                            });

                GetWingetAppsItemsDone++;
            }

            stopwatch.Stop();
            Console.WriteLine($"Time elapsed in CreateAppList: {stopwatch.ElapsedMilliseconds}ms");

            return appList;
        }

        private async Task<T> GetOrLoadFromCache<T>(string url, string cacheFile)
        {
            var cacheDirectory = Path.GetDirectoryName(cacheFile);

            if (cacheDirectory != null && !Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            if (File.Exists(cacheFile))
            {
                if (typeof(T) == typeof(string))
                {
                    return (T)Convert.ChangeType(File.ReadAllText(cacheFile), typeof(T));
                }

                return JsonSerializer.Deserialize<T>(File.ReadAllText(cacheFile));
            }

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "request");
            var response = await httpClient.GetStringAsync(url);

            var fileDirectory = Path.GetDirectoryName(cacheFile);

            if (fileDirectory != null && !Directory.Exists(fileDirectory))
            {
                Directory.CreateDirectory(fileDirectory);
            }

            File.WriteAllText(cacheFile, response);

            if (typeof(T) == typeof(string))
            {
                return (T)Convert.ChangeType(response, typeof(T));
            }

            return JsonSerializer.Deserialize<T>(response);
        }
    }
}
