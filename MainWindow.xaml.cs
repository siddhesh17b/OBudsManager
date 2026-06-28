using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace OBudsManager
{
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        private readonly BluetoothManager _btManager;
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private bool _isExiting;
        private bool _lowBatteryWarnedL;
        private bool _lowBatteryWarnedR;

        public MainWindow()
        {
            InitializeComponent();
            
            // Force native handle creation so that the WndProc hook can be registered and tray notifications work
            // even when the application starts hidden (minimized to system tray)
            IntPtr handle = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
            
            // Hook the WndProc message loop immediately to ensure we receive restore messages even if started minimized
            var hwndSource = System.Windows.Interop.HwndSource.FromHwnd(handle);
            hwndSource?.AddHook(WndProc);
            
            // Watch system theme changes (Light/Dark mode)
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);

            _btManager = new BluetoothManager();
            _btManager.ConnectionStateChanged += BtManager_ConnectionStateChanged;
            _btManager.BatteryUpdated += BtManager_BatteryUpdated;

            // Initialize tray, startup toggle state, and Bluetooth scan immediately.
            // This ensures logic runs even if the app starts minimized to the tray (where WPF Loaded is not fired).
            InitializeTrayIcon();
            
            // Load saved settings
            AppSettings settings = LoadSettings();
            ToggleTray.IsChecked = settings.MinimizeToTray;
            
            // Restore window size
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;

            ToggleStartup.IsChecked = IsStartupEnabled();
            _btManager.Start();

            Closing += MainWindow_Closing;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == App.RestoreMessageId)
            {
                RestoreWindow();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void InitializeTrayIcon()
        {
            try
            {
                _notifyIcon = new System.Windows.Forms.NotifyIcon();
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                if (File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
                }
                else
                {
                    _notifyIcon.Icon = CreateTrayIcon();
                }
                _notifyIcon.Text = "OBuds Manager";
                _notifyIcon.Visible = true;

                // Double click restores the UI
                _notifyIcon.DoubleClick += (s, e) => RestoreWindow();

                // Context Menu
                var contextMenu = new System.Windows.Forms.ContextMenuStrip();
                contextMenu.Items.Add("Open Manager", null, (s, e) => RestoreWindow());
                contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                
                var ancMenu = new System.Windows.Forms.ToolStripMenuItem("Noise Control");
                ancMenu.DropDownItems.Add("ANC ON", null, async (s, e) => await _btManager.SetAncModeAsync("on"));
                ancMenu.DropDownItems.Add("Transparent", null, async (s, e) => await _btManager.SetAncModeAsync("trans"));
                ancMenu.DropDownItems.Add("ANC OFF", null, async (s, e) => await _btManager.SetAncModeAsync("off"));
                contextMenu.Items.Add(ancMenu);

                contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

                _notifyIcon.ContextMenuStrip = contextMenu;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize tray icon: {ex.Message}");
            }
        }

        private System.Drawing.Icon CreateTrayIcon()
        {
            // Dynamically generate a beautiful 16x16 icon (Red circle with white 'O')
            using (Bitmap bitmap = new Bitmap(16, 16))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    // Red Circle (OnePlus Red accent)
                    using (Brush brush = new SolidBrush(Color.FromArgb(235, 10, 30)))
                    {
                        g.FillEllipse(brush, 0, 0, 15, 15);
                    }

                    // White 'O'
                    using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", 8, System.Drawing.FontStyle.Bold))
                    using (Brush textBrush = new SolidBrush(Color.White))
                    {
                        // Centered text
                        g.DrawString("O", font, textBrush, 3.5f, 0.5f);
                    }
                }
                IntPtr hIcon = bitmap.GetHicon();
                return System.Drawing.Icon.FromHandle(hIcon);
            }
        }

        private void RestoreWindow()
        {
            Dispatcher.Invoke(() =>
            {
                Show();
                WindowState = WindowState.Normal;
                Visibility = Visibility.Visible;
                Activate();
            });
        }

        private void ExitApplication()
        {
            _isExiting = true;
            Close();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save settings (including window size)
            SaveCurrentSettings();

            // If Tray minimization is enabled, hide window and cancel closing
            if (ToggleTray.IsChecked == true && !_isExiting)
            {
                e.Cancel = true;
                Hide();
                ShowNotification("Minimized to Tray", "OBuds Manager is still running in the background.");
            }
            else
            {
                // Clean up resources
                _btManager.Stop();
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
            }
        }

        private void BtManager_ConnectionStateChanged(object? sender, ConnectionStateEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.IsConnected)
                {
                    StatusIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.BluetoothConnected24;
                    StatusIcon.Foreground = System.Windows.Media.Brushes.Red; // Brand color accent
                    
                    StatusTitle.Text = "Connected";
                    StatusSubtitle.Text = e.Message;
                    ConnectionLoading.Visibility = Visibility.Collapsed;

                    BatteryPanel.IsEnabled = true;
                    AncPanel.IsEnabled = true;

                    ShowNotification("Connected", e.Message);
                }
                else
                {
                    StatusIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.BluetoothSearching24;
                    StatusIcon.Foreground = System.Windows.Media.Brushes.Gray;

                    StatusTitle.Text = "Searching...";
                    StatusSubtitle.Text = e.Message;
                    ConnectionLoading.Visibility = Visibility.Visible;

                    BatteryPanel.IsEnabled = false;
                    AncPanel.IsEnabled = false;

                    // Reset values
                    LeftBatteryRing.Progress = 0;
                    LeftBatteryText.Text = "--";
                    RightBatteryRing.Progress = 0;
                    RightBatteryText.Text = "--";
                    
                    // Reset low battery warnings
                    _lowBatteryWarnedL = false;
                    _lowBatteryWarnedR = false;
                }
            });
        }

        private void BtManager_BatteryUpdated(object? sender, BatteryEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Left Earbud
                if (e.Left >= 0)
                {
                    LeftBatteryRing.Progress = e.Left;
                    LeftBatteryText.Text = $"{e.Left}%";
                    
                    // Low battery notification (below 15%)
                    if (e.Left <= 15 && !_lowBatteryWarnedL)
                    {
                        _lowBatteryWarnedL = true;
                        ShowNotification("Low Battery", $"Left earbud is low: {e.Left}%");
                    }
                    else if (e.Left > 15)
                    {
                        _lowBatteryWarnedL = false; // Reset warning
                    }
                }
                else
                {
                    LeftBatteryRing.Progress = 0;
                    LeftBatteryText.Text = "--";
                }

                // Right Earbud
                if (e.Right >= 0)
                {
                    RightBatteryRing.Progress = e.Right;
                    RightBatteryText.Text = $"{e.Right}%";

                    // Low battery notification
                    if (e.Right <= 15 && !_lowBatteryWarnedR)
                    {
                        _lowBatteryWarnedR = true;
                        ShowNotification("Low Battery", $"Right earbud is low: {e.Right}%");
                    }
                    else if (e.Right > 15)
                    {
                        _lowBatteryWarnedR = false; // Reset warning
                    }
                }
                else
                {
                    RightBatteryRing.Progress = 0;
                    RightBatteryText.Text = "--";
                }
            });
        }

        private void ShowNotification(string title, string message)
        {
            try
            {
                _notifyIcon?.ShowBalloonTip(3000, title, message, System.Windows.Forms.ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing balloon notification: {ex.Message}");
            }
        }

        private async void BtnAncOn_Click(object sender, RoutedEventArgs e)
        {
            SetActiveAncButton("on");
            await _btManager.SetAncModeAsync("on");
        }

        private async void BtnAncOff_Click(object sender, RoutedEventArgs e)
        {
            SetActiveAncButton("off");
            await _btManager.SetAncModeAsync("off");
        }

        private async void BtnAncTrans_Click(object sender, RoutedEventArgs e)
        {
            SetActiveAncButton("trans");
            await _btManager.SetAncModeAsync("trans");
        }

        private void SetActiveAncButton(string mode)
        {
            BtnAncOn.Appearance = mode == "on" ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary;
            BtnAncOff.Appearance = mode == "off" ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary;
            BtnAncTrans.Appearance = mode == "trans" ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary;
        }

        private async void BtnInfo_Click(object sender, RoutedEventArgs e)
        {
            var messageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "About OBuds Manager",
                CloseButtonText = "Close",
                MaxWidth = 340,
                Topmost = false
            };

            var mainPanel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };

            // App title text
            var titleBlock = new System.Windows.Controls.TextBlock
            {
                Text = "OBuds Manager",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.Red,
                Margin = new Thickness(0, 0, 0, 4)
            };
            mainPanel.Children.Add(titleBlock);

            // Version text
            var versionBlock = new System.Windows.Controls.TextBlock
            {
                Text = "Version 1.1.0",
                FontSize = 12,
                Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush"),
                Margin = new Thickness(0, 0, 0, 12)
            };
            mainPanel.Children.Add(versionBlock);

            // Description text
            var descBlock = new System.Windows.Controls.TextBlock
            {
                Text = "A Fluent utility to manage noise control modes and monitor battery levels for Oppo, OnePlus, and Realme earbuds on Windows 11.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 16)
            };
            mainPanel.Children.Add(descBlock);

            // Developer text
            var devBlock = new System.Windows.Controls.TextBlock
            {
                Text = "Developer: Siddhesh Bisen",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            mainPanel.Children.Add(devBlock);

            // GitHub link
            var linkPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal
            };

            var githubLabel = new System.Windows.Controls.TextBlock
            {
                Text = "GitHub: ",
                FontSize = 13,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            linkPanel.Children.Add(githubLabel);

            var linkButton = new Wpf.Ui.Controls.Button
            {
                Content = "siddhesh17b/OBudsManager",
                Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent,
                Padding = new Thickness(0),
                Foreground = System.Windows.Media.Brushes.DodgerBlue,
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            linkButton.Click += (s, args) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://github.com/siddhesh17b/OBudsManager") { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Could not open website: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            linkPanel.Children.Add(linkButton);
            mainPanel.Children.Add(linkPanel);

            messageBox.Content = mainPanel;
            messageBox.Owner = this;

            await messageBox.ShowDialogAsync();
        }

        private void ToggleStartup_Click(object sender, RoutedEventArgs e)
        {
            bool enable = ToggleStartup.IsChecked == true;
            SetStartup(enable);
        }

        private void SetStartup(bool runAtStartup)
        {
            try
            {
                string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(path, true);
                if (key != null)
                {
                    string appName = "OBudsManager";
                    string appPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (runAtStartup && !string.IsNullOrEmpty(appPath))
                    {
                        key.SetValue(appName, $"\"{appPath}\" --minimized");
                    }
                    else
                    {
                        key.DeleteValue(appName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to modify startup registry: {ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool IsStartupEnabled()
        {
            try
            {
                string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(path, false);
                if (key != null)
                {
                    return key.GetValue("OBudsManager") != null;
                }
            }
            catch { }
            return false;
        }

        private void BtnMenu_Click(object sender, RoutedEventArgs e)
        {
            OpenDrawer();
        }

        private void BtnCloseDrawer_Click(object sender, RoutedEventArgs e)
        {
            CloseDrawer();
        }

        private void DrawerBacking_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CloseDrawer();
        }

        private void OpenDrawer()
        {
            SettingsDrawer.Visibility = Visibility.Visible;
            
            // Slide in animation (TranslateTransform.X: 280 -> 0)
            var slideIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 280,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            
            // Fade in dim backing overlay (Opacity: 0 -> 0.4)
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 0.4,
                Duration = TimeSpan.FromMilliseconds(250)
            };

            DrawerTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slideIn);
            DrawerBacking.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void CloseDrawer()
        {
            // Slide out animation (TranslateTransform.X: 0 -> 280)
            var slideOut = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 280,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
            };

            // Fade out dim backing overlay (Opacity: 0.4 -> 0)
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0.4,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200)
            };

            slideOut.Completed += (s, e) =>
            {
                SettingsDrawer.Visibility = Visibility.Collapsed;
            };

            DrawerTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slideOut);
            DrawerBacking.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "OBudsManager");
        private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

        private void ToggleTray_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentSettings();
        }

        private async void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string appPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(appPath))
                {
                    // Release the single-instance mutex to allow the new process to start
                    App.ReleaseMutexForRestart();
                    
                    // Start new process
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(appPath) { UseShellExecute = true });
                    
                    // Close this instance
                    ExitApplication();
                }
            }
            catch (Exception ex)
            {
                var errorBox = new Wpf.Ui.Controls.MessageBox
                {
                    Owner = this,
                    Title = "Error",
                    Content = $"Failed to restart application: {ex.Message}",
                    CloseButtonText = "OK",
                    Topmost = false
                };
                await errorBox.ShowDialogAsync();
            }
        }

        private async void BtnReconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnReconnect.IsEnabled = false;
                _btManager.Stop();
                
                // Reset connection status UI
                StatusTitle.Text = "Disconnecting...";
                StatusSubtitle.Text = "Resetting Bluetooth link...";
                
                await Task.Delay(500);
                
                _btManager.Start();
                BtnReconnect.IsEnabled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reconnecting: {ex.Message}");
                BtnReconnect.IsEnabled = true;
            }
        }

        private async void BtnUpdates_Click(object sender, RoutedEventArgs e)
        {
            var messageBox = new Wpf.Ui.Controls.MessageBox
            {
                Owner = this,
                Title = "Check for Updates",
                Content = "Redirecting to GitHub. Continue?",
                PrimaryButtonText = "Yes",
                CloseButtonText = "Close",
                Topmost = false
            };

            var result = await messageBox.ShowDialogAsync();

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://github.com/siddhesh17b/OBudsManager/releases",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    var errorBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Owner = this,
                        Title = "Error",
                        Content = $"Failed to open browser: {ex.Message}",
                        CloseButtonText = "OK",
                        Topmost = false
                    };
                    await errorBox.ShowDialogAsync();
                }
            }
        }

        private void SaveCurrentSettings()
        {
            AppSettings settings = LoadSettings();
            
            if (WindowState == WindowState.Normal)
            {
                settings.WindowWidth = Width;
                settings.WindowHeight = Height;
            }
            else if (!RestoreBounds.IsEmpty)
            {
                settings.WindowWidth = RestoreBounds.Width;
                settings.WindowHeight = RestoreBounds.Height;
            }

            settings.MinimizeToTray = ToggleTray.IsChecked == true;
            SaveSettings(settings);
        }

        private void SaveSettings(AppSettings settings)
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }
                string json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }
            return new AppSettings(); // Return defaults
        }
    }

    public class AppSettings
    {
        public bool MinimizeToTray { get; set; } = true;
        public double WindowWidth { get; set; } = 470;
        public double WindowHeight { get; set; } = 530;
    }
}