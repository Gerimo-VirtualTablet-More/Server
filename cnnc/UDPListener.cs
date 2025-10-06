using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace cnnc
{
    public class UDPListener
    {
        private const int listenPort = 5000;
        static Writing writing;

        static void Main()
        {
            writing = new Writing();
         
          

            Thread thread = new Thread(StartListener);

            thread.Start();
        }

        static void StartListener()
        {
            UdpClient listener = new UdpClient(listenPort);
            IPAddress ipAddress = IPAddress.Parse("192.168.0.105");
            IPEndPoint groupEP = new IPEndPoint(ipAddress, listenPort);

            Console.WriteLine("UDP Listener gestartet...");

            while (true)
            {
                byte[] bytes = listener.Receive(ref groupEP);
                Debug.WriteLine($"Received broadcast from {groupEP} :");
                Debug.WriteLine($" {Encoding.ASCII.GetString(bytes, 0, bytes.Length)}");

                String[] message = Encoding.ASCII.GetString(bytes, 0, bytes.Length).Split(";");
                int xPos = int.Parse(message[0]);
                int yPos = int.Parse(message[1]);
                float pressure = float.Parse(message[2], CultureInfo.InvariantCulture);
                string action = message[3];
                writing.simulatePenTap(action,xPos, yPos, pressure);

            }

            try
            {
               
            }
            catch (SocketException e)
            {
                Debug.WriteLine(e);
            }
            finally
            {
                listener.Close();
            }
        }

       
    }

}
