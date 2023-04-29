namespace TestApplication
{
    internal class Program
    {
        /// <summary>
        /// Выводит информацию после запуска приложения.
        /// </summary>
        static void PrintStartScreen()
        {
           var lines =  new List<string>
           {
               "-----------------Test screen shot service-----------------",
               "Input test number and press enter.",
               "",
               "1: Begin one thread request test.",
               "2: Convert phpMyAdmin file.",
               "3: Exit.",
           };
            
           foreach (var line in lines) Console.WriteLine(line);
        }

        static void Main(string[] args)
        {
            PrintStartScreen();
        }
    }
}