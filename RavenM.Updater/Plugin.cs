using BepInEx;
using BepInEx.Configuration;
using System.Collections;
using System.Reflection;
using UnityEngine.Networking;
using System;
using System.IO;
using System.IO.Compression;
using SimpleJSON;
using System.Net;

namespace RavenM.Updater 
{
    /// <summary>
    /// Unfortunately, BepInEx loads plugin .dll(s) by file path rather than loading them into memory first.
    /// This means the .dll is locked by the file system and can't overwrite itself. That's why this plugin exists --
    /// since the dependency resolver will load and execute this first, we can overwrite RavenM.dll just in time.
    /// </summary>
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    class Plugin : BaseUnityPlugin
    {
        static readonly string STAGING_DIR = "RavenM_staging/";
        static readonly string PLUGIN_DIR = "BepInEx/plugins/";

        private void Awake()
        {
            var force_current_version = Config.Bind("General", 
                                                    "Force Disable Updates",
                                                    false,
                                                    "If you're using a potentially outdated version and know what you're doing, this will disabled the updater.");

            if (force_current_version.Value)
            {
                return;
            }

            Directory.CreateDirectory(STAGING_DIR);

            if (File.Exists(STAGING_DIR + "latest.zip"))
            {
                Logger.LogWarning("Updating...");

                using (var archive = ZipFile.OpenRead(STAGING_DIR + "latest.zip"))
                {
                    foreach (var s in archive.Entries)
                    {
                        // We can't update the updater, since it's already locked by BepInEx.
                        if (s.Name != "RavenM.Updater.dll")
                        {
                            try
                            {
                                s.ExtractToFile(PLUGIN_DIR + s.Name, true);
                            } catch (Exception e)
                            {
                                Logger.LogError($"Failed to update file {s.Name}. {e}");
                            }
                        }
                    }
                }
                File.Delete(STAGING_DIR + "latest.zip");

                Logger.LogInfo("All done... have a nice day :)");
            }

            StartCoroutine(LookForUpdates());
        }


        IEnumerator LookForUpdates()
        {
            Logger.LogInfo("Checking for updates...");

            var mod_version = Assembly.LoadFrom(PLUGIN_DIR + "RavenM.dll").GetName().Version;

            UnityWebRequest uwr = UnityWebRequest.Get("https://api.github.com/repos/iliadsh/RavenM/releases");
            yield return uwr.Send();

            var json = JSON.Parse(uwr.downloadHandler.text);
            
            foreach (var kv in json.AsArray)
            {
                var release = kv.Value;
                var tag = release["tag_name"];
                if (Version.TryParse(tag, out Version release_version))
                {
                    if (release_version > mod_version)
                    {
                        Logger.LogInfo($"Newer version found: {tag}");

                        var download_url = release["assets"][0]["browser_download_url"];
                        using var client = new WebClient();
                        client.DownloadFile(download_url, STAGING_DIR + "latest.zip");

                        Logger.LogWarning("Downloaded new version. Update will be applied on next launch.");
                        break;
                    }
                }
            }
        }
    }
}