namespace TestService
{
    /// <summary>
    /// Результат теста.
    /// </summary>
    internal class TestResults
    {
        public int Id { get; set; }

        /// <summary>
        /// Адрес сайта с которым работали. 
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// json ответ сервера создания скринов.
        /// </summary>
        public string Response { get; set; }

        /// <summary>
        /// Время выполнения общего запроса.
        /// </summary>
        public string ElapsedTime { get; set; }

        /// <summary>
        /// Время создания записи в таблице.
        /// </summary>
        public DateTime Create { get; set; }
    }
}
