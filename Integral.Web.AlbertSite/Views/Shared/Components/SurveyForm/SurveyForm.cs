using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.ViewComponents {

  public class SurveyForm : ViewComponent {

    // The legacy UserControls/SurveyForm.ascx took a bool IsJSPartial property set
    // from the host page. Razor pages now pass the equivalent flag through the
    // InvokeAsync argument list:
    //   @(await Component.InvokeAsync("SurveyForm", new { isJSPartial = true }))
    // The other host pages omit the argument and get the default (false).
    public IViewComponentResult Invoke(bool isJSPartial = false) {
      var model = SurveyFormModel.Build(HttpContext, isJSPartial);
      return View(model);
    }

  }
}
