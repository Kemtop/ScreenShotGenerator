using System;

namespace TestServices
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Test screen shot service.");
            Console.WriteLine("1:Begin one thread big request(100) per iteration.");
            Console.WriteLine("2:Multi thread.");

            int res = 0;
            while (true)
            {
                string key = Console.ReadKey().KeyChar.ToString();
        
              if(Int32.TryParse(key, out res))
               {
                   break;
                }
              else
                {
                    Console.WriteLine("Error value!");
                }

            }


            Tests test = new Tests();
            switch(res)
            {
                case 1: test.Test1(); break;
                    
            }
            // test.run();

            
        }
    }
}
