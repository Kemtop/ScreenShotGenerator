using ScreenShotGenerator.Services.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Services
{
    /// <summary>
    /// Делегат для события по превышению лимита swap.
    /// </summary>
    /// <returns></returns>
    public delegate void swapLimit(int browserId);


    /// <summary>
    /// Набор функций для мониторинга свопа.
    /// </summary>
    public class SwapMonitor
    {
        //Имена процессов драйвера, браузера.
        private static string[] browserProcesseNames = { "GeckoMain", "Web Content" }; //geckodriver
        //private static string[] browserProcesseNames = { "netdata", "chronyd" };
        /// <summary>
        /// Лимит свопа для процесса, после которого генерируется событие.
        /// </summary>
         private static UInt32 swapLimit = 50000;//~100Мб 

        /// <summary>
        /// Интервал мониторинга.
        /// </summary>
        private static int monitoringInterval  = 30000;//1m

        /// <summary>
        /// Таймер запускающий задачу проверки необходимости очистки кеша.
        /// </summary>
        private Timer timerPeriodMonitoring;

        /// <summary>
        /// Информация о процессах.
        /// </summary>
        private List<mPidInfo> SystemctlInfo;

        /// <summary>
        /// Блокировка для много поточной работы.
        /// </summary>
        private object lockSystemctlInfo;

        /// <summary>
        ///Cобытие по превышению лимита swap.
        /// </summary>
        /// <returns></returns>
        public event swapLimit eventSwapLimit;
        public SwapMonitor()
        {
            SystemctlInfo = new List<mPidInfo>();
            lockSystemctlInfo = new object();
        }


        /// <summary>
        /// Вызывает команду systemctl status, сохраняет информацию о текущих процессах.
        /// </summary>
        public void SaveCurentPids()
        {
            List<mPidInfo> info = getSystemctlInfo();
            SystemctlInfo = info;
        }

        /// <summary>
        /// Запускает мониторинг использования swap браузерами.
        /// </summary>
        public void runMonitoring()
        {
            timerPeriodMonitoring = new Timer((Object stateInfo) =>
            {
                Monitor();
            }, null, monitoringInterval, monitoringInterval);
        }


        /// <summary>
        /// Получаю информацию о текущих процессах.
        /// </summary>
        /// <returns></returns>
        private List<mPidInfo> getSystemctlInfo()
        {
            string ret = "";
            try
            {
                string command = "systemctl status screenShotService"; 
                ret = runCommand(command);
 
                List<mPidInfo> SystemctlLines = ParceSystemctlAnswer(ret);
                return SystemctlLines;
            }
            catch (Exception ex)
            {
                Log.Information("Exception in getSystemctlInfo(SwapMonitor):" + ex.Message+";answer="+ret);
                return null;
            }
        }

        /// <summary>
        /// Тестовый метод. Считывает из файла ответ системы systemctl status screenShotService 
        /// и передает на вход getSystemctlInfo().
        /// </summary>
        public void TestGetSystemctlInfo()
        {
            //Файл с ответом systemctl status screenShotService в правильной кодировке(Unix(LF)).
            string path = @"SwapTest.txt"; //Должен находиться в корне проекта.
            string readText = File.ReadAllText(path);
            List<mPidInfo> SystemctlLines = ParceSystemctlAnswer(readText);
            int y = 0;
        }


        /// <summary>
        /// Считывает и сохраняет PID процессов драйвера.
        /// </summary>
        public bool getDriverPids(int browserId)
        {
            List<mPidInfo> info = getSystemctlInfo();
            if (info == null) return false; 

            //Возвращает новые элементы из info которых нет в SystemctlInfo.
            IEnumerable<mPidInfo> whichAreNot = null;
            lock (lockSystemctlInfo)
            {
                whichAreNot = info.Except(SystemctlInfo);
            }
            
            bool hasNew = false;
            if (whichAreNot.Count() > 0)
            {
                hasNew = true;
                Log.Information("--------Add new browser pids to monitoring.--------");
            }

            //Сохраняю новый.
            foreach (mPidInfo p in whichAreNot)
            {
                p.browserId = browserId;
                lock (lockSystemctlInfo)
                {
                    SystemctlInfo.Add(p);
                }
                Log.Information("browserId=" + browserId.ToString() + ";pid:" + p.pid.ToString() + " " + p.sysctlInfo);
            }

            if (hasNew)
                Log.Information("----------------");

            return true;
        }

        /// <summary>
        /// Удаляет все сведения о pid процессов для данного браузера.
        /// </summary>
        /// <param name="browserId"></param>
        public void removePid(int browserId)
        {
            lock (lockSystemctlInfo)
            {
                SystemctlInfo.RemoveAll(p => p.browserId == browserId);
            }
        }


        /// <summary>
        /// Отладочный метод. Выводит в лог текущую информацию о процессах.
        /// </summary>
        public void showInfo()
        {
            lock(lockSystemctlInfo)
            {
                foreach (mPidInfo l in SystemctlInfo)
                {
                    Log.Information(l.pid.ToString() + "#" + l.browserId.ToString() + "#" + l.sysctlInfo);
                }
            }           
        }

        /// <summary>
        /// Преобразовывает ответ systemctl status в список.
        /// </summary>
        /// <param name="answ"></param>
        private List<mPidInfo> ParceSystemctlAnswer(string answ)
        {
            char ch211 = (char)9500;  //символ ├ 9500
            char ch208 = (char)9492; //символ └ 9492
                                     //символ - 9472

            //Правильный ли ответ команды? есть ли такие символы.
            int pos = answ.IndexOf(ch211);

            if (pos == -1)
            {
                Log.Information("Bad answer format!");
                return null; //Не верный формат.
            }
            //answ = answ.Substring(pos+1); //Беру все после первого символа.

            List<mPidInfo> Lines = new List<mPidInfo>();
            bool wait = true;
            int waitCnt = 0;
            int posLineEnd;
            while (wait)
            {
                //Антизависалка.
                if (waitCnt > 300)
                {
                    Log.Information("Bad command answer!");
                    return null;
                }

                int pos211 = answ.IndexOf(ch211);
                int pos208 = answ.IndexOf(ch208);

                //Еще есть строки.
                if (pos211 != -1)
                {
                    answ = answ.Substring(pos211 + 1); //Беру строку после символа.
                    posLineEnd = answ.IndexOf('\n'); //Ищу конец строки.
                    string tmp = answ.Substring(1, posLineEnd - 1); //Копирую строку с данными.

                    Lines.Add(toPidInfo(tmp));
                }
                else
                {
                    //Конец блока данных
                    if (pos208 != -1)
                    {
                        answ = answ.Substring(pos208 + 1); //Беру строку после символа.
                        posLineEnd = answ.IndexOf('\n'); //Ищу конец строки.
                        string tmp = answ.Substring(1, posLineEnd - 1); //Копирую строку с данными.
                        Lines.Add(toPidInfo(tmp));
                        break;
                    }
                }

                waitCnt++;
            }

            return Lines;
        }


        /// <summary>
        /// Преобразовывает информационную строку Linux systemctl status.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        private mPidInfo toPidInfo(string info)
        {
            //В некоторых случаях(4цифры в номере процесса) строка дополняется пробелами в начале.
            info = info.Trim();
            mPidInfo m = new mPidInfo();
            int pos = info.IndexOf(' ');
            string pid = info.Substring(0, pos);
            string str = info.Substring(pos + 1);

            m.pid = Convert.ToUInt32(pid);
            m.sysctlInfo = str;
            return m;
        }



        /// <summary>
        /// Выполняет команду на linux системе.
        /// </summary>
        private string runCommand(string command)
        {          
            string result = "";
            using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.FileName = "/bin/bash";
                proc.StartInfo.Arguments = "-c \" " + command + " \"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.Start();

                result += proc.StandardOutput.ReadToEnd();
                result += proc.StandardError.ReadToEnd();

                proc.WaitForExit();
            }
            return result;
        }


        /// <summary>
        /// Получает информацию о программах использующих swap.
        /// </summary>
        public List<mSwapInfo> GetSwapInfo()
        {
            //Информация об использовании процессами swap.
            List<mSwapInfo> swapData = new List<mSwapInfo>();
            try
            {
                string ret=runCommand("./swapMonitoring");               
                //Удаляю шапку.
                ret = clearTitleSwapInfo(ret);
                string[] Lines = ret.Split('\n');

                foreach (string ln in Lines)
                {
                    string line = ln.Trim();
                    if (String.IsNullOrEmpty(line)) continue; //Пропуск пустой строки.

                    //Есть ли в свопе наши процессы?
                    foreach (string procName in browserProcesseNames)
                    {
                        TherapyScriptOutPut(ref line); //Лечение аномалии.
                        if (line.Contains(procName))
                        {
                            string pid = "";
                            string swap = "";

                            //Обрабатываю строку ответа. Сохраняю если есть данные.
                            if (getPidFromSwapInfo(ref line, procName, ref pid, ref swap))
                            {
                                mSwapInfo sI = new mSwapInfo();
                                sI.pid = Convert.ToUInt32(pid);
                                sI.swap = Convert.ToUInt32(swap);
                                sI.name = procName;
                                swapData.Add(sI);
                            }

                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Information("Exception in GetSwapInfo(SwapMonitor):" + ex.Message);
                return null;
            }

            //Нет данных.
            if (swapData.Count == 0) return null;

            return swapData;
        }

        //


        /// <summary>
        /// Удаляет шапку вывода.
        /// </summary>
        /// <param name="ret"></param>
        /// <returns></returns>
        private string clearTitleSwapInfo(string ret)
        {
            int pos = ret.IndexOf("SWAP");
            return ret.Substring(pos + 4);
        }

        /// <summary>
        /// Лечение аномальных пробелов в выводе Web              Content258579  337680  kB.
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private void TherapyScriptOutPut(ref string line)
        {
            if(line.Contains("Web")&& line.Contains("Content"))
            {
                int pos = line.IndexOf("Content");
                string tmp = line.Substring(pos);//Отрезаю значения.
                line = "Web " + tmp; //Превращаю строку в нормальный вид.
            }

        }


        /// <summary>
        /// Из строки получает данные.
        /// </summary>
        /// <param name="answ"></param>
        /// <param name="procName"></param>
        /// <param name="pid"></param>
        /// <param name="swap"></param>
        private bool getPidFromSwapInfo(ref string answ, string procName, ref string pid, ref string swap)
        {
            //В ответе символы могут слипаться.Берем строку после имени процесса,если он есть.
            int pos = answ.IndexOf(procName);
            if (pos == -1) return false;

            //Вырезаем нужное нам значение после имени процесса.
            //Если название процесса влазит-идут пробелы.
            answ = answ.Substring(pos + procName.Length).Trim();

            //Ищем конец PID id.
            pos = answ.IndexOf(' ');
            pid = answ.Substring(0, pos); //PID
            answ = answ.Substring(pos + 1); //Вырезаем строку после.
            pos = answ.IndexOf("kB");
            swap = answ.Substring(0, pos).Trim();

            return true;
        }

        /// <summary>
        /// Мониторит состояние swap.
        /// </summary>
        private void Monitor()
        {
            //Получаю информацию о занимаемом пространстве swap процессами.
            List<mSwapInfo> swapData = GetSwapInfo();
            if (swapData == null) return;
            
            //Проверка лимитов.
            foreach (mSwapInfo p in swapData)
            {
                if(p.swap> swapLimit)
                {
                    //Ищем процесс с указанным pid.
                    mPidInfo process;
                    lock (lockSystemctlInfo)
                    {
                      process = SystemctlInfo.FirstOrDefault(x => x.pid == p.pid);
                    }
                        
                    if(process==null)
                    {
                        Log.Error("SwapMonitor:Can't found browser with pid=" + p.pid.ToString()+".");
                        continue;
                    }

                    Log.Information("Swap limit for browserId=" + process.browserId.ToString()+
                        ",process("+p.pid.ToString()+")="+p.name+",swap usage(Kb)="+p.swap+".");
                    //Генерирую событие.
                    eventSwapLimit(process.browserId);                    
                }
                   
            }
        }

        /// <summary>
        /// Работают ли какие либо процессы браузера?
        /// </summary>
        /// <param name="browseId"></param>
        public bool hasAnyProcess(int browseId)
        {
            //Узнаю все pid для данного браузера.
            List<mPidInfo> bprocesses;
            lock (lockSystemctlInfo)
            {
               bprocesses = SystemctlInfo.Where(x => x.browserId == browseId).ToList();
            }

            if (bprocesses.Count()==0)
            {
                Log.Error("Can't find any pid for browser "+browseId.ToString()+ " in SystemctlInfo.");
                return false;
            }

            List<mPidInfo> info = getSystemctlInfo(); //Получаю все текущие процессы.

            //Количество процессов браузера которых нет в info. Т.е. остановленных.       
            int pCount=bprocesses.Except(info).Count();

            //Все процессы браузера остановлены.
            if (bprocesses.Count() == pCount) return true;

            return false;
        }
    }
}
