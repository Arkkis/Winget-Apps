using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace WingetAppsLibrary
{
    public class WingetApps
    {
        public static int GetWingetAppsEstimatedTotal;
        public static int GetWingetAppsItemsDone;

        public async Task<List<AppDto>> GetWingetApps(int cacheExpirationTimeInMinutes)
        {
            const int monthExpireTimeInMinutes = 43200;
            var cacheDirectory = $"{AppContext.BaseDirectory}cache\\";
            var appListCachePath = $"{cacheDirectory}AppListCache.json";

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
                    Console.WriteLine($"Time elapsed: {stopwatch.ElapsedMilliseconds}ms");
                    
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

            GetWingetAppsEstimatedTotal = apps.Count;

            var appList = new List<AppDto>();

            foreach (var app in apps)
            {
                var yamlPath = $"{cacheDirectory}{app.path.Replace("/", "\\")}";
                var yamlResponse = await GetOrLoadFromCache<string>($"https://raw.githubusercontent.com/microsoft/winget-pkgs/master/manifests/{app.path}", yamlPath, monthExpireTimeInMinutes);

                var stringReader = new StringReader(yamlResponse);
                var deserializer = new Deserializer();
                var appData = deserializer.Deserialize<dynamic>(stringReader);

                try
                {
                    if (!string.IsNullOrEmpty(appData["PackageIdentifier"]) && !string.IsNullOrEmpty(appData["PackageName"]))
                    {
                        appList.Add(
                            new AppDto
                            {
                                PackageId = appData["PackageIdentifier"],
                                Name = appData["PackageName"],
                                ShortDescription = appData["ShortDescription"] ?? ""
                            });
                    }
                }
                catch (Exception)
                {
                    // ignored
                }

                GetWingetAppsItemsDone++;
            }

            if (File.Exists(appListCachePath))
            {
                File.WriteAllText(appListCachePath, "");
            }

            File.WriteAllText(appListCachePath, JsonSerializer.Serialize(new AppListDto { Apps = appList, CreatedAt = DateTime.UtcNow }));

            stopwatch.Stop();
            Console.WriteLine($"Time elapsed: {stopwatch.ElapsedMilliseconds}ms");

            return appList;
        }

        private async Task<T> GetOrLoadFromCache<T>(string url, string cacheFile, int expireTimeInMinutes = 60)
        {
            var response = "";
            var cacheDirectory = Path.GetDirectoryName(cacheFile);
            var cacheInvalidated = false;

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

                var cachedFile = JsonSerializer.Deserialize<CachedFile>(File.ReadAllText(cacheFile));

                if (cachedFile?.CreatedAt.AddMinutes(expireTimeInMinutes) > DateTime.UtcNow)
                {
                    response = Encoding.UTF8.GetString(Convert.FromBase64String(cachedFile.Content));
                }
                else
                {
                    cacheInvalidated = true;
                }
            }
            else
            {
                cacheInvalidated = true;
            }

            if (cacheInvalidated)
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "request");
                response = await httpClient.GetStringAsync(url);

                var cachedFile = new CachedFile { CreatedAt = DateTime.UtcNow, Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(response)) };

                var fileDirectory = Path.GetDirectoryName(cacheFile);

                if (fileDirectory != null && !Directory.Exists(fileDirectory))
                {
                    Directory.CreateDirectory(fileDirectory);
                }

                File.WriteAllText(cacheFile, JsonSerializer.Serialize(cachedFile));

                await Task.Delay(1000);
            }

            if (typeof(T) == typeof(string))
            {
                return (T)Convert.ChangeType(response, typeof(T));
            }

            var responseDto = JsonSerializer.Deserialize<T>(response);
            return responseDto;
        }
    }
}
