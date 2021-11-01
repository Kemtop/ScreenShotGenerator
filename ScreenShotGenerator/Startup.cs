using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScreenShotGenerator.Data;
using ScreenShotGenerator.Entities;
using ScreenShotGenerator.Services;


namespace ScreenShotGenerator
{
    public class Startup
    {
        private readonly IConfiguration _conf; 

        public Startup(IConfiguration configuration)
        {
            _conf = configuration;
        }

       
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //Строка соединения.
            string psqlConStr = "Host=" + _conf["PgSqlConnectionStrings:Host"] + ";" +
                "Port=" + _conf["PgSqlConnectionStrings:Port"] + ";" +
                "Database=" + _conf["PgSqlConnectionStrings:Database"] + ";" +
                "Username=" + _conf["PgSqlConnectionStrings:Username"] + ";" +
                "Password=" + _conf["PgSqlConnectionStrings:Password"];


            services.AddDbContext<ApplicationDbContext>(config =>
            {
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
                    config.AppId = _conf["Authentication:Facebook:AppId"];
                    config.AppSecret = _conf["Authentication:Facebook:AppSecret"];
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

                //Политика пользователя.
                options.AddPolicy(RolesConst.User, builder =>
                {
                    builder.RequireClaim(ClaimTypes.Role, RolesConst.User);
                });

            });

            services.AddControllersWithViews();
            services.AddHttpContextAccessor(); //Для получения URL хоста.
 
            services.AddSingleton<IScreenShoter, ScreenShoter>();
          
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
                    pattern: "{controller=Home}/{action=Index}");
            });
        }
    }
}
