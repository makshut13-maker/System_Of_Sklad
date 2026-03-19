using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;

namespace Sklad_System
{
    public class Database
    {
        // ИЗМЕНИ ЭТУ СТРОКУ НА СВОЮ!
        public string connectionString = @"Server=HUTOK_30FPS\SQLEXPRESS;Database=skla;Trusted_Connection=True;TrustServerCertificate=True;";

        // СОЗДАНИЕ ТАБЛИЦ (если их нет)
        public void СоздатьТаблицы()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Таблица Товары
                conn.Execute(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Товары' AND xtype='U')
            CREATE TABLE Товары (
                Номер_товара INT PRIMARY KEY IDENTITY(1,1),
                Название NVARCHAR(255) NOT NULL,
                Средняя_рыночная_цена DECIMAL(18,2) NOT NULL
            )");

                // Таблица Партии
                conn.Execute(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Партии' AND xtype='U')
            CREATE TABLE Партии (
                Номер_партии INT PRIMARY KEY IDENTITY(1,1),
                Номер_товара INT NOT NULL FOREIGN KEY REFERENCES Товары(Номер_товара),
                Срок_годности DATE NOT NULL,
                Активна BIT NOT NULL DEFAULT 1,
                Цена_закупки DECIMAL(18,2) NOT NULL,
                Количество INT NOT NULL DEFAULT 0
            )");

                // Таблица Пользователи
                conn.Execute(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Пользователи' AND xtype='U')
            CREATE TABLE Пользователи (
                Идентификатор NVARCHAR(100) PRIMARY KEY,
                Роль_Пользователя NVARCHAR(50) NOT NULL
            )");

                // Таблица для логов
                conn.Execute(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Журнал' AND xtype='U')
            CREATE TABLE Журнал (
                ID INT PRIMARY KEY IDENTITY(1,1),
                Пользователь NVARCHAR(100) NOT NULL,
                Действие NVARCHAR(500) NOT NULL,
                Время DATETIME NOT NULL
            )");

                // Добавим тестового пользователя, если нет
                var user = conn.QueryFirstOrDefault("SELECT * FROM Пользователи WHERE Идентификатор = 'admin'");
                if (user == null)
                {
                    conn.Execute("INSERT INTO Пользователи (Идентификатор, Роль_Пользователя) VALUES ('admin', 'Менеджер')");
                    conn.Execute("INSERT INTO Пользователи (Идентификатор, Роль_Пользователя) VALUES ('worker', 'Кладовщик')");
                }
            }
        }

        // ========== ТОВАРЫ ==========
        public List<Товар> ВсеТовары()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                return conn.Query<Товар>("SELECT * FROM Товары").ToList();
            }
        }

