using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Integral.Web.PortalSite.AppCode;

namespace Integral.Web.PortalSite.ViewComponents {

  public class ChartAlbert360FunctionTable : ViewComponent {

    public Task<IViewComponentResult> InvokeAsync() {

      string benchType = WebHelper.GetQueryStringValue("benchType");
      if (benchType.IsNullOrEmpty() || (benchType != "o" && benchType != "g")) {
        benchType = "o"; // default to Organisation
      }
      bool useGlobalBench = benchType == "g";

      var ctx = Coachee360Context.GetOrLoad(HttpContext, useGlobalBench);
      if (!ctx.IsAvailable) return Task.FromResult<IViewComponentResult>(Content(""));

      var model = new ChartAlbert360FunctionTableModel {
        coacheeInfo = ctx.CoacheeInfo,
        reportResults = ctx.ReportResults,
        benchTypeName = useGlobalBench
          ? ChartAlbert360FunctionTableModel.eBenchType.Global
          : ChartAlbert360FunctionTableModel.eBenchType.Organisation
      };
      model.Initialize();

      return Task.FromResult<IViewComponentResult>(View(model));
    }

  }
}
