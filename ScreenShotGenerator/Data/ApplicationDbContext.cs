using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ScreenShotGenerator.Entities;
using ScreenShotGenerator.Services.ScreenShoterLogic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Data
{
    /// <summary>
    /// Работа с базой данных через EntityFrameWork.
    /// </summary>
    public class ApplicationDbContext:IdentityDbContext<ApplicationUser, ApplicationRole,Guid>
    {
        public  ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            :base(options)
        {

            //Возникает исключение при играх с сетью.
                Database.EnsureCreated();
                //Если включено-пытается создать миграцию на готовой БД в linux, при старте системы.
               // Database.Migrate();
        }

        /// <summary>
        /// Данные о производительности системы.
        /// </summary>
        public DbSet<mPerformanceInfo> performanceInfo { get; set; } 

        /// <summary>
        /// Настройки сервиса.
        /// </summary>
        public DbSet<mServiceSettings> serviceSettings { get; set; }

        /// <summary>
        /// Кеш скрин шоттов.
        /// </summary>
        public DbSet<mCashTable> screnshotCache { get; set; }

        /// <summary>
        /// Ошибки браузера.
        /// </summary>
        public DbSet<mBrowserErrors> browserErrors { get; set; }

    }
}
