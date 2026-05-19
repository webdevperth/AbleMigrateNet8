using System;

namespace Integral.Web.PortalSite.AppCode.PageBaseClasses {

  public class CoachInfoBase : LoggedInPageBase {

    // Note, unlike other site areas, the Coach (Partner) pages can operate without an ID given in the querystring.
    // If CoachId is missing from the querystring, we assume the ID of the currently logged in user User.

    public int UrlCoachUserId { get; protected set; } = 0;
    public bool IsNewCoach { get; protected set; } = false;
    public DbHelper.AlbertCoaches.AlbertCoachInfo CoachInfo { get; protected set; }
    public DbHelper.TenantOrg.TenantOrgInfo TenantOrgInfo { get; protected set; }

    public bool CanViewHiddenPartners { get; protected set; } = false;
    public bool CanViewInactivePartners { get; protected set; } = false;

    protected override void Page_Init(object sender, EventArgs e) {

      if (WebHelper.IsRequestExiting()) return;

      base.Page_Init(sender, e);

      WebHelper.AddBodyClass("BasePage-Coach");

      if (SessionHelper.IsUserRoleAdmin || SessionHelper.IsUserRoleCoach) FallbackUrl = PathHelper.Pages.Coaches();

      // Coach ID must be a number. There is no "add" function yet for coaches/partners.
      // If CoachId is omitted, assume the current logged in UserId
      UrlCoachUserId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.CoachId) ?? 0;

      if (UrlCoachUserId == 0) {
        SetFallbackRedirect();
        return;
      }

      CoachInfo = DbHelper.AlbertCoaches.GetCoachInfo(UrlCoachUserId, false); // Currently anyone can view anyone, no Org check here.

      // Mirror onto LayoutModel for future ViewComponent consumers. See LayoutModel.cs.
      LayoutModel.GetCurrent().CoachInfo = CoachInfo;

      if (CoachInfo == null) { // Coach id not found.
        SetFallbackRedirect();
        return;
      }

      // Check user access to Coach pages.
      if (!CheckUserPageAccess()) {
        SetFallbackRedirect();
        return;
      }

      TenantOrgInfo = DbHelper.TenantOrg.GetTenantOrgById(CoachInfo.OrgId);

      CanViewHiddenPartners = SessionHelper.AppAccess.Coaches.CanViewHiddenPartners();
      CanViewInactivePartners = SessionHelper.AppAccess.Coaches.CanViewInactivePartners();
    }

    internal bool CheckUserPageAccess() {

      if (!PathHelper.IsCurrentPage(PathHelper.Partials.PartnerSlideoutPanel(null))) {

        // If page is not the slideout panel, first check if user can access Partner's profile
        if (!SessionHelper.AppAccess.Coaches.CanViewCoachProfile(CoachInfo)) return false;

        if (PathHelper.IsCurrentPage(PathHelper.Pages.CoachEdit())) {
          if (!SessionHelper.AppAccess.PageAccess.CanAccessPartnerProfile()) return false;

        } else if (PathHelper.IsCurrentPage(PathHelper.Pages.CoachPayRuns())) {
          if (!SessionHelper.AppAccess.PageAccess.CanAccessPayruns()) return false;

        } else if (PathHelper.IsCurrentPage(PathHelper.Pages.CoachReferrals())) {
          if (!SessionHelper.AppAccess.PageAccess.CanAccessPartnerReferrals()) return false;

        } else if (PathHelper.IsCurrentPage(PathHelper.Pages.CoachUpcoming())) {
          if (!SessionHelper.AppAccess.PageAccess.CanAccessPartnerUpcoming()) return false;

        }
      }

      return true;
    }
  }
}
