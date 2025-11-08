using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Windows.Devices.Display.Core;
using Windows.Devices.Radios;
using Windows.Graphics;
using WinRT;
using static WindowsInput.Native.SystemMetrics;


namespace cnnc
{

    public sealed partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
            Launcher launcher = new Launcher();
            _ = launcher.main(); // Fire-and-forget
        }



    }












}
