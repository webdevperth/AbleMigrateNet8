using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Integral.Web.PortalSite.AppCode;

namespace Integral.Web.PortalSite.ViewComponents {

  public class OrgRpt_Focus : ViewComponent {

    public Task<IViewComponentResult> InvokeAsync() {

      var ctx = OrgReportContext.GetOrLoad(HttpContext);
      if (!ctx.IsAvailable) return Task.FromResult<IViewComponentResult>(Content(""));

      var model = new OrgRpt_FocusModel {
        reportData = ctx.ReportData
      };
      model.Initialize();

      return Task.FromResult<IViewComponentResult>(View(model));
    }

  }
}
