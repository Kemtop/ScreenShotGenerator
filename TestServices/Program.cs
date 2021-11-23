using System;

namespace TestServices
{
    class Program
    {
        static void Main(string[] args)
        {

            Console.WriteLine("-----------------Test screen shot service-----------------");
            Console.WriteLine("Input test number and press enter.");
            Console.WriteLine();
            Console.WriteLine("1:Begin one thread request test.");
    
            // Console.WriteLine("2:Multi thread.");

            //Автоматически запускать тест.
            if (args.Length != 0)
            {
                Tests test1 = new Tests();
                test1.runTest1(args);
                return;
            }
                


            int res = 0;
            //Ждем пока пользователь введет число.
            while (true)
            {
                string key = Console.ReadLine();
        
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
                case 1: test.runTest1(args); break;

                default: break;
            }

                        
        }
    }
}
