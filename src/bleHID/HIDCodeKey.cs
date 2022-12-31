using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Globalization;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace BLE_Tiny_HoGP_server
{
    class HIDCodeKey
    {
        static readonly byte protocolMode = (0x01);    // 0: Boot Protocol 1: Rport Protocol

        private static readonly GattLocalCharacteristicParameters c_hidInputReportParameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read | GattCharacteristicProperties.Notify,
            ReadProtectionLevel = GattProtectionLevel.Plain
        };

        private static readonly GattLocalCharacteristicParameters c_hidProtocolModeParameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read,
            ReadProtectionLevel = GattProtectionLevel.Plain,
            StaticValue = new byte[]
            {
                protocolMode        // 0: Boot Protocol 1: Rport Protocol
            }.AsBuffer()
        };

        private static readonly uint c_hidReportReferenceDescriptorShortUuid = 0x2908;

        private static readonly GattLocalDescriptorParameters c_hidKeyboardReportReferenceParameters = new GattLocalDescriptorParameters
        {
            ReadProtectionLevel = GattProtectionLevel.Plain,
            StaticValue = new byte[]
            {
                0x01, // Report ID: 1
                0x01  // Report Type: Input
            }.AsBuffer()
        };

        private static readonly GattLocalCharacteristicParameters c_hidReportMapParameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read,
            ReadProtectionLevel = GattProtectionLevel.Plain,
            StaticValue = new byte[]
            {
                /*
                 Consumer Control
                */
                0x05, 0x0C, // Usage Page (Consumer Devices)
                0x09, 0x01, // Usage (Consumer Control)
                0xA1, 0x01, // Collection (Application)
                0x85, 0x01, // Report ID
                0x75, 0x08, // Report Size
                0x95, 0x01, // Report Count
                0x15, 0x00, // Logical Minimum (0)
                0x25, 0xFF, // Logical Maximum (255)
                0x19, 0x00, // Usage Minimum (0)
                0x29, 0xFF, // Usage Maximum (255)
                0x81, 0x00, // Input (Data, Ary, Abs)
                0xC0,       // End Collection
            }.AsBuffer()
        };

        private static readonly GattLocalCharacteristicParameters c_hidInformationParameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read,
            ReadProtectionLevel = GattProtectionLevel.Plain,
            StaticValue = new byte[]
            {
                0x11, 0x01, // HID Version: 1101
                0x00,       // Country Code: 0
                0x01        // Not Normally Connectable, Remote Wake supported
            }.AsBuffer()
        };

        private static readonly GattLocalCharacteristicParameters c_hidControlPointParameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.WriteWithoutResponse,
            WriteProtectionLevel = GattProtectionLevel.Plain
        };

        private static readonly GattLocalCharacteristicParameters c_batteryLevelParameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read | GattCharacteristicProperties.Notify,
            ReadProtectionLevel = GattProtectionLevel.Plain
        };

        private static readonly uint c_sizeOfKeyboardReportDataInBytes = 0x1;
        private GattServiceProvider m_hidServiceProvider;
        private GattLocalService m_hidService;
        private GattLocalCharacteristic m_hidKeyboardReport;
        private GattLocalCharacteristic m_hidControlPoint;
        private Object m_lock = new Object();
        private bool m_initializationFinished = false;

        public delegate void SubscribedHidClientsChangedHandler(IReadOnlyList<GattSubscribedClient> subscribedClients);
        public event SubscribedHidClientsChangedHandler SubscribedHidClientsChanged;


        private static string GetStringFromBuffer(IBuffer buffer)
        {
            return GetStringFromBuffer(buffer.ToArray());
        }

        private static string GetStringFromBuffer(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", " ");
        }

        public async Task InitiliazeAsync()
        {
            await CreateHidService();

            lock (m_lock)
            {
                m_initializationFinished = true;
            }
        }

        public void Enable()
        {
            PublishService(m_hidServiceProvider);
        }

        public void Disable()
        {
            UnpublishService(m_hidServiceProvider);
        }
        public bool IsBLEServiceStarted()
        {
            return (m_hidServiceProvider.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Started);
        }

        public void PressKey(byte keycode)
        {
            try
            {
                ChangeKeyState(keycode);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to change the key state due to: " + e.Message);
            }
        }

        public void ReleaseKey()
        {
            try
            {
                ChangeKeyState(0);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to change the key state due to: " + e.Message);
            }
        }

        private async Task CreateHidService()
        {
            // HID service.
            var hidServiceProviderCreationResult = await GattServiceProvider.CreateAsync(GattServiceUuids.HumanInterfaceDevice);
            if (hidServiceProviderCreationResult.Error != BluetoothError.Success)
            {
                Console.WriteLine("Failed to create the HID service provider: " + hidServiceProviderCreationResult.Error);
                throw new Exception("Failed to create the HID service provider: " + hidServiceProviderCreationResult.Error);
            }
            m_hidServiceProvider = hidServiceProviderCreationResult.ServiceProvider;
            m_hidService = m_hidServiceProvider.Service;

            // HID Protocol Mode characteristic.
            var hidProtocolModeCharacteristicCreationResult = await m_hidService.CreateCharacteristicAsync(GattCharacteristicUuids.ProtocolMode, c_hidProtocolModeParameters);
            if (hidProtocolModeCharacteristicCreationResult.Error != BluetoothError.Success)
            {
                Console.WriteLine("Failed to create the protocol mode characteristic: " + hidProtocolModeCharacteristicCreationResult.Error);
                throw new Exception("Failed to create the protocol mode characteristic: " + hidProtocolModeCharacteristicCreationResult.Error);
            }

            // HID keyboard Report characteristic.
            var hidKeyboardReportCharacteristicCreationResult = await m_hidService.CreateCharacteristicAsync(GattCharacteristicUuids.Report, c_hidInputReportParameters);
            if (hidKeyboardReportCharacteristicCreationResult.Error != BluetoothError.Success)
            {
                Console.WriteLine("Failed to create the keyboard report characteristic: " + hidKeyboardReportCharacteristicCreationResult.Error);
                throw new Exception("Failed to create the keyboard report characteristic: " + hidKeyboardReportCharacteristicCreationResult.Error);
            }
            m_hidKeyboardReport = hidKeyboardReportCharacteristicCreationResult.Characteristic;
            m_hidKeyboardReport.SubscribedClientsChanged += HidKeyboardReport_SubscribedClientsChanged;

            // HID keyboard Report Reference descriptor.
            var hidKeyboardReportReferenceCreationResult = await m_hidKeyboardReport.CreateDescriptorAsync(BluetoothUuidHelper.FromShortId(c_hidReportReferenceDescriptorShortUuid), c_hidKeyboardReportReferenceParameters);
            if (hidKeyboardReportReferenceCreationResult.Error != BluetoothError.Success)
            {
                Console.WriteLine("Failed to create the keyboard report reference descriptor: " + hidKeyboardReportReferenceCreationResult.Error);
                throw new Exception("Failed to create the keyboard report reference descriptor: " + hidKeyboardReportReferenceCreationResult.Error);
            }

            // HID Report Map characteristic.
            var hidReportMapCharacteristicCreationResult = await m_hidService.CreateCharacteristicAsync(GattCharacteristicUuids.ReportMap, c_hidReportMapParameters);
            if (hidReportMapCharacteristicCreationResult.Error != BluetoothError.Success)
            {
                Console.WriteLine("Failed to create the HID report map characteristic: " + hidReportMapCharacteristicCreationResult.Error);
                throw new Exception("Failed to create the HID report map characteristic: " + hidReportMapCharacteristicCreationResult.Error);
            }

            // HID Information characteristic.
            var hidInformationCharacteristicCreationResult = await m_hidService.CreateCharacteristicAsync(GattCharacteristicUuids.HidInformation, c_hidInformationParameters);
            if (hidInformationCharacteristicCreationResult.Error != BluetoothError.Success)
            {
                Console.WriteLine("Failed to create the HID information characteristic: " + hidInformationCharacteristicCreationResult.Error);
                throw new Exception("Failed to create the HID information characteristic: " + hidInformationCharacteristicCreationResult.Error);
            }

            // HID Control Point characteristic.
            var hidControlPointCharacteristicCreationResult = await m_hidService.CreateCharacteristicAsync(GattCharacteristicUuids.HidControlPoint, c_hidControlPointParameters);
            if (hidControlPointCharacteristicCreationResult.Error != BluetoothError.Success)
            {
                Console.WriteLine("Failed to create the HID control point characteristic: " + hidControlPointCharacteristicCreationResult.Error);
                throw new Exception("Failed to create the HID control point characteristic: " + hidControlPointCharacteristicCreationResult.Error);
            }
            m_hidControlPoint = hidControlPointCharacteristicCreationResult.Characteristic;
            m_hidControlPoint.WriteRequested += HidControlPoint_WriteRequested;

            m_hidServiceProvider.AdvertisementStatusChanged += HidServiceProvider_AdvertisementStatusChanged;
        }

        private void PublishService(GattServiceProvider provider)
        {
            var advertisingParameters = new GattServiceProviderAdvertisingParameters
            {
                IsDiscoverable = true,
                IsConnectable = true // Peripheral role support is required for Windows to advertise as connectable.
            };

            provider.StartAdvertising(advertisingParameters);
        }

        private void UnpublishService(GattServiceProvider provider)
        {
            try
            {
                if ((provider.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Started) ||
                    (provider.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Aborted))
                {
                    provider.StopAdvertising();
                    SubscribedHidClientsChanged?.Invoke(null);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to stop advertising due to: " + e.Message);
            }
        }

        private async void HidControlPoint_WriteRequested(GattLocalCharacteristic sender, GattWriteRequestedEventArgs args)
        {
            try
            {
                var deferral = args.GetDeferral();
                var writeRequest = await args.GetRequestAsync();
                Console.WriteLine("Value written to HID Control Point: " + GetStringFromBuffer(writeRequest.Value));
                // Control point only supports WriteWithoutResponse.
                deferral.Complete();
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to handle write to Hid Control Point due to: " + e.Message);
            }
        }

        private void HidServiceProvider_AdvertisementStatusChanged(GattServiceProvider sender, GattServiceProviderAdvertisementStatusChangedEventArgs args)
        {
            Console.WriteLine("HID advertisement status changed to " + args.Status);
        }

        private void BatteryServiceProvider_AdvertisementStatusChanged(GattServiceProvider sender, GattServiceProviderAdvertisementStatusChangedEventArgs args)
        {
            Console.WriteLine("Battery advertisement status changed to " + args.Status);
        }

        private void HidKeyboardReport_SubscribedClientsChanged(GattLocalCharacteristic sender, object args)
        {
            Console.WriteLine("Number of clients now registered for notifications: " + sender.SubscribedClients.Count);
            SubscribedHidClientsChanged?.Invoke(sender.SubscribedClients);
        }

        private void ChangeKeyState(byte keycode)
        {
            if (!m_initializationFinished || m_hidServiceProvider.AdvertisementStatus != GattServiceProviderAdvertisementStatus.Started)
            {
                //Console.WriteLine("server not Start. " + m_hidServiceProvider.AdvertisementStatus);
                return;
            }
            if (m_hidKeyboardReport.SubscribedClients.Count == 0)
            {
                //Console.WriteLine("No clients are currently subscribed.");
                return;
            }

            var reportValue = new byte[c_sizeOfKeyboardReportDataInBytes];
            reportValue[0] = keycode;

            //Console.Write(string.Format("{0:x2} ", keycode));
            var asyncOp = m_hidKeyboardReport.NotifyValueAsync(reportValue.AsBuffer());
        }
    }
}
