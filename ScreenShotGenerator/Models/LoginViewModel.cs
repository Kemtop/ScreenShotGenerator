using Microsoft.AspNetCore.Authentication;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Models
{
    public class LoginViewModel
    {
        [Required] //Требуется проверка.
        public string Username { get; set; }
        [Required]
        public string Password { get; set; }
        [Required]
        public string ReturnUrl { get; set; }
        public IEnumerable<AuthenticationScheme> ExternalProviders { get; internal set; }
    }
}
