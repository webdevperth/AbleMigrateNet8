using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class ProgramInsights : AppCode.PageBaseClasses.ProgramPageBase {

    public IActionResult OnGet() {
      PageTitle = "Program Insights";
      return Page();
    }

  }
}
