using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.UI;

namespace Integral.Web.PortalSite.MasterPages {

  public partial class SiteMaster : System.Web.UI.MasterPage {

    public AppCode.LayoutModel Layout => AppCode.LayoutModel.GetCurrent();

    protected void Page_Load(object sender, EventArgs e) {

      if (WebHelper.IsRequestExiting()) return;

      SystemWeb.AddResponseHeader("Cache-Control", "no-cache, no-store");

      // Get page message, if any, and clear it.
      // TODO: Static (not popup) page messages at top of page.
      Layout.PageMessageText = WebHelper.GetNextPageMessageText();
      Layout.PageMessageType = WebHelper.GetNextPageMessageType();
      WebHelper.ClearNextPageMessageText();

      string browserPageTitle = "";
      if (SystemWeb.GetRequestItemValue(ConfigHelper.RequestItems.IsLoggedInPage).ToBooleanOrDefault(false) == true) {
        browserPageTitle = SystemWeb.GetRequestItemValue(ConfigHelper.RequestItems.PageTitle).ToString();
        if (browserPageTitle.IsNullOrEmpty()) {
          // Get page title from url. e.g. If url is "/ProgramParticipants.aspx" then title will be "Program Participants"
          browserPageTitle = Request.Path.RegexFirstGroupOrNull(@"([^/]*)\..*$").EmptyIfNull().RegexReplace("([a-z])([A-Z])", "$1 $2");
        }
        browserPageTitle = " - " + browserPageTitle;
      }

      // Add title tag for dev or staging.
      if (!browserPageTitle.IsNullOrEmpty()) browserPageTitle += " ";
      browserPageTitle += WebHelper.GetDevOrStagingSiteTagText();
      Layout.BrowserPageTitle = browserPageTitle;

      // Determine body class for this page url.
      string urlPath = Request.RawUrl.ToLower();
      urlPath = urlPath.Mid(PathHelper.WebRoot.Length); // Ensures root subfolder, if any, is removed.
      urlPath = urlPath.Replace("/", "-");
      urlPath = Regex.Replace(urlPath, @"/?\?.*", "");
      if (urlPath == "-" || urlPath == "") urlPath = "-default";
      WebHelper.AddBodyClass($"{(ConfigHelper.IsDevServer ? "devserver" : "")} hold-transition skin-blue sidebar-mini page{urlPath}");
    }

    protected override void Render(HtmlTextWriter writer) {

      if (WebHelper.IsRequestExiting()) return;

      base.Render(writer);
    }

    public string GetTrackingUserRoleName() {

      switch (SessionHelper.GetUserRole()) {
        case ConfigHelper.UserRole.Coach:
          return "Coach";
        case ConfigHelper.UserRole.Client:
          return "Client";
        case ConfigHelper.UserRole.Leader:
          return "Participant";
        default:
          return "Unknown";
      }
    }

    /// <summary>
    /// Checks if Amplitude should be loaded for the current request.
    /// </summary>
    /// <returns>True if Amplitude SDK should be loaded, false otherwise.</returns>
    public bool ShouldLoadAmplitude() {
      return !string.IsNullOrEmpty(ConfigHelper.Amplitude.ApiKey)
        && SessionHelper.IsUserLoggedIn;
    }

    /// <summary>
    /// Checks if Amplitude session replay is enabled for the current logged-in user based on their role.
    /// If no roles are configured (null or empty), session replay is enabled for all roles.
    /// </summary>
    /// <returns>True if session replay should be enabled for the current user's role, false otherwise.</returns>
    public bool IsAmplitudeSessionReplayEnabled() {
      if (!SessionHelper.IsUserLoggedIn) {
        return false;
      }

      // If no configuration provided (null or empty), allow session replay for all roles
      if (string.IsNullOrEmpty(ConfigHelper.Amplitude.SessionReplayAllowedRoles)) {
        return true;
      }

      var userRole = SessionHelper.GetUserRole();
      var allowedRoles = GetAmplitudeSessionReplayAllowedRoles();

      return allowedRoles.Any(r => r.Equals(userRole.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the list of user roles that are allowed to have session replay enabled.
    /// </summary>
    private System.Collections.Generic.List<string> GetAmplitudeSessionReplayAllowedRoles() {
      return ConfigHelper.Amplitude.SessionReplayAllowedRoles
        .Split(',')
        .Select(r => r.Trim())
        .Where(r => !string.IsNullOrEmpty(r))
        .ToList();
    }

    /// <summary>
    /// Generates the JavaScript for an amplitude.identify() call with user properties.
    /// This sets user properties directly on the frontend SDK as a reliable complement
    /// to the backend Identify API, ensuring properties are always available in Amplitude.
    /// </summary>
    public string GetAmplitudeIdentifyScript() {
      if (!SessionHelper.IsUserLoggedIn) return string.Empty;

      var userInfo = SessionHelper.GetUserInfoOrNull();
      if (userInfo == null) return string.Empty;

      var userRole = SessionHelper.GetUserRole();
      var planType = userInfo.GetLearnerPlanType();

      var script = new System.Text.StringBuilder();
      script.AppendLine("var identifyEvent = new window.amplitude.Identify();");
      script.AppendLine($"identifyEvent.set('user_name', '{userInfo.EmailAddress.JSEncode()}');");
      script.AppendLine($"identifyEvent.set('first_name', '{userInfo.FirstName.JSEncode()}');");
      script.AppendLine($"identifyEvent.set('last_name', '{userInfo.LastName.JSEncode()}');");
      script.AppendLine($"identifyEvent.set('email', '{userInfo.EmailAddress.JSEncode()}');");
      script.AppendLine($"identifyEvent.set('environment', '{ConfigHelper.EnvironmentType.JSEncode()}');");
      script.AppendLine($"identifyEvent.set('user_role', '{userRole.ToString().JSEncode()}');");
      script.AppendLine($"identifyEvent.set('internal_id', {userInfo.UserId});");
      script.AppendLine($"identifyEvent.set('org_id', {userInfo.OrgId});");
      script.AppendLine($"identifyEvent.set('org_name', '{userInfo.OrgName.JSEncode()}');");

      if (userInfo.ClientCompanyId.HasValue && userInfo.ClientCompanyId.Value > 0) {
        script.AppendLine($"identifyEvent.set('client_company_id', {userInfo.ClientCompanyId.Value});");
        script.AppendLine($"identifyEvent.set('client_company_name', '{userInfo.ClientCompanyName.JSEncode()}');");
      }

      if (!string.IsNullOrEmpty(planType)) {
        script.AppendLine($"identifyEvent.set('plan_type', '{planType.JSEncode()}');");
      }

      script.AppendLine("window.amplitude.identify(identifyEvent);");

      return script.ToString();
    }

  }

}
