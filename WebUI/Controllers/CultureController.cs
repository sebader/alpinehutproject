using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace WebUI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CultureController : ControllerBase
    {
        /// <summary>
        /// Action to set the selected user culture in a cookie
        /// Source: https://stackoverflow.com/a/51804296/1537195
        /// </summary>
        /// <param name="culture"></param>
        /// <param name="returnUrl"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]  
        public IActionResult SetCulture([FromForm] string culture, [FromForm] string returnUrl)
        {
            HttpContext.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                // making cookie valid for the actual app root path (which is not necessarily "/" e.g. if we're behind a reverse proxy)
                new CookieOptions { Path = Url.Content("~/") });

            return Redirect(returnUrl);
        }
    }
}