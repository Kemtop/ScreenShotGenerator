using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Models
{
    /// <summary>
    /// Для внешней авторизации через соц сети.
    /// </summary>
    public class ExternalLoginViewModel
    {
        [Required] //Требуется проверка.
        public string Username { get; set; }
    
        [Required]
        public string ReturnUrl { get; set; }
       
        /// <summary>
        /// Ошибка сохранения пользователя(привязка аккаунта внешнего провайдера и имени пользователя)
        /// </summary>
        public string Error { get; set; }
    }
}
