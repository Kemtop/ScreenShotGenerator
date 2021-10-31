using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScreenShotGenerator.Data;
using ScreenShotGenerator.Entities;
using ScreenShotGenerator.Services;
using ScreenShotGenerator.Services.ScreenShoterLogic;

namespace ScreenShotGenerator
{
    public class Startup
    {
        private readonly IConfiguration _configuration; 

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

       

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //Настройка подключения к  базе данных. Получаю из json файла.
            var conSection = new ConfigurationBuilder().AddJsonFile("appsettings.json").
                Build().GetSection("PgSqlConnectionStrings");

            string psqlConStr = "Host=" + conSection["Host"] + ";" +
                "Port=" + conSection["Port"] + ";" +
                "Database=" + conSection["Database"] + ";" +
                "Username=" + conSection["Username"] + ";" +
                "Password=" + conSection["Password"];


            services.AddDbContext<ApplicationDbContext>(config =>
            {
                // config.UseNpgsql("Host=localhost;Port=5432;Database=ScreenShotServiceDb;Username=postgres;Password=926926");
                config.UseNpgsql(psqlConStr);
            })
             .AddIdentity<ApplicationUser, ApplicationRole>(config=> 
             {
                 //Настройка идентификации.
                 config.Password.RequireDigit = false; //Пароль должен содержать цифры.
                 config.Password.RequireLowercase = false;
                 config.Password.RequireUppercase = false;
                 config.Password.RequireNonAlphanumeric = false;
                 config.Password.RequiredLength= 6;
             })
            .AddEntityFrameworkStores<ApplicationDbContext>();

            //Добавить авторизацию через Google.
            services.AddAuthentication()
                //.AddGoogle()
                .AddFacebook(config=> {
                    config.AppId = _configuration["Authentication:Facebook:AppId"];
                    config.AppSecret = _configuration["Authentication:Facebook:AppSecret"];
                });


            //Microsoft Identity
            services.ConfigureApplicationCookie(config =>
            {
                config.LoginPath = "/Admin/Login"; ;
                //Путь к странице аллерта.
                config.AccessDeniedPath = "/Home/AccessDenied";
            });


            services.AddAuthorization(options =>
            {
                //Используем Clime.
                //Политика администратора.
                options.AddPolicy(RolesConst.Admin, builder =>
                     {
                         builder.RequireClaim(ClaimTypes.Role, RolesConst.Admin);
                     });

                /*
                //Политика пользователя.
                options.AddPolicy(RolesConst.User, builder =>
                {
                    builder.RequireClaim(ClaimTypes.Role, RolesConst.User);
                });
                */

                options.AddPolicy(RolesConst.User, builder =>
                {
                    builder.RequireAssertion(x=>x.User.HasClaim(ClaimTypes.Role,RolesConst.User)||
                    x.User.HasClaim(ClaimTypes.Role,RolesConst.Admin)) ;
                });
            });

            services.AddControllersWithViews();
            services.AddHttpContextAccessor(); //Для получения URL хоста.

            //Тест IOC.
            //Вы можете попробовать CreateInstance<T>
            services.AddSingleton<IScreenShoter>(x=>
                    new ScreenShoter(x.GetRequiredService<IHttpContextAccessor>(),
                    x.GetRequiredService<IServiceScopeFactory>(),
                    x.GetRequiredService<IConfiguration>(),
                    a=>{
                        a.timeGo = 1000;
                        //_configuration["Authentication:Facebook:AppSecret"];

                    }));
            

            /*
               services.AddDbContext<ApplicationDbContext>(config =>
            {
                // config.UseNpgsql("Host=localhost;Port=5432;Database=ScreenShotServiceDb;Username=postgres;Password=926926");
                config.UseNpgsql(psqlConStr);
            })
             */



            /*
            services.AddDefaultIdentity<IdentityUser>
                (options => options.SignIn.RequireConfirmedAccount = true
                )
                .AddRoles<IdentityRole>()
             .AddEntityFrameworkStores<DatabaseContext>();

           
            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });
           */
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();


            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}");///{parameters?}
            });

            /*
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "allimage",
                    pattern: "{controller=Home}/{action=Allimage}");///{parameters?}
            });
            */
        }
    }
}
