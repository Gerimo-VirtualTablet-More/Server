using System;
using System.Diagnostics;
using Windows.UI.Input.Preview.Injection;

namespace cnnc
{
    public class Writing
    {
        private InputInjector injector;

        static InjectedInputTouchInfo[] pointerDown = new InjectedInputTouchInfo[2];
        static InjectedInputTouchInfo[] pointerUp = new InjectedInputTouchInfo[2];

        // globaler Lock, um gleichzeitige InjectTouchInput-Aufrufe zu serialisieren
        private static readonly object injectLock = new object();

        public Writing()
        {
            injector = InputInjector.TryCreate();
            if (injector == null)
            {
                Debug.WriteLine("InputInjector konnte nicht erstellt werden.");
            }

            pointerDown[0] = new InjectedInputTouchInfo();
            pointerDown[1] = new InjectedInputTouchInfo();
            pointerUp[0] = new InjectedInputTouchInfo();
            pointerUp[1] = new InjectedInputTouchInfo();
        }

        public void simulatePenTap(string action, int x, int y, float pressure )
        {
            if (action.Equals("HOVER"))
            {
                InjectedInputPenInfo hoverInjector = new InjectedInputPenInfo
                {
                    PointerInfo = new InjectedInputPointerInfo
                    {
                        PointerOptions = InjectedInputPointerOptions.InRange,
                        PixelLocation = new InjectedInputPoint
                        {
                            PositionX = x,
                            PositionY = y
                        }
                    },
                };

                injector.InjectPenInput(hoverInjector);
            }
            else if (action.Equals("CLICK"))
            {
                InjectedInputPenInfo clickInjector = new InjectedInputPenInfo
                {
                    PointerInfo = new InjectedInputPointerInfo
                    {
                        PointerOptions = InjectedInputPointerOptions.InContact,
                        PixelLocation = new InjectedInputPoint
                        {
                            PositionX = x,
                            PositionY = y
                        }
                    },

                    Pressure = pressure,
                    PenParameters = InjectedInputPenParameters.Pressure
                };

                injector.InjectPenInput(clickInjector);
            }
        }

        public void simulatePenTap(string action, int x, int y, float pressure,int tiltX,int tiltY)
        {
            if (action.Equals("HOVER"))
            {
                InjectedInputPenInfo hoverInjector = new InjectedInputPenInfo
                {
                    PointerInfo = new InjectedInputPointerInfo
                    {
                        PointerOptions = InjectedInputPointerOptions.InRange,
                        PixelLocation = new InjectedInputPoint
                        {
                            PositionX = x,
                            PositionY = y
                        }
                    },
                };

                injector.InjectPenInput(hoverInjector);
            }
            else if (action.Equals("CLICK"))
            {
                InjectedInputPenInfo clickInjector = new InjectedInputPenInfo
                {
                    PointerInfo = new InjectedInputPointerInfo
                    {
                        PointerOptions = InjectedInputPointerOptions.InContact,
                        PixelLocation = new InjectedInputPoint
                        {
                            PositionX = x,
                            PositionY = y
                        }
                    }, TiltX = tiltX, TiltY = tiltY,

                    Pressure = pressure,
                    PenParameters = InjectedInputPenParameters.Pressure
                };

                injector.InjectPenInput(clickInjector);
            }
        }

        private bool isPinching = false;
        private int lastX1, lastY1, lastX2, lastY2;

        // Overload, nutzt die Instanz-eigene Injector-Instanz (empfohlen für Worker mit eigener Writing-Instanz)
        public void simulatePinch(int x1, int x2, int y1, int y2)
        {
            simulatePinch(this.injector, x1, x2, y1, y2);
        }

        public void simulatePinch(InputInjector injector, int x1, int x2, int y1, int y2)
        {
            lock (injectLock)
            {
                // Ende der Geste
                if (x1 == 0 && x2 == 0 && y1 == 0 && y2 == 0)
                {
                    if (isPinching)
                    {
                        var pointerUp = new InjectedInputTouchInfo[2];
                        pointerUp[0] = new InjectedInputTouchInfo
                        {
                            PointerInfo = new InjectedInputPointerInfo
                            {
                                PointerId = 1,
                                PixelLocation = new InjectedInputPoint(lastX1, lastY1),
                                PointerOptions = InjectedInputPointerOptions.PointerUp
                            }
                        };
                        // Korrigierte Koordinaten: lastX2, lastY2
                        pointerUp[1] = new InjectedInputTouchInfo
                        {
                            PointerInfo = new InjectedInputPointerInfo
                            {
                                PointerId = 2,
                                PixelLocation = new InjectedInputPoint(lastX2, lastY2),
                                PointerOptions = InjectedInputPointerOptions.PointerUp
                            }
                        };
                        injector.InjectTouchInput(pointerUp);
                        isPinching = false;
                    }
                    return;
                }

                var pointers = new InjectedInputTouchInfo[2];
                if (!isPinching)
                {
                    isPinching = true;
                    pointers[0] = new InjectedInputTouchInfo
                    {
                        PointerInfo = new InjectedInputPointerInfo
                        {
                            PointerId = 1,
                            PixelLocation = new InjectedInputPoint(x1, y1),
                            PointerOptions = InjectedInputPointerOptions.PointerDown | InjectedInputPointerOptions.InContact
                        }
                    };
                    pointers[1] = new InjectedInputTouchInfo
                    {
                        PointerInfo = new InjectedInputPointerInfo
                        {
                            PointerId = 2,
                            PixelLocation = new InjectedInputPoint(x2, y2),
                            PointerOptions = InjectedInputPointerOptions.PointerDown | InjectedInputPointerOptions.InContact
                        }
                    };
                }
                else
                {
                    pointers[0] = new InjectedInputTouchInfo
                    {
                        PointerInfo = new InjectedInputPointerInfo
                        {
                            PointerId = 1,
                            PixelLocation = new InjectedInputPoint(x1, y1),
                            PointerOptions = InjectedInputPointerOptions.Update | InjectedInputPointerOptions.InContact
                        }
                    };
                    pointers[1] = new InjectedInputTouchInfo
                    {
                        PointerInfo = new InjectedInputPointerInfo
                        {
                            PointerId = 2,
                            PixelLocation = new InjectedInputPoint(x2, y2),
                            PointerOptions = InjectedInputPointerOptions.Update | InjectedInputPointerOptions.InContact
                        }
                    };
                }

                injector.InjectTouchInput(pointers);

                lastX1 = x1; lastY1 = y1; lastX2 = x2; lastY2 = y2;
            }
        }

        // --- NEUE METHODEN ---

        public void simulateMouseMove(int x, int y)
        {
         

            var moveInjector = new InjectedInputMouseInfo
            {
                MouseOptions = InjectedInputMouseOptions.Move | InjectedInputMouseOptions.Absolute,
                DeltaX = x,
                DeltaY = y
            };

            injector.InjectMouseInput(new[] { moveInjector });
        }


        public void simulateMouseClick(int button)
        {
            var clickInjector = new InjectedInputMouseInfo();
            if (button == 1) // Links-Klick
            {
                clickInjector.MouseOptions = InjectedInputMouseOptions.LeftDown;
                injector.InjectMouseInput(new[] { clickInjector });
                clickInjector.MouseOptions = InjectedInputMouseOptions.LeftUp;
                injector.InjectMouseInput(new[] { clickInjector });
            }
            else if (button == 2) // Rechts-Klick
            {
                clickInjector.MouseOptions = InjectedInputMouseOptions.RightDown;
                injector.InjectMouseInput(new[] { clickInjector });
                clickInjector.MouseOptions = InjectedInputMouseOptions.RightUp;
                injector.InjectMouseInput(new[] { clickInjector });
            }
        }
    }
}