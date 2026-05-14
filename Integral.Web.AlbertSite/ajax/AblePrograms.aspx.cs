using System;

namespace Integral.Web.PortalSite.ajax {

  public partial class AblePrograms : AppCode.PageBaseClasses.LoggedInPageBase {

    int urlCompanyId = 0;

    protected void Page_Load(object sender, EventArgs e) {

      if ("" + WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CompanyId) != "") {
        int.TryParse("" + WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CompanyId), out urlCompanyId);
        if (urlCompanyId >= 0) {
          WebHelper.WriteJsonAndEnd(GetProgramsForCompanyId(urlCompanyId));
        }
      }
    }

    private GetProgramsForCompanyId_Response GetProgramsForCompanyId(int companyId) {

      return new GetProgramsForCompanyId_Response() {
        Programs = DbHelper.AblePrograms.GetProgramListForCompany(
          companyId,
          SessionHelper.IsUserRoleAdmin
            ? DbHelper.AblePrograms.WhereRelatedUserIs.NoCheck
            : DbHelper.AblePrograms.WhereRelatedUserIs.Tenant_PLC_PC,
          SessionHelper.UserInfo
        )
      };
    }

    private class GetProgramsForCompanyId_Response {
      public DbHelper.AblePrograms.AbleProgramList Programs { get; internal set; }
      public GetProgramsForCompanyId_Response() {
        Programs = new DbHelper.AblePrograms.AbleProgramList();
      }
    }

  }
}
