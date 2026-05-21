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
using Sparkles;
using CoreGraphics;
using Foundation;
using ObjCRuntime;

namespace SparkleShare {

    public class About : NSWindow {

        public AboutController Controller = new AboutController ();

        private NSTextField version_text_field, credits_text_field;
        private SparkleStatusLink updates_status;
        private SparkleLink website_link, credits_link, report_problem_link, debug_log_link;
        private NSImage about_image;
        private NSImageView about_image_view;
        private NSButton hidden_close_button;


        public About (NativeHandle handle) : base (handle) { }


        public About () : base ()
        {
            SetFrame (new CGRect (0, 0, 640, 281), display: true);
            Center ();

            Delegate    = new SparkleAboutDelegate ();
            StyleMask   = (NSWindowStyle.Closable | NSWindowStyle.Titled);
            Title       = "About SparkleShare";
            MaxSize     = new CGSize (640, 281);
            MinSize     = new CGSize (640, 281);
            HasShadow   = true;
            IsOpaque    = false;
            Level       = NSWindowLevel.Floating;

            this.hidden_close_button = new NSButton () {
                Frame                     = new CGRect (0, 0, 0, 0),
                KeyEquivalentModifierMask = NSEventModifierMask.CommandKeyMask,
                KeyEquivalent             = "w"
            };

            CreateAbout ();


            this.hidden_close_button.Activated += delegate { Controller.WindowClosed (); };

            Controller.HideWindowEvent += delegate {
                SparkleShare.Controller.Invoke (() => PerformClose (this));
            };

            Controller.ShowWindowEvent += delegate {
                SparkleShare.Controller.Invoke (() => {
                    RefreshVersionLabel ();
                    OrderFrontRegardless ();
                });
            };

            Controller.UpdateStatusEvent += delegate (string text, string download_url) {
                SparkleShare.Controller.Invoke (() => {
                    this.updates_status.SetStatus (text, download_url);
                });
            };


            ContentView.AddSubview (this.hidden_close_button);
        }


        /// <summary>Version shown in About; use the app bundle plist (matches Finder/Get Info).</summary>
        static string BundleDisplayVersion ()
        {
            var version = NSBundle.MainBundle.InfoDictionary?.ObjectForKey (
                new NSString ("CFBundleShortVersionString"));

            if (version != null) {
                string text = version.ToString ();

                if (!string.IsNullOrWhiteSpace (text))
                    return text.Trim ();
            }

            return InstallationInfo.Version;
        }


        void RefreshVersionLabel ()
        {
            Controller.RefreshRunningVersion ();
            string display = BundleDisplayVersion ();
            Controller.RunningVersion = display;
            this.version_text_field.StringValue = "version " + display;
        }


