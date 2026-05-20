using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.ViewComponents {

  public class AdminLTEHeaderNav : ViewComponent {

    public IViewComponentResult Invoke() {
      var model = AdminLTEHeaderNavModel.Build();
      return View(model);
    }

  }
}
