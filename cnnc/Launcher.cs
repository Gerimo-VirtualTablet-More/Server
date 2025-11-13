using Makaretu.Dns;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.UI.Input.Preview.Injection;
using WindowsInput;
using WindowsInput.Events;

namespace cnnc
{
    public class Launcher
    {
        static InputInjector _injector;

        public  int port = HelperClass.DEFAULT_PORT;
        public const string SERVICE_TYPE = "_gerimo._tcp.";

        // WICHTIG: Diese Felder außerhalb der Methode definieren
       public MulticastService mdns;
        ServiceDiscovery sd;
        ServiceProfile serviceProfile;

        UdpClient getInputData;
        IPEndPoint clientEndpoint;

        public async Task main()
        {
            await run();
        }

        public async Task run()
        {
            try
            {
                Debug.WriteLine("Server wird gestartet...");

                // Initialisieren als FELDVARIABLEN (keine lokalen Variablen verwenden!)
                mdns = new MulticastService();
                sd = new ServiceDiscovery(mdns);

                // Logging für erkannte Netzwerkinterfaces
                mdns.NetworkInterfaceDiscovered += (s, e) =>
                {
                    Console.WriteLine($"[Info] Netzwerk-Interfaces werden durchsucht...");
                    foreach (var nic in e.NetworkInterfaces)
                    {
                        if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                            (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 ||
                             nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet))
                        {
                            Console.WriteLine($"[Info] Aktives Interface gefunden: '{nic.Name}' ({nic.Description})");
                        }
                    }
                };

                Console.WriteLine("Starte mDNS-Dienst (für Diensterkennung)...");
                mdns.Start();
                Console.WriteLine("mDNS-Dienst erfolgreich gestartet.");

                // Initialisieren des InputInjectors
                _injector = InputInjector.TryCreate();
                if (_injector == null)
                {
                    Console.WriteLine("[Warnung] InputInjector konnte nicht erstellt werden. PINCH-Funktionalität wird nicht verfügbar sein.");
                }
                else
                {
                    Console.WriteLine("[Info] InputInjector erfolgreich erstellt.");
                }

                // Dienstprofil anlegen (ebenfalls Feld!)
                serviceProfile = new ServiceProfile(
                    instanceName: Environment.MachineName,
                    serviceName: SERVICE_TYPE,
                    port: (ushort)port
                );

                Console.WriteLine($"Mache Dienst '{serviceProfile.FullyQualifiedName}' im Netzwerk bekannt...");
                sd.Advertise(serviceProfile);

                // Clean-up Handler für App-Exit
                AppDomain.CurrentDomain.ProcessExit += (s, e) =>
                {
                    stopService();
                };

                Console.WriteLine("\n=======================================================");
                Console.WriteLine(" Server ist jetzt aktiv und sollte sichtbar sein.");
                Console.WriteLine(" Testen Sie jetzt mit der 'Service Browser' App auf Ihrem Handy.");
                Console.WriteLine(" WENN DER DIENST NICHT ERSCHEINT -> PROBLEM = FIREWALL.");
                Console.WriteLine("=======================================================\n");

                // Zeichen-Dienst starten und Server lebendig halten
                StartDrawingService();
                await Task.Delay(-1); // Blockiert asynchron => Server bleibt aktiv
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATALER FEHLER] Ein Fehler hat den Start des Servers verhindert: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[Details] {ex.InnerException.Message}");
                }
                Console.WriteLine("\nDieser Fehler passiert oft, wenn:");
                Console.WriteLine("  a) Ein anderes Programm bereits den UDP-Port 5353 verwendet (z.B. iTunes, Skype).");
                Console.WriteLine("  b) Die Windows-Firewall den Zugriff blockiert (sehr wahrscheinlich!).");
                Console.WriteLine("\nDrücken Sie eine beliebige Taste zum Beenden.");
                Console.ReadKey();
            }
            finally
            {
                // Fallback-Cleanup (sollte eigentlich durch ProcessExit abgedeckt sein)
                if (sd != null && serviceProfile != null)
                    sd.Unadvertise(serviceProfile);
                if (mdns != null)
                    mdns.Stop();
            }
        }

        public void stopService()
        {
            if (sd != null && serviceProfile != null)
            {
                Console.WriteLine("Dienst wird abgemeldet (Exit) ...");
                sd.Unadvertise(serviceProfile);
            }
            if (mdns != null)
            {
                mdns.Stop();
            }
        }

        void StartDrawingService()
        {
            Thread connectDraw = new Thread(() =>
            {
                var hotkeyChannel = Channel.CreateUnbounded<string[]>();
                var clickChannel = Channel.CreateUnbounded<string[]>();
                var pinchChannel = Channel.CreateUnbounded<string[]>();

                #region Worker Tasks
                Task.Run(async () =>
                {
                    await foreach (var msg in hotkeyChannel.Reader.ReadAllAsync())
                    {
                        try
                        {
                            int keyCount = Math.Max(0, msg.Length - 1);
                            KeyCode[] keyCodes = new KeyCode[keyCount];
                            for (int i = 0; i < keyCount; i++)
                            {
                                keyCodes[i] = (KeyCode)int.Parse(msg[i + 1]);
                            }
                            WindowsInput.Simulate.Events().ClickChord(keyCodes).Invoke();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Fehler im HOTKEY-Worker: " + ex.Message);
                        }
                    }
                });
                Task.Run(async () =>
                {
                    Writing writingWorker = new Writing();
                    await foreach (var msg in clickChannel.Reader.ReadAllAsync())
                    {
                        try
                        {
                            string action = msg[0];
                            if (action.Equals("CLICK"))
                            {
                                int xPos = int.Parse(msg[1]);
                                int yPos = int.Parse(msg[2]);
                                float pressure = float.Parse(msg[3], CultureInfo.InvariantCulture);
                                int tiltX = int.Parse(msg[4]);
                                int tiltY = int.Parse(msg[5]);
                                writingWorker.simulatePenTap(action, xPos, yPos, pressure, tiltX, tiltY);
                            }
                            else
                            {
                                int xPos = int.Parse(msg[1]);
                                int yPos = int.Parse(msg[2]);
                                float pressure = float.Parse(msg[3], CultureInfo.InvariantCulture);
                                writingWorker.simulatePenTap(action, xPos, yPos, pressure);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Fehler im CLICK-Worker: " + ex.Message);
                        }
                    }
                });
                Task.Run(async () =>
                {
                    Writing writingPinch = new Writing();
                    await foreach (var msg in pinchChannel.Reader.ReadAllAsync())
                    {
                        try
                        {
                            if (_injector == null)
                            {
                                Console.WriteLine("PINCH empfangen, aber kein InputInjector verfügbar.");
                                continue;
                            }
                            int x1 = int.Parse(msg[1]);
                            int x2 = int.Parse(msg[2]);
                            int y1 = int.Parse(msg[3]);
                            int y2 = int.Parse(msg[4]);
                            writingPinch.simulatePinch(_injector, x1, x2, y1, y2);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Fehler im PINCH-Worker: " + ex.Message);
                        }
                    }
                });
                #endregion

                if(getInputData == null)
                {
                    getInputData = new UdpClient(port);
                }
                if (clientEndpoint == null)
                {
                    clientEndpoint = new IPEndPoint(IPAddress.Any, 0);
                }

                 
            

                Console.WriteLine($"Zeichen-Dienst auf Port {port} gestartet. Warte auf Daten...");

                while (true)
                {
                    try
                    {
                        byte[] msg_byte = getInputData.Receive(ref clientEndpoint);
                        string received_text = Encoding.UTF8.GetString(msg_byte);

                        if (received_text.Equals("RESOLUTION"))
                        {
                            Console.WriteLine($"[Anfrage] Auflösung von Client {clientEndpoint.Address} angefordert.");
                            byte[] responseBytes = HelperClass.GetWorkAreasUtf8();
                            getInputData.Send(responseBytes, responseBytes.Length, clientEndpoint);
                            continue;
                        }

                        string[] msg = received_text.Split(';');
                        if (msg.Length == 0)
                            continue;

                        string getAction = msg[0];

                        if (getAction.Equals("PINCH"))
                        {
                            if (!pinchChannel.Writer.TryWrite(msg))
                                Console.WriteLine("PINCH-Nachricht konnte nicht in die Queue geschrieben werden.");
                        }
                        else if (getAction.Equals("HOTKEY"))
                        {
                            if (!hotkeyChannel.Writer.TryWrite(msg))
                                Console.WriteLine("HOTKEY-Nachricht konnte nicht in die Queue geschrieben werden.");
                        }
                        else
                        {
                            if (!clickChannel.Writer.TryWrite(msg))
                                Console.WriteLine("Pen-Nachricht konnte nicht in die Queue geschrieben werden.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Fehler im Zeichen-Dienst-Thread: " + ex.Message);
                    }
                }
            });
            connectDraw.IsBackground = true;
            connectDraw.Start();
        }


    }
}
