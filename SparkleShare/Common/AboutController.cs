//   SparkleShare, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hi@planetpeanut.uk>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

using Sparkles;

namespace SparkleShare {

    public class AboutController {

        const string LatestReleaseApi =
            "https://api.github.com/repos/markusstoll/SparkleShare/releases/latest";

        public event Action ShowWindowEvent = delegate { };
        public event Action HideWindowEvent = delegate { };

        /// <summary>Status text; non-null download_url makes the line a download link.</summary>
        public event Action<string, string> UpdateStatusEvent = delegate { };

        public readonly string WebsiteLinkAddress          = "https://www.sparkleshare.org/";
        public readonly string OriginalProjectLinkAddress  = "https://github.com/hbons/SparkleShare";
        public readonly string CreditsLinkAddress          = "https://github.com/hbons/SparkleShare/blob/master/.github/AUTHORS.md";
        public readonly string ReleasesLinkAddress         = "https://github.com/markusstoll/SparkleShare/releases";
        public readonly string ReportProblemLinkAddress    = "https://github.com/markusstoll/SparkleShare/issues";
        public readonly string DebugLogLinkAddress         = "file://" + SparkleShare.Controller.Config.LogFilePath;

        public const string UpdateDownloadSuffix = " (download)";

        /// <summary>Shown in the About dialog; keep Hylke Bons as the credited creator.</summary>
        public static string CreditsParagraph {
            get {
                return "Created by Hylke Bons.\n\n" +
                    "Maintained by Markus Stoll as a community continuation of the original project.\n\n" +
                    "Open Source — use, modify, and redistribute under the GNU GPLv3.";
            }
        }

        public string RunningVersion;


        public AboutController ()
        {
            RunningVersion = InstallationInfo.Version;

            SparkleShare.Controller.ShowAboutWindowEvent += delegate {
                ShowWindowEvent ();
                new Thread (CheckForNewVersion).Start ();
            };
        }


        public void WindowClosed ()
        {
            HideWindowEvent ();
        }


        void CheckForNewVersion ()
        {
            UpdateStatusEvent ("Checking for updates…", null);
            Thread.Sleep (500);

            try {
                using (var client = CreateGitHubClient ())
                using (HttpResponseMessage response = client.GetAsync (LatestReleaseApi).GetAwaiter ().GetResult ()) {

                    if (response.StatusCode == HttpStatusCode.NotFound) {
                        UpdateStatusEvent ("No published release found", null);
                        return;
                    }

                    response.EnsureSuccessStatusCode ();
                    string json = response.Content.ReadAsStringAsync ().GetAwaiter ().GetResult ();

                    if (!TryParseLatestRelease (json, out string tag_name, out string html_url)) {
                        UpdateStatusEvent ("Couldn't check for updates", null);
                        return;
                    }

                    string display_version = NormalizeTag (tag_name);

                    if (IsNewerRelease (tag_name, RunningVersion)) {
                        UpdateStatusEvent (
                            "Update available: " + display_version + UpdateDownloadSuffix,
                            html_url ?? ReleasesLinkAddress);
                    } else {
                        UpdateStatusEvent ("✓ You are running the latest version", null);
                    }
                }

            } catch (Exception e) {
                Logger.LogInfo ("UI", "Failed to check GitHub release at " + LatestReleaseApi, e);
                UpdateStatusEvent ("Couldn't check for updates", null);
            }
        }


        static HttpClient CreateGitHubClient ()
        {
            var client = new HttpClient ();
            client.DefaultRequestHeaders.UserAgent.ParseAdd ("SparkleShare");
            client.DefaultRequestHeaders.Accept.ParseAdd ("application/vnd.github+json");
            return client;
        }


        static bool TryParseLatestRelease (string json, out string tag_name, out string html_url)
        {
            tag_name = null;
            html_url = null;

            using (JsonDocument document = JsonDocument.Parse (json)) {
                JsonElement root = document.RootElement;

                if (root.TryGetProperty ("tag_name", out JsonElement tag))
                    tag_name = tag.GetString ();

                if (root.TryGetProperty ("html_url", out JsonElement url))
                    html_url = url.GetString ();
            }

            return !string.IsNullOrWhiteSpace (tag_name);
        }


        static string NormalizeTag (string tag)
        {
            if (string.IsNullOrWhiteSpace (tag))
                return tag;

            tag = tag.Trim ();

            if (tag.StartsWith ("v", StringComparison.OrdinalIgnoreCase))
                tag = tag.Substring (1);

            return tag;
        }


        static bool IsNewerRelease (string latest_tag, string running_version)
        {
            string latest  = NormalizeTag (latest_tag);
            string running = NormalizeTag (running_version);

            if (!TryParseReleaseVersion (latest, out Version latest_version)
                || !TryParseReleaseVersion (running, out Version current_version))
                return false;

            int compare = latest_version.CompareTo (current_version);

            if (compare > 0)
                return true;

            if (compare < 0)
                return false;

            return string.Compare (latest, running, StringComparison.OrdinalIgnoreCase) > 0;
        }


        /// <summary>
        /// Parses a dotted version for update checks, ignoring SemVer pre-release (after '-') and build metadata (after '+').
        /// </summary>
        static bool TryParseReleaseVersion (string text, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace (text))
                return false;
            text = text.Trim ();
            int cut = text.IndexOf ('-');
            if (cut >= 0)
                text = text.Substring (0, cut).Trim ();
            int plus = text.IndexOf ('+');
            if (plus >= 0)
                text = text.Substring (0, plus).Trim ();
            return Version.TryParse (text, out version);
        }
    }
}
