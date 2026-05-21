using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class SurveyQuestions : AppCode.PageBaseClasses.LoggedInPageModel {

    public IActionResult OnGet() => Page();

  }
}
