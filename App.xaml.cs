using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace OBudsManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private static Mutex? _appMutex;
        private const string MutexName = "OBudsManagerMutex";

        // Win32 APIs for broadcasting custom messages to existing instances
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ChangeWindowMessageFilter(uint message, uint dwFlag);

        private const uint MSGFLT_ADD = 1;
        private static readonly IntPtr HWND_BROADCAST = (IntPtr)0xffff;
        public static uint RestoreMessageId { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Register the custom system-wide message for restoring the window
            RestoreMessageId = RegisterWindowMessage("OBudsManager_RestoreMessage");
            if (RestoreMessageId != 0)
            {
                ChangeWindowMessageFilter(RestoreMessageId, MSGFLT_ADD);
            }

            // Acquire system-wide named Mutex
            _appMutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                // Another instance is already running. Broadcast the restore message to wake it up.
                PostMessage(HWND_BROADCAST, RestoreMessageId, IntPtr.Zero, IntPtr.Zero);

                // Shutdown this duplicate process immediately
                System.Windows.Application.Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            var mainWindow = new MainWindow();

            if (e.Args.Contains("--minimized"))
            {
                mainWindow.Visibility = Visibility.Hidden;
            }
            else
            {
                mainWindow.Show();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_appMutex != null)
            {
                try
                {
                    _appMutex.ReleaseMutex();
                }
                catch { }
                _appMutex.Dispose();
            }
            base.OnExit(e);
        }

        public static void ReleaseMutexForRestart()
        {
            if (_appMutex != null)
            {
                try
                {
                    _appMutex.ReleaseMutex();
                }
                catch { }
                _appMutex.Dispose();
                _appMutex = null;
            }
        }
    }
}
