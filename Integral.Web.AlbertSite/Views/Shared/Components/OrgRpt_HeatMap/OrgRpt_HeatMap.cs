using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.PortalSite.Reports;

namespace Integral.Web.PortalSite.ViewComponents {

  public class OrgRpt_HeatMap : ViewComponent {

    public Task<IViewComponentResult> InvokeAsync() {

      var ctx = OrgReportContext.GetOrLoad(HttpContext, ignorePartCount: true);
      if (!ctx.IsAvailable) return Task.FromResult<IViewComponentResult>(Content(""));

      var model = new OrgRpt_HeatMapModel {
        reportData = ctx.ReportData,
        categoryDimensionNo = OrgReports.GetCategoryFromQuery()
      };
      model.HeatMapData = model.GetHeatMapData();

      return Task.FromResult<IViewComponentResult>(View(model));
    }

  }
}
