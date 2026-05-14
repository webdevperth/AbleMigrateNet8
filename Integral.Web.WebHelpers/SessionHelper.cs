using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Web; // Migration: Replace with Microsoft.AspNetCore.Http
using Integral.Web.Services;

namespace Integral.Web {

  public partial class SessionHelper {

    private const int LastRequestUtc_MinGap_Milliseconds = 2000;

    public enum LoginRequiredRedirectedFrom { None, Login, Registration }

    internal class SessionInfo {

      public Guid SessionGuid { get; private set; }
      public DbHelper.AbleUser.AbleUserInfo UserInfo { get; internal set; }
      public DateTime CreatedUtc { get; private set; }
      public DateTime LastRequestUtc { get; private set; }
      public string SettingJsonFromDb { get; internal set; }
      public SessionSettings Settings { get; internal set; }

      // Note sessions must exist first in the db, so dbSessionInfo is required to create a session object.
      public SessionInfo(DbHelper.UserLoginSession.SessionInfo dbSessionInfo) {
        Init(dbSessionInfo, null, null);
      }

      public SessionInfo(DbHelper.UserLoginSession.SessionInfo dbSessionInfo, DbHelper.AbleUser.AbleUserInfo loggedInUserInfo, SessionInfo.SessionSettings settingsOrNullForDefault) {
        Init(dbSessionInfo, loggedInUserInfo, settingsOrNullForDefault);
      }

      private void Init(DbHelper.UserLoginSession.SessionInfo dbSessionInfo, DbHelper.AbleUser.AbleUserInfo loggedInUserInfo, SessionInfo.SessionSettings settingsOrNullForDefault) {

        if (dbSessionInfo == null) throw new ArgumentException("dbSessionInfo required.");
        if ((dbSessionInfo.LoggedInUserId ?? 0) != (loggedInUserInfo?.UserId ?? 0)) throw new ArgumentException("dbSessionInfo doesn't match loggedInUserInfo");

        SessionGuid = dbSessionInfo.SessionGuid;
        CreatedUtc = dbSessionInfo.CreatedUtc;
        LastRequestUtc = dbSessionInfo.LastRequestUtc;
        UserInfo = loggedInUserInfo; // Null if browser session isn't logged in.
        Settings = settingsOrNullForDefault ?? new SessionSettings();
      }

      // Object used for persistent session settings in the db as a Json string.
      internal class SessionSettings {
        public bool LoggedInAsAdmin = false;
        public bool LoggedInWithAdminTools = false;
        public bool LoginRememberMe = true;
        public string LoginLastUsedEmailAddress = null;
        public bool ShowAdminMenu = false;
        [JsonConverter(typeof(StringEnumConverter))]
        public ConfigHelper.UserRole UserRole = ConfigHelper.UserRole.Unset;
        public string PublicReport_AccessCode = null;
        public Guid? PublicReport_CoacheeGuid = null;
        public bool PublicReport_IsLoggedIn = false;
      }

    }

