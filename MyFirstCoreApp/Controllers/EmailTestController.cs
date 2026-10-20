using Microsoft.AspNetCore.Mvc;
using MyFirstCoreApp.Services;

namespace MyFirstCoreApp.Controllers
{
    public class EmailTestController : Controller
    {
        private readonly IEmailSender _emailSender;

        public EmailTestController(IEmailSender emailSender)
        {
            _emailSender = emailSender;
        }

        public async Task<IActionResult> Index()
        {
            await _emailSender.SendEmailAsync(
                "ahmadiamin2044@gmail.com",
                "تست ارسال ایمیل",
                "<h2>سلام</h2><p>این یک ایمیل تستی از MyFirstCoreApp است.</p>");

            return Content("ایمیل با موفقیت ارسال شد.");
        }
    }
}