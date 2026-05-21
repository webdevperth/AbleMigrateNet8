using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class Logout : PageModel {

    public IActionResult OnGet() {

      SessionHelper.LogOut();
      WebHelper.Redirect(PathHelper.WebRoot);
      return new EmptyResult();
    }
  }
}
