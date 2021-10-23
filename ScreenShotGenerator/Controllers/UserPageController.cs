using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScreenShotGenerator.Controllers
{
    [Authorize]
    [Authorize(Policy = RolesConst.User)]
    public class UserPageController : Controller
    {
        public IActionResult Index()
        {
            ViewBag.Name = User.Identity.Name;
            return View();
        }
    }
}
