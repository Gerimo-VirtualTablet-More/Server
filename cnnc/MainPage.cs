using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Windows.ApplicationModel;
using Windows.Storage;

namespace cnnc
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
        StartupTask startupTask;
        public MainPage()
        {
            this.InitializeComponent();



            MachineNameText.Text = Environment.MachineName;
            PortTextBox.Text = HelperClass.DEFAULT_PORT.ToString();

            RunButton.Click += Button1_Click;
            PortTextBox.TextChanged += PortTextBox_TextChanged;

            // Checkbox laden
            var local = ApplicationData.Current.LocalSettings;
            var enabled = (local.Values[SettingsKey] as bool?) ?? false;
            AutostartCheckbox.IsChecked = enabled;

            AutostartCheckbox.Checked += (_, __) => SetAutostart(true);
            AutostartCheckbox.Unchecked += (_, __) => SetAutostart(false);

            AutostartCheckbox.Checked += (_, __) => SetAutostartMinimize(true);
            AutostartCheckbox.Unchecked += (_, __) => SetAutostartMinimize(false);

            if (AutostartCheckbox.IsChecked== true)
            {
               start();
            }


        }


        private  void Button1_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
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
                    int tempPort = int.Parse(PortTextBox.Text.ToString());
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
