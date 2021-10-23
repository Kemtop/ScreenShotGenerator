using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ScreenShotGenerator.Entities;
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
                Database.EnsureCreated();
                Database.Migrate();
        }

       // public DbSet<ApplicationUser> Users { get; set; } 

    }
}
