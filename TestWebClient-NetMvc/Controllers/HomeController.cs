using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using TestWebClient_NetMvc.Models;

using System.Net;
using System.Text;
using System.Net.Sockets;

namespace TestWebClient_NetMvc.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";
            return View(new LoginViewModel());
        }

        public ActionResult About(LoginViewModel mdl)
        {
            ViewBag.Message = "Fuck off, me busy";
            return View(mdl);
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public enum MsgType : byte { Null, Login, Logout };

        [HttpPost]
        public ActionResult Edit(LoginViewModel model)
        {
            var client = new TcpClient();

            try
            {
                client.Connect(IPAddress.Parse("127.0.0.1"), 31337);
            }
            catch (SocketException)
            {
                model.Login = "Unable to connect to server.";
                return View("About", model);
            }
            NetworkStream clientStream = client.GetStream();
            
            int login_length = model.Login.Length;
            byte[] msg = Encoding.UTF8.GetBytes("  " + Convert.ToBase64String(Encoding.UTF8.GetBytes(model.Login + model.Password)));
            msg[0] = (byte)(msg.Length - 2);
            msg[1] = (byte)login_length;
            clientStream.Write(msg, 0, msg.Length);
            
            MsgType sv;
            sv = (MsgType)clientStream.ReadByte();
            model.Success = (sv == MsgType.Login);
            client.Close();

            return View("About", model);
        }
    }
}