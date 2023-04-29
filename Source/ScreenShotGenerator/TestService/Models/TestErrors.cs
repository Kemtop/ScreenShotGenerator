namespace TestService
{
    /// <summary>
    /// Модель лога об ошибках.
    /// </summary>
    internal class TestErrors
    {
        public int Id { get; set; }

        /// <summary>
        /// Адресс сайта для которого возникла ошибка. 
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Текст ошибки.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Время создания записи.
        /// </summary>
        public DateTime Create { get; set; }
    }
}
