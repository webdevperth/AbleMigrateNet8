using System;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class ProjectPrograms : AppCode.PageBaseClasses.ProjectPageBase {

    public DbHelper.AblePrograms.AbleProgramList ProgramsInProject;
    public bool CanCreateProgram;

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Programs";

      ProgramsInProject = DbHelper.AblePrograms.GetProgramsByJobNumber(
        ProjectInfo.JobNumber,
        SessionHelper.AppAccess.Programs.CanViewAllProjectPrograms()
          ? DbHelper.AblePrograms.WhereRelatedUserIs.NoCheck
          : DbHelper.AblePrograms.WhereRelatedUserIs.Tenant_AnyRelated,
        SessionHelper.UserInfo);

      CanCreateProgram = SessionHelper.AppAccess.Programs.CanCreateProgram(ProjectInfo);
    }

    public string GetRowLinkUrl() {
      return PathHelper.Pages.ProgramOverview(null);
    }

    public string GetRevenueProgressHtml(DbHelper.AblePrograms.AbleProgramInfo program) {

      if (SessionHelper.AppAccess.Programs.Revenue.CanViewTotalRevenue(program)) {
        return WebHelper.GetProgressBarHtml(program.CompletedRevenueAmt, program.TotalRevenueAmt, "", WebHelper.ProgressBarType.Currency);
      } else {
        return "-";
      }
    }
  }
}