        public void ДобавитьТовар(Товар товар)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Execute("INSERT INTO Товары (Название, Средняя_рыночная_цена) VALUES (@Название, @Средняя_рыночная_цена)", товар);
            }
        }

        public bool МожноУдалитьТовар(int номерТовара)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                int count = conn.QueryFirst<int>("SELECT COUNT(*) FROM Партии WHERE Номер_товара = @id AND Активна = 1 AND Количество > 0",
                    new { id = номерТовара });
                return count == 0;
            }
        }

        public void УдалитьТовар(int номерТовара)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Execute("DELETE FROM Товары WHERE Номер_товара = @id", new { id = номерТовара });
            }
        }

        // ========== ПАРТИИ ==========
        public bool ПроверитьЦену(int номерТовара, decimal ценаЗакупки)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                var товар = conn.QueryFirstOrDefault<Товар>("SELECT * FROM Товары WHERE Номер_товара = @id",
                    new { id = номерТовара });
                if (товар == null) return false;

                return ценаЗакупки <= товар.Средняя_рыночная_цена * 1.1m;
            }
        }

        public bool НомерПартииУникален(int номерПартии)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                int count = conn.QueryFirst<int>("SELECT COUNT(*) FROM Партии WHERE Номер_партии = @id",
                    new { id = номерПартии });
                return count == 0;
            }
        }

        public void ДобавитьПартию(Партия партия)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                string sql = @"INSERT INTO Партии (Номер_товара, Срок_годности, Активна, Цена_закупки, Количество) 
                              VALUES (@Номер_товара, @Срок_годности, 1, @Цена_закупки, @Количество)";
                conn.Execute(sql, партия);
            }
        }

        public List<Партия> ВсеПартии()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                return conn.Query<Партия>("SELECT * FROM Партии WHERE Активна = 1").ToList();
            }
        }

        // ========== СПИСАНИЕ ==========
        public List<Партия> ПартииДляСписания(int номерТовара, int количество)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                string sql = @"
                    SELECT * FROM Партии 
                    WHERE Номер_товара = @id 
                        AND Активна = 1 
                        AND Количество > 0
                        AND DATEDIFF(day, GETDATE(), Срок_годности) >= 3
                    ORDER BY Срок_годности";  // FEFO

                return conn.Query<Партия>(sql, new { id = номерТовара }).ToList();
            }
        }

        public void СписатьТовар(int номерПартии, int количество, string пользователь)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    // Уменьшаем количество
                    conn.Execute("UPDATE Партии SET Количество = Количество - @kol WHERE Номер_партии = @id",
                        new { kol = количество, id = номерПартии }, transaction);

                    // Проверяем остаток
                    int остаток = conn.QueryFirst<int>("SELECT Количество FROM Партии WHERE Номер_партии = @id",
                        new { id = номерПартии }, transaction);

                    // Если товара больше нет - деактивируем партию
                    if (остаток == 0)
                    {
                        conn.Execute("UPDATE Партии SET Активна = 0 WHERE Номер_партии = @id",
                            new { id = номерПартии }, transaction);
                    }

                    // Логируем
                    conn.Execute("INSERT INTO Журнал (Пользователь, Действие, Время) VALUES (@user, @action, @time)",
                        new { user = пользователь, action = $"Списание {количество} шт из партии {номерПартии}", time = DateTime.Now }, transaction);

                    transaction.Commit();
                }
            }
        }

        // ========== ОТЧЕТЫ ==========
        public List<Остаток> ОстаткиПоПартиям()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                string sql = @"
            SELECT 
                Т.Название AS Товар,
                П.Номер_партии,
                П.Срок_годности,
                П.Количество,
                П.Цена_закупки AS Цена,
                CASE 
                    WHEN П.Срок_годности < GETDATE() THEN 'ПРОСРОЧЕНО'
                    WHEN DATEDIFF(day, GETDATE(), П.Срок_годности) <= 3 THEN 'КРИТИЧЕСКИЙ'
                    WHEN DATEDIFF(day, GETDATE(), П.Срок_годности) <= 7 THEN 'Скоро истекает'
                    ELSE 'Норма'
                END AS Статус
            FROM Партии П
            JOIN Товары Т ON П.Номер_товара = Т.Номер_товара
            WHERE П.Количество > 0 
              AND П.Активна = 1  -- ЭТО ВАЖНО!
            ORDER BY П.Срок_годности";

                return conn.Query<Остаток>(sql).ToList();
            }
        }

        public List<Остаток> ИстекаетЧерез7Дней()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                string sql = @"
                    SELECT 
                        Т.Название AS Товар,
                        П.Номер_партии,
                        П.Срок_годности,
                        П.Количество,
                        П.Цена_закупки AS Цена,
                        DATEDIFF(day, GETDATE(), П.Срок_годности) AS ДнейОсталось
                    FROM Партии П
                    JOIN Товары Т ON П.Номер_товара = Т.Номер_товара
                    WHERE П.Количество > 0 
                        AND DATEDIFF(day, GETDATE(), П.Срок_годности) BETWEEN 0 AND 7
                    ORDER BY П.Срок_годности";

                return conn.Query<Остаток>(sql).ToList();
            }
        }

        // ========== ПОЛЬЗОВАТЕЛИ ==========
        public Пользователь Войти(string логин)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                return conn.QueryFirstOrDefault<Пользователь>(
                    "SELECT Идентификатор, Роль_Пользователя FROM Пользователи WHERE Идентификатор = @login",
                    new { login = логин });
            }
        }

        // ========== ЛОГИ (просмотр для менеджера) ==========
        public List<Лог> ПоследниеЛоги(int количество = 20)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                return conn.Query<Лог>("SELECT TOP (@count) * FROM Журнал ORDER BY Время DESC",
                    new { count = количество }).ToList();
            }
        }

        // ========== ЛОГИРОВАНИЕ ==========
        public void ДобавитьВЖурнал(string пользователь, string действие)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    string sql = "INSERT INTO Журнал (Пользователь, Действие, Время) VALUES (@user, @action, @time)";
                    conn.Execute(sql, new { user = пользователь, action = действие, time = DateTime.Now });
                }
            }
            catch (Exception ex)
            {
                // Если не можем записать в БД - игнорируем, чтобы программа не падала
                Console.WriteLine($"Не удалось записать в журнал БД: {ex.Message}");
            }
        }

        // ========== ОЧИСТКА КРИТИЧЕСКИХ СТАТУСОВ ==========

        // 1. Автоматическая очистка просроченных партий
        public int ОчиститьПросроченныеПартии()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                string sql = @"
            UPDATE Партии 
            SET Активна = 0 
            WHERE Срок_годности < GETDATE() 
                AND Активна = 1";

                return conn.Execute(sql);
            }
        }

        // 2. Получить все просроченные партии
        public List<Партия> GetПросроченныеПартии()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                string sql = @"
            SELECT П.*, Т.Название AS НазваниеТовара
            FROM Партии П
            JOIN Товары Т ON П.Номер_товара = Т.Номер_товара
            WHERE П.Срок_годности < GETDATE() 
                AND П.Количество > 0
            ORDER BY П.Срок_годности";

                return conn.Query<Партия>(sql).ToList();
            }
        }

        // 3. Деактивировать конкретную просроченную партию
        public void ДеактивироватьПартию(int номерПартии)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                string sql = "UPDATE Партии SET Активна = 0 WHERE Номер_партии = @id";
                conn.Execute(sql, new { id = номерПартии });
            }
        }

        // 4. Полностью удалить просроченные товары (если нужно)
        public int УдалитьПросроченныеПартии()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                string sql = "DELETE FROM Партии WHERE Срок_годности < GETDATE()";
                return conn.Execute(sql);
            }
        }
    }
}