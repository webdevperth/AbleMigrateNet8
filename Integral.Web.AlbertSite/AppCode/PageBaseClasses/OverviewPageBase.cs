using System;

namespace Integral.Web.PortalSite.AppCode.PageBaseClasses {

  public class OverviewPageBase : LoggedInPageBase {

    public int urlCoachUserId { get; protected set; } = 0;
    public DbHelper.AlbertCoaches.AlbertCoachInfo OverviewCoachInfo { get; protected set; }

    protected override void Page_Init(object sender, EventArgs e) {

      if (WebHelper.IsRequestExiting()) return;

      base.Page_Init(sender, e);
      DashboardMenuIsActive = true;
      // Mirror onto LayoutModel for future ViewComponent consumers. See LayoutModel.cs.
      // (base.Page_Init mirrored DashboardMenuIsActive=false; re-mirror the corrected value.)
      LayoutModel.GetCurrent().DashboardMenuIsActive = DashboardMenuIsActive;
      WebHelper.AddBodyClass("BasePage-Overview");

      if (SessionHelper.IsUserRoleAdmin || SessionHelper.IsUserRoleCoach) {
        FallbackUrl = PathHelper.Pages.Coachees();
      }

      // User for all overview pages is the current logged in user.
      if (!SessionHelper.IsUserLoggedIn) {
        SetFallbackRedirect();
        return;
      }

      OverviewCoachInfo = DbHelper.AlbertCoaches.GetCoachInfo(userInfo.UserId, false);
      if (OverviewCoachInfo == null) {
        SetFallbackRedirect();
        return;
      }

      // Check user access to Overview pages.
      if (!CheckUserPageAccess()) {
        SetFallbackRedirect();
        return;
      }
    }

    internal bool CheckUserPageAccess() {

      if (PathHelper.IsCurrentPage(PathHelper.Pages.OverviewPayruns())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessPayruns()) return false;

      } else if (PathHelper.IsCurrentPage(PathHelper.Pages.OverviewUpcoming())) {
        if (!SessionHelper.AppAccess.PageAccess.CanAccessOverviewUpcoming()) return false;
      }

      return true;
    }

  }
}
