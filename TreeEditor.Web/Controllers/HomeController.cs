using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using TreeEditor.Web.Models;

namespace TreeEditor.Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        // Main editor page serving the frontend for DBTreeView and CachedTreeView
        public IActionResult Editor()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
