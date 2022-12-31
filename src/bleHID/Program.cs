using System;
using System.Threading.Tasks;
using System.Linq;
using System.Net;
using System.Net.Sockets;

using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;


namespace BLE_Tiny_HoGP_server
{
    class Program
    {
        static HIDCodeKey m_hidCodeKey;

        static void Main(string[] args)
        {
            Task.Run(AsyncMain).Wait();
        }

        // restart bluetooth adapter
        // <<Build target x86 or x64 must be set.>>
        static async Task RestartBluetoothRadio()
        { 
            BluetoothAdapter btAdapter = await BluetoothAdapter.GetDefaultAsync();
            if (btAdapter == null)
            {
                return;
            } 
            Console.WriteLine("local BluetoothAddress = " + string.Format("{0:x12}", btAdapter.BluetoothAddress));

            await Radio.RequestAccessAsync();
            var radios = await Radio.GetRadiosAsync();
            var bluetoothRadio = radios.FirstOrDefault(radio => radio.Kind == RadioKind.Bluetooth);
            if (bluetoothRadio == null)
            {
                return;
            }
            Console.WriteLine("now bluetoothRadio.State = " + bluetoothRadio.State);

            await bluetoothRadio.SetStateAsync(RadioState.Off);
            await Task.Delay(100);

            await bluetoothRadio.SetStateAsync(RadioState.On);
            await Task.Delay(100);
        }

        static async Task AsyncMain()
        {
            Console.WriteLine("start");

            // Restart Radio
            await RestartBluetoothRadio();

            m_hidCodeKey = new HIDCodeKey();
            await m_hidCodeKey.InitiliazeAsync();

            // Start Advertise
            m_hidCodeKey.Enable();
            while (!m_hidCodeKey.IsBLEServiceStarted()) await Task.Delay(100);

            Console.WriteLine("BLE peripheral server started.");

            var local = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 55740);
            var remote = new IPEndPoint(IPAddress.Any, 0);
            var client = new UdpClient(local);

            Console.WriteLine("UDP server started. " + local.ToString());

            while (true)
            {
                Byte[] buffer = client.Receive(ref remote);

                Console.Write(string.Format("{0:x2} ", buffer[0]));
                m_hidCodeKey.PressKey(buffer[0]);
                await Task.Delay(80);
                m_hidCodeKey.PressKey(0x00);  // release key
            }

        }

    }
}