        private void CreateAbout ()
        {
            this.about_image = NSImage.ImageNamed ("about");
            this.about_image.Size = new CGSize (720, 260);

            this.about_image_view = new NSImageView () {
                Image = this.about_image,
                Frame = new CGRect (0, 0, 720, 260)
            };

            this.version_text_field = new SparkleLabel ("version " + BundleDisplayVersion (), NSTextAlignment.Left) {
                DrawsBackground = false,
                Frame           = new CGRect (295, 140, 318, 22),
                TextColor       = NSColor.White
            };

            this.credits_text_field = new SparkleLabel (
                AboutController.CreditsParagraph, NSTextAlignment.Left) {

                DrawsBackground = false,
                Frame           = new CGRect (295, 28, 318, 72),
                TextColor       = NSColor.White
            };

            this.updates_status = new SparkleStatusLink () {
                Frame = new CGRect (295, 115, 318, 22)
            };
            this.updates_status.SetStatus ("Checking for updates…", null);

            const float FooterLinksY = 10;

            this.website_link       = new SparkleLink ("Website", Controller.WebsiteLinkAddress);
            this.website_link.Frame = new CGRect (new CGPoint (295, FooterLinksY), this.website_link.Frame.Size);

            this.credits_link       = new SparkleLink ("Credits", Controller.CreditsLinkAddress);
            this.credits_link.Frame = new CGRect (
                new CGPoint (this.website_link.Frame.X + this.website_link.Frame.Width + 10, FooterLinksY),
                this.credits_link.Frame.Size);

            this.report_problem_link       = new SparkleLink ("Report a problem", Controller.ReportProblemLinkAddress);
            this.report_problem_link.Frame = new CGRect (
                new CGPoint (this.credits_link.Frame.X + this.credits_link.Frame.Width + 10, FooterLinksY),
                this.report_problem_link.Frame.Size);

            this.debug_log_link       = new SparkleLink ("Debug log", Controller.DebugLogLinkAddress);
            this.debug_log_link.Frame = new CGRect (
                new CGPoint (this.report_problem_link.Frame.X + this.report_problem_link.Frame.Width + 10, FooterLinksY),
                this.debug_log_link.Frame.Size);

            ContentView.AddSubview (this.about_image_view);
            ContentView.AddSubview (this.credits_text_field);
            ContentView.AddSubview (this.updates_status);
            ContentView.AddSubview (this.version_text_field);
            ContentView.AddSubview (this.website_link);
            ContentView.AddSubview (this.credits_link);
            ContentView.AddSubview (this.report_problem_link);
            ContentView.AddSubview (this.debug_log_link);
        }


        public override void OrderFrontRegardless ()
        {
            AppKitCompat.ActivateApp ();
            MakeKeyAndOrderFront (this);
            base.OrderFrontRegardless ();
        }


        public override void PerformClose (NSObject sender)
        {
            base.OrderOut (this);
            return;
        }


        private class SparkleAboutDelegate : NSWindowDelegate {

            public override bool WindowShouldClose (NSObject sender)
            {
                (sender as About).Controller.WindowClosed ();
                return false;
            }
        }


        /// <summary>Update line: plain label, or clickable when a download URL is set.</summary>
        private class SparkleStatusLink : NSTextField {

            NSUrl url;


            public SparkleStatusLink () : base ()
            {
                Font = NSFont.SystemFontOfSize (11);
                Bordered        = false;
                DrawsBackground = false;
                Editable        = false;
                Selectable      = false;
            }


            public void SetStatus (string text, string download_url)
            {
                StringValue = text;

                if (string.IsNullOrEmpty (download_url)) {
                    url       = null;
                    TextColor = NSColor.FromCalibratedRgba (1.0f, 1.0f, 1.0f, 0.5f);
                } else {
                    url       = new NSUrl (download_url);
                    TextColor = NSColor.FromCalibratedRgba (0.7f, 0.85f, 1.0f, 1.0f);
                }
            }


            public override void MouseUp (NSEvent e)
            {
                if (url != null)
                    SparkleShare.Controller.OpenWebsite (url.ToString ());
            }


            public override void ResetCursorRects ()
            {
                if (url != null)
                    AddCursorRect (Bounds, NSCursor.PointingHandCursor);
            }
        }


        private class SparkleLink : NSTextField {

            NSUrl url;


            public SparkleLink (string text, string address) : base ()
            {
                StringValue = text;
                this.url = new NSUrl (address);

                Font = NSFont.SystemFontOfSize (11);

                TextColor = NSColor.FromCalibratedRgba (1.0f, 1.0f, 1.0f, 0.5f);
                BackgroundColor = NSColor.White;

                AllowsEditingTextAttributes = true;
                Bordered        = false;
                DrawsBackground = false;
                Editable        = false;
                Selectable      = false;

                SizeToFit ();
            }


            public override void MouseUp (NSEvent e)
            {
                SparkleShare.Controller.OpenWebsite (this.url.ToString ());
            }


            public override void ResetCursorRects ()
            {
                AddCursorRect (Bounds, NSCursor.PointingHandCursor);
            }
        }
    }
}
