using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace cnnc
{
   public class HelperClass
    {

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

    }
}