    private static SessionInfo GetOrCreateSession() {
      // Note this method will ALWAYS return a valid session object, never null.
      // A session is either found or a new one is created.

      // First check if session info stored in request collection (i.e. it's been obtained previously in this same request).
      var requestItemSession = GetSessionFromRequestItem();
      if (requestItemSession != null) return requestItemSession;

      // First time getting session info for this request. Check for session Guid in cookie,
      bool sessionGuidExists = Guid.TryParse(GetSessionCookieGuidString() ?? "", out var sessionGuid);
      if (!sessionGuidExists) {
        return CreateNewSession();
      }

      // Find session Guid in db.
      // If not found or is expired, return with new session.
      var dbSessionInfo = DbHelper.UserLoginSession.GetSession(sessionGuid);
      if (dbSessionInfo == null || dbSessionInfo.LastRequestUtc.AddDays(ConfigHelper.DefaultLoginSessionTimeoutDays) < DateTime.UtcNow) {
        return CreateNewSession();
      }

      // Found session in db - this occurs once per request, so update last request time.
      // Note only update LastRequestUtc for GET requests to actual "pages", i.e. not partials, and not POSTs.
      if (SystemWeb.IsHttpGet && PathHelper.IsCurrentUrlAPage()) {
        DbHelper.UserLoginSession.UpdateLastRequestUtc(dbSessionInfo);
      }

      // Get settings object from the db json.
      var sessionSettings = new SessionInfo.SessionSettings(); // Default settings.
      if (!dbSessionInfo.SettingsJson.IsNullOrEmpty()) {
        try {
          sessionSettings = JsonToSettings(dbSessionInfo.SettingsJson);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation(nameof(GetOrCreateSession))
            .WithOperationContext("JsonToSettings")
            .WithProperty(ApplicationInsightsConstants.SessionGuid, dbSessionInfo?.SessionGuid.ToString())
            .WithProperty(ApplicationInsightsConstants.SettingsJson, dbSessionInfo?.SettingsJson)
            .Track();
          // ignore, default settings will remain.
        }
      }

      // Get user if logged in.
      DbHelper.AbleUser.AbleUserInfo sessionUserInfo = null;
      if (dbSessionInfo.LoggedInUserId != null) {
        sessionUserInfo = DbHelper.AbleUser.GetUserByIdOrNull(dbSessionInfo.LoggedInUserId.Value, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);
      }

      // If user is in Integral tenant, allow access to Jarvis Org survey templates.
      if (sessionUserInfo?.OrgId == ConfigHelper.IntegralTenantOrgId) sessionUserInfo.AddSurveyTemplateOrgId(ConfigHelper.JarvisOrgId);

      requestItemSession = new SessionInfo(dbSessionInfo, sessionUserInfo, sessionSettings);
      SaveSessionInRequestItem(requestItemSession); // Store for the rest of this request.

      return requestItemSession;
    }

    private static SessionInfo CreateNewSession() {

      var sessionSettings = new SessionInfo.SessionSettings(); // Default settings.
      var dbSessionInfo = DbHelper.UserLoginSession.GetNewSessionInfo(SettingsToJson(sessionSettings));

      DbHelper.UserLoginSession.CreateSession(dbSessionInfo);

      SetSessionCookie(dbSessionInfo.SessionGuid); // Set/refresh browser cookie.

      var sessionInfo = new SessionInfo(dbSessionInfo, null, sessionSettings);
      SaveSessionInRequestItem(sessionInfo); // Store for the rest of this request.

      return sessionInfo;
    }

    public static bool IsPostLoginRedirectToParticipantPage() => PathHelper.IsParticipantPage(GetLoginRedirectUrl());

    public static bool TryLogin(string emailAddress, string password, out DbHelper.AbleUser.AbleUserInfo loggedInUserInfo) {

      loggedInUserInfo = null;

      // Get user from db by email address.
      var userInfo = DbHelper.AbleUser.GetUserByEmailOrNull(emailAddress, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);

      if (userInfo == null) return false; // User not found.

      if (userInfo.IsSoftDeleted) return false; // Do not allow to log int blocked users.

      // Only these user types can log in at the moment.
      if (!userInfo.IsAbleAdmin && !userInfo.IsAbleCoach && !userInfo.IsAbleClient && !userInfo.IsParticipant) return false;

      // If hashed password doesn't yet exist, create it from the plain-text password.
      if (userInfo.PasswordHashed.IsNullOrEmpty() && !userInfo.PasswordPlainText.IsNullOrEmpty()) {
        DbHelper.AbleUser.UpdatePassword(null, userInfo, userInfo.PasswordPlainText); // This also removes the plain-text password.
      }

      if (DbHelper.AbleUser.IsPasswordCorrect(password, userInfo.PasswordSalt, userInfo.PasswordHashed)
        || (ConfigHelper.IsDevServer && password == ConfigHelper.DevServerTestPassword)) {

        var sessionInfo = GetOrCreateSession();

        if (sessionInfo.Settings.LoginRememberMe && sessionInfo.Settings.LoginLastUsedEmailAddress != userInfo.EmailAddress) {
          // Remember login email.
          SetLoginLastUsedEmailAddress(userInfo.EmailAddress);
        }

        SetLoggedInUser(userInfo);
        loggedInUserInfo = userInfo;
        DbHelper.AbleUser.UpdateLastLoginUtc(null, userInfo, DateTime.UtcNow);

        // If user is admin, remember admin status for when user changes login to another user.
        // This is so an admin can change users more than once without logging out and back in as admin.
        if (userInfo.IsAbleAdmin && ConfigHelper.IsDevServer) {
          sessionInfo.Settings.LoggedInAsAdmin = true;
          if (AppAccess.PageAccess.CanAccessAdminTools()) sessionInfo.Settings.LoggedInWithAdminTools = true;
          SaveSessionSettings();
        }

        return true;
      }

      return false;
    }

