using System;
using System.Web;

namespace TestWebClient_NetMvc.Models
{
    public class LoginViewModel
    {
        public string Login { get; set; }
        public string Password { get; set; }
        public LoginViewModel() { Success = false; }
        public bool Success { get; set; }
    }
}