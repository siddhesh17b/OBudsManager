using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace OBudsManager
{
    public class BatteryEventArgs : EventArgs
    {
        public int Left { get; }
        public int Right { get; }

        public BatteryEventArgs(int left, int right)
        {
            Left = left;
            Right = right;
        }
    }

    public class ConnectionStateEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string Message { get; }

        public ConnectionStateEventArgs(bool isConnected, string message)
        {
            IsConnected = isConnected;
            Message = message;
        }
    }

    public class BluetoothManager
    {
        // UUIDs
        private static readonly Guid OPO_SERVICE_UUID = new Guid("0000079a-d102-11e1-9b23-00025b00a5a5");
        private static readonly Guid WRITE_CHAR_UUID = new Guid("0100079a-d102-11e1-9b23-00025b00a5a5");
        private static readonly Guid NOTIFY_CHAR_UUID = new Guid("0200079a-d102-11e1-9b23-00025b00a5a5");
        private static readonly Guid FE2C_CHAR_UUID = new Guid("fe2c123a-8366-4814-8eb0-01de32100bea");

        // Packets
        private static readonly byte[] HELLO_PKT = { 0xAA, 0x07, 0x00, 0x00, 0x00, 0x01, 0x23, 0x00, 0x00, 0x12 };
        private static readonly byte[] REGISTER_PKT = { 0xAA, 0x0C, 0x00, 0x00, 0x00, 0x85, 0x41, 0x05, 0x00, 0x00, 0xB5, 0x50, 0xA0, 0x69 };
        private static readonly byte[] ANC_QUERY_PKT = { 0xAA, 0x09, 0x00, 0x00, 0x04, 0x82, 0x44, 0x02, 0x00, 0x00, 0xF2 };
        private static readonly byte[] BATTERY_QUERY_PKT = { 0xAA, 0x07, 0x00, 0x00, 0x06, 0x01, 0x25, 0x00, 0x00 };

        private static readonly byte[] ANC_ON_PKT = { 0xAA, 0x0A, 0x00, 0x00, 0x04, 0x04, 0x42, 0x03, 0x00, 0x01, 0x01, 0x01 };
        private static readonly byte[] ANC_OFF_PKT = { 0xAA, 0x0A, 0x00, 0x00, 0x04, 0x04, 0x42, 0x03, 0x00, 0x01, 0x01, 0x02 };
        private static readonly byte[] ANC_TRANS_PKT = { 0xAA, 0x0A, 0x00, 0x00, 0x04, 0x04, 0x42, 0x03, 0x00, 0x01, 0x01, 0x04 };

        // Events
        public event EventHandler<BatteryEventArgs>? BatteryUpdated;
        public event EventHandler<ConnectionStateEventArgs>? ConnectionStateChanged;

        private BluetoothLEDevice? _device;
        private GattCharacteristic? _writeCharacteristic;
        private GattCharacteristic? _notifyCharacteristic;
        private GattCharacteristic? _fe2cCharacteristic;
        private BluetoothLEAdvertisementWatcher? _watcher;
        private CancellationTokenSource? _connectionCancellation;
        private bool _isConnected;
        private bool _isConnecting;
        private readonly object _lock = new object();

        public bool IsConnected => _isConnected;

        public void Start()
        {
            _connectionCancellation = new CancellationTokenSource();
            Task.Run(() => ConnectionLoop(_connectionCancellation.Token));
        }

        public void Stop()
        {
            _connectionCancellation?.Cancel();
            Cleanup();
        }

        private async Task ConnectionLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!_isConnected && !_isConnecting)
                {
                    UpdateStatus(false, "Scanning for buds...");
                    bool success = await TryConnectToPairedDeviceAsync(token);
                    if (!success && !token.IsCancellationRequested)
                    {
                        // Bypass active scanner if matching paired devices exist to avoid audio interference
                        bool hasPaired = await HasPairedDeviceAsync();
                        if (!hasPaired)
                        {
                            await StartScanningAsync(token);
                        }
                    }
                }
                // Sleep for a while before checking connection status again
                await Task.Delay(5000, token).ContinueWith(t => { });
            }
        }

        private async Task<bool> HasPairedDeviceAsync()
        {
            try
            {
                string selector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
                var pairedDevices = await DeviceInformation.FindAllAsync(selector);
                return pairedDevices.Any(deviceInfo => IsMatchingDeviceName(deviceInfo.Name));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking paired devices: {ex.Message}");
                return false;
            }
        }

        private void UpdateStatus(bool isConnected, string message)
        {
            lock (_lock)
            {
                _isConnected = isConnected;
            }
            ConnectionStateChanged?.Invoke(this, new ConnectionStateEventArgs(isConnected, message));
        }

        private bool IsMatchingDeviceName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string lowerName = name.ToLower();
            return lowerName.Contains("nord buds") || 
                   lowerName.Contains("oneplus") || 
                   lowerName.Contains("enco") || 
                   lowerName.Contains("realme") || 
                   lowerName.Contains("oppo");
        }

        private async Task<bool> TryConnectToPairedDeviceAsync(CancellationToken token)
        {
            try
            {
                string selector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
                var pairedDevices = await DeviceInformation.FindAllAsync(selector);
                foreach (var deviceInfo in pairedDevices)
                {
                    if (token.IsCancellationRequested) return false;

                    if (IsMatchingDeviceName(deviceInfo.Name))
                    {
                        Debug.WriteLine($"Found paired device: {deviceInfo.Name}. Attempting connection...");
                        UpdateStatus(false, $"Connecting to paired device {deviceInfo.Name}...");
                        bool connected = await ConnectToDeviceAsync(deviceInfo.Id, token);
                        if (connected) return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding paired devices: {ex.Message}");
            }
            return false;
        }

        private async Task StartScanningAsync(CancellationToken token)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (token.Register(() => tcs.TrySetResult(false)))
            {
                _watcher = new BluetoothLEAdvertisementWatcher
                {
                    ScanningMode = BluetoothLEScanningMode.Passive
                };

                bool deviceDiscovered = false;
                _watcher.Received += async (sender, args) =>
                {
                    if (token.IsCancellationRequested) return;

                    string name = args.Advertisement.LocalName;
                    if (IsMatchingDeviceName(name))
                    {
                        lock (_lock)
                        {
                            if (deviceDiscovered) return;
                            deviceDiscovered = true;
                        }

                        try { ((BluetoothLEAdvertisementWatcher)sender).Stop(); } catch { }
                        
                        Debug.WriteLine($"Discovered device via advertisement: {name}. Connecting...");
                        UpdateStatus(false, $"Found {name}, connecting...");
                        
                        bool connected = await ConnectToDeviceByAddressAsync(args.BluetoothAddress, token);
                        tcs.TrySetResult(connected);
                    }
                };

                try
                {
                    _watcher.Start();
                    // Wait for scanning to resolve or cancel
                    await tcs.Task;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Scanning failed: {ex.Message}");
                    UpdateStatus(false, "Bluetooth is off or unavailable.");
                }
                finally
                {
                    try { _watcher?.Stop(); } catch { }
                }
            }
        }

        private async Task<T?> WithTimeout<T>(Task<T> task, int durationMs, CancellationToken token) where T : class
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                var delayTask = Task.Delay(durationMs, cts.Token);
                var completedTask = await Task.WhenAny(task, delayTask);
                if (completedTask == task)
                {
                    cts.Cancel(); // Cancel delay task
                    return await task;
                }
                else
                {
                    return null;
                }
            }
        }

        private async Task<bool> ConnectToDeviceByAddressAsync(ulong address, CancellationToken token)
        {
            try
            {
                var deviceTask = BluetoothLEDevice.FromBluetoothAddressAsync(address).AsTask(token);
                _device = await WithTimeout(deviceTask, 3000, token);
                if (_device != null)
                {
                    return await SetupDeviceServicesAsync(token);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Connection by address failed: {ex.Message}");
            }
            return false;
        }

        private async Task<bool> ConnectToDeviceAsync(string deviceId, CancellationToken token)
        {
            try
            {
                var deviceTask = BluetoothLEDevice.FromIdAsync(deviceId).AsTask(token);
                _device = await WithTimeout(deviceTask, 3000, token);
                if (_device != null)
                {
                    return await SetupDeviceServicesAsync(token);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Connection by ID failed: {ex.Message}");
            }
            return false;
        }

        private async Task<bool> SetupDeviceServicesAsync(CancellationToken token)
        {
            _isConnecting = true;
            try
            {
                var device = _device;
                if (device == null) return false;

                device.ConnectionStatusChanged += Device_ConnectionStatusChanged;

                // Discover services (try Cached first for speed and compatibility with already connected devices, fallback to Uncached)
                var servicesResult = await device.GetGattServicesAsync(BluetoothCacheMode.Cached);
                if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
                {
                    Debug.WriteLine("Cached services not found or failed. Trying Uncached...");
                    if (token.IsCancellationRequested) return false;
                    servicesResult = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                }

                if (servicesResult.Status != GattCommunicationStatus.Success)
                {
                    Debug.WriteLine("Failed to get GATT services.");
                    Cleanup();
                    return false;
                }

                foreach (var service in servicesResult.Services)
                {
                    if (token.IsCancellationRequested) return false;
                    var charsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Cached);
                    if (charsResult.Status != GattCommunicationStatus.Success || charsResult.Characteristics.Count == 0)
                    {
                        if (token.IsCancellationRequested) return false;
                        charsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    }
                    if (charsResult.Status != GattCommunicationStatus.Success) continue;

                    foreach (var ch in charsResult.Characteristics)
                    {
                        if (ch.Uuid == WRITE_CHAR_UUID)
                        {
                            _writeCharacteristic = ch;
                        }
                        else if (ch.Uuid == NOTIFY_CHAR_UUID)
                        {
                            _notifyCharacteristic = ch;
                        }
                        else if (ch.Uuid == FE2C_CHAR_UUID)
                        {
                            _fe2cCharacteristic = ch;
                        }
                    }
                }

                if (_writeCharacteristic == null || _notifyCharacteristic == null)
                {
                    Debug.WriteLine("Required characteristics not found.");
                    Cleanup();
                    return false;
                }

                // Start Notifications
                await SubscribeToNotificationsAsync(_notifyCharacteristic);
                if (_fe2cCharacteristic != null)
                {
                    await SubscribeToNotificationsAsync(_fe2cCharacteristic);
                }

                // Authentication Handshake
                UpdateStatus(false, "Authenticating with buds...");
                
                // Write HELLO
                await WriteBytesAsync(HELLO_PKT);
                await Task.Delay(2000, token);

                // Write REGISTER
                await WriteBytesAsync(REGISTER_PKT);
                await Task.Delay(1500, token);

                UpdateStatus(true, $"Connected to {device.Name}");

                // Immediately query battery status
                await QueryBatteryAsync();

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Setup device failed: {ex.Message}");
                Cleanup();
            }
            finally
            {
                _isConnecting = false;
            }
            return false;
        }

        private async Task SubscribeToNotificationsAsync(GattCharacteristic characteristic)
        {
            try
            {
                var result = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if (result == GattCommunicationStatus.Success)
                {
                    characteristic.ValueChanged += Characteristic_ValueChanged;
                    Debug.WriteLine($"Subscribed to notifications on characteristic {characteristic.Uuid}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error subscribing to notifications: {ex.Message}");
            }
        }

        private void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            try
            {
                if (args.CharacteristicValue == null || args.CharacteristicValue.Length == 0) return;

                var reader = DataReader.FromBuffer(args.CharacteristicValue);
                byte[] data = new byte[args.CharacteristicValue.Length];
                reader.ReadBytes(data);

                Debug.WriteLine($"Received notification from {sender.Uuid}: {BitConverter.ToString(data)}");

                // Parse Battery Packet
                // Format matches: data.Length >= 16 && data[4] == 0x06 && data[5] == 0x81
                if (data.Length >= 16 && data[4] == 0x06 && data[5] == 0x81)
                {
                    int left = data[12];
                    int right = data[14];

                    // Standard validation: 0xFF/255 represents unavailable (e.g. earbud in case/disconnected)
                    // Valid ranges are 0 to 100
                    if (left > 100) left = -1;
                    if (right > 100) right = -1;

                    BatteryUpdated?.Invoke(this, new BatteryEventArgs(left, right));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing notification: {ex.Message}");
            }
        }

        private void Device_ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                Debug.WriteLine("Device disconnected.");
                Cleanup();
                UpdateStatus(false, "Disconnected from buds.");
            }
        }

        private async Task WriteBytesAsync(byte[] bytes)
        {
            var writeChar = _writeCharacteristic;
            if (writeChar == null) return;
            try
            {
                var result = await writeChar.WriteValueWithResultAsync(
                    bytes.AsBuffer(), 
                    GattWriteOption.WriteWithoutResponse);
                
                if (result.Status != GattCommunicationStatus.Success)
                {
                    Debug.WriteLine($"Write failed: {result.Status}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Write error: {ex.Message}");
            }
        }

        public async Task SetAncModeAsync(string mode)
        {
            if (!_isConnected || _writeCharacteristic == null) return;

            byte[]? modePacket = mode.ToLower() switch
            {
                "on" => ANC_ON_PKT,
                "off" => ANC_OFF_PKT,
                "trans" => ANC_TRANS_PKT,
                _ => null
            };

            if (modePacket == null) return;

            try
            {
                // Protocol: Query state first
                await WriteBytesAsync(ANC_QUERY_PKT);
                await Task.Delay(500);

                // Write actual ANC mode packet
                await WriteBytesAsync(modePacket);
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting ANC mode: {ex.Message}");
            }
        }

        public async Task QueryBatteryAsync()
        {
            if (!_isConnected) return;
            await WriteBytesAsync(BATTERY_QUERY_PKT);
        }

        private void Cleanup()
        {
            BluetoothLEDevice? device;
            GattCharacteristic? notifyChar;
            GattCharacteristic? fe2cChar;
            BluetoothLEAdvertisementWatcher? watcher;

            lock (_lock)
            {
                _isConnected = false;
                device = _device;
                notifyChar = _notifyCharacteristic;
                fe2cChar = _fe2cCharacteristic;
                watcher = _watcher;

                _device = null;
                _writeCharacteristic = null;
                _notifyCharacteristic = null;
                _fe2cCharacteristic = null;
                _watcher = null;
            }

            if (device != null)
            {
                try { device.ConnectionStatusChanged -= Device_ConnectionStatusChanged; } catch { }
                try { device.Dispose(); } catch { }
            }

            if (notifyChar != null)
            {
                try { notifyChar.ValueChanged -= Characteristic_ValueChanged; } catch { }
            }

            if (fe2cChar != null)
            {
                try { fe2cChar.ValueChanged -= Characteristic_ValueChanged; } catch { }
            }

            if (watcher != null)
            {
                try { watcher.Stop(); } catch { }
            }
        }
    }
}
