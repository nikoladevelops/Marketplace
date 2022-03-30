using Marketplace.Models;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Controllers
{
    [Authorize(Roles = Helper.AdminRole)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult AdminPanel()
        {
            return View();
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public IActionResult SearchUser(string username)
        {
            var userExists = _context.Users
                .SingleOrDefault(x => x.UserName == username);

            var vm = new AdminPanelViewModel()
            {
                Username = username
            };

            if (userExists == null)
            {
                vm.UserNotFound = true;
                return View("AdminPanel", vm);
            }

            return View("AdminPanel",vm);
        }
    }
}
