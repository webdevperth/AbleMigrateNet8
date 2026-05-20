using System.Collections.Generic;

namespace Integral.Web.PortalSite.ViewComponents {

  // Model for the AdminLTEHeaderNav ViewComponent. Mirrors the public API of the
  // legacy UserControls/AdminLTEHeaderNav.ascx.cs codebehind so the .cshtml view
  // can call the same helper methods used by the original <% %> blocks.
  public class AdminLTEHeaderNavModel {

    public DbHelper.AbleUser.AbleUserInfo UserInfo;
    public string AdminViewByLabel = "";
    public List<ConfigHelper.UserRole> UserRoleOptions = new List<ConfigHelper.UserRole>();
    private ConfigHelper.UserRole CurrentUserRole;

    // Mirrors the legacy Page_Load. Pulls session state into the model.
    public static AdminLTEHeaderNavModel Build() {

      var model = new AdminLTEHeaderNavModel {
        UserInfo = SessionHelper.GetUserInfoOrNull(),
        UserRoleOptions = SessionHelper.GetAvailableUserRoles(),
        CurrentUserRole = SessionHelper.GetUserRole()
      };

      return model;
    }

    public string GetUserRoleName() {

      if (UserRoleOptions.Count < 2) return "";

      string userRoleDisp = SessionHelper.GetUserRoleDisplayName(CurrentUserRole);
      int maxRoleLength = userRoleDisp.Length == 6 ? 6 : 5;

      return $"({userRoleDisp.Left(maxRoleLength)})";
    }

    public string GetUserRoleSubmenuHtml() {

      // If user doesn't have more than one role, don't show the dropdown.
      if (UserRoleOptions.Count < 2) return "";

      string optionHtml = "", html = "";

      foreach (var userRole in UserRoleOptions) {
        // Don't show the current role in the dropdown.
        if (userRole == CurrentUserRole) continue;
        string userRoleName = SessionHelper.GetUserRoleDisplayName(userRole);
        optionHtml += "<span tabindex=\"0\" class=\"dropdown-item submenu-toggle switch-user-role\" data-role=\"" + userRole.ToString().HTMLEncode() + "\">" + userRoleName.HTMLEncode() + "</span>";
      }
      html = $@"
        <div class=""dropdown-divider""></div>
        <div class=""dropdown-submenu"">
          <span tabindex=""0"" class=""dropdown-item submenu-toggle""><i class=""fas fa-users fa-sm fa-fw""></i>
            <span>Change Role</span>
            <div class=""submenu-content"">{optionHtml}</div>
          </span>
        </div>";

      return html;
    }

    public string GetUpcomingPath() {
      if (SessionHelper.IsUserRoleLeader) {
        return PathHelper.Pages.ParticipantUpcoming();
      }
      return PathHelper.Pages.OverviewUpcoming();
    }

    public string GetNavBarLogoHtml() {

      string ableLogoHtml = GetAbleLogoHtml();
      string companyLogoHtml = GetCompanyLogoHtml();

      if (companyLogoHtml.IsNullOrEmpty()) return ableLogoHtml;

      return $@"
        <div class=""flex"">
          {companyLogoHtml}
          {ableLogoHtml}
        </div>";
    }

    public string GetAbleLogoHtml() {
      return $@"
        <div class=""logo-lg"">
          <img src=""{PathHelper.Images.AbleHeaderLogo()}"" /> {WebHelper.GetDevOrStagingSiteTagText()}
          {(!ConfigHelper.EmailRecipientOverrideAddress.IsNullOrEmpty() ? $"<div>All email goes to: {ConfigHelper.EmailRecipientOverrideAddress.HTMLEncode()}</div>" : "")}
        </div>";
    }

    public string GetCompanyLogoHtml() {
      string html = "";
      if (SessionHelper.AppAccess.Users.CanDisplayCompanyLogoInNavBar()) {
        string companyImg = PathHelper.Images.TenantOrgLogo(UserInfo, false);
        if (!companyImg.IsNullOrEmpty()) {
          html = $@"
            <div class=""logo-lg mr10"">
              <img src=""{companyImg.HTMLEncode()}"" />
            </div>";
        }
      }
      return html;
    }
  }
}
