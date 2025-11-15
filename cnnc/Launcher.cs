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

        public int port = HelperClass.DEFAULT_PORT;
        public const string SERVICE_TYPE = "_gerimo._tcp.";

        // Felder für Dienste und Netzwerk
        public MulticastService mdns;
        ServiceDiscovery sd;
        ServiceProfile serviceProfile;
        UdpClient getInputData;
        IPEndPoint clientEndpoint;

        // Channel-Felder (neu!)
        Channel<string[]> hotkeyChannel;
        Channel<string[]> clickChannel;
        Channel<string[]> pinchChannel;
        Channel<string[]> mouseChannel;

        // Für sauberen Shutdown
        CancellationTokenSource _cts = new CancellationTokenSource();

        public async Task main()
        {
            await run();
        }

        public async Task run()
        {
            try
            {
                Debug.WriteLine("Server wird gestartet...");

                // Initialisieren als Feldvariablen
                mdns = new MulticastService();
                sd = new ServiceDiscovery(mdns);

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

                // InputInjector initialisieren
                _injector = InputInjector.TryCreate();
                if (_injector == null)
                    Console.WriteLine("[Warnung] InputInjector konnte nicht erstellt werden. PINCH-Funktionalität wird nicht verfügbar sein.");
                else
                    Console.WriteLine("[Info] InputInjector erfolgreich erstellt.");

                // Dienstprofil anlegen
                serviceProfile = new ServiceProfile(
                    instanceName: Environment.MachineName,
                    serviceName: SERVICE_TYPE,
                    port: (ushort)port
                );

                Console.WriteLine($"Mache Dienst '{serviceProfile.FullyQualifiedName}' im Netzwerk bekannt...");
                sd.Advertise(serviceProfile);

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
                    Console.WriteLine($"[Details] {ex.InnerException.Message}");
                Console.WriteLine("\nDieser Fehler passiert oft, wenn:");
                Console.WriteLine("  a) Ein anderes Programm bereits den UDP-Port 5353 verwendet (z.B. iTunes, Skype).");
                Console.WriteLine("  b) Die Windows-Firewall den Zugriff blockiert (sehr wahrscheinlich!).");
                Console.WriteLine("\nDrücken Sie eine beliebige Taste zum Beenden.");
                Console.ReadKey();
            }
            finally
            {
                // Fallback-Cleanup
                if (sd != null && serviceProfile != null)
                    sd.Unadvertise(serviceProfile);
                if (mdns != null)
                    mdns.Stop();
                getInputData?.Close();
                getInputData?.Dispose();

                // Channels korrekt abschließen
                hotkeyChannel?.Writer.Complete();
                clickChannel?.Writer.Complete();
                pinchChannel?.Writer.Complete();
                mouseChannel?.Writer.Complete();

                _cts.Cancel();
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
                mdns.Stop();

            getInputData?.Close();
            getInputData?.Dispose();

            // Channels schließen
            hotkeyChannel?.Writer.Complete();
            clickChannel?.Writer.Complete();
            pinchChannel?.Writer.Complete();
            mouseChannel?.Writer.Complete();

            _cts.Cancel();
        }

        void StartDrawingService()
        {
            // Channels als Felder initialisieren
            hotkeyChannel = Channel.CreateUnbounded<string[]>();
            clickChannel = Channel.CreateUnbounded<string[]>();
            pinchChannel = Channel.CreateUnbounded<string[]>();
            mouseChannel = Channel.CreateUnbounded<string[]>();

            Thread connectDraw = new Thread(() =>
            {
                #region Worker Tasks
                Task.Run(async () =>
                {
                    await foreach (var msg in hotkeyChannel.Reader.ReadAllAsync(_cts.Token))
                    {
                        try
                        {
                            int keyCount = Math.Max(0, msg.Length - 1);
                            KeyCode[] keyCodes = new KeyCode[keyCount];
                            for (int i = 0; i < keyCount; i++)
                                keyCodes[i] = (KeyCode)int.Parse(msg[i + 1]);
                            WindowsInput.Simulate.Events().ClickChord(keyCodes).Invoke();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Fehler im HOTKEY-Worker: " + ex.Message);
                        }
                    }
                }, _cts.Token);
                Task.Run(async () =>
                {
                    Writing writingWorker = new Writing();
                    await foreach (var msg in clickChannel.Reader.ReadAllAsync(_cts.Token))
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
                }, _cts.Token);
                Task.Run(async () =>
                {
                    Writing writingPinch = new Writing();
                    await foreach (var msg in pinchChannel.Reader.ReadAllAsync(_cts.Token))
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
                }, _cts.Token);
                Task.Run(async () =>
                {
                    Writing mouse = new Writing();
                    await foreach (var msg in mouseChannel.Reader.ReadAllAsync(_cts.Token))
                    {
                        try
                        {
                            if (_injector == null)
                            {
                                Console.WriteLine("Mouse empfangen, aber kein InputInjector verfügbar.");
                                continue;
                            }
                            string action = msg[0];
                            if (action.Equals("MOUSE"))
                            {
                                int x = int.Parse(msg[1]);
                                int y = int.Parse(msg[2]);
                                mouse.simulateMouseMove(x, y);
                            }
                            else if (action.Equals("LEFT_MOUSE_CLICK"))
                            {
                                mouse.simulateMouseClick(1);
                            }
                            else if (action.Equals("RIGHT_MOUSE_CLICK"))
                            {
                                mouse.simulateMouseClick(2);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Fehler im mouse-Worker: " + ex.Message);
                        }
                    }
                }, _cts.Token);
                #endregion

                if (getInputData == null)
                    getInputData = new UdpClient(port);
                if (clientEndpoint == null)
                    clientEndpoint = new IPEndPoint(IPAddress.Any, 0);

                Console.WriteLine($"Zeichen-Dienst auf Port {port} gestartet. Warte auf Daten...");

                while (!_cts.Token.IsCancellationRequested)
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
                        else if (getAction.Equals("MOUSE") || getAction.Equals("LEFT_MOUSE_CLICK") || getAction.Equals("RIGHT_MOUSE_CLICK"))
                        {
                            Console.WriteLine(msg);
                            if (!mouseChannel.Writer.TryWrite(msg))
                                Console.WriteLine("MOUSE-Nachricht konnte nicht in die Queue geschrieben werden.");
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
