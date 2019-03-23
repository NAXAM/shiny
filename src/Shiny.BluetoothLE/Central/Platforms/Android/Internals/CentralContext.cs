using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using ScanMode = Android.Bluetooth.LE.ScanMode;


namespace Shiny.BluetoothLE.Central.Internals
{
    public class CentralContext
    {
        readonly ConcurrentDictionary<string, IPeripheral> devices;
        LollipopScanCallback callbacks;


        public CentralContext(IAndroidContext context)
        {
            this.Manager = (BluetoothManager)context.AppContext.GetSystemService(global::Android.App.Application.BluetoothService);
            this.Android = context;
            this.devices = new ConcurrentDictionary<string, IPeripheral>();
        }


        public BluetoothManager Manager { get; }
        public IAndroidContext Android { get; }


        public IPeripheral GetDevice(BluetoothDevice btDevice) => this.devices.GetOrAdd(
            btDevice.Address,
            x => new Peripheral(this, btDevice)
        );


        public IEnumerable<IPeripheral> GetConnectedDevices()
        {
            var nativeDevices = this.Manager
                .GetDevicesMatchingConnectionStates(ProfileType.Gatt, new[]
                {
                    (int) ProfileState.Connecting,
                    (int) ProfileState.Connected
                });
            foreach (var native in nativeDevices)
                yield return this.GetDevice(native);
        }


        public void Clear()
        {
            var connectedDevices = this.GetConnectedDevices().ToList();
            this.devices.Clear();
            foreach (var dev in connectedDevices)
                this.devices.TryAdd(((BluetoothDevice) dev.NativeDevice).Address, dev);
        }


        public IObservable<IScanResult> Scan(ScanConfig config)  => Observable.Create<ScanResult>(ob =>
        {
            this.devices.Clear();

            this.callbacks = new LollipopScanCallback(sr =>
            {
                var scanResult = this.ToScanResult(sr.Device, sr.Rssi, new AdvertisementData(sr));
                ob.OnNext(scanResult);
            });

            var builder = new ScanSettings.Builder();
            var scanMode = this.ToNative(config.ScanType);
            builder.SetScanMode(scanMode);

            var scanFilters = new List<ScanFilter>();
            if (config.ServiceUuids != null && config.ServiceUuids.Count > 0)
            {
                foreach (var guid in config.ServiceUuids)
                {
                    var uuid = guid.ToParcelUuid();
                    scanFilters.Add(new ScanFilter.Builder()
                        .SetServiceUuid(uuid)
                        .Build()
                    );
                }
            }

            if (config.AndroidUseScanBatching && this.Manager.Adapter.IsOffloadedScanBatchingSupported)
                builder.SetReportDelay(100);

            this.Manager.Adapter.BluetoothLeScanner.StartScan(
                scanFilters,
                builder.Build(),
                this.callbacks
            );

            return () => this.Manager.Adapter.BluetoothLeScanner?.StopScan(this.callbacks);
        });


        public void StopScan()
        {
            if (this.callbacks == null)
                return;

            this.Manager.Adapter.BluetoothLeScanner?.StopScan(this.callbacks);
            this.callbacks = null;
        }


        protected ScanResult ToScanResult(BluetoothDevice native, int rssi, IAdvertisementData ad)
        {
            var dev = this.GetDevice(native);
            var result = new ScanResult(dev, rssi, ad);
            return result;
        }


        protected virtual ScanMode ToNative(BleScanType scanType)
        {
            switch (scanType)
            {
                //case BleScanType.Background:
                case BleScanType.LowPowered:
                    return ScanMode.LowPower;

                case BleScanType.Balanced:
                    return ScanMode.Balanced;

                case BleScanType.LowLatency:
                    return ScanMode.LowLatency;

                default:
                    throw new ArgumentException("Invalid BleScanType");
            }
        }
    }
}