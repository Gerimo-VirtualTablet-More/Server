using Makaretu.Dns;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Windows.UI.Input.Preview.Injection;
using WindowsInput;
using WindowsInput.Events;

namespace cnnc
{
    public class Launcher
    {
        static InputInjector _injector;

        public const int DRAWING_PORT = 5000;
        public const string SERVICE_TYPE = "_gerimo._tcp.";

        public async Task main()
        {
            await run();
        }




        public static async Task run()
        {
            try
            {
                Debug.WriteLine("Server wird gestartet...");

                // 1. MulticastService und ServiceDiscovery erstellen
                var mdns = new MulticastService();
                var sd = new ServiceDiscovery(mdns);

                // Logging hinzufügen, um zu sehen, ob Netzwerkinterfaces erkannt werden
                mdns.NetworkInterfaceDiscovered += (s, e) =>
                {
                    Console.WriteLine($"[Info] Netzwerk-Interfaces werden durchsucht...");
                    foreach (var nic in e.NetworkInterfaces)
                    {
                        // Nur aktive WLAN- und Ethernet-Interfaces anzeigen, um die Logs zu vereinfachen
                        if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                           (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 ||
                            nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet))
                        {
                            Console.WriteLine($"[Info] Aktives Interface gefunden: '{nic.Name}' ({nic.Description})");
                        }
                    }
                };

                Console.WriteLine("Starte mDNS-Dienst (für Diensterkennung)...");
                // mdns.Start() wird automatisch die IP-Adressen des Hosts im Netzwerk bekanntgeben
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

                // 2. Dienstprofil erstellen (die einfachste und korrekte Methode)
                // Die Bibliothek findet die IP-Adressen automatisch heraus.
                var serviceProfile = new ServiceProfile(
                    instanceName: Environment.MachineName,
                    serviceName: SERVICE_TYPE,
                    port: (ushort)DRAWING_PORT
                );

                // 3. Dienst im Netzwerk bekannt machen
                Console.WriteLine($"Mache Dienst '{serviceProfile.FullyQualifiedName}' im Netzwerk bekannt...");
                sd.Advertise(serviceProfile);

                Console.WriteLine("\n=======================================================");
                Console.WriteLine(" Server ist jetzt aktiv und sollte sichtbar sein.");
                Console.WriteLine(" Testen Sie jetzt mit der 'Service Browser' App auf Ihrem Handy.");
                Console.WriteLine(" WENN DER DIENST NICHT ERSCHEINT -> PROBLEM = FIREWALL.");
                Console.WriteLine("=======================================================\n");

                // 4. Zeichen-Dienst starten und App am Leben halten
                StartDrawingService();
                await Task.Delay(-1);
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
        }
        static void StartDrawingService()
        {
            Thread connectDraw = new Thread(() =>
            {
                // Channels für unterschiedliche Aktionstypen
                var hotkeyChannel = Channel.CreateUnbounded<string[]>();
                var clickChannel = Channel.CreateUnbounded<string[]>();
                var pinchChannel = Channel.CreateUnbounded<string[]>();

                // Worker für HOTKEY: verarbeitet nur Hotkey-Nachrichten sequenziell
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

                            // WindowsInput-Aufruf in eigenem Worker
                            WindowsInput.Simulate.Events().ClickChord(keyCodes).Invoke();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Fehler im HOTKEY-Worker: " + ex.Message);
                        }
                    }
                });

                // Worker für CLICK/HOVER/pen taps
                Task.Run(async () =>
                {
                    // eigene Writing-Instanz, um Injector-Instanz nicht zwischen Threads zu teilen
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

                // Worker für PINCH
                Task.Run(async () =>
                {
                    // Writing-Instanz nur für pinch helper (simulatePinch benötigt keinen internen injector)
                    Writing writingPinch = new Writing();

                    await foreach (var msg in pinchChannel.Reader.ReadAllAsync())
                    {
                        try
                        {
                            if (_injector == null)
                            {
                                Console.WriteLine("PINCH empfangen, aber kein InputInjector verfügbar. Vorgang übersprungen.");
                                continue;
                            }

                            int x1 = int.Parse(msg[1]);
                            int x2 = int.Parse(msg[2]);
                            int y1 = int.Parse(msg[3]);
                            int y2 = int.Parse(msg[4]);

                            writingPinch.simulatePinch(_injector, x1, x2, y1, y2);
                            Console.WriteLine($"{x1} {x2} {y1} {y2}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Fehler im PINCH-Worker: " + ex.Message);
                        }
                    }
                });

                Writing writing = new Writing();
                UdpClient getInputData = new UdpClient(DRAWING_PORT);
                IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Any, 0);

                Console.WriteLine($"Zeichen-Dienst auf Port {DRAWING_PORT} gestartet. Warte auf Daten...");

                while (true)
                {
                    try
                    {
                        byte[] msg_byte = getInputData.Receive(ref clientEndpoint);
                        string[] msg = Encoding.UTF8.GetString(msg_byte).Split(';');

                        if (msg.Length == 0)
                            continue;

                        string getAction = msg[0];

                        // Anstatt direkt auszuführen, verarbeite in dedizierten Worker-Queues
                        if (getAction.Equals("PINCH"))
                        {
                            // dispatch to pinch worker
                            if (!pinchChannel.Writer.TryWrite(msg))
                            {
                                Console.WriteLine("PINCH-Nachricht konnte nicht in die Queue geschrieben werden.");
                            }
                        }
                        else if (getAction.Equals("HOTKEY"))
                        {
                            if (!hotkeyChannel.Writer.TryWrite(msg))
                            {
                                Console.WriteLine("HOTKEY-Nachricht konnte nicht in die Queue geschrieben werden.");
                            }
                        }
                        else if (getAction.Equals("CLICK") || getAction.Equals("HOVER"))
                        {
                            if (!clickChannel.Writer.TryWrite(msg))
                            {
                                Console.WriteLine("CLICK/HOVER-Nachricht konnte nicht in die Queue geschrieben werden.");
                            }
                        }
                        else
                        {
                            // Alle anderen Pen-Aktionen ebenfalls an den Click-Worker
                            if (!clickChannel.Writer.TryWrite(msg))
                            {
                                Console.WriteLine("Pen-Nachricht konnte nicht in die Queue geschrieben werden.");
                            }
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