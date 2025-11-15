using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel;

namespace gerimo
{
    public partial class App : Application
    {
        private Window? _window;

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            var actArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
            bool launchedByStartupTask = IsStartupTaskActivation(actArgs);

            var local = Windows.Storage.ApplicationData.Current.LocalSettings;
            bool autoMinimize = (local.Values["AutostartMinimize"] as bool?) ?? false;

            _window = new MainWindow(launchedByStartupTask && autoMinimize);
            _window.Activate();
        }

        private static bool IsStartupTaskActivation(AppActivationArguments? actArgs)
        {
            if (actArgs == null) return false;
            var uwpArgs = actArgs.Data as Windows.ApplicationModel.Activation.IActivatedEventArgs;
            if (uwpArgs is Windows.ApplicationModel.Activation.IStartupTaskActivatedEventArgs)
            {
                return true;
            }
            return false;
        }
    }
}
