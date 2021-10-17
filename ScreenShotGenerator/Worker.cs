using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotGenerator
{
    public class Worker : IWorker
    {
        private readonly ILogger<Worker> logger;
        private int number = 0;

        public Worker(ILogger<Worker> logger)
        {
            this.logger = logger;
        }

        public async Task DoWork(CancellationToken cancellationToken)
        {
            Thread thread1 = new Thread(()=>{

                while (true)
                {
                   // logger.LogInformation("Hello_1");
                    Task.Delay(3000);
                }
                
            });
            thread1.Start();

            Thread thread2 = new Thread(() => {

                while (true)
                {
                    logger.LogInformation("Hello_2");
                    Task.Delay(1500);
                }

            });
            thread2.Start();

            Thread thread3 = new Thread(() => {

                while (true)
                {
                    logger.LogInformation("Hello_3_");
                    Task.Delay(1000);
                }

            });
            thread3.Start();


            while (!cancellationToken.IsCancellationRequested)
            {
                Interlocked.Increment(ref number);
                logger.LogInformation($"Worker printing number {number}");
                await Task.Delay(1000 * 5);
            }
        }

        public int getX()
        {
            return number;
        }
    }
}
