using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotGenerator
{
    public interface IWorker
    {
        Task DoWork(CancellationToken cancellationToken);
        int getX();

    }
}
