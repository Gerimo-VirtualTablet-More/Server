using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Drawing;
using System.Windows.Forms; // NuGet: Microsoft.Windows.Compatibility
using Windows.Storage;

namespace cnnc
{
    public sealed partial class MainWindow : Window
    {
        private NotifyIcon trayIcon;
        private readonly bool _hideOnFirstShow;

        public MainWindow(bool hideOnFirstShow)
        {
            _hideOnFirstShow = hideOnFirstShow;

            Content = new MainPage();
            this.Title = "Gerimo - Server";
            ConfigureWindow(400, 800);

            CreateTrayIcon();
            trayIcon.Click += TrayIcon_Click;

            if (_hideOnFirstShow)
            {
                this.Activated += MainWindow_ActivatedOnce;
            }
        }

        private void MainWindow_ActivatedOnce(object? sender, WindowActivatedEventArgs e)
        {
            var appWindow = this.AppWindow;
            appWindow.Hide();
            this.Activated -= MainWindow_ActivatedOnce;
        }

        private void ConfigureWindow(int width, int height)
        {
            var appWindow = this.AppWindow;
            appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            var wa = displayArea.WorkArea;
            int x = wa.X + (wa.Width - width) / 2;
            int y = wa.Y + (wa.Height - height) / 2;
            appWindow.Move(new Windows.Graphics.PointInt32(x, y));
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = true;
            }
        }

        public void CreateTrayIcon()
        {
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string assetsDir = System.IO.Path.Combine(exeDir, "Assets");
            string iconPath = System.IO.Path.Combine(assetsDir, "trayicon.ico");
            trayIcon = new NotifyIcon();
            trayIcon.Icon = new Icon(iconPath);
            trayIcon.Text = "Gerimo is running!";
            trayIcon.Visible = true;
        }

        private void TrayIcon_Click(object sender, EventArgs e)
        {
            var appWindow = this.AppWindow;
            appWindow.Show();
            if (appWindow.Presenter is OverlappedPresenter presenter)
                presenter.Restore();
        }
    }
}
