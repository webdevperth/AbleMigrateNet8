using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Integral.Web.PortalSite.AppCode;

namespace Integral.Web.PortalSite.ViewComponents {

  public class ChartAlbert360Focus : ViewComponent {

    public Task<IViewComponentResult> InvokeAsync() {

      var ctx = Coachee360Context.GetOrLoad(HttpContext);
      if (!ctx.IsAvailable) return Task.FromResult<IViewComponentResult>(Content(""));

      var model = new ChartAlbert360FocusModel {
        coacheeInfo = ctx.CoacheeInfo,
        reportResults = ctx.ReportResults
      };
      model.Initialize();

      return Task.FromResult<IViewComponentResult>(View(model));
    }

  }
}
