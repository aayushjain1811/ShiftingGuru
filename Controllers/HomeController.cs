using Microsoft.AspNetCore.Mvc;

namespace ShiftingGuru.Controllers;

public class HomeController : Controller
{
    // GET /
    // Passes an empty view model so the quote form has something to bind to.
    public IActionResult Index()
    {
        ViewData["Title"] = "Compare Moving & Logistics Quotes";
        ViewData["MetaDescription"] =
            "ShiftingGuru connects you with verified moving and logistics professionals across India. "
            + "Tell us what you are moving, compare your options, and choose with confidence.";

        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();

    /// <summary>
    /// Reached via UseStatusCodePagesWithReExecute. Re-executing preserves the
    /// original status code, so this really is a 404 - not a 200 that looks
    /// like one, which would let search engines index every broken URL.
    /// </summary>
    [Route("/error/{code:int}")]
    public IActionResult StatusCodeHandler(int code)
    {
        ViewData["Seo"] = ShiftingGuru.ViewModels.Seo.SeoViewModel.NoIndex(
            code == 404 ? "Page not found" : "Something went wrong");

        Response.StatusCode = code;

        return code == 404 ? View("NotFound") : View("Error");
    }
}