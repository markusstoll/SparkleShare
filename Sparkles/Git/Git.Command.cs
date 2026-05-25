//   SparkleShare, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hi@planetpeanut.uk>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU Lesser General Public License as
//   published by the Free Software Foundation, either version 3 of the
//   License, or (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

using Sparkles;

namespace Sparkles.Git {

    public class GitCommand : SSHCommand {

        public static string ExecPath;


        static string git_path;

        public static string GitPath {
            get {
                if (git_path == null)
                    git_path = LocateCommand ("git");

                return git_path;
            }

            set {
                git_path = value;
            }
        }


        public static string GitVersion {
            get {
                if (GitPath == null)
                    GitPath = LocateCommand ("git");

                var git_version = new Command (GitPath, "--version", false);

                if (ExecPath != null)
                    git_version.SetEnvironmentVariable ("GIT_EXEC_PATH", ExecPath);

                string version = git_version.StartAndReadStandardOutput ();
                return version.Replace ("git version ", "");
            }
        }


        public static string GitLFSVersion {
            get {
                if (GitPath == null)
                    GitPath = LocateCommand ("git");

                var git_lfs_version = new Command (GitPath, "lfs version", false);

                if (ExecPath != null)
                    git_lfs_version.SetEnvironmentVariable ("GIT_EXEC_PATH", ExecPath);

                string version = git_lfs_version.StartAndReadStandardOutput ();
                return version.Replace ("git-lfs/", "").Split (' ') [0];
            }
        }


        public GitCommand (string working_dir, string args) : this (working_dir, args, null)
        {
        }


        public GitCommand (string working_dir, string args, SSHAuthenticationInfo auth_info) : base (GitPath, args)
        {
            StartInfo.WorkingDirectory = working_dir;

            string GIT_SSH_COMMAND = SSHCommand.SSHCommandPath;

            if (auth_info != null) {
                GIT_SSH_COMMAND = FormatGitSSHCommand (auth_info);
                DisableSSHAgentEnvironment ();
            }

            if (ExecPath != null)
                SetEnvironmentVariable ("GIT_EXEC_PATH", ExecPath);

            SetEnvironmentVariable ("GIT_SSH_COMMAND", GIT_SSH_COMMAND);
            SetEnvironmentVariable ("GIT_TERMINAL_PROMPT", "0");

            // Don't let Git try to read the config options in PREFIX/etc or ~
            SetEnvironmentVariable ("GIT_CONFIG_NOSYSTEM", "1");
            SetEnvironmentVariable ("PREFIX", "");
            SetEnvironmentVariable ("HOME", "");

            SetEnvironmentVariable ("LANG", "en_US.UTF8");
            SetEnvironmentVariable ("LC_ALL", "en_US.UTF8");
        }


        static Regex progress_regex = new Regex (@"([0-9]+)%", RegexOptions.Compiled);
        static Regex progress_regex_lfs = new Regex (@".*\(([0-9]+) of ([0-9]+) files\).*", RegexOptions.Compiled);
        static Regex progress_regex_lfs_skipped = new Regex (@".*\(([0-9]+) of ([0-9]+) files, ([0-9]+) skipped\).*", RegexOptions.Compiled);
        static Regex speed_regex = new Regex (@"([0-9\.]+) ([KM])iB/s", RegexOptions.Compiled);

        public static ErrorStatus ParseProgress (string line, out double percentage, out double speed, out string information)
        {
            percentage = 0;
            speed = 0;
            information = "";

            Match match;

            if (line.StartsWith ("Git LFS:")) {
                match = progress_regex_lfs_skipped.Match (line);

                int current_file = 0;
                int total_file_count = 0;
                int skipped_file_count = 0;

                if (match.Success) {
                    // "skipped" files are objects that have already been transferred
                    skipped_file_count = int.Parse (match.Groups [3].Value);

                } else {

                    match = progress_regex_lfs.Match (line);

                    if (!match.Success)
                        return ErrorStatus.None;
                }

                current_file = int.Parse (match.Groups [1].Value);

                if (current_file == 0)
                    return ErrorStatus.None;

                total_file_count = int.Parse (match.Groups [2].Value) - skipped_file_count;

                percentage = Math.Round ((double) current_file / total_file_count * 100, 0);
                information = string.Format ("{0} of {1} files", current_file, total_file_count);

                return ErrorStatus.None;
            }

            match = progress_regex.Match (line);

            if (!match.Success || string.IsNullOrWhiteSpace (line)) {
                if (!string.IsNullOrWhiteSpace (line))
                    Logger.LogInfo ("Git", line);

                return FindError (line);
            }

            int number = int.Parse (match.Groups [1].Value);

            // The transfer process consists of two stages: the "Compressing
            // objects" stage which we count as 20% of the total progress, and
            // the "Writing objects" stage which we count as the last 80%
            if (line.Contains ("Compressing objects")) {
                // "Compressing objects" stage
                percentage = (number / 100 * 20);

            } else if (line.Contains ("Writing objects")) {
                percentage = (number / 100 * 80 + 20);
                Match speed_match = speed_regex.Match (line);

                if (speed_match.Success) {
                    speed = double.Parse (speed_match.Groups [1].Value, new CultureInfo ("en-US")) * 1024;

                    if (speed_match.Groups [2].Value.Equals ("M"))
                        speed = speed * 1024;

                    information = speed.ToSize ();
                }
            }

            return ErrorStatus.None;
        }


        static ErrorStatus FindError (string line)
        {
            ErrorStatus error = ErrorStatus.None;

            if (line.Contains ("WARNING: REMOTE HOST IDENTIFICATION HAS CHANGED!") ||
                line.Contains ("WARNING: POSSIBLE DNS SPOOFING DETECTED!")) {

                error = ErrorStatus.HostIdentityChanged;
            }

            if (line.StartsWith ("Permission denied") ||
                       line.StartsWith ("ssh_exchange_identification: Connection closed by remote host") ||
                       line.StartsWith ("The authenticity of host")) {

                error = ErrorStatus.AuthenticationFailed;
            }

            if (line.EndsWith ("does not appear to be a git repository"))
                error = ErrorStatus.NotFound;

            if (line.EndsWith ("expected old/new/ref, got 'shallow"))
                error = ErrorStatus.IncompatibleClientServer;

            if (line.StartsWith ("error: Disk space exceeded") ||
                       line.EndsWith ("No space left on device") ||
                       line.EndsWith ("file write error (Disk quota exceeded)")) {

                error = ErrorStatus.DiskSpaceExceeded;
            }

            return error;
        }


        public static string FormatGitSSHCommand (SSHAuthenticationInfo auth_info)
        {
            return SSHCommandPath + " " +
                "-i " + auth_info.PrivateKeyFilePath.Replace ("\\", "/").Replace (" ", "\\ ") + " " +
                "-o UserKnownHostsFile=" + auth_info.KnownHostsFilePath.Replace ("\\", "/").Replace (" ", "\\ ") + " " +
                "-o IdentitiesOnly=yes" + " " + // Don't fall back to other keys on the system
                "-o IdentityAgent=none" + " " + // Don't use ssh-agent (broken agents can hang)
                "-o PasswordAuthentication=no" + " " + // Don't hang on possible password prompts
                "-o BatchMode=yes" + " " +
                "-F /dev/null"; // Ignore the system's SSH config file
        }


        /// <summary>
        /// Prefix for POSIX shell hooks and Linux LFS filters (uses the <c>env</c> utility).
        /// </summary>
        public static string FormatGitSSHEnvPrefix (SSHAuthenticationInfo auth_info)
        {
            return "env SSH_AUTH_SOCK= SSH_AGENT_PID= GIT_SSH_COMMAND='" +
                FormatGitSSHCommand (auth_info) + "' ";
        }


        /// <summary>
        /// Lines for <c>#!/bin/sh</c> hooks on Windows (no standalone <c>env</c> in PATH).
        /// </summary>
        public static string FormatGitSSHShellScriptPrefix (SSHAuthenticationInfo auth_info)
        {
            return "unset SSH_AUTH_SOCK SSH_AGENT_PID" + Environment.NewLine +
                "export GIT_SSH_COMMAND='" + FormatGitSSHCommand (auth_info) + "'" + Environment.NewLine;
        }


        static string EscapeForShSingleQuoted (string value)
        {
            return value.Replace ("'", "'\\''");
        }


        /// <summary>
        /// Git config filter.lfs.smudge on Windows (bundled Git: run via MSYS <c>sh.exe</c>, not <c>env</c>).
        /// </summary>
        static string FormatWindowsLfsSmudgeCommand (SSHAuthenticationInfo auth_info)
        {
            string bin = Configuration.DefaultConfiguration.BinPath.TrimEnd ('\\', '/');
            string scm_root = Path.GetDirectoryName (bin);
            string sh = Path.GetFullPath (Path.Combine (scm_root, "..", "usr", "bin", "sh.exe"));
            string git_lfs = Path.Combine (bin, "git-lfs.exe");

            if (!File.Exists (git_lfs))
                git_lfs = Path.Combine (bin, "git-lfs");

            string ssh_command = EscapeForShSingleQuoted (FormatGitSSHCommand (auth_info));

            if (!File.Exists (sh)) {
                Logger.LogInfo ("Git",
                    "Bundled sh.exe not found for LFS smudge; SSH env may not apply to git-lfs");
                return "\"" + git_lfs.Replace ("\\", "/") + "\" smudge %f";
            }

            return string.Format (
                "\"{0}\" -c \"unset SSH_AUTH_SOCK SSH_AGENT_PID; export GIT_SSH_COMMAND='{1}'; '{2}' smudge %f\"",
                sh.Replace ("\\", "/"),
                ssh_command,
                git_lfs.Replace ("\\", "/"));
        }


        /// <summary>
        /// True when this repo already has Git LFS filter entries (e.g. from an earlier clone).
        /// </summary>
        public static bool LfsFilterIsConfigured (string working_dir)
        {
            var git_config = new GitCommand (working_dir, "config --get filter.lfs.smudge");
            git_config.StartAndWaitForExit ();
            return git_config.ExitCode == 0;
        }


        /// <summary>
        /// Writes filter.lfs.* in the repo so LFS smudge uses the SparkleShare SSH key (not ssh-agent).
        /// Safe to call on every repo load; keeps .git/config in sync with FormatGitSSHCommand changes.
        /// </summary>
        public static void ConfigureLfsFilter (string working_dir, SSHAuthenticationInfo auth_info)
        {
            string smudge_command;
            string clean_command;

            string git_lfs = Path.Combine (Configuration.DefaultConfiguration.BinPath, "git-lfs")
                .Replace ("\\", "/");

            if (InstallationInfo.OperatingSystem == OS.Windows) {
                smudge_command = FormatWindowsLfsSmudgeCommand (auth_info);
                clean_command = git_lfs + " clean %f";

            } else if (InstallationInfo.OperatingSystem == OS.macOS) {
                smudge_command = FormatGitSSHEnvPrefix (auth_info) + git_lfs + " smudge %f";
                clean_command = git_lfs + " clean %f";

            } else {
                smudge_command = FormatGitSSHEnvPrefix (auth_info) + "git-lfs smudge %f";
                clean_command = "git-lfs clean %f";
            }

            var git_config_required = new GitCommand (working_dir, "config filter.lfs.required true");
            var git_config_clean = new GitCommand (working_dir,
                string.Format ("config filter.lfs.clean '{0}'", clean_command));
            var git_config_smudge = new GitCommand (working_dir,
                string.Format ("config filter.lfs.smudge \"{0}\"", smudge_command));

            git_config_required.StartAndWaitForExit ();
            git_config_clean.StartAndWaitForExit ();
            git_config_smudge.StartAndWaitForExit ();
        }


        void DisableSSHAgentEnvironment ()
        {
            RemoveEnvironmentVariable ("SSH_AUTH_SOCK");
            RemoveEnvironmentVariable ("SSH_AGENT_PID");
        }
    }
}
