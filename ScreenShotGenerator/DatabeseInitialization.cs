using ScreenShotGenerator.Data;
using System;
using Microsoft.Extensions.DependencyInjection;
using ScreenShotGenerator.Entities;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;

namespace ScreenShotGenerator
{
    /// <summary>
    /// Логика содания двух пользователей при первоначальной инициализации системы.
    /// </summary>
    public static class DatabeseInitialization
    {
        public  static async Task InitAsync(IServiceProvider scopeServiceProvider)
        {
            var context = scopeServiceProvider.GetService<ApplicationDbContext>();
            var userManager = scopeServiceProvider.GetService<UserManager<ApplicationUser>>();
         

            //Были ли добавлены в системе какие либо пользователи?
            var hasAny=context.Users.Take(1).ToList();

            //Первоначальная инициализация пользователей.
            //Нет записей.
            if(hasAny.Count==0)
            {
                var admin = new ApplicationUser
                {
                    UserName = "SuperAdmin",
                    email = "norepli@your.mail"
                };

                var user = new ApplicationUser
                {
                    UserName = "Tom",
                    email = "norepli@your.mail"
                };



                var result = userManager.CreateAsync(admin, "123456").GetAwaiter().GetResult();
                if (!result.Succeeded)
                {
                   throw new Exception("Can't create record to db for Admin.");                  
                }

                result = userManager.CreateAsync(user, "123456").GetAwaiter().GetResult();
                if (!result.Succeeded)
                {
                    throw new Exception("Can't create record to db for User.");
                }

                //Добавлем роль.
                userManager.AddClaimAsync(admin, new Claim(ClaimTypes.Role, RolesConst.Admin)).
                   GetAwaiter().GetResult();
                userManager.AddClaimAsync(user, new Claim(ClaimTypes.Role, RolesConst.User)).
                  GetAwaiter().GetResult();


                //Добавление сведений о настроках системы.
                createApplicationSettings(context);

            }         
            
        }

        /// <summary>
        /// Добавляет записи в таблицу настроек(serviceSettings).
        /// </summary>
        private static void createApplicationSettings(ApplicationDbContext db)
        {
            Dictionary<string, string> settings = new Dictionary<string, string>();
            settings.Add("poolBrowserSize", "1");
            settings.Add("browserTasksPerThread", "5");
            settings.Add("clearCashInterval", "24");
           
            //Добавление в БД.
            foreach(KeyValuePair<string,string> item in settings )
            {
                mServiceSettings m = new mServiceSettings();
                m.Name = item.Key;
                m.Value = item.Value;
                m.LastChange = DateTime.Now;

                db.Add(m);
            }

            db.SaveChanges();
        }
    }
}
