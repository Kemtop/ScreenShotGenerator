using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestServices
{
    class dbContext : DbContext
    {
        public DbSet<mTableTestResults> TestResults { get; set; }

        public DbSet<mTableTestErrors> TestErrors { get; set; }
        
        public dbContext()
        {
            Database.EnsureCreated();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename=TestResults.db");
        }
    }
}
