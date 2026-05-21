using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class Guide : AppCode.PageBaseClasses.LoggedInPageModel {

    public IActionResult OnGet() {

      return Page();
    }

  }
}
