using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Integral.Web.PortalSite.AppCode;

namespace Integral.Web.PortalSite.ViewComponents {

  public class OrgRpt_Ovw_TopChart : ViewComponent {

    public Task<IViewComponentResult> InvokeAsync() {

      var ctx = OrgReportContext.GetOrLoad(HttpContext);
      if (!ctx.IsAvailable) return Task.FromResult<IViewComponentResult>(Content(""));

      var model = new OrgRpt_Ovw_TopChartModel {
        reportData = ctx.ReportData
      };
      model.GetChartData();

      return Task.FromResult<IViewComponentResult>(View(model));
    }

  }
}
