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
        static readonly string DEV_DOWNLOAD_URL = "https://nightly.link/iliadsh/RavenM/workflows/main/master/RavenM.zip";

        enum UpdateChannel
        {
            Release,
            Dev
        }

        private Stream MakeRequest(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Accept = "application/vnd.github+json";
            req.UserAgent = "Def-Not-RavenM";
            req.Timeout = 5000;

            return req.GetResponse().GetResponseStream();
        }

        private void Awake()
        {
            var force_current_version = Config.Bind("General",
                                                    "Force Disable Updates",
                                                    false,
                                                    "If you're using a potentially outdated version and know what you're doing, this will disabled the updater.");

            var update_channel = Config.Bind("General",
                                             "Update Channel",
                                             UpdateChannel.Release);

            var current_update_channel = (UpdateChannel)Enum.Parse(typeof(UpdateChannel), PlayerPrefs.GetString("UpdateChannel", "Release"), true);

            if (force_current_version.Value)
            {
                Logger.LogWarning("Updates are disabled!");
                return;
            }

            Logger.LogInfo("Checking for updates...");

            DateTime mod_creation_time = DateTime.MinValue;
            string time;
            string download_url;

            var mod_file = PLUGIN_DIR + "RavenM.dll";
            if (File.Exists(mod_file))
            {
                mod_creation_time = File.GetCreationTime(mod_file)
            }

            if (update_channel.Value == UpdateChannel.Release)
            {
                var response = MakeRequest("https://api.github.com/repos/iliadsh/RavenM/releases/latest");
                var json = JSON.Parse(new StreamReader(response).ReadToEnd());
                var asset = json["assets"][0];
                time = asset["updated_at"];
                download_url = asset["browser_download_url"];
            }
            else if (update_channel.Value == UpdateChannel.Dev)
            {
                var response = MakeRequest("https://api.github.com/repos/iliadsh/RavenM/actions/artifacts?name=RavenM&per_page=1");
                var json = JSON.Parse(new StreamReader(response).ReadToEnd());
                var artifact = json["artifacts"][0];
                time = artifact["updated_at"];
                download_url = DEV_DOWNLOAD_URL;
            }
            else
            {
                Logger.LogError($"Update channel {update_channel.Value} not found");
                return;
            }

            DateTime upload_time;
            if (DateTime.TryParse(time, out upload_time))
            {
                if ((current_update_channel == update_channel.Value && upload_time > mod_creation_time) || current_update_channel != update_channel.Value)
                {
                    Logger.LogWarning($"Newer version found: {time}. Downloading...");

                    var zip = MakeRequest(download_url);

                    Logger.LogWarning("Downloaded new version. Updating...");

                    var archive = new ZipArchive(zip, ZipArchiveMode.Read);
                    foreach (var s in archive.Entries)
                    {
                        // We can't update the updater, since it's already locked by BepInEx.
                        if (s.Name != "RavenM.Updater.dll")
                        {
                            try
                            {
                                s.ExtractToFile(PLUGIN_DIR + s.Name, true);
                                File.SetCreationTime(PLUGIN_DIR + s.Name, upload_time);
                            }
                            catch (Exception e)
                            {
                                Logger.LogError($"Failed to update file {s.Name}. {e}");
                            }
                        }
                    }

                    PlayerPrefs.SetString("UpdateChannel", update_channel.Value.ToString());
                    PlayerPrefs.Save();

                }
            }

            Logger.LogInfo("All done... have a nice day :)");
        }
    }
}
