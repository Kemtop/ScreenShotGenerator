namespace TestService
{
    /// <summary>
    /// Модель возвращаемого json.
    /// </summary>
    internal class RetJson
    {
        /// <summary>
        /// Путь к сайту.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Результат работы скриншоттера.
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// Путь к локальной папке.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Сообщение -в случае ошибки, и информационные сообщения.
        /// </summary>
        public string Log { get; set; }

    }
}
