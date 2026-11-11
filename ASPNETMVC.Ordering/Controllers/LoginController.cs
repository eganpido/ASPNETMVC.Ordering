using ASPNETMVC.Ordering.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ASPNETMVC.Ordering.Controllers
{
    public class LoginController : Controller
    {
        private readonly OrderingDataContext db = new OrderingDataContext(System.Configuration.ConfigurationManager.ConnectionStrings["liteclerk_orderingConnectionString"].ConnectionString);

        [HttpGet]
        public ActionResult Index()
        {
            ViewBag.Terminals = db.MstTerminals.ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string Username, string Password, int TerminalId)
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password) || TerminalId <= 0)
            {
                ModelState.AddModelError("", "Please fill-in username, password, and select a terminal.");
                ViewBag.Terminals = db.MstTerminals.ToList();
                return View("Index");
            }

            var user = db.MstUsers
                         .FirstOrDefault(u => u.UserName == Username && u.Password == Password);

            if (user != null)
            {
                Session["UserId"] = user.Id;
                Session["StaffUser"] = user.UserName;
                Session["TerminalId"] = TerminalId;
                var terminal = db.MstTerminals
                         .FirstOrDefault(u => u.Id == TerminalId);
                Session["Terminal"] = terminal.Terminal;

                return RedirectToAction("Index", "Order");
            }
            else
            {
                ModelState.AddModelError("", "Invalid username / password!");
                ViewBag.Terminals = db.MstTerminals.ToList();
                return View("Index");
            }
        }
    }
}