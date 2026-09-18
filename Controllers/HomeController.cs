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

    /// <summary>
    /// GET /about
    ///
    /// The [Route] attribute is what makes the URL "/about" rather than
    /// "/Home/About". The default route would still serve the second one, so
    /// both would work and be two URLs for one page - which search engines
    /// treat as duplicate content. The canonical tag below settles it.
    /// </summary>
    [Route("/about")]
    public IActionResult About()
    {
        ViewData["Title"] = "About Us";
        ViewData["MetaDescription"] =
            "About ShiftingGuru: how we connect customers with verified moving and "
            + "car transport professionals across India.";
        ViewData["Canonical"] = AbsoluteUrl("/about");

        return View();
    }

    // GET /how-it-works
    [Route("/how-it-works")]
    public IActionResult HowItWorks()
    {
        ViewData["Title"] = "How It Works";
        ViewData["MetaDescription"] =
            "How car transport and home shifting works with ShiftingGuru: send your requirement, "
            + "compare quotes from verified professionals, and choose with confidence.";
        ViewData["Canonical"] = AbsoluteUrl("/how-it-works");

        return View();
    }

    // GET /contact
    [Route("/contact")]
    public IActionResult Contact()
    {
        ViewData["Title"] = "Contact Us";
        ViewData["MetaDescription"] =
            "Contact ShiftingGuru by phone, WhatsApp or email for car transport, "
            + "home shifting and logistics across India.";
        ViewData["Canonical"] = AbsoluteUrl("/contact");

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

    /// <summary>
    /// Builds a full URL from the current request, so the canonical tag is
    /// right on localhost, on staging and in production without a hardcoded
    /// domain anywhere.
    /// </summary>
    private string AbsoluteUrl(string path) =>
        $"{Request.Scheme}://{Request.Host}{path}";
}