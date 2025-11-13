using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace cnnc
{
   public class HelperClass
    {

        public const int DEFAULT_PORT = 5000;

        public static string GetHost_IP()
        {
            IPAddress[] a = System.Net.Dns.GetHostAddresses(Dns.GetHostName());

            foreach (IPAddress ip in a)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }

            return null;
        }

     public   static byte[] GetWorkAreasUtf8()
        {
            var allScreens = Screen.AllScreens;
            var sb = new StringBuilder();

            for (int i = 0; i < allScreens.Length; i++)
            {
                var s = allScreens[i];
                sb.Append(s.Primary)
                  .Append(';').Append(s.DeviceName)
                  .Append(';').Append(s.WorkingArea.Width)
                  .Append(';').Append(s.WorkingArea.Height)
                  .Append(';').Append(s.WorkingArea.X)
                  .Append(';').Append(s.WorkingArea.Y)
                  .Append(';').Append(s.WorkingArea.Bottom)
                  .Append(';').Append(s.WorkingArea.Top)
                  .Append(';').Append(s.WorkingArea.Left)
                  .Append(';').Append(s.WorkingArea.Right);

                if (i < allScreens.Length - 1)
                    sb.Append(',');
            }
            string regex = @"\\\.\"; // ggf. anpassen, falls nicht benötigt
            return Encoding.UTF8.GetBytes("RESOLUTION:" + sb.ToString().Replace(regex, ""));
        }

    }
}
