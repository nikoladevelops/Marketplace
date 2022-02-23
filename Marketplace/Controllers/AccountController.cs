using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Controllers
{
    public class AccountController:Controller
    {
        public IActionResult Register() 
        {
            return View();
        }

    }
}
