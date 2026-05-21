using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class DevelopmentPlanForm : AppCode.PageBaseClasses.LoggedInPageModel {

    public IActionResult OnGet() {

      PageTitle = "Development Plan";

      return Page();
    }

  }
}
