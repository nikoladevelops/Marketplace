using Marketplace.Models;
using Marketplace.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize]
        public IActionResult AdminPanel()
        {
            if (!(User.IsInRole(Helper.AdminRole)))
            {
                return NotFound();
            }


            return View();
        }
    }
}
