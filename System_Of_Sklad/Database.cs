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
            }
        }
    }
}