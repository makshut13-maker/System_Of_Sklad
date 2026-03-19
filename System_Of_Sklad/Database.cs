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
            }
        }
    }
}