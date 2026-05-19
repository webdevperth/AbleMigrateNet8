using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Integral.Web.PortalSite.AppCode;

namespace Integral.Web.PortalSite.ViewComponents {

  public class OrgRpt_Ovw_IOIDirs : ViewComponent {

    public Task<IViewComponentResult> InvokeAsync() {

      var ctx = OrgReportContext.GetOrLoad(HttpContext);
      if (!ctx.IsAvailable) return Task.FromResult<IViewComponentResult>(Content(""));

      if (ctx.ReportData.DirectorateChartScores.IsNullOrEmpty()) {
        // No Directorate scores to show.
        // Note that only ReportBase should return "No Content" status.
        WebHelper.EndRequest();
        return Task.FromResult<IViewComponentResult>(Content(""));
      }

      var model = new OrgRpt_Ovw_IOIDirsModel {
        reportData = ctx.ReportData
      };
      model.GetChartData();

      return Task.FromResult<IViewComponentResult>(View(model));
    }

  }
}
