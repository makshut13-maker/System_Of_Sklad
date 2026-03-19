using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
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

                // АВТОМАТИЧЕСКАЯ ОЧИСТКА ПРИ ЗАПУСКЕ
                int очищено = db.ОчиститьПросроченныеПартии();
                if (очищено > 0)
                {
                    Console.WriteLine($"Автоматически очищено просроченных партий: {очищено}");
                }

                Авторизация();

                if (текущийПользователь != null)
                {
                    Logger.Log(текущийПользователь.Идентификатор, "Вход в систему");
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
                Console.WriteLine("2. Списание товара");
                Console.WriteLine("3. Показать остатки");

                if (текущийПользователь.Роль_Пользователя == "Менеджер")
                {
                    Console.WriteLine("4. Управление товарами");
                    Console.WriteLine("5. Отчет по истекающим срокам");
                    Console.WriteLine("6. Просмотр логов");
                    Console.WriteLine("7. Очистка просроченных товаров");
                }

                Console.WriteLine("0. Выход");
                Console.Write("Выберите действие: ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1": Приемка(); break;
                    case "2": Списание(); break;
                    case "3": ПоказатьОстатки(); break;
                    case "4": if (текущийПользователь.Роль_Пользователя == "Менеджер") УправлениеТоварами(); break;
                    case "5": if (текущийПользователь.Роль_Пользователя == "Менеджер") ОтчетПоСрокам(); break;
                    case "6": if (текущийПользователь.Роль_Пользователя == "Менеджер") ПросмотрЛогов(); break;
                    case "7": if (текущийПользователь.Роль_Пользователя == "Менеджер") ОчисткаПросроченных(); break;
                    case "0":
                        Logger.Log(текущийПользователь.Идентификатор, "Выход из системы");
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
                    Logger.Log(текущийПользователь.Идентификатор, "Ошибка при приемке: неверный формат");
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
                    Logger.Log(текущийПользователь.Идентификатор, $"Отказ в приемке: цена {цена} выше допустимой");
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
                Logger.Log(текущийПользователь.Идентификатор, $"Приемка: {выбранныйТовар.Название}, {количество} шт, цена {цена} руб.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                Logger.Log(текущийПользователь.Идентификатор, "Ошибка при приемке", ex);
            }

            Console.ReadKey();
        }

        static void Списание()
        {
            Console.Clear();
            Console.WriteLine("=== СПИСАНИЕ ТОВАРА ===");

            try
            {
                var товары = db.ВсеТовары();
                if (товары.Count == 0)
                {
                    Console.WriteLine("Нет товаров!");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine("\nДоступные товары:");
                foreach (var т in товары)
                {
                    Console.WriteLine($"{т.Номер_товара}. {т.Название}");
                }

                Console.Write("\nВыберите номер товара: ");
                if (!int.TryParse(Console.ReadLine(), out int номерТовара))
                {
                    Console.WriteLine("Ошибка ввода!");
                    Console.ReadKey();
                    return;
                }

                Console.Write("Сколько списать: ");
                if (!int.TryParse(Console.ReadLine(), out int нужноКоличество) || нужноКоличество <= 0)
                {
                    Console.WriteLine("Ошибка ввода!");
                    Console.ReadKey();
                    return;
                }

                var партии = db.ПартииДляСписания(номерТовара, нужноКоличество);

                if (партии.Count == 0)
                {
                    Console.WriteLine("Нет доступных партий для списания!");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine("\nПартии для списания (FEFO порядок):");
                int всего = 0;
                foreach (var п in партии)
                {
                    Console.WriteLine($"Партия {п.Номер_партии}: срок {п.Срок_годности:dd.MM.yyyy}, доступно {п.Количество} шт.");
                    всего += п.Количество;
                }

                if (всего < нужноКоличество)
                {
                    Console.WriteLine($"\nОшибка: на складе всего {всего} шт., а нужно {нужноКоличество}!");
                    Console.ReadKey();
                    return;
                }

                Console.Write("\nПодтвердите списание (y/n): ");
                if (Console.ReadLine().ToLower() != "y")
                {
                    Console.WriteLine("Списание отменено");
                    Console.ReadKey();
                    return;
                }

                int осталосьСписать = нужноКоличество;
                foreach (var партия in партии)
                {
                    if (осталосьСписать <= 0) break;

                    int списатьСПартии = Math.Min(партия.Количество, осталосьСписать);
                    db.СписатьТовар(партия.Номер_партии, списатьСПартии, текущийПользователь.Идентификатор);

                    осталосьСписать -= списатьСПартии;
                    Console.WriteLine($"Списано {списатьСПартии} шт. из партии {партия.Номер_партии}");
                }

                Console.WriteLine("✓ Списание завершено!");
                Logger.Log(текущийПользователь.Идентификатор, $"Списание: товар ID {номерТовара}, {нужноКоличество} шт.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                Logger.Log(текущийПользователь.Идентификатор, "Ошибка при списании", ex);
            }

            Console.ReadKey();
        }

        static void ПоказатьОстатки()
        {
            Console.Clear();
            Console.WriteLine("=== ОСТАТКИ НА СКЛАДЕ ===");
            Console.WriteLine();

            try
            {
                var остатки = db.ОстаткиПоПартиям();

                if (остатки.Count == 0)
                {
                    Console.WriteLine("Склад пуст");
                }
                else
                {
                    Console.WriteLine($"{"Товар",-20} {"Партия",-8} {"Срок годности",-15} {"Кол-во",-8} {"Статус",-15}");
                    Console.WriteLine(new string('-', 70));

                    foreach (var item in остатки)
                    {
                        if (item.Статус == "ПРОСРОЧЕНО")
                            Console.ForegroundColor = ConsoleColor.Red;
                        else if (item.Статус == "КРИТИЧЕСКИЙ")
                            Console.ForegroundColor = ConsoleColor.Magenta;
                        else if (item.Статус == "Скоро истекает")
                            Console.ForegroundColor = ConsoleColor.Yellow;
                        else
                            Console.ForegroundColor = ConsoleColor.Green;

                        Console.WriteLine($"{item.Товар,-20} {item.Номер_партии,-8} {item.Срок_годности:dd.MM.yyyy,-15} {item.Количество,-8} {item.Статус,-15}");

                        Console.ResetColor();
                    }
                }

                Logger.Log(текущийПользователь.Идентификатор, "Просмотр остатков");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                Logger.Log(текущийПользователь.Идентификатор, "Ошибка при просмотре остатков", ex);
            }

            Console.ReadKey();
        }

        static void УправлениеТоварами()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== УПРАВЛЕНИЕ ТОВАРАМИ ===");

                var товары = db.ВсеТовары();
                if (товары.Count > 0)
                {
                    Console.WriteLine("\nСписок товаров:");
                    foreach (var т in товары)
                    {
                        Console.WriteLine($"{т.Номер_товара}. {т.Название} - {т.Средняя_рыночная_цена} руб.");
                    }
                }

                Console.WriteLine("\n1. Добавить товар");
                Console.WriteLine("2. Удалить товар");
                Console.WriteLine("0. Назад");
                Console.Write("Выберите действие: ");

                string choice = Console.ReadLine();
                if (choice == "0") break;

                if (choice == "1")
                {
                    ДобавитьТовар();
                }
                else if (choice == "2")
                {
                    УдалитьТовар();
                }
            }
        }

        static void ДобавитьТовар()
        {
            Console.Clear();
            Console.WriteLine("=== ДОБАВЛЕНИЕ ТОВАРА ===");

            try
            {
                Console.Write("Название товара: ");
                string название = Console.ReadLine();

                Console.Write("Средняя рыночная цена: ");
                if (!decimal.TryParse(Console.ReadLine(), out decimal цена) || цена <= 0)
                {
                    Console.WriteLine("Ошибка ввода цены!");
                    Console.ReadKey();
                    return;
                }

                var товар = new Товар
                {
                    Название = название,
                    Средняя_рыночная_цена = цена
                };

                db.ДобавитьТовар(товар);
                Console.WriteLine("✓ Товар добавлен!");
                Logger.Log(текущийПользователь.Идентификатор, $"Добавлен товар: {название}, цена {цена} руб.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                Logger.Log(текущийПользователь.Идентификатор, "Ошибка при добавлении товара", ex);
            }

            Console.ReadKey();
        }

        static void УдалитьТовар()
        {
            Console.Clear();
            Console.WriteLine("=== УДАЛЕНИЕ ТОВАРА ===");

            var товары = db.ВсеТовары();
            if (товары.Count == 0)
            {
                Console.WriteLine("Нет товаров для удаления!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\nСписок товаров:");
            foreach (var т in товары)
            {
                Console.WriteLine($"{т.Номер_товара}. {т.Название}");
            }

            Console.Write("\nВведите номер товара для удаления: ");
            if (!int.TryParse(Console.ReadLine(), out int номерТовара))
            {
                Console.WriteLine("Ошибка ввода!");
                Console.ReadKey();
                return;
            }

            if (db.МожноУдалитьТовар(номерТовара))
            {
                db.УдалитьТовар(номерТовара);
                Console.WriteLine("✓ Товар удален!");
                Logger.Log(текущийПользователь.Идентификатор, $"Удален товар ID {номерТовара}");
            }
            else
            {
                Console.WriteLine("Ошибка: Нельзя удалить товар - есть активные партии!");
                Logger.Log(текущийПользователь.Идентификатор, $"Попытка удалить товар ID {номерТовара} с активными партиями");
            }

            Console.ReadKey();
        }

        static void ОтчетПоСрокам()
        {
            Console.Clear();
            Console.WriteLine("=== ТОВАРЫ С ИСТЕКАЮЩИМ СРОКОМ (7 ДНЕЙ) ===");
            Console.WriteLine();

            try
            {
                var истекающие = db.ИстекаетЧерез7Дней();

                if (истекающие.Count == 0)
                {
                    Console.WriteLine("Нет товаров с истекающим сроком");
                }
                else
                {
                    Console.WriteLine($"{"Товар",-20} {"Партия",-8} {"Срок",-12} {"Кол-во",-8} {"Дней",-6}");
                    Console.WriteLine(new string('-', 60));

                    foreach (var item in истекающие)
                    {
                        string дней = item.Статус;

                        if (int.TryParse(дней, out int днейОсталось) && днейОсталось <= 3)
                            Console.ForegroundColor = ConsoleColor.Red;
                        else
                            Console.ForegroundColor = ConsoleColor.Yellow;

                        Console.WriteLine($"{item.Товар,-20} {item.Номер_партии,-8} {item.Срок_годности:dd.MM.yyyy,-12} {item.Количество,-8} {дней,-6}");

                        Console.ResetColor();
                    }
                }

                Logger.Log(текущийПользователь.Идентификатор, "Просмотр отчета по срокам");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                Logger.Log(текущийПользователь.Идентификатор, "Ошибка при просмотре отчета", ex);
            }

            Console.ReadKey();
        }

        static void ПросмотрЛогов()
        {
            Console.Clear();
            Console.WriteLine("=== ЖУРНАЛ ДЕЙСТВИЙ ===");
            Console.WriteLine();

            try
            {
                var логи = db.ПоследниеЛоги(30);

                if (логи.Count == 0)
                {
                    Console.WriteLine("Журнал в БД пуст");
                }
                else
                {
                    Console.WriteLine("--- Логи из базы данных ---");
                    Console.WriteLine($"{"Время",-20} {"Пользователь",-15} {"Действие"}");
                    Console.WriteLine(new string('-', 80));

                    foreach (var лог in логи)
                    {
                        Console.WriteLine($"{лог.Время:yyyy-MM-dd HH:mm:ss,-20} {лог.Пользователь,-15} {лог.Действие}");
                    }
                }

                Console.WriteLine("\n--- Файловый лог (actions.log) ---");
                if (File.Exists("actions.log"))
                {
                    var lines = File.ReadAllLines("actions.log");

                    if (lines.Length == 0)
                    {
                        Console.WriteLine("Файл лога пуст");
                    }
                    else
                    {
                        Console.WriteLine($"Всего записей: {lines.Length}");
                        Console.WriteLine("Последние 15 записей:");
                        Console.WriteLine(new string('-', 80));

                        int startIndex = Math.Max(0, lines.Length - 15);
                        for (int i = startIndex; i < lines.Length; i++)
                        {
                            if (lines[i].Contains("Ошибка"))
                                Console.ForegroundColor = ConsoleColor.Red;

                            Console.WriteLine(lines[i]);

                            Console.ResetColor();
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Файл actions.log еще не создан");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при просмотре логов: {ex.Message}");
            }

            Console.WriteLine("\nНажмите любую клавишу...");
            Console.ReadKey();
        }

        static void ОчисткаПросроченных()
        {
            Console.Clear();
            Console.WriteLine("=== ОЧИСТКА ПРОСРОЧЕННЫХ ТОВАРОВ ===");
            Console.WriteLine();

            try
            {
                Console.WriteLine("ТЕКУЩИЕ ОСТАТКИ ДО ОЧИСТКИ:");
                Console.WriteLine($"{"Товар",-20} {"Партия",-8} {"Срок годности",-15} {"Кол-во",-8} {"Статус",-15}");
                Console.WriteLine(new string('-', 70));

                var остаткиДо = db.ОстаткиПоПартиям();
                foreach (var item in остаткиДо)
                {
                    if (item.Статус == "ПРОСРОЧЕНО")
                        Console.ForegroundColor = ConsoleColor.Red;
                    else if (item.Статус == "КРИТИЧЕСКИЙ")
                        Console.ForegroundColor = ConsoleColor.Magenta;
                    else if (item.Статус == "Скоро истекает")
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    else
                        Console.ForegroundColor = ConsoleColor.Green;

                    Console.WriteLine($"{item.Товар,-20} {item.Номер_партии,-8} {item.Срок_годности:dd.MM.yyyy,-15} {item.Количество,-8} {item.Статус,-15}");
                    Console.ResetColor();
                }

                Console.WriteLine("\n" + new string('=', 70));

                Console.WriteLine("\nПОИСК ПРОСРОЧЕННЫХ ПАРТИЙ...");
                var просроченные = db.GetПросроченныеПартии();

                if (просроченные.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✅ Просроченных товаров нет!");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"⚠️ Найдено просроченных партий: {просроченные.Count}");
                    Console.ResetColor();
                    Console.WriteLine();

                    Console.WriteLine($"{"№",-5} {"Товар",-20} {"Партия",-8} {"Срок годности",-15} {"Кол-во",-8}");
                    Console.WriteLine(new string('-', 60));

                    for (int i = 0; i < просроченные.Count; i++)
                    {
                        var п = просроченные[i];
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{i + 1,-5} {п.НазваниеТовара,-20} {п.Номер_партии,-8} {п.Срок_годности:dd.MM.yyyy,-15} {п.Количество,-8}");
                        Console.ResetColor();
                    }
                }

                Console.WriteLine();
                Console.WriteLine(new string('=', 70));
                Console.WriteLine("ВЫБЕРИТЕ ДЕЙСТВИЕ:");
                Console.WriteLine("1. Деактивировать все просроченные партии (рекомендуется)");
                Console.WriteLine("2. Полностью удалить все просроченные партии из БД");
                Console.WriteLine("3. Показать детальную информацию о просрочках");
                Console.WriteLine("4. Автоматическая очистка (деактивировать всё просроченное)");
                Console.WriteLine("0. Назад в меню");
                Console.Write("Ваш выбор: ");

                string choice = Console.ReadLine();
                Console.WriteLine();

                switch (choice)
                {
                    case "1":
                        Console.Write("Деактивировать все просроченные партии? (y/n): ");
                        if (Console.ReadLine().ToLower() == "y")
                        {
                            int деактивировано = db.ОчиститьПросроченныеПартии();

                            if (деактивировано > 0)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"✅ Деактивировано партий: {деактивировано}");
                                Console.ResetColor();
                                Logger.Log(текущийПользователь.Идентификатор, $"Деактивировано просроченных партий: {деактивировано}");
                            }
                            else
                            {
                                Console.WriteLine("Нет партий для деактивации");
                            }
                        }
                        break;

                    case "2":
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("⚠️ ВНИМАНИЕ! Это действие необратимо!");
                        Console.ResetColor();
                        Console.Write("Вы уверены, что хотите УДАЛИТЬ все просроченные партии? (y/n): ");

                        if (Console.ReadLine().ToLower() == "y")
                        {
                            Console.Write("Для подтверждения введите 'DELETE': ");
                            if (Console.ReadLine() == "DELETE")
                            {
                                int удалено = db.УдалитьПросроченныеПартии();
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"✅ Удалено партий: {удалено}");
                                Console.ResetColor();
                                Logger.Log(текущийПользователь.Идентификатор, $"Удалено просроченных партий: {удалено}");
                            }
                            else
                            {
                                Console.WriteLine("Операция отменена");
                            }
                        }
                        break;

                    case "3":
                        Console.WriteLine("\nДЕТАЛЬНАЯ ИНФОРМАЦИЯ О ПРОСРОЧКАХ:");

                        using (var conn = new SqlConnection(db.connectionString))
                        {
                            var детально = conn.Query(@"
                        SELECT 
                            Т.Название AS Товар,
                            П.Номер_партии,
                            П.Срок_годности,
                            П.Количество,
                            DATEDIFF(day, П.Срок_годности, GETDATE()) AS ДнейПросрочено,
                            П.Цена_закупки,
                            П.Активна
                        FROM Партии П
                        JOIN Товары Т ON П.Номер_товара = Т.Номер_товара
                        WHERE П.Срок_годности < GETDATE() 
                            AND П.Количество > 0
                        ORDER BY П.Срок_годности").ToList();

                            if (детально.Count > 0)
                            {
                                Console.WriteLine($"{"Товар",-20} {"Партия",-8} {"Просрочен с",-15} {"Дней",-6} {"Кол-во",-8} {"Активна",-8}");
                                Console.WriteLine(new string('-', 70));

                                foreach (var д in детально)
                                {
                                    Console.ForegroundColor = д.Активна ? ConsoleColor.Red : ConsoleColor.DarkRed;
                                    Console.WriteLine($"{д.Товар,-20} {д.Номер_партии,-8} {д.Срок_годности:dd.MM.yyyy,-15} {д.ДнейПросрочено,-6} {д.Количество,-8} {(д.Активна ? "Да" : "Нет"),-8}");
                                    Console.ResetColor();
                                }
                            }
                        }
                        break;

                    case "4":
                        Console.WriteLine("Запуск автоматической очистки...");
                        int автоочистка = db.ОчиститьПросроченныеПартии();

                        if (автоочистка > 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"✅ Автоматически деактивировано партий: {автоочистка}");
                            Console.ResetColor();
                            Logger.Log(текущийПользователь.Идентификатор, $"Автоматическая очистка: {автоочистка} партий");
                        }
                        else
                        {
                            Console.WriteLine("Просроченных партий не найдено");
                        }
                        break;

                    case "0":
                        return;
                }

                if (choice != "0" && choice != "3")
                {
                    Console.WriteLine("\n" + new string('=', 70));
                    Console.WriteLine("ОСТАТКИ ПОСЛЕ ОЧИСТКИ:");

                    var остаткиПосле = db.ОстаткиПоПартиям();
                    if (остаткиПосле.Count == 0)
                    {
                        Console.WriteLine("Склад пуст");
                    }
                    else
                    {
                        Console.WriteLine($"{"Товар",-20} {"Партия",-8} {"Срок годности",-15} {"Кол-во",-8} {"Статус",-15}");
                        Console.WriteLine(new string('-', 70));

                        foreach (var item in остаткиПосле)
                        {
                            if (item.Статус == "ПРОСРОЧЕНО")
                                Console.ForegroundColor = ConsoleColor.Red;
                            else if (item.Статус == "КРИТИЧЕСКИЙ")
                                Console.ForegroundColor = ConsoleColor.Magenta;
                            else if (item.Статус == "Скоро истекает")
                                Console.ForegroundColor = ConsoleColor.Yellow;
                            else
                                Console.ForegroundColor = ConsoleColor.Green;

                            Console.WriteLine($"{item.Товар,-20} {item.Номер_партии,-8} {item.Срок_годности:dd.MM.yyyy,-15} {item.Количество,-8} {item.Статус,-15}");
                            Console.ResetColor();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
                Console.ResetColor();
                Logger.Log(текущийПользователь.Идентификатор, "Ошибка при очистке просроченных", ex);
            }

            Console.WriteLine("\nНажмите любую клавишу для продолжения...");
            Console.ReadKey();
        }
    }
}