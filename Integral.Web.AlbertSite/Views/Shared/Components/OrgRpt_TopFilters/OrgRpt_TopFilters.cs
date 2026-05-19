using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Integral.Web.PortalSite.Reports;

namespace Integral.Web.PortalSite.ViewComponents {

  public class OrgRpt_TopFilters : ViewComponent {

    public Task<IViewComponentResult> InvokeAsync() {

      OrgReports.GetExternalIOSUIDsFromQuery(out string urlSurveyUId, out string urlPartUId);
      if (urlSurveyUId.IsNullOrEmpty()) urlSurveyUId = WebHelper.GetQueryStringSurveyUID(PathHelper.AbleUrlKeys.SurveyUId);
      var surveyInfo = DbHelper.OrgSurveys.GetSurveyInfo(urlSurveyUId);

      var model = new OrgRpt_TopFiltersModel {
        surveyInfo = surveyInfo
      };

      return Task.FromResult<IViewComponentResult>(View(model));
    }

  }
}
