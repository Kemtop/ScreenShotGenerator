using System;

namespace TestServices
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Tests test = new Tests();
            test.run();

            Console.ReadKey();
        }
    }
}
