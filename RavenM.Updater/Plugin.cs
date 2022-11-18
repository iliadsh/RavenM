using BepInEx;
using System.Reflection;
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
        static readonly string PLUGIN_DIR = "BepInEx/plugins/";

        private void Awake()
        {
            var force_current_version = Config.Bind("General", 
                                                    "Force Disable Updates",
                                                    false,
                                                    "If you're using a potentially outdated version and know what you're doing, this will disabled the updater.");

            if (force_current_version.Value)
            {
                Logger.LogWarning("Updates are disabled!");
                return;
            }

            Logger.LogInfo("Checking for updates...");

            var mod_version = AssemblyName.GetAssemblyName(PLUGIN_DIR + "RavenM.dll").Version;

            var req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/iliadsh/RavenM/releases");
            req.Method = "GET";
            req.Accept = "text / html,application / xhtml + xml,application / xml; q = 0.9,image / webp,image / apng,*/*;q=0.8;";
            req.UserAgent = "Def-Not-RavenM";
            req.Timeout = 5000;
            using var response = new StreamReader(req.GetResponse().GetResponseStream());

            var json = JSON.Parse(response.ReadToEnd());

            foreach (var kv in json.AsArray)
            {
                var release = kv.Value;
                var tag = release["tag_name"];
                if (Version.TryParse(tag, out Version release_version))
                {
                    if (release_version > mod_version)
                    {
                        Logger.LogWarning($"Newer version found: {tag}. Downloading...");

                        var download_url = release["assets"][0]["browser_download_url"];
                        using var client = new WebClient();
                        using var zip = new MemoryStream(client.DownloadData(download_url));

                        Logger.LogWarning("Downloaded new version. Updating...");

                        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);
                        foreach (var s in archive.Entries)
                        {
                            // We can't update the updater, since it's already locked by BepInEx.
                            if (s.Name != "RavenM.Updater.dll")
                            {
                                try
                                {
                                    s.ExtractToFile(PLUGIN_DIR + s.Name, true);
                                }
                                catch (Exception e)
                                {
                                    Logger.LogError($"Failed to update file {s.Name}. {e}");
                                }
                            }
                        }
                        break;
                    }
                }
            }

            Logger.LogInfo("All done... have a nice day :)");
        }
    }
}