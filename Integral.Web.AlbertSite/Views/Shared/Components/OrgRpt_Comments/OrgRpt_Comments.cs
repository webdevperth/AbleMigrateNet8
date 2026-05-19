using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Integral.Web.PortalSite.AppCode;

namespace Integral.Web.PortalSite.ViewComponents {

  public class OrgRpt_Comments : ViewComponent {

    public Task<IViewComponentResult> InvokeAsync() {

      var ctx = OrgReportContext.GetOrLoad(HttpContext);
      if (!ctx.IsAvailable) return Task.FromResult<IViewComponentResult>(Content(""));

      var model = new OrgRpt_CommentsModel {
        reportData = ctx.ReportData,
        reportParticipantInfo = ctx.ReportParticipantInfo
      };

      // When ?tabmode= is set, the original codebehind branched in Page_Load to emit
      // either themes-table or open-text-responses HTML and then EndRequest()'d.
      // The Razor view is bypassed in that case.
      if (WebHelper.GetQueryStringValue("tabmode") != null) {

        string mode = WebHelper.GetQueryStringValue("tabmode");
        int questionNumber = 0;

        if (!int.TryParse(WebHelper.GetQueryStringValue("qn"), out questionNumber)) {
          return Task.FromResult<IViewComponentResult>(Content(""));
        }

        if (mode == "themes") {
          return Task.FromResult<IViewComponentResult>(Content(model.GetThemes(questionNumber)));
        } else if (mode == "responses") {
          return Task.FromResult<IViewComponentResult>(Content(model.GetResponses(questionNumber)));
        }
        return Task.FromResult<IViewComponentResult>(Content(""));
      }

      return Task.FromResult<IViewComponentResult>(View(model));
    }

  }
}
