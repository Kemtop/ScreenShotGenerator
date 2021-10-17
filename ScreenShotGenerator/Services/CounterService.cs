using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services
{
    public class CounterService
    {
        public int y;
        protected internal ICounter Counter { get; }

        public CounterService(ICounter counter)
        {
            Counter = counter;
        }
      

    }
}