    // Note this can be used for
    // a) initial login,
    // b) by admin to switch identities,
    // c) log out (pass null)
    public static void SetLoggedInUser(DbHelper.AbleUser.AbleUserInfo userInfo) {

      var sessionInfo = GetOrCreateSession();

      sessionInfo.UserInfo = userInfo;
      DbHelper.UserLoginSession.UpdateLoggedInUserId(sessionInfo.SessionGuid, userInfo?.UserId);

      if (userInfo == null) {
        SetUserRole(ConfigHelper.UserRole.Unset);
        return;
      }

      var userRole = GetDefaultUserRole(userInfo);

      // If the user was directed to log in from a participant page, and has IsParticipant set, set the user role to Participant.
      if (PathHelper.IsParticipantPage(GetLoginRedirectUrl()) && userInfo?.IsParticipant == true) userRole = ConfigHelper.UserRole.Leader;

      SetUserRole(userRole);
      SetShowAdminMenu(AppAccess.PageAccess.CanAccessAdminTools());

      // Default admin coach view to current user.
      AppState.Coachees.SetFilterScope(AppState.Coachees.FilterScope.Coach, userInfo.UserId, userInfo.GetFullName());
    }

    public static void LogOut() {
      SetLoggedInUser(null);
    }

    public static void SetLoginRememberMe(bool rememberMe) {
      var sessionInfo = GetOrCreateSession();
      sessionInfo.Settings.LoginRememberMe = rememberMe;
      SaveSessionSettings();
    }

    public static void SetLoginLastUsedEmailAddress(string loginLastUsedEmailAddress) {
      var sessionInfo = GetOrCreateSession();
      sessionInfo.Settings.LoginLastUsedEmailAddress = loginLastUsedEmailAddress;
      SaveSessionSettings();
    }

    public static void SetShowAdminMenu(bool showAdminMenu) {
      var sessionInfo = GetOrCreateSession();
      sessionInfo.Settings.ShowAdminMenu = showAdminMenu;
      SaveSessionSettings();
    }

    private static void SaveSessionSettings() {
      var sessionInfo = GetOrCreateSession();
      DbHelper.UserLoginSession.UpdateSettingsJson(sessionInfo.SessionGuid, SettingsToJson(sessionInfo.Settings));
    }

    private static string SettingsToJson(SessionInfo.SessionSettings settings) {
      return JsonConvert.SerializeObject(settings, Formatting.None);
    }

    private static SessionInfo.SessionSettings JsonToSettings(string settingsJson) {
      return JsonConvert.DeserializeObject<SessionInfo.SessionSettings>(settingsJson);
    }

    private static string GetSessionCookieGuidString() {
      return SystemWeb.GetRequestCookieValue(ConfigHelper.CookieNames.AbleLoginSession);
    }

