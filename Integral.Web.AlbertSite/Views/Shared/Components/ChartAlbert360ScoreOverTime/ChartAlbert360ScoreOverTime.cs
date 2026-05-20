using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Integral.Web.PortalSite.AppCode;

namespace Integral.Web.PortalSite.ViewComponents {

  public class ChartAlbert360ScoreOverTime : ViewComponent {

    public Task<IViewComponentResult> InvokeAsync() {

      var ctx = Coachee360Context.GetOrLoad(HttpContext);
      if (!ctx.IsAvailable) return Task.FromResult<IViewComponentResult>(Content(""));

      string benchType = WebHelper.GetQueryStringValue("benchType");
      if (benchType.IsNullOrEmpty() || (benchType != "o" && benchType != "g")) benchType = "o";

      var model = new ChartAlbert360ScoreOverTimeModel {
        coacheeInfo = ctx.CoacheeInfo,
        reportResults = ctx.ReportResults,
        benchTypeName = benchType == "o"
          ? ChartAlbert360ScoreOverTimeModel.eBenchType.Organisation
          : ChartAlbert360ScoreOverTimeModel.eBenchType.Global
      };
      model.Initialize();

      return Task.FromResult<IViewComponentResult>(View(model));
    }

  }
}
