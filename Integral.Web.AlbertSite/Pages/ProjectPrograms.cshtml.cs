using System;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class ProjectPrograms : AppCode.PageBaseClasses.ProjectPageBase {

    public DbHelper.AblePrograms.AbleProgramList ProgramsInProject;
    public bool CanCreateProgram;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Programs";

      ProgramsInProject = DbHelper.AblePrograms.GetProgramsByJobNumber(
        ProjectInfo.JobNumber,
        SessionHelper.AppAccess.Programs.CanViewAllProjectPrograms()
          ? DbHelper.AblePrograms.WhereRelatedUserIs.NoCheck
          : DbHelper.AblePrograms.WhereRelatedUserIs.Tenant_AnyRelated,
        SessionHelper.UserInfo);

      CanCreateProgram = SessionHelper.AppAccess.Programs.CanCreateProgram(ProjectInfo);

      return Page();
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