    private static void SetSessionCookie(Guid sessionGuid) {

      var descriptor = new SessionCookieDescriptor {
        Name = ConfigHelper.CookieNames.AbleLoginSession,
        Value = sessionGuid.ToString(),
        HttpOnly = true,
        SameSite = SameSiteMode.Lax, // Lax mainly so user cookie isn't lost when redirecting from protection.outlook.com.
        Secure = !ConfigHelper.IsDevServer,
        Expires = DateTime.UtcNow.AddDays(ConfigHelper.LoginSessionTimeoutDays)
      };
      SystemWeb.AddResponseCookie(descriptor);
    }

    private static SessionInfo GetSessionFromRequestItem() {
      return AppHelper.GetRequestItemOrNull(ConfigHelper.RequestItems.SessionHelper_SessionInfo) as SessionInfo;
    }

    private static void SaveSessionInRequestItem(SessionInfo sessionInfo) {
      AppHelper.SetRequestItem(ConfigHelper.RequestItems.SessionHelper_SessionInfo, sessionInfo);
      if (sessionInfo != null && UserInfo != null) {
        UserInfo.SetCurrentRole(sessionInfo.Settings?.UserRole ?? ConfigHelper.UserRole.Unset);
      }
    }

    public static ConfigHelper.UserRole GetUserRole() {
      var session = GetOrCreateSession();
      if (!IsValidUserRole(session)) {
        SetUserRole(GetDefaultUserRole(session.UserInfo));
      }
      return session.Settings.UserRole;
    }

    public static DateTime? GetSessionCreatedUtc() => GetOrCreateSession()?.CreatedUtc;

    public static void SetUserRole(ConfigHelper.UserRole newUserRole) {

      var sessionInfo = GetOrCreateSession();

      if (IsValidUserRole(sessionInfo.UserInfo, newUserRole)) {
        sessionInfo.Settings.UserRole = newUserRole;
      } else {
        sessionInfo.Settings.UserRole = GetDefaultUserRole(sessionInfo.UserInfo);
      }
      SaveSessionSettings();

      if (UserInfo != null) {
        UserInfo.SetCurrentRole(sessionInfo.Settings.UserRole);
      }
    }

    private static bool IsValidUserRole(SessionInfo session) {
      return IsValidUserRole(session.UserInfo, session.Settings.UserRole);
    }

    private static bool IsValidUserRole(DbHelper.AbleUser.AbleUserInfo user, ConfigHelper.UserRole userRole) {

      if (user == null && userRole == ConfigHelper.UserRole.Unset) return true; // Not logged in + no role.

      if (user == null) {
        throw new InvalidOperationException($"Invalid user state - not logged in but has role: {userRole}");
      } else if (userRole == ConfigHelper.UserRole.Unset) {
        throw new InvalidOperationException($"Invalid user state - logged in but role not set.");
      }

      switch (userRole) {
        case ConfigHelper.UserRole.Admin:
          return user.IsAbleAdmin;
        case ConfigHelper.UserRole.TenantOrgAdmin:
          return user.IsTenantOrgAdmin;
        case ConfigHelper.UserRole.Client:
          return user.IsAbleClient;
        case ConfigHelper.UserRole.Coach:
          return user.IsAbleCoach;
        case ConfigHelper.UserRole.Leader:
          return user.IsParticipant;
        case ConfigHelper.UserRole.OrgReportViewer:
          return user.IsIOSReportViewer;
        default:
          throw new InvalidOperationException($"Unhandled {nameof(userRole)}: {userRole}.");
      }
    }

    public static ConfigHelper.UserRole GetDefaultUserRole() {
      return GetDefaultUserRole(UserInfo);
    }

