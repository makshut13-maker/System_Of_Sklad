using System;

namespace Sklad_System
{
    // Таблица: Товары
    public class Товар
    {
        public int Номер_товара { get; set; }
        public string Название { get; set; }
        public decimal Средняя_рыночная_цена { get; set; }
    }

    // Таблица: Партии
    public class Партия
    {
        public int Номер_партии { get; set; }
        public int Номер_товара { get; set; }
        public DateTime Срок_годности { get; set; }
        public bool Активна { get; set; }
        public decimal Цена_закупки { get; set; }
        public int Количество { get; set; }

        // Добавь это поле для отчетов
        public string НазваниеТовара { get; set; }
    }

    // Таблица: Пользователи
    public class Пользователь
    {
        public string Идентификатор { get; set; }
        public string Роль_Пользователя { get; set; }  // Исправлено - точно как в БД
    }

    // Для отчетов
    public class Остаток
    {
        public string Товар { get; set; }
        public int Номер_партии { get; set; }
        public DateTime Срок_годности { get; set; }
        public int Количество { get; set; }
        public decimal Цена { get; set; }
        public string Статус { get; set; }
    }

    // Для журнала
    public class Лог
    {
        public int ID { get; set; }
        public string Пользователь { get; set; }
        public string Действие { get; set; }
        public DateTime Время { get; set; }
    }
}