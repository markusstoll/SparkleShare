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

using AppKit;

namespace SparkleShare {

    // Helpers that hide AppKit API differences between supported macOS versions.
    internal static class AppKitCompat {

        // NSApplication.Activate() exists since macOS 14 and replaces
        // ActivateIgnoringOtherApps(bool). We still need to support macOS 12 and 13.
        public static void ActivateApp ()
        {
            if (OperatingSystem.IsMacOSVersionAtLeast (14)) {
                NSApplication.SharedApplication.Activate ();
                return;
            }

#pragma warning disable CA1422
            NSApplication.SharedApplication.ActivateIgnoringOtherApps (true);
#pragma warning restore CA1422
        }
    }
}