    public static ConfigHelper.UserRole GetDefaultUserRole(DbHelper.AbleUser.AbleUserInfo user) {

      if (user == null) throw new NullReferenceException($"User cannot be null.");

      // TODO: Default to same role when last logged in (if possible).
      // if (LastLoggedInRole != null) {
      //   Set role if valid for user and return.
      //   If not valid, clear the LastLoggedInRole and continue.
      // }

      // Default to highest privilege first.
      if (user.IsAbleAdmin) return ConfigHelper.UserRole.Admin;
      //if (user.IsTenantOrgAdmin) return ConfigHelper.UserRole.TenantOrgAdmin; - enable when a separate role menu & UX is made available.
      if (user.IsAbleClient) return ConfigHelper.UserRole.Client;
      if (user.IsAbleCoach) return ConfigHelper.UserRole.Coach;
      if (user.IsParticipant) return ConfigHelper.UserRole.Leader;
      if (user.IsIOSReportViewer && user.ViewOnlyIOSReportUniqueId != null) return ConfigHelper.UserRole.OrgReportViewer;

      throw new InvalidOperationException($"Cannot set default user role.");
    }

    public static List<ConfigHelper.UserRole> GetAvailableUserRoles() {
      return GetAvailableUserRoles(UserInfo);
    }

    public static List<ConfigHelper.UserRole> GetAvailableUserRoles(DbHelper.AbleUser.AbleUserBasicInfo user) {

      if (user == null) return null;

      var roleOptions = new List<ConfigHelper.UserRole>();
      if (user.IsAbleAdmin) roleOptions.Add(ConfigHelper.UserRole.Admin);
      //if (user.IsTenantOrgAdmin) roleOptions.Add(ConfigHelper.UserRole.TenantOrgAdmin);
      if (user.IsAbleClient) roleOptions.Add(ConfigHelper.UserRole.Client);
      if (user.IsAbleCoach) roleOptions.Add(ConfigHelper.UserRole.Coach);
      if (user.IsParticipant) roleOptions.Add(ConfigHelper.UserRole.Leader);

      return roleOptions;
    }

    public static string GetUserRoleDisplayName(ConfigHelper.UserRole userRole) {
      if (ConfigHelper.UserRoleDisplayNames.ContainsKey(userRole)) {
        return ConfigHelper.UserRoleDisplayNames[userRole];
      } else {
        throw new InvalidOperationException($"Display name not found for role: {userRole}");
      }
    }

    public static int? GetUserRoleId(ConfigHelper.UserRole userRole) {

      if (userRole == ConfigHelper.UserRole.Unset) return null;

      if (ConfigHelper.UserRoleId.ContainsKey(userRole)) {
        return ConfigHelper.UserRoleId[userRole];
      } else {
        throw new InvalidOperationException($"UserRoleId not found for role: {userRole}");
      }
    }

    public static bool LoggedInAsAdmin => GetOrCreateSession().Settings.LoggedInAsAdmin;

    public static bool LoggedInWithAdminTools => GetOrCreateSession().Settings.LoggedInWithAdminTools;

    public static bool IsUserTester => GetUserInfoOrNull()?.IsUserTester ?? false;

    public static bool IsUserEmailTester => GetUserInfoOrNull()?.IsUserEmailTester ?? false;

    public static bool IsUserIntegral => UserInfo.OrgId == ConfigHelper.IntegralTenantOrgId;

    public static bool IsUserRoleAdmin => GetUserRole() == ConfigHelper.UserRole.Admin;

    // Tenant Admin Note: Includes Coach (Practitioner) role for now, until a separate "Account Admin" role is implemented in UI.
    // i.e. Currently the user can only perform Tenant Admin actions while in the Coach/Prac role,
    //      later we will probably have a separate "Account Admin" option in the Role selector.
    public static bool IsUserRoleTenantAdmin {
      get {
        if (UserInfo == null) return false;
        var u = UserInfo;
        if (UserInfo.CurrentRole == ConfigHelper.UserRole.TenantOrgAdmin) return true;
        if (UserInfo.CurrentRole == ConfigHelper.UserRole.Coach && u.IsTenantOrgAdmin) return true;
        return false;
      }
    }

    public static bool IsUserRoleCoach => GetUserRole() == ConfigHelper.UserRole.Coach;

    public static bool IsUserRoleClient => GetUserRole() == ConfigHelper.UserRole.Client;

