using ASPNETMVC.Ordering.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security; // <-- 1. I-add kini para sa FormsAuthentication

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

            var terminalObj = db.MstTerminals.FirstOrDefault(t => t.Id == TerminalId);

            if (user != null && terminalObj != null)
            {
                // 1. I-save ang Sessions (Para ma-access nimo ang username, terminal, etc.)
                Session["StaffUser"] = user.UserName;
                Session["UserId"] = user.Id;
                Session["TerminalId"] = terminalObj.Id;
                Session["Terminal"] = terminalObj.Terminal;

                // 2. I-set ang Forms Authentication Cookie (Aron dili ka ma-block sa [Authorize])
                FormsAuthentication.SetAuthCookie(user.UserName, false);

                return RedirectToAction("Index", "Order");
            }
            else
            {
                ModelState.AddModelError("", "Invalid username / password or terminal!");
                ViewBag.Terminals = db.MstTerminals.ToList();
                return View("Index");
            }
        }
        [HttpGet]
        public ActionResult SignOut()
        {
            // 1. Limpyohi ug i-abandon ang tanang Session variables
            Session.Clear();
            Session.Abandon();

            // 2. Opisyal nga i-sign out ang Forms Authentication cookie
            System.Web.Security.FormsAuthentication.SignOut();

            // 3. I-prevent ang browser caching sa logout page
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetExpires(DateTime.UtcNow.AddDays(-1));
            Response.Cache.SetNoStore();

            // 4. I-redirect pabalik sa Login Index page
            return RedirectToAction("Index", "Login");
        }
    }
}