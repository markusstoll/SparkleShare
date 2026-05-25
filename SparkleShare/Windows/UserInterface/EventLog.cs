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
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;

using Sparkles;

namespace SparkleShare {

    public class EventLog : Window {

        const string EventLogVirtualHost = "sparkleshare.local";

        public EventLogController Controller = new EventLogController ();

        Label label_Size;
        Label label_History;

        WebView2 webView;
        Spinner spinner;
        ComboBox combobox;
        Grid grid_Base;

        bool webViewReady;
        string pendingHtml;


        public EventLog ()
        {
            Title      = "SparkleShare Changes";
            Width      = 720;
            Height     = 600;
            ResizeMode = ResizeMode.CanResize;

            CreateEventLog ();

            Background = new SolidColorBrush (Color.FromRgb (240, 240, 240));
            AllowsTransparency = false;
            Icon = UserInterfaceHelpers.GetImageSource ("sparkleshare-app", "ico");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            WriteOutImages ();

            label_Size.Content    = "Size: " + Controller.Size;
            label_History.Content = "History: " + Controller.HistorySize;

            Loaded += async (sender, args) => await InitializeWebViewAsync ();

            Closing += OnClosing;

            Controller.ShowWindowEvent += delegate {
                Dispatcher.BeginInvoke ((Action) (() => {
                    Show ();
                    Activate ();
                    BringIntoView ();
                }));
            };

            Controller.HideWindowEvent += delegate {
                Dispatcher.BeginInvoke ((Action) (() => {
                    Hide ();
                    spinner.Visibility  = Visibility.Visible;
                    webView.Visibility  = Visibility.Collapsed;
                }));
            };

            Controller.UpdateSizeInfoEvent += delegate (string size, string history_size) {
                Dispatcher.BeginInvoke ((Action) (() => {
                    label_Size.Content    = "Size: " + size;
                    label_History.Content = "History: " + history_size;
                }));
            };

            Controller.UpdateChooserEvent += delegate (string [] folders) {
                Dispatcher.BeginInvoke ((Action) (() => UpdateChooser (folders)));
            };

            Controller.UpdateChooserEnablementEvent += delegate (bool enabled) {
                Dispatcher.BeginInvoke ((Action) (() => combobox.IsEnabled = enabled));
            };

            Controller.UpdateContentEvent += delegate (string html) {
                Dispatcher.BeginInvoke ((Action) (() => {
                    UpdateContent (html);
                    spinner.Visibility = Visibility.Collapsed;
                    webView.Visibility = Visibility.Visible;
                }));
            };

            Controller.ContentLoadingEvent += () => Dispatcher.BeginInvoke ((Action) (() => {
                spinner.Visibility = Visibility.Visible;
                spinner.Start ();
                webView.Visibility = Visibility.Collapsed;
            }));

            Controller.ShowSaveDialogEvent += delegate (string file_name, string target_folder_path) {
                Dispatcher.BeginInvoke ((Action) (() => {
                    var dialog = new SaveFileDialog () {
                        FileName         = file_name,
                        InitialDirectory = target_folder_path,
                        Title            = "Restore from History",
                        DefaultExt       = "." + Path.GetExtension (file_name),
                        Filter           = "All Files|*.*"
                    };

                    bool? result = dialog.ShowDialog (this);

                    if (result == true)
                        Controller.SaveDialogCompleted (dialog.FileName);
                    else
                        Controller.SaveDialogCancelled ();
                }));
            };
        }


        async Task InitializeWebViewAsync ()
        {
            try {
                await webView.EnsureCoreWebView2Async ();

                CoreWebView2 core = webView.CoreWebView2;
                string tmp_path   = Sparkles.Configuration.DefaultConfiguration.TmpPath;

                core.SetVirtualHostNameToFolderMapping (
                    EventLogVirtualHost,
                    tmp_path,
                    CoreWebView2HostResourceAccessKind.Allow);

                core.Settings.AreDefaultScriptDialogsEnabled = false;
                core.Settings.IsStatusBarEnabled             = false;
                core.Settings.AreDevToolsEnabled             = false;

                core.WebMessageReceived += Core_WebMessageReceived;
                core.NavigationStarting += Core_NavigationStarting;

                webViewReady = true;

                if (!string.IsNullOrEmpty (pendingHtml)) {
                    string html = pendingHtml;
                    pendingHtml = null;
                    UpdateContent (html);
                }

            } catch (Exception e) {
                Logger.LogInfo ("UI",
                    "WebView2 runtime is required for the event log. Install the WebView2 Runtime from Microsoft.",
                    e);
            }
        }


        void Core_WebMessageReceived (object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try {
                string href = e.TryGetWebMessageAsString ();

                if (!string.IsNullOrEmpty (href))
                    Controller.LinkClicked (href);

            } catch (Exception ex) {
                Logger.LogInfo ("UI", "Failed to handle WebView2 message", ex);
            }
        }


        void Core_NavigationStarting (object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (!e.IsUserInitiated)
                return;

            string uri = e.Uri ?? "";

            if (uri.StartsWith ("https://" + EventLogVirtualHost, StringComparison.OrdinalIgnoreCase))
                return;

            e.Cancel = true;
            Controller.LinkClicked (uri);
        }


        void CreateEventLog ()
        {
            grid_Base = new Grid { Background = Brushes.White };
            grid_Base.RowDefinitions.Add (new RowDefinition { Height = GridLength.Auto });
            grid_Base.RowDefinitions.Add (new RowDefinition { Height = new GridLength (1, GridUnitType.Star) });

            label_Size = new Label {
                Content              = "Size: ?",
                Height               = 28,
                HorizontalAlignment  = HorizontalAlignment.Left,
                Margin               = new Thickness (20, 0, 0, 0),
                FontWeight           = FontWeights.Bold
            };

            label_History = new Label {
                Content              = "History: ?",
                Height               = 28,
                HorizontalAlignment  = HorizontalAlignment.Left,
                Margin               = new Thickness (100, 0, 0, 0),
                FontWeight           = FontWeights.Bold
            };

            combobox = new ComboBox {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness (0, 0, 6, 0),
                MinWidth            = 120
            };

            spinner = new Spinner { Name = "spinner" };
            webView = new WebView2 { Name = "webView" };

            var border = new Border {
                VerticalAlignment = VerticalAlignment.Top,
                Height            = 35,
                Background        = new SolidColorBrush (Color.FromArgb (255, 240, 240, 240)),
                BorderBrush       = new SolidColorBrush (Color.FromArgb (255, 223, 223, 223)),
                BorderThickness   = new Thickness (0, 0, 0, 1)
            };

            var borderGrid = new Grid ();
            borderGrid.Children.Add (label_Size);
            borderGrid.Children.Add (label_History);
            borderGrid.Children.Add (combobox);
            border.Child = borderGrid;

            var browserGrid = new Grid { Margin = new Thickness (0, 35, 0, 0) };

            browserGrid.RowDefinitions.Add (new RowDefinition { Height = new GridLength (1, GridUnitType.Star) });
            browserGrid.RowDefinitions.Add (new RowDefinition { Height = GridLength.Auto });
            browserGrid.ColumnDefinitions.Add (new ColumnDefinition { Width = new GridLength (1, GridUnitType.Star) });
            browserGrid.ColumnDefinitions.Add (new ColumnDefinition { Width = GridLength.Auto });

            browserGrid.Children.Add (spinner);
            browserGrid.Children.Add (webView);

            var sizingControlHeight = new System.Windows.Shapes.Rectangle {
                Name       = "sizingControlHeight",
                Visibility = Visibility.Hidden
            };

            Grid.SetColumn (sizingControlHeight, 1);

            var sizingControlWidth = new System.Windows.Shapes.Rectangle {
                Name       = "sizingControlWidth",
                Visibility = Visibility.Hidden
            };

            Grid.SetColumn (sizingControlWidth, 0);
            Grid.SetRow (sizingControlWidth, 0);

            browserGrid.Children.Add (sizingControlHeight);
            browserGrid.Children.Add (sizingControlWidth);

            webView.SetBinding (HeightProperty,
                new Binding ("ActualHeight") { ElementName = sizingControlHeight.Name });
            webView.SetBinding (WidthProperty,
                new Binding ("ActualWidth") { ElementName = sizingControlWidth.Name });

            Grid.SetRow (border, 0);
            Grid.SetRow (browserGrid, 1);
            grid_Base.Children.Add (border);
            grid_Base.Children.Add (browserGrid);
            Content = grid_Base;
        }


        void OnClosing (object sender, CancelEventArgs cancel_event_args)
        {
            Controller.WindowClosed ();
            cancel_event_args.Cancel = true;
        }


        void UpdateContent (string html)
        {
            if (!webViewReady || webView.CoreWebView2 == null) {
                pendingHtml = html;
                return;
            }

            string tmp_path     = Sparkles.Configuration.DefaultConfiguration.TmpPath;
            string asset_prefix = "https://" + EventLogVirtualHost;

            html = html.Replace ("<a href=", "<a class='windows' href=");
            html = html.Replace ("<!-- $body-font-family -->", "Segoe UI");
            html = html.Replace ("<!-- $day-entry-header-font-size -->", "13px");
            html = html.Replace ("<!-- $body-font-size -->", "12px");
            html = html.Replace ("<!-- $secondary-font-color -->", "#bbb");
            html = html.Replace ("<!-- $small-color -->", "#ddd");
            html = html.Replace ("<!-- $small-font-size -->", "90%");
            html = html.Replace ("<!-- $day-entry-header-background-color -->", "#f5f5f5");
            html = html.Replace ("<!-- $a-color -->", "#0085cf");
            html = html.Replace ("<!-- $a-hover-color -->", "#009ff8");
            html = html.Replace ("<!-- $pixmaps-path -->", asset_prefix + "/Images");
            html = html.Replace ("<!-- $document-added-background-image -->", asset_prefix + "/Images/document-added-12.png");
            html = html.Replace ("<!-- $document-edited-background-image -->", asset_prefix + "/Images/document-edited-12.png");
            html = html.Replace ("<!-- $document-deleted-background-image -->", asset_prefix + "/Images/document-deleted-12.png");
            html = html.Replace ("<!-- $document-moved-background-image -->", asset_prefix + "/Images/document-moved-12.png");

            string html_path = Path.Combine (tmp_path, "event-log-view.html");
            File.WriteAllText (html_path, html, new UTF8Encoding (encoderShouldEmitUTF8Identifier: true));

            spinner.Stop ();
            webView.CoreWebView2.Navigate (asset_prefix + "/event-log-view.html");
        }


        public void UpdateChooser (string [] folders)
        {
            if (folders == null)
                folders = Controller.Folders;

            combobox.Items.Clear ();
            combobox.Items.Add (new ComboBoxItem () { Content = "Summary" });
            combobox.Items.Add (new Separator ());
            combobox.SelectedItem = combobox.Items [0];

            int row = 2;

            foreach (string folder in folders) {
                combobox.Items.Add (new ComboBoxItem () { Content = folder });

                if (folder.Equals (Controller.SelectedFolder))
                    combobox.SelectedItem = combobox.Items [row];

                row++;
            }

            combobox.SelectionChanged += delegate {
                Dispatcher.BeginInvoke ((Action) delegate {
                    int index = combobox.SelectedIndex;

                    if (index == 0)
                        Controller.SelectedFolder = null;
                    else
                        Controller.SelectedFolder = (string) ((ComboBoxItem) combobox.Items [index]).Content;
                });
            };
        }


        void WriteOutImages ()
        {
            string tmp_path     = Sparkles.Configuration.DefaultConfiguration.TmpPath;
            string pixmaps_path = Path.Combine (tmp_path, "Images");

            if (!Directory.Exists (pixmaps_path)) {
                Directory.CreateDirectory (pixmaps_path);
                File.SetAttributes (tmp_path, File.GetAttributes (tmp_path) | FileAttributes.Hidden);
            }

            BitmapSource image = UserInterfaceHelpers.GetImageSource ("user-icon-default");
            string file_path   = Path.Combine (pixmaps_path, "user-icon-default.png");

            using (var stream = new FileStream (file_path, FileMode.Create)) {
                var encoder = new PngBitmapEncoder ();
                encoder.Frames.Add (BitmapFrame.Create (image));
                encoder.Save (stream);
            }

            string [] actions = { "added", "deleted", "edited", "moved" };

            foreach (string action in actions) {
                image     = UserInterfaceHelpers.GetImageSource ("document-" + action + "-12");
                file_path = Path.Combine (pixmaps_path, "document-" + action + "-12.png");

                using (var stream = new FileStream (file_path, FileMode.Create)) {
                    var encoder = new PngBitmapEncoder ();
                    encoder.Frames.Add (BitmapFrame.Create (image));
                    encoder.Save (stream);
                }
            }
        }
    }
}
