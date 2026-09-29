using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DentalLab.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            var statusCodeResult = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
            if (statusCode == 404)
            {
                ViewBag.Message = "الصفحة المطلوبة غير موجودة";
                ViewBag.Path = statusCodeResult?.OriginalPath;
            }
            return View("NotFound");
        }

        [Route("Error")]
        public IActionResult Error()
        {
            var exceptionDetails = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            ViewBag.ExceptionPath = exceptionDetails?.Path;
            ViewBag.ExceptionMessage = exceptionDetails?.Error.Message;
            return View("Error");
        }
    }
}
