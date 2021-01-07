using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace USoundServer
{
    class Program
    {
        private static UdpClient UdpServer;
        private static TcpListener Listener;
        private static Dictionary<string, string> ShortcutToFile;
        private static Dictionary<string, List<string>> GroupedShortcuts;
        private static byte[] ResponseData;
        private static bool Running = true;
        private static bool GotConnection = false;
        private static string Path = "C:\\SoundData";
        private static bool UseLoopback = false;

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public static void Handle(object TcpClient)
        {
            TcpClient Client = (TcpClient)TcpClient;

            byte[] Serialized = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(GroupedShortcuts));

            Client.GetStream().Write(Serialized, 0, Serialized.Length);

            while (true)
            {
                NetworkStream stream = Client.GetStream();
                byte[] buffer = new byte[1024];
                int byte_count = stream.Read(buffer, 0, buffer.Length);

                if (byte_count == 0)
                {
                    break;
                }

                string data = Encoding.UTF8.GetString(buffer, 0, byte_count);
                OnDispatch(data);
                Console.WriteLine(data);
            }

            Client.Client.Shutdown(SocketShutdown.Both);
            Client.Close();
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            UdpServer = new UdpClient(8888);
            ShortcutToFile = new Dictionary<string, string>();
            GroupedShortcuts = new Dictionary<string, List<string>>();
            ProcessEntries();
            ResponseData = Encoding.ASCII.GetBytes("UnarySoundboardServer");
            Listener = new TcpListener(9999);
            Listener.Start();

            while (Running)
            {
                if(!GotConnection)
                {
                    var ClientEp = new IPEndPoint(IPAddress.Any, 0);
                    var ClientRequestData = UdpServer.Receive(ref ClientEp);
                    var ClientRequest = Encoding.ASCII.GetString(ClientRequestData);
                    if (ClientRequest == "UnarySoundboardClient")
                    {
                        GotConnection = true;
                        UdpServer.Send(ResponseData, ResponseData.Length, ClientEp);
                    }
                    Console.WriteLine("Recived {0} from {1}, sending response", ClientRequest, ClientEp.Address.ToString());
                }

                if (Listener.Pending())
                {
                    TcpClient Client = Listener.AcceptTcpClient();
                    Console.WriteLine("Someone connected!!");
                    Thread t = new Thread(Handle);
                    t.Start(Client);
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        public static void ProcessEntries()
        {
            string[] Categories = Directory.GetDirectories(Path, "*.*", SearchOption.TopDirectoryOnly);

            foreach (var CategoryPath in Categories)
            {
                string Category = System.IO.Path.GetFileName(CategoryPath);

                GroupedShortcuts[Category] = new List<string>();

                List<string> Files = Directory.GetFiles(Path + '/' + Category, "*.*", SearchOption.AllDirectories).ToList();

                foreach (var File in Files)
                {
                    GroupedShortcuts[Category].Add(System.IO.Path.GetFileNameWithoutExtension(File));
                    ShortcutToFile[Category + "\\" + System.IO.Path.GetFileNameWithoutExtension(File)] = Category + '\\' + System.IO.Path.GetFileName(File);
                }
            }
        }

        private static void Run(string Path, string Command)
        {
            System.Diagnostics.Process Process = new System.Diagnostics.Process();
            Process.StartInfo.FileName = Path;
            Process.StartInfo.Arguments = Command;
            Process.StartInfo.CreateNoWindow = true;
            Process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            Process.Start();
        }

        public static void OnDispatch(string Exec)
        {
            if (File.Exists(Path + '\\' + "Use"))
            {
                UseLoopback = true;
            }
            else
            {
                UseLoopback = false;
            }

            Run("C:/FoobarGame/foobar2000.exe", "/command:\"Remove playlist\" /add /immediate \"" + Path + '\\' + ShortcutToFile[Exec] + "\" /command:hide");

            if (UseLoopback == true)
            {
                Run("C:/FoobarLoopback/foobar2000.exe", "/command:\"Remove playlist\" /add /immediate \"" + Path + '\\' + ShortcutToFile[Exec] + "\" /command:hide");
            }
        }
    }
}