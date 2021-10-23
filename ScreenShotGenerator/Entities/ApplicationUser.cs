using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Entities
{
    /// <summary>
    /// П Говорим что нам нужно использовать Guid в качестве типа Id.
    /// </summary>
    public class ApplicationUser:IdentityUser<Guid>
    {

        public ApplicationUser()
        {

        }

        public ApplicationUser(string username):base(username) 
        {

        }


        public string email { get; set; }
    }
}
