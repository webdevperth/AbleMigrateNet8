using System;

namespace Integral.Web.PortalSite.ajax {

  public partial class AbleProjects : AppCode.PageBaseClasses.LoggedInPageBase {

    int urlCompanyId = 0;

    protected void Page_Load(object sender, EventArgs e) {

      if ("" + WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CompanyId) != "") {
        int.TryParse("" + WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CompanyId), out urlCompanyId);
        if (urlCompanyId >= 0) {
          WebHelper.WriteJsonAndEnd(GetProjectsForCompanyId(urlCompanyId));
        }
      }
    }

    private GetProjectsForCompanyId_Response GetProjectsForCompanyId(int companyId) {

      return new GetProjectsForCompanyId_Response() {
        Projects = SessionHelper.AppAccess.Quotes.CanSelectProjectsFromAllOrgs()
          ? DbHelper.Projects.GetProjectsForCompany(companyId)
          : DbHelper.Projects.GetProjectsForCompanyAndUserOrg(companyId, SessionHelper.GetUserInfoOrNull())
      };
    }

    private class GetProjectsForCompanyId_Response {
      public DbHelper.Projects.ProjectListPaged Projects { get; internal set; }
      public GetProjectsForCompanyId_Response() {
        Projects = new DbHelper.Projects.ProjectListPaged();
      }
    }

  }
}
