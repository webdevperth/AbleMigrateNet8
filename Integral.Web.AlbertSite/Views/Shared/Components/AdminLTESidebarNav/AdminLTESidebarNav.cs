using Microsoft.AspNetCore.Mvc;
using Integral.Web.PortalSite.AppCode;

namespace Integral.Web.PortalSite.ViewComponents {

  public class AdminLTESidebarNav : ViewComponent {

    public IViewComponentResult Invoke() {
      var model = AdminLTESidebarNavModel.Build(LayoutModel.GetCurrent());
      return View(model);
    }

  }
}
