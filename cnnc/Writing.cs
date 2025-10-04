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

        public void simulatePenTap(int x, int y, float pressure)
        {

            InjectedInputPenInfo injectedInputPenInfo = new InjectedInputPenInfo()
            {
                PointerInfo = new InjectedInputPointerInfo()
                {
                    PointerOptions = InjectedInputPointerOptions.InContact ,
                    PixelLocation = new InjectedInputPoint()
                    {
                        PositionX = x,
                        PositionY = y   
                    }
                    
                },


                Pressure = pressure,
                PenParameters = InjectedInputPenParameters.Pressure


            };

            injector.InjectPenInput(injectedInputPenInfo);






        }
    }
}
