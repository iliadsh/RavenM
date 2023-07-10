using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using System.IO;
using System.IO.Compression;
using System.Net;
using SimpleJSON;

namespace RavenM.Installer
{
    public partial class RavenM : Form
    {
        static readonly string BEPINEX_DOWNLOAD_URL = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.21/BepInEx_x64_5.4.21.0.zip";
        static readonly string RELEASE_URL = "https://api.github.com/repos/iliadsh/RavenM/releases/latest";
        static readonly string DEV_DOWNLOAD_URL = "https://nightly.link/iliadsh/RavenM/workflows/main/master";

        private string releaseAsset = string.Empty;

        public RavenM()
        {
            InitializeComponent();
        }

        private void Installer_Load(object sender, EventArgs e)
        {
            RavenMFolder.Text = TryFindRavenfield();

            GetRelease();
        }

        void GetRelease()
        {
            var response = MakeRequest(RELEASE_URL);
            var json = JSON.Parse(new StreamReader(response).ReadToEnd());
            releaseAsset = json["assets"][0]["browser_download_url"];
            var name = json["name"].ToString().Trim('"');
            Release.Text += $" ({name})";
        }

        string TryFindRavenfield()
        {
            var steamInstallPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string 
                                    ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null) as string;

            if (steamInstallPath == null)
                return string.Empty;

            string[] foldersvdf;
            try
            {
                foldersvdf = File.ReadAllLines($"{steamInstallPath}\\steamapps\\libraryfolders.vdf");
            } catch (Exception e)
            {
                Console.WriteLine($"read file: {e}");
                return string.Empty;
            }

            string folderPath = FindApp(foldersvdf);
            if (folderPath == null)
                return string.Empty;

            string ravenfield = $"{folderPath}\\steamapps\\common\\Ravenfield";
            if (!Directory.Exists(ravenfield))
                return string.Empty;

            return ravenfield;
        }

        string FindApp(string[] foldersvdf)
        {
            string currentPath = null;
            bool seenApps = false;
            foreach(var line in foldersvdf)
            {
                var lineT = line.Trim();
                if (lineT.StartsWith("\"path\""))
                {
                    currentPath = lineT.Split('\t').Last().Trim('"').Replace(@"\\", "\\");
                    seenApps = false;
                }
                if (lineT.StartsWith("\"apps\""))
                {
                    seenApps = true;
                }
                if (line.Contains("\"636480\"") && seenApps)
                {
                    return currentPath;
                }
            }
            return null;
        }

        private void FolderPicker_Click(object sender, EventArgs e)
        {
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                RavenMFolder.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private async void Install_Click(object sender, EventArgs e)
        {
            progressBar1.Value = 0;
            DoneText.Text = string.Empty;
            if (!Directory.Exists(RavenMFolder.Text))
                return;

            DoneText.Text = "Installing BepInEx...";
            await Task.Run(() => InstallBepInEx());
            progressBar1.Value = 30;

            DoneText.Text = "Installing RavenM...";
            var downloadUrl = Release.Checked ? releaseAsset : $"{DEV_DOWNLOAD_URL}/RavenM.zip";
            await Task.Run(() => InstallRavenM(downloadUrl));
            if (Dev.Checked)
                await Task.Run(() => InstallRavenM($"{DEV_DOWNLOAD_URL}/Updater.zip"));
            progressBar1.Value = 80;

            DoneText.Text = "Writing Config...";
            string configValue = Release.Checked ? "Release" : "Dev";
            await Task.Run(() => WriteConfig(configValue));
            progressBar1.Value = 100;

            DoneText.Text = "Done!";
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

        void InstallBepInEx()
        {
            var zip = MakeRequest(BEPINEX_DOWNLOAD_URL);
            var archive = new ZipArchive(zip, ZipArchiveMode.Read);
            ExtractZip(archive, RavenMFolder.Text);

            string pluginsFolder = $"{RavenMFolder.Text}\\BepInEx\\plugins";
            if (!Directory.Exists(pluginsFolder))
                Directory.CreateDirectory(pluginsFolder);

            string configFolder = $"{RavenMFolder.Text}\\BepInEx\\config";
            if (!Directory.Exists(configFolder))
                Directory.CreateDirectory(configFolder);
        }

        void ExtractZip(ZipArchive archive, string path)
        {
            foreach (var entry in archive.Entries)
            {
                string fileName = Path.Combine(path, entry.FullName);
                string directory = Path.GetDirectoryName(fileName);

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (entry.Name != string.Empty)
                    entry.ExtractToFile(fileName, true);
            }
        }

        void InstallRavenM(string downloadUrl)
        {
            var zip = MakeRequest(downloadUrl);
            var archive = new ZipArchive(zip, ZipArchiveMode.Read);
            ExtractZip(archive, $"{RavenMFolder.Text}\\BepInEx\\plugins");
        }

        void WriteConfig(string configValue)
        {
            var config = 
$@"## Settings file was created by plugin RavenM.Updater v1.0.0
## Plugin GUID: RavenM.Updater

[General]

## If you're using a potentially outdated version and know what you're doing, this will disabled the updater.
# Setting type: Boolean
# Default value: false
Force Disable Updates = false

# Setting type: UpdateChannel
# Default value: Release
# Acceptable values: Release, Dev
Update Channel = {configValue}
";

            File.WriteAllText($"{RavenMFolder.Text}\\BepInEx\\config\\RavenM.Updater.cfg", config);
        }
    }
}
