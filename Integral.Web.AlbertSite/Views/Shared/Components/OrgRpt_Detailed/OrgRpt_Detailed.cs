using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Integral.Web.PortalSite.AppCode;

namespace Integral.Web.PortalSite.ViewComponents {

  public class OrgRpt_Detailed : ViewComponent {

    public Task<IViewComponentResult> InvokeAsync() {

      var ctx = OrgReportContext.GetOrLoad(HttpContext);
      if (!ctx.IsAvailable) return Task.FromResult<IViewComponentResult>(Content(""));

      if (PathHelper.IsCurrentPage(PathHelper.Reports.OrganisationIOSReports(0))) {
        // For OrgIOSReport page, split IOI questions into sections.
        ctx.ReportData.GroupAndSortLikertQuestionsByQuadrant();
      }

      var model = new OrgRpt_DetailedModel {
        reportData = ctx.ReportData
      };

      return Task.FromResult<IViewComponentResult>(View(model));
    }

  }
}
