using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Integral.Web.PortalSite.Pages_Albert.Error {

  public class NotFound : PageModel {

    public IActionResult OnGet() {

      Response.StatusCode = StatusCodes.Status404NotFound;
      return Page();
    }
  }
}