    public static bool IsUserRoleLeader => GetUserRole() == ConfigHelper.UserRole.Leader;

    public static bool IsUserIOSClientHR => GetUserInfoOrNull()?.IsIOSClientHR ?? false;

    public static bool CanShowApplicationErrors => ConfigHelper.IsDevServer;

    // Note debug messages are disabled if running in IDE debug mode (Debugger.IsAttached) as response buffering seems to differ from IIS and causes an error.
    public static bool CanShowDebugMessages => ConfigHelper.IsDevServer;

    public static bool IsUserLoggedIn => GetUserInfoOrNull()?.UserId > 0;

    public static bool GetShowAdminMenu() => GetOrCreateSession().Settings.ShowAdminMenu;

    public static string GetLoginLastUsedEmailAddress() => GetOrCreateSession().Settings.LoginLastUsedEmailAddress;

    public static TimeZoneInfo GetSessionTimeZone() => GetUserInfoOrNull()?.TimeZoneInfo ?? ConfigHelper.DefaultTimeZoneInfo;

    public static int? GetUserIdOrNull() => GetUserInfoOrNull()?.UserId;

    public static string GetUserEmailOrNull() => GetUserInfoOrNull()?.EmailAddress;

    public static DbHelper.AbleUser.AbleUserInfo UserInfo => GetUserInfoOrNull();

    public static ConfigHelper.UserRole UserRole => GetUserRole();

    public static DbHelper.AbleUser.AbleUserInfo GetUserInfoOrNull() => GetOrCreateSession()?.UserInfo;

    public static bool TryGetUserInfo(out DbHelper.AbleUser.AbleUserInfo userInfo) {
      userInfo = GetOrCreateSession()?.UserInfo;
      return userInfo != null;
    }

    public static Guid GetSessionGuid() => GetOrCreateSession().SessionGuid;

    public static string GetNextPageMessageText() => AppState.General.NextPageMessageText;

    public static AjaxSubmitHelper.PageMessageType GetNextPageMessageType() => AppState.General.NextPageMessageType;

    public static void SetNextPageMessageText(string message) => AppState.General.NextPageMessageText = message;

    public static void AppendNextPageMessageText(string message) {
      string existingMessage = GetNextPageMessageText();
      if (existingMessage.IsNullOrEmpty() || message.IsNullOrEmpty()) return;
      SetNextPageMessageText(existingMessage + message);
    }

    public static void SetNextPageMessageType(AjaxSubmitHelper.PageMessageType pageMessageType) {
      AppState.General.NextPageMessageType = pageMessageType;
    }

    public static LoginRequiredRedirectedFrom GetLoginRequiredRedirectedFrom(bool clearValue = true) {
      if (clearValue) ClearLoginRequiredRedirectedFrom();
      return AppState.General.LoginRequiredRedirectedFrom;
    }

    public static void SetLoginRequiredRedirectedFrom(LoginRequiredRedirectedFrom loginRequiredRedirectedFrom) {
      AppState.General.LoginRequiredRedirectedFrom = loginRequiredRedirectedFrom;
    }

    public static void ClearLoginRequiredRedirectedFrom() {
      AppState.General.LoginRequiredRedirectedFrom = LoginRequiredRedirectedFrom.None;
    }

    public static string GetLoginRedirectUrl() {
      return AppState.General.LoginRedirectUrl;
    }

    public static void SetLoginRedirectUrl(string url) {
      AppState.General.LoginRedirectUrl = url;
    }

    public static void ClearLoginRedirectUrl() {
      AppState.General.LoginRedirectUrl = null;
    }

