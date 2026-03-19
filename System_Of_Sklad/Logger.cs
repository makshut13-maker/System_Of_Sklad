using System;
using System.IO;

namespace Sklad_System
{
    public static class Logger
    {
        private static string logFile = "actions.log";
        private static Database db = new Database();

        // Запись действия (и в БД, и в файл)
        public static void Log(string пользователь, string действие)
        {
            try
            {
                // 1. Запись в файл
                string запись = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {пользователь}: {действие}";
                File.AppendAllText(logFile, запись + Environment.NewLine);

                // 2. Запись в БД
                db.ДобавитьВЖурнал(пользователь, действие);
            }
            catch (Exception ex)
            {
                // Если не можем записать - хотя бы в консоль выведем
                Console.WriteLine($"Ошибка логирования: {ex.Message}");
            }
        }

        // Запись ошибки
        public static void Log(string пользователь, string действие, Exception ex)
        {
            Log(пользователь, $"{действие}. Ошибка: {ex.Message}");
        }
    }
}