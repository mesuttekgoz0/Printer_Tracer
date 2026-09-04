using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using YaziciTakip.Models;

namespace YaziciTakip.Controllers;

/// <summary>
/// Yalnızca gizlilik ve hata sayfalarını barındırır. Uygulamanın açılış sayfası
/// artık "Sayaç Oku" (<see cref="ReadingsController"/>).
/// </summary>
public class HomeController : Controller
{
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
