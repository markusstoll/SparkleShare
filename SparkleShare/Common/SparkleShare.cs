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

using Sparkles;

namespace SparkleShare {

    public class SparkleShare {

        public static Controller Controller;
        public static UserInterface UI;


        #if !__MonoCS__
        [STAThread]
        #endif
        public static void Main (string [] args)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            InstallationInfo.SetHostAssembly (typeof (SparkleShare).Assembly);

            // The platform-specific Controller constructor swaps in a native guard
            // (e.g. NSRunningApplication on macOS). Until that runs, SingleInstance
            // uses its default PID-file implementation.
            Controller = new Controller (Configuration.DefaultConfiguration);

            if (!SingleInstance.TryAcquire ()) {
                Console.Error.WriteLine ("SparkleShare is already running; exiting silently.");
                Environment.Exit (0);
            }

            Controller.Initialize ();

            UI = new UserInterface ();
            UI.Run (args);

            #if !__MonoCS__
            // Suppress assertion messages in debug mode
            GC.Collect (GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers ();
            #endif
        }


        static void OnUnhandledException (object sender, UnhandledExceptionEventArgs exception_args)
        {
            var exception = (Exception) exception_args.ExceptionObject;
            Logger.WriteCrashReport (exception);
        }
    }
}
