using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using YaziciTakip.Models;

namespace YaziciTakip.Controllers;

/// <summary>
/// Yalnızca hata sayfasını barındırır (<c>UseExceptionHandler("/Home/Error")</c>).
/// Uygulamanın açılış sayfası "Sayaç Oku" (<see cref="ReadingsController"/>).
/// </summary>
public class HomeController : Controller
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
