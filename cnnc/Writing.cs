using System.Diagnostics;
using Windows.UI.Input.Preview.Injection;

namespace cnnc
{
    public class Writing
    {
        private InputInjector injector;

     

        public Writing()
        {
            injector = InputInjector.TryCreate();
            if (injector == null)
            {
                Debug.WriteLine("InputInjector konnte nicht erstellt werden.");
            }
           

           
            
        }

        public void simulatePenTap(string action, int x, int y, float pressure)
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
            else if(action.Equals("CLICK"))
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
    }
}
