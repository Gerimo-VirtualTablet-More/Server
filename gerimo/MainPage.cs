using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using NullSoftware.ToolKit;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Windows.ApplicationModel;
using Windows.Storage;

namespace gerimo
{
    public sealed partial class MainPage : Page
    {
        Launcher launcher = new Launcher();
        bool isRunning = false;
        private static readonly Regex DigitsOnly = new(@"^\d{0,5}$");
        private string lastValid = "";

        private const string SettingsKey = "AutostartEnabled";
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "Gerimo - Server";
        private const string SavedPort = "SavedPort";
        StartupTask startupTask;

        public MainPage()
        {
            this.InitializeComponent();

            MachineNameText.Text = Environment.MachineName;

            var local = ApplicationData.Current.LocalSettings;
            // Lade gespeicherten Port, falls vorhanden, ansonsten Standardwert
            if (local.Values[SavedPort] is int port)
            {
                PortTextBox.Text = port.ToString();
            }
            else
            {
                PortTextBox.Text = HelperClass.DEFAULT_PORT.ToString();
            }

            RunButton.Click += Button1_Click;
            PortTextBox.TextChanged += PortTextBox_TextChanged;

            var enabled = (local.Values[SettingsKey] as bool?) ?? false;
            AutostartCheckbox.IsChecked = enabled;

            AutostartCheckbox.Checked += (_, __) => SetAutostart(true);
            AutostartCheckbox.Unchecked += (_, __) => SetAutostart(false);

            AutostartCheckbox.Checked += (_, __) => SetAutostartMinimize(true);
            AutostartCheckbox.Unchecked += (_, __) => SetAutostartMinimize(false);

            if (AutostartCheckbox.IsChecked==true)
            {
                start();
            }
        }

        private void Button1_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            start();
        }

        void start()
        {
            isRunning = !isRunning;

            if (isRunning)
            {
                if (PortTextBox.Text.Length > 0)
                {
                    int tempPort = int.Parse(PortTextBox.Text);

                    // Port direkt nach Start speichern
                    var local = ApplicationData.Current.LocalSettings;
                    local.Values[SavedPort] = tempPort;

                    launcher.port = tempPort == HelperClass.DEFAULT_PORT ? HelperClass.DEFAULT_PORT : tempPort;
                }
                RunButton.Background = new SolidColorBrush(Microsoft.UI.Colors.Red);
                RunButton.Content = "Stop Service";
                _ = launcher.main();
            }
            else
            {
                launcher.stopService();
                RunButton.Content = "Start Service";
                RunButton.Background = new SolidColorBrush(Microsoft.UI.Colors.Green);
            }
        }

        private void PortTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = (TextBox)sender;
            if (DigitsOnly.IsMatch(tb.Text))
            {
                lastValid = tb.Text;
                tb.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.White);
            }
            else
            {
                tb.Text = lastValid;
                tb.SelectionStart = tb.Text.Length;
                tb.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
        }

        private async void SetAutostart(bool enable)
        {
            ApplicationData.Current.LocalSettings.Values[SettingsKey] = enable;
            startupTask = await StartupTask.GetAsync("GerimoAutoTask");
            if (enable)
            {
                await startupTask.RequestEnableAsync();
            
            }
            else
            {
                startupTask.Disable();
            }
        }

        private void SetAutostartMinimize(bool minimize)
        {
            ApplicationData.Current.LocalSettings.Values["AutostartMinimize"] = minimize;
        }
    }
}
