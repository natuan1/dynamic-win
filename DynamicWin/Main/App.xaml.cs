using DynamicWin.Main;
using DynamicWin.Resources;
using DynamicWin.Utils;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace DynamicWin
{
    public partial class DynamicWinMain : Application
    {
        public static MMDevice defaultDevice;
        public static MMDevice defaultMicrophone;

        public static string Version => "1.1.0b";

        [STAThread]
        public static void Main()
        {
            // Elevated firewall-install relaunch: must exit before the
            // single-instance mutex would reject this child process.
            if (LocalSend.LocalSendFirewall.TryHandleInstallArg()) return;

            DynamicWinMain m = new DynamicWinMain();
            m.Run();
        }

        private static void UpdateStartup(DynamicWin.Platform.IStartupShortcutAdapter startupShortcuts)
        {
            try
            {
                if (Settings.RunOnStartup)
                {
                    startupShortcuts.CreateShortcut();
                }
                else
                {
                    startupShortcuts.RemoveShortcut();
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions here
                MessageBox.Show($"Failed to add application to startup: {ex.Message}");
            }
        }


        Mutex mutex;
        private ApplicationLifetime? runtime;
        private MainForm? mainForm;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Handle unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Dispatcher.UnhandledException += Dispatcher_UnhandledException;

            bool result;
            mutex = new System.Threading.Mutex(true, "FlorianButz.DynamicWin", out result);

            if (!result)
            {
                ErrorForm errorForm = new ErrorForm();
                errorForm.Show();
                return;
            }

            var platformAdapters = new DynamicWin.Platform.WindowsPlatformAdapters();
            runtime = new ApplicationCompositionRoot(platformAdapters, () => UpdateStartup(platformAdapters.StartupShortcuts)).Create(
                InitializeAudioDevices,
                ShowMainForm,
                DisposeMainForm);
            runtime.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            runtime?.Dispose();
            GC.KeepAlive(mutex); // Important
        }

        private static void InitializeAudioDevices()
        {
            try
            {
                using var deviceEnumerator = new MMDeviceEnumerator();
                defaultDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                defaultMicrophone = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            }
            catch
            {
                defaultDevice = null;
                defaultMicrophone = null;
            }
        }

        private void ShowMainForm(IApplicationServices services)
        {
            mainForm = new MainForm(services);
            mainForm.Show();
        }

        private void DisposeMainForm()
        {
            mainForm?.DisposeTrayIcon();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Unhandled exception: {e.ExceptionObject}");
        }

        private void Dispatcher_UnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Unhandled exception: {e.Exception}");
            e.Handled = true; // Prevent the application from terminating
        }


        private static readonly DateTime Jan1st1970 = new DateTime
            (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long CurrentTimeMillis()
        {
            return (long)(DateTime.UtcNow - Jan1st1970).TotalMilliseconds;
        }

        public static long NanoTime()
        {
            long nano = 10000L * Stopwatch.GetTimestamp();
            nano /= TimeSpan.TicksPerMillisecond;
            nano *= 100L;
            return nano;
        }
    }

}
