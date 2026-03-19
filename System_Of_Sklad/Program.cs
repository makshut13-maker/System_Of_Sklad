using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;

namespace Sklad_System
{
    class Program
    {
        static Database db = new Database();
        static Пользователь текущийПользователь = null;

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "Система управления складом";

            try
            {
                db.СоздатьТаблицы();
                Авторизация();

                if (текущийПользователь != null)
                {
                    ГлавноеМеню();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Критическая ошибка: {ex.Message}");
                Console.WriteLine("Проверьте подключение к БД!");
            }
        }

        static void Авторизация()
        {
            Console.Clear();
            Console.WriteLine("=== АВТОРИЗАЦИЯ ===");
            Console.WriteLine("Есть два логина:");
            Console.WriteLine("admin - полный доступ");
            Console.WriteLine("worker - ограниченный доступ");
            Console.Write("Логин: ");
            string логин = Console.ReadLine();

            текущийПользователь = db.Войти(логин);

            if (текущийПользователь == null)
            {
                Console.WriteLine("Пользователь не найден!");
                Console.WriteLine("Доступны тестовые логины: admin, worker");
                Console.WriteLine("Нажмите любую клавишу...");
                Console.ReadKey();
                Авторизация();
            }
            else
            {
                Console.WriteLine($"Добро пожаловать, {текущийПользователь.Идентификатор}!");
                Console.WriteLine($"Ваша роль: {текущийПользователь.Роль_Пользователя}");
                System.Threading.Thread.Sleep(1000);
            }
        }

        static void ГлавноеМеню()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine($"=== СКЛАДСКАЯ СИСТЕМА ===");
                Console.WriteLine($"Пользователь: {текущийПользователь.Идентификатор} ({текущийПользователь.Роль_Пользователя})");
                Console.WriteLine("==========================");

                Console.WriteLine("1. Приемка товара");
                Console.WriteLine("0. Выход");
                Console.Write("Выберите действие: ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1": Приемка(); break;
                    case "0":
                        return;
                }
            }
        }

        static void Приемка()
        {
            Console.Clear();
            Console.WriteLine("=== ПРИЕМКА ТОВАРА ===");

            try
            {
                var товары = db.ВсеТовары();
                if (товары.Count == 0)
                {
                    Console.WriteLine("Сначала добавьте товары!");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine("\nДоступные товары:");
                foreach (var т in товары)
                {
                    Console.WriteLine($"{т.Номер_товара}. {т.Название} (ср.цена: {т.Средняя_рыночная_цена} руб.)");
                }

                Console.Write("\nВыберите номер товара: ");
                if (!int.TryParse(Console.ReadLine(), out int номерТовара))
                {
                    Console.WriteLine("Ошибка ввода!");
                    Console.ReadKey();
                    return;
                }

                var выбранныйТовар = товары.FirstOrDefault(t => t.Номер_товара == номерТовара);
                if (выбранныйТовар == null)
                {
                    Console.WriteLine("Товар не найден!");
                    Console.ReadKey();
                    return;
                }

                Console.Write("Цена закупки: ");
                if (!decimal.TryParse(Console.ReadLine(), out decimal цена))
                {
                    Console.WriteLine("Ошибка ввода!");
                    Console.ReadKey();
                    return;
                }

                if (!db.ПроверитьЦену(номерТовара, цена))
                {
                    Console.WriteLine($"Ошибка: Цена закупки не может быть выше средней более чем на 10%!");
                    Console.WriteLine($"Максимальная цена: {выбранныйТовар.Средняя_рыночная_цена * 1.1m} руб.");
                    Console.ReadKey();
                    return;
                }

                Console.Write("Срок годности (ГГГГ-ММ-ДД): ");
                if (!DateTime.TryParse(Console.ReadLine(), out DateTime срок))
                {
                    Console.WriteLine("Ошибка ввода даты!");
                    Console.ReadKey();
                    return;
                }

                Console.Write("Количество: ");
                if (!int.TryParse(Console.ReadLine(), out int количество) || количество <= 0)
                {
                    Console.WriteLine("Ошибка ввода количества!");
                    Console.ReadKey();
                    return;
                }

                var партия = new Партия
                {
                    Номер_товара = номерТовара,
                    Срок_годности = срок,
                    Цена_закупки = цена,
                    Количество = количество,
                    Активна = true
                };

                db.ДобавитьПартию(партия);
                Console.WriteLine("✓ Товар успешно принят на склад!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }

            Console.ReadKey();
        }
    }
}