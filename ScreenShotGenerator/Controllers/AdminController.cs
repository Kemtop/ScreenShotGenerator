using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScreenShotGenerator.Data;
using ScreenShotGenerator.Entities;
using ScreenShotGenerator.Models;
using ScreenShotGenerator.Services;
using ScreenShotGenerator.Services.BrowserControl;
using ScreenShotGenerator.Services.Models;
using ScreenShotGenerator.Services.ScreenShoterPools;

namespace ScreenShotGenerator.Controllers
{
    [Authorize]
    [Authorize(Roles = RolesConst.Admin)]
    public class AdminController : Controller
    {
        //Для работы с базой данных.
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _dbContext;

        //Интерфейс для взаимодействия с сервисом скрин шоттов.
        private readonly IScreenShoter _screenShoter;

        public AdminController(UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext dbContext, IScreenShoter screenShoter)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _dbContext = dbContext;
            _screenShoter = screenShoter;
        }


        /// <summary>
        /// Контроллер админ страницы.
        /// </summary>
        /// <returns></returns>
        public IActionResult Index()
        {
            //Получаю сведения о настройках сервиса.
            SystemSettingModel model = _screenShoter.getSettings();
            model.cacheFilesSize = _screenShoter.getTmpDirSize();// Возвращает размер файлов во временной папке в Мб.
            model.InfoMessage = "Сейчас " + DateTime.Now.ToString("hh:mm:ss dd.MM.yyyy");
            
            return View(model);
        }

        /// <summary>
        /// Кеш картинок.
        /// </summary>
        /// <returns></returns>
        [Authorize(Roles = RolesConst.Admin)]
        public IActionResult CashImages(CashImagesModel model)
        {
            //Пустое поле поиска.
            if (String.IsNullOrEmpty(model.searchUrl))
            {
                //Получение списка имен файлов.
                List<mImageList> fileNames = _screenShoter.DiskItems();
                model.Files = fileNames; 
               
                return View(model);
            }

            model.Files = _screenShoter.FindFile(model.searchUrl); 
            return View(model);
        }
                

        /// <summary>
        /// Возвращает данные о состоянии памяти.
        /// </summary>
        /// <returns></returns>
        public JsonResult GetMemoryUsage()
        {
            List<mJsonChart> memoryUsages = new List<mJsonChart>();

            foreach (var line in _dbContext.performanceInfo)
            {
                memoryUsages.Add(new mJsonChart { value = line.memoryUsage, date = line.date });
            }


            //Возвращает массив.
            return Json(memoryUsages);
        }

        /// <summary>
        /// Возвращает данные о нагрузке процессора.
        /// </summary>
        /// <returns></returns>
        public JsonResult GetCPUusage()
        {
            List<mJsonChart> cpuLoad = new List<mJsonChart>();

            foreach (var line in _dbContext.performanceInfo)
            {
                cpuLoad.Add(new mJsonChart { value = (int)line.cpuLoad, date = line.date });
            }


            //Возвращает массив.
            return Json(cpuLoad);
        }

        /// <summary>
        /// Возвращает данные о количестве ожидающих задач в пуле.
        /// </summary>
        /// <returns></returns>
        public JsonResult GetPoolWaitTask()
        {
            List<mJsonChart> poolWait = new List<mJsonChart>();

            foreach (var line in _dbContext.performanceInfo)
            {
                poolWait.Add(new mJsonChart { value = line.poolWaiterTask, date = line.date });
            }


            //Возвращает массив.
            return Json(poolWait);
        }

        /// <summary>
        /// Сохраняет настройки сервиса.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        public IActionResult Index(SystemSettingModel model)
        {
            if (model == null) return View(model);

            model.InfoMessage = "Сейчас " + DateTime.Now.ToString("hh:mm:ss dd:MM:yyyy");

            if (!ModelState.IsValid)
            {
                model.ErrorMessage = "Введены не верные данные. Настройки не сохранены.";
                return View(model);
            }

            //Сохраняю настройки.
            _screenShoter.setSettings(model);
            model.InfoMessage += " Настройки успешно сохранены.";

            return View(model);
        }

        /// <summary>
        /// Нажатие на кнопку перезапустить браузеры.
        /// </summary>
        /// <param name="button"></param>
        /// <returns></returns>
        public IActionResult rebootBrowser(string button)
        {
            //Перезапуск сервиса.
            _screenShoter.restartAllBrowsers();
            return View();
        }

        /// <summary>
        /// Нажатие на кнопку очистить кэш.
        /// </summary>
        /// <param name="button"></param>
        /// <returns></returns>
        public  IActionResult clearCache()
        {
            //Получить и показать пользователю количество удаляемых записей на диске и в памяти.
            ViewBag.cacheItemsCount = _screenShoter.CacheItemsCount();
            //Получение списка имен файлов.
            List<mImageList> fileNames = _screenShoter.DiskItems();
            ViewBag.diskItems = fileNames.Count();

            //Запуск процесса удаления данных.
             Task.Run(()=>_screenShoter.RunCleaning(fileNames));
                        
            return View();
        }

        /// <summary>
        /// Очистка ошибок браузера.
        /// </summary>
        /// <param name="button"></param>
        /// <returns></returns>
        public IActionResult clearBrowserError()
        {
            //Запуск процесса удаления данных.
            Task.Run(() => _screenShoter.ClearBrowserErrors());
            return View();
        }


        /// <summary>
        /// Отображает список лог файлов.
        /// </summary>
        /// <returns></returns>
        public IActionResult ShowLogs()
        {
            //Получение списка имен файлов.
            string dirPath = @"./Logs";
            //var directory 
            IEnumerable<string> listNames = Directory
            .GetFiles(dirPath, "*", SearchOption.TopDirectoryOnly)
            .Select(f => Path.GetFileName(f));


            List<ShowLogsModel> fileNames = new List<ShowLogsModel>();
            foreach (string str in listNames)
            {
                long length = new System.IO.FileInfo(@"./Logs/" + str).Length;

                string size = (length / 1024).ToString() + " Кб";

                fileNames.Add(new ShowLogsModel
                {
                    name = str,
                    size = size
                }

                ); ;
            }

            //Сортировка по дате.            
            return View(fileNames.OrderByDescending(x => x.name));
        }


        /// <summary>
        /// Возврат содержимого лог файла в браузер.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [Route("/showFile/{id}")]
        public IActionResult showFile(string id)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Logs", id);

            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite);

            using (StreamReader reader = new StreamReader(fileStream))
            {
                string line = reader.ReadToEnd();
                return Content(line);
            }
        }

        /// <summary>
        /// Просмотр данных таблицы ошибок браузера.
        /// </summary>
        /// <returns></returns>
        public IActionResult ShowBrowserErrors()
        {

            //Данные из таблицы. Первым в списке будут последние записи. В количестве что бы браузер не умер.
            int selectLinesCnt = 1000; //Количество выбранных записей.
            List<mBrowserErrors> data = _dbContext.browserErrors.OrderByDescending(x=>x.Id).Take(selectLinesCnt).ToList();

            //Названия ошибок.
            // Возвращает словарь имя=значение перечисления.
            Dictionary<int, string> nameLevels = getNamesEnum(typeof(enumBrowserError));
           
            ViewBag.selectLinesCnt = selectLinesCnt;
            ViewBag.nameLevels = nameLevels;
            return View(data);
        }



        /// <summary>
        /// Непосредственно контролер на который происходит редирект, если пользователь не авторизировался.
        /// </summary>
        /// <param name="returnUrl"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public async Task<IActionResult> Login(string returnUrl)
        {
            //Для авторизации через соц сети.
            var externalProvider = await _signInManager.GetExternalAuthenticationSchemesAsync();

            return View(new LoginViewModel
            {
                ReturnUrl = returnUrl,
                ExternalProviders = externalProvider
            });

        }



        /// <summary>
        ///Контроллер для авторизации через логин и пароль.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            //Список внешних провайдеров.
            var externalProvider = await _signInManager.GetExternalAuthenticationSchemesAsync();

            //В некоторых случаях если пользователь очень быстро нажимает на кнопку "Войти"
            //в контроллер приходит пустая модель. Решено сделать так. Иначе исключение.
            if (model == null)
            {
                return Ok();
            }

            //Передаю список внешних провайдеров.
            model.ExternalProviders = externalProvider;

            //Если не чего не ввел.
            if (!ModelState.IsValid)
            {
                return View(model);
            }


            var user = await _userManager.FindByNameAsync(model.Username);

            //Пользователь не найден.
            if (user == null)
            {
                ModelState.AddModelError("", "User not found/Пользователь не найден.");
                return View(model);
            }

            //Авторизация
            var result = await _signInManager.PasswordSignInAsync(user, model.Password, false, false);
            if (result.Succeeded) //Авторизировался.
            {
                return Redirect(model.ReturnUrl);
            }

            return View(model);
        }



        /// <summary>
        /// Авторизация через внешнего провайдера-соц. сети. Контроллер для нажатия кнопки.
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="returnUrl"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public IActionResult ExternalLogin(string provider, string returnUrl)
        {
            //Url на который вернет нас внешний провайдер после проверки пользователя.
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Admin", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);

        }


        /// <summary>
        /// Контроллер на который переадресует после прохождения авторизации во внешнем провайдере.
        /// </summary>
        /// <param name="returnUrl"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl)
        {
            //Возвращаем информаци об удаленной авторизации.
            var info = await _signInManager.GetExternalLoginInfoAsync();


            //По чему то авторизация не произошла.
            if (info == null)
            {
                return RedirectToAction("Login");
            }

            //Все хорошо, и пользователь авторизировался.

            //Авторизуем пользователя при помощи провайдера в нашей системе.
            var result =
                 await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false, false);

            //Пользователь существует в нашей системе.
            if (result.Succeeded)
            {
                //Переход на страницу пользователя.
                return RedirectToAction("", "UserPage");
            }

            return RedirectToAction("RegisterExternal", new ExternalLoginViewModel
            {
                ReturnUrl = returnUrl,
                Username = info.Principal.FindFirstValue(ClaimTypes.Name)
            }); ;
        }


        /// <summary>
        /// Контроллер для переадресации после прохождения авторизации внешним провайдером.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [AllowAnonymous]
        public IActionResult RegisterExternal(ExternalLoginViewModel model)
        {
            return View(model);
        }


        /// <summary>
        /// Контроллер после авторизации и нажатия на кнопку "Сохранить".
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>

        [HttpPost]
        [AllowAnonymous]
        [ActionName("RegisterExternal")]
        public async Task<IActionResult> RegisterExternalConfirmed(ExternalLoginViewModel model)
        {
            //Пользователь действительно авторизировался через внешний провайдер.
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return RedirectToAction("Login");
            }

            //Пользователь подтвержден. Сохраняем пользователя в базу данных.
            var user = new ApplicationUser(model.Username);
            var result = await _userManager.CreateAsync(user);
            //Успешно добавлен в БД.
            if (result.Succeeded)
            {
                var calimsResult =
                    await _userManager.AddClaimAsync(user, new Claim(ClaimTypes.Role, RolesConst.User));

                if (calimsResult.Succeeded)
                {
                    var identityResult = await _userManager.AddLoginAsync(user, info);
                    if (identityResult.Succeeded)
                    {
                        //Авторизируем пользователя в нашем приложении.
                        await _signInManager.SignInAsync(user, false);
                        return RedirectToAction("", "UserPage");
                    }
                }
            }
            else
            {
                //Возвращаю сведения об ошибке.
                IdentityError ir = result.Errors.First();
                model.Error = ir.Description;

                return View(model);
            }


            return View(model);
        }


        /// <summary>
        /// Выход.
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public async Task<IActionResult> LogoutAsync()
        {
            await _signInManager.SignOutAsync();
            return Redirect("/Home/Index"); ;
        }




        /// <summary>
        /// Просмотр пула задач.
        /// </summary>
        /// <returns></returns>
        public  async Task<IActionResult> ShowTaskPool()
        {
            //Получаю информацию о состоянии пула. Последние last записей.
             const int last = 300;
             List<mJobPool> tasks= await Task.Run(()=>_screenShoter.getPoolTasksInfo(last));

            //List<mBrowserErrors> data = _dbContext.browserErrors.OrderByDescending(x => x.Id).Take(selectLinesCnt).ToList();

            //Названия статусов.
            ViewBag.nameStatus = getNamesEnum(typeof(enumTaskStatus));
            ViewBag.selectLinesCnt = last;
            
            return View(tasks);
        }

       
        /// <summary>
        /// Просмотр кеши.
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> ShowCache()
        {
            //Получаю кеш. Последние last записей.
            const int last = 50;
            List<mCacheRam> cache = await Task.Run(() => _screenShoter.getCacheItems(last));

            //Названия статусов.
            ViewBag.selectLinesCnt = last;

            return View(cache);
        }


        /// <summary>
        /// Возвращает словарь имя=значение перечисления.
        /// </summary>
        /// <returns></returns>
        private Dictionary<int, string> getNamesEnum(Type t)
        {
            Dictionary<int, string> nameValues = new Dictionary<int, string>();
            foreach (var name in Enum.GetNames(t))
            {
                nameValues.Add((int)Enum.Parse(t, name), name);
            }

            return nameValues;
        }
    }
}
