using System;

namespace Integral.Web.PortalSite.AppCode {

  // Shared authentication gate used by both LoggedInPageModel (Razor Pages) and the
  // Minimal API ajax/api endpoint handlers in Program.cs. Returns false when the
  // request can't proceed; in that case the response (redirect / 401 / ajax
  // session-expired payload) has already been written.
  public static class AuthInitialization {

    public static bool RequireLoggedInUser() {

      var userInfo = SessionHelper.GetUserInfoOrNull();

      if (userInfo == null) {
        // Allow public-report coachees through if they have a valid report login.
        if (Guid.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CoacheeGuid, ""), out Guid coacheeGuid)
            && SessionHelper.PublicReport.GetIsLoggedIn(coacheeGuid)) {
          return true;
        }
      } else if (userInfo.IsSoftDeleted) {
        SessionHelper.LogOut();
        WebHelper.Redirect(PathHelper.WebRoot);
        return false;
      }

      if (SessionHelper.RedirectIfNotLoggedIn(PathHelper.WebRoot)) {
        return false;
      }

      return true;
    }
  }
}
