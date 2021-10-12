using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Speech.Synthesis;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi;
using System.Threading;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Audio_Switcheroo
{
    class AudioEventArgs : EventArgs
    {
        public CoreAudioController controller { get; set; }
        public IEnumerable<CoreAudioDevice> devices { get; set; }

        public CoreAudioDevice device { get; set; }
        public AudioEventArgs(CoreAudioController controller, IEnumerable<CoreAudioDevice> devices, CoreAudioDevice device)
        {
            this.controller = controller;
            this.devices = devices;
            this.device = device;
        }
    }

    class AudioSwitcher
    {
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int iKey);
        public event EventHandler<AudioEventArgs> OnAudioSwitch;
        public IEnumerable<CoreAudioDevice> devices { get; set; }
        public IEnumerable<CoreAudioDevice> microphones { get; set; }
        public CoreAudioDevice device { get; set; }
        public CoreAudioController controller { get; set; }
        public int iIndex { get; set; }

        public AudioSwitcher()
        {
            this.controller = new CoreAudioController();
            this.devices = controller.GetDevices();
            this.microphones = new List<CoreAudioDevice>();
            this.iIndex = 0;



            foreach (var _device in devices)
            {
                //INPUT DECL
                if (_device.DeviceType == DeviceType.Capture &&
                    _device.State != DeviceState.Disabled)
                {
                    this.microphones = this.microphones.Append(_device);
                }

                //OUTPUT DECL
                if (_device.DeviceType == DeviceType.Capture || 
                    _device.State == DeviceState.Disabled    ||
                    _device.FullName.Contains("AMD High Definition"))
                    devices = devices.Where(m => m != _device).ToList();
            }
        }

        public void StartListener()
        {
            new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(1);
                    if (GetAsyncKeyState(0x13) == -32767)
                    {
                        if (this.iIndex >= this.devices.Count())
                            this.iIndex = 0;
                        
                        OnAudioSwitch.Invoke(this, new AudioEventArgs(this.controller, this.devices, this.devices.ElementAt(this.iIndex)));

                        this.iIndex++;
                        Thread.Sleep(200);
                    }


                }


            }).Start();
        }
    }

    class Program
    {
        [DllImport("User32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("User32.dll")]
        public static extern void ReleaseDC(IntPtr dc);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public static string sMessage { get; set; }
        public static int iTick { get; set; }
        public static bool bShouldDraw { get; set; }

        static void Main(string[] args)
        {
            ShowWindow(GetConsoleWindow(), 0);
            SetStartup();

            AudioSwitcher audioSwitcher = new AudioSwitcher();
            audioSwitcher.StartListener();
            audioSwitcher.OnAudioSwitch += (s, e) =>
            {
                bShouldDraw = true;
                sMessage = e.device.FullName;
                e.controller.SetDefaultDevice(e.device);
                e.controller.SetDefaultCommunicationsDevice(e.device);


                switch (e.device.FullName)
                {
                    case "Headphones (Oculus Virtual Audio Device)":
                        {
                            e.controller.SetDefaultDevice(audioSwitcher.microphones.FirstOrDefault(m => m.FullName == "Headset Microphone (Oculus Virtual Audio Device)"));
                            e.controller.SetDefaultCommunicationsDevice(audioSwitcher.microphones.FirstOrDefault(m => m.FullName == "Headset Microphone (Oculus Virtual Audio Device)"));
                            break;
                        }
                    default:
                        {
                            e.controller.SetDefaultCommunicationsDevice(audioSwitcher.microphones.FirstOrDefault(m => m.FullName == "Microphone (Razer Nari Essential)"));
                            e.controller.SetDefaultDevice(audioSwitcher.microphones.FirstOrDefault(m => m.FullName == "Microphone (Razer Nari Essential)"));
                            break;
                        }
                }

                Paint();
            };

            
        }

        static void Paint()
        {
            IntPtr desktopDC = GetDC(IntPtr.Zero);

            Thread.Sleep(1);



            Graphics g = Graphics.FromHdc(desktopDC);

            if (!String.IsNullOrEmpty(sMessage))
            {
                Font font = new Font(FontFamily.GenericSansSerif, 16);
                g.FillRectangle(Brushes.White, 5, 5, font.Size * sMessage.Length, font.Height + 5);
                g.DrawString(sMessage, font, Brushes.Red, 10, 10);
            }

            g.Dispose();
            ReleaseDC(desktopDC);
        }

        static void SetStartup()
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey
                ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                rk.SetValue("Audio Switcheroo", Application.ExecutablePath);

        }
    }
}
