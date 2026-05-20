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
using System.Diagnostics;
using System.IO;

namespace Sparkles {

    // Cross-platform single-instance guard. Platforms can replace CheckGuard / ReleaseGuard
    // with native mechanisms (e.g. NSRunningApplication on macOS); otherwise a PID file
    // inside the SparkleShare configuration directory is used.
    public static class SingleInstance {

        // Returns true if another SparkleShare instance is already running. The default
        // implementation atomically claims a PID file and treats a stale PID file
        // (process gone, or owned by an unrelated PID) as "no other instance".
        public static Func<bool> CheckGuard { get; set; } = PidFileCheckAndClaim;

        // Cleans up whatever CheckGuard reserved. Invoked automatically on process exit.
        public static Action ReleaseGuard { get; set; } = PidFileRelease;


        // Returns true if this process is now the sole instance and may proceed.
        // Returns false if another instance is already running.
        public static bool TryAcquire ()
        {
            bool another_running;

            try {
                another_running = CheckGuard ();

            } catch (Exception e) {
                // Never block startup because of a failing guard; log and proceed.
                Logger.LogInfo ("SingleInstance",
                    "Single-instance check failed; assuming no other instance is running", e);
                return true;
            }

            if (another_running)
                return false;

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            return true;
        }


        static void OnProcessExit (object sender, EventArgs e)
        {
            try {
                ReleaseGuard ();
            } catch {
                // Best-effort cleanup; never surface here.
            }
        }


        // Path used by the default PID-file implementation. Exposed so tests / tooling
        // can verify it without duplicating the path logic.
        public static string PidFilePath {
            get { return Path.Combine (Configuration.DefaultConfiguration.DirectoryPath, "sparkleshare.pid"); }
        }


        // Default implementation: atomic O_EXCL-style claim of <config>/sparkleshare.pid,
        // with a stale-file recovery step that verifies the recorded PID belongs to a
        // process whose name actually contains "SparkleShare" (mitigates PID reuse).
        static bool PidFileCheckAndClaim ()
        {
            string path = PidFilePath;
            int own_pid = Environment.ProcessId;

            for (int attempt = 0; attempt < 3; attempt++) {
                try {
                    using (var fs = new FileStream (path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var sw = new StreamWriter (fs)) {
                        sw.Write (own_pid);
                    }

                    Logger.LogInfo ("SingleInstance", "Claimed PID file at " + path);
                    return false;

                } catch (IOException) {
                    int existing_pid = -1;
                    string existing_content = null;

                    try {
                        existing_content = File.ReadAllText (path).Trim ();
                        int.TryParse (existing_content, out existing_pid);
                    } catch {
                        // Corrupt file; treat as stale.
                    }

                    if (existing_pid > 0 && IsSparkleShareProcessAlive (existing_pid)) {
                        Logger.LogInfo ("SingleInstance",
                            "Another SparkleShare instance is running (PID " + existing_pid + ")");
                        return true;
                    }

                    Logger.LogInfo ("SingleInstance",
                        "Stale PID file (" + (existing_content ?? "<empty>") + ") at " + path + "; removing");

                    try {
                        File.Delete (path);
                    } catch (Exception e) {
                        Logger.LogInfo ("SingleInstance", "Failed to remove stale PID file", e);
                        return true;
                    }
                }
            }

            // Three failed attempts to claim the file — be cautious and refuse to start.
            return true;
        }


        static bool IsSparkleShareProcessAlive (int pid)
        {
            try {
                using (var p = Process.GetProcessById (pid)) {
                    return p.ProcessName.IndexOf ("SparkleShare", StringComparison.OrdinalIgnoreCase) >= 0;
                }

            } catch (ArgumentException) {
                return false;

            } catch (InvalidOperationException) {
                return false;
            }
        }


        static void PidFileRelease ()
        {
            try {
                string path = PidFilePath;

                if (!File.Exists (path))
                    return;

                string content = File.ReadAllText (path).Trim ();
                if (int.TryParse (content, out int pid) && pid == Environment.ProcessId)
                    File.Delete (path);

            } catch (Exception e) {
                Logger.LogInfo ("SingleInstance", "Failed to release PID file", e);
            }
        }
    }
}