    // Note if this returns true, the caller should return from the page.
    public static bool RedirectIfNotLoggedIn(string loginUrl = null) {

      if (UserInfo != null) return false; // User logged in ok, not redirecting.

      // Redirct back home, or return a suitabl ajax response.
      // Use SetLoginRedirectUrl() to remember and return to the current page after logging back in.
      string homeUrl = loginUrl.ValueIfNullOrEmpty(PathHelper.Pages.Home());
      if (SystemWeb.IsAjaxPost) {
        // If POST, return a useful response to the ajax call.
        AjaxSubmitHelper.Process(ajax => {
          if (ajax.Referrer != null) SetLoginRedirectUrl(ajax.Referrer.AbsoluteUri); // The page that made the ajax call.
          ajax.RespondSessionExpired();
          ajax.SetRedirectUrl(homeUrl);
        });
      } else {
        SetLoginRedirectUrl(SystemWeb.RequestRawUrl);
        WebHelper.Redirect(homeUrl);
      }

      return true;
    }

    public static DateTime UserTimeToUtc(DateTime userLocalTime) {
      return userLocalTime.ToUniversalTime(GetSessionTimeZone());
    }

    public static DateTime? UserTimeToUtc(DateTime? userLocalTime) {
      return userLocalTime.ToUniversalTimeOrNull(GetSessionTimeZone());
    }

    public static DateTime UtcToUserTime(DateTime fromUtc) {
      return fromUtc.UtcToTZ(GetSessionTimeZone());
    }

    public static DateTime? UtcToUserTime(DateTime? fromUtc) {
      return fromUtc.UtcToTZOrNull(GetSessionTimeZone());
    }

    public static DateTime UtcNowToUserTime() {
      return DateTime.UtcNow.UtcToTZ(GetSessionTimeZone());
    }

    // Redirect from a public page (e.g. BookSession page) to a logged-in page.
    // Redirection will differ based on user status.
    // If already logged in, go straight there.
    // Otherwise save destination, and if need to register, go to registration, else go to login.
    public static void PublicPageUserRedirect(int userId, string redirectPath) {

      if (IsUserLoggedIn) {
        WebHelper.Redirect(redirectPath);
        return;
      }

      SetLoginRedirectUrl(redirectPath); // Remember destination.

      var userInfo = DbHelper.AbleUser.GetUserByIdOrNull(userId, DbHelper.AbleUser.RegisteredFilter.Any);

      if (userInfo != null && !userInfo.IsRegistered) {

        if (userInfo.InviteCode.IsNullOrEmpty()) {
          int invitedByUserId = DbHelper.AbleUser.GetInvitedByUserId(userInfo);
          DbHelper.AbleUser.UpdateInviteDetails(userInfo, invitedByUserId);
        }

        WebHelper.Redirect(PathHelper.Pages.RegisterInvited(userInfo.InviteCode));
        return;

      } else {

        WebHelper.Redirect(PathHelper.Pages.Home());
        return;
      }
    }

    public class PublicReport {

      public static void SetIsLoggedIn(Guid coacheeGuid, bool isLoggedIn) {
        var sessionInfo = GetOrCreateSession();
        sessionInfo.Settings.PublicReport_CoacheeGuid = coacheeGuid;
        sessionInfo.Settings.PublicReport_IsLoggedIn = isLoggedIn;
        SaveSessionSettings();
      }

      public static bool GetIsLoggedIn(Guid coacheeGuid) {
        var sessionSettings = GetOrCreateSession().Settings;
        return sessionSettings.PublicReport_IsLoggedIn && sessionSettings.PublicReport_CoacheeGuid == coacheeGuid;
      }

      public static void SetAccessCode(Guid coacheeGuid, string accessCode) {
        var sessionSettings = GetOrCreateSession().Settings;
        sessionSettings.PublicReport_CoacheeGuid = coacheeGuid;
        sessionSettings.PublicReport_AccessCode = accessCode;
        SaveSessionSettings();
      }

      public static string GetAccessCode(Guid coacheeGuid) {
        var sessionSettings = GetOrCreateSession().Settings;
        if (sessionSettings.PublicReport_CoacheeGuid == coacheeGuid) {
          return sessionSettings.PublicReport_AccessCode;
        } else {
          return null;
        }
      }

    }

  }

}
