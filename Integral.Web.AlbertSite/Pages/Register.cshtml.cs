using Integral.Integrations.Amplitude;
using Integral.Web;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;
using static Integral.Web.AmplitudeHelper;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class Register : PageModel {

    public class FormFields {
      public const string FirstName = "FirstName";
      public const string LastName = "LastName";
      public const string EmailAddress = "EmailAddress";
      public const string CompanyName = "CompanyName";
      public const string Password1 = "Password1";
      public const string Password2 = "Password2";
      public const string AcceptedTerms = "AcceptedTerms";
      public const string UserRole = "UserRole";
    }

    public class FormValues {
      public string FirstName;
      public string LastName;
      public string EmailAddress;
      public string CompanyName;
      public string Password1;
      public string Password2;
      public bool AcceptedTerms;
      public ConfigHelper.UserRole UserRole;
      public int? SelfRegisteredAsRoleId;
    }

    public class AjaxAction {
      public const string Register = "Register";
    }

    public DbHelper.AbleUser.AbleUserBasicInfo InviteeUserInfo = null;
    public DbHelper.AbleUser.AbleUserBasicInfo InvitedByUserInfo = null;
    public DbHelper.AblePrograms.AbleProgramInfo ProgramInfo = null;

    public bool IsInvited = false;
    public bool IsInvalidCode = false;
    public bool IsAlreadyClaimed = false;
    public bool ShowRegistrationForm = false;

    public string InvitedByCompanyName;

    public IActionResult OnGet() {

      var user = SessionHelper.GetUserInfoOrNull();

      // If user logged in, redirect to the page shown after logging in.
      if (user != null) {
        if (user.IsSoftDeleted) {
          WebHelper.Redirect(PathHelper.Pages.Home());
          return new EmptyResult();
        }
        WebHelper.Redirect(PathHelper.GetDefaultPostLoginURL(user));
        return new EmptyResult();
      }

      if (!SetupForm()) return new EmptyResult();

      return Page();
    }

    public IActionResult OnPost() {

      var user = SessionHelper.GetUserInfoOrNull();

      // If user logged in, redirect to the page shown after logging in.
      if (user != null) {
        if (user.IsSoftDeleted) {
          WebHelper.Redirect(PathHelper.Pages.Home());
          return new EmptyResult();
        }
        AjaxSubmitHelper.Process(ajax => {
          ajax.SetRedirectUrl(PathHelper.GetDefaultPostLoginURL(user));
        });
        return new EmptyResult();
      }

      if (!SetupForm()) return new EmptyResult();

      AjaxSubmitHelper.Process(ajax => {
        if (ShowRegistrationForm && !IsInvalidCode && !IsAlreadyClaimed) {
          CheckForm(ajax);
        }
      });
      return new EmptyResult();
    }

    private bool SetupForm() {

      SetInviteeInfo();
      SetInvitedByInfo();

      ShowRegistrationForm = true;

      // Check if URL contains ProgramId
      int programJobId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.ProgramJobId) ?? 0;
      if (programJobId > 0) {
        ProgramInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(programJobId);
      }

      if (IsInvalidCode || IsAlreadyClaimed) {
        SessionHelper.ClearLoginRedirectUrl();
        WebHelper.Redirect(PathHelper.Pages.Home());
        return false;
      }

      return true;
    }

    // If there's a referral code, try to find the referral info. Must be unclaimed.
    private void SetInviteeInfo() {

      string inviteCode = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.UserInviteCode);

      if (inviteCode.IsNullOrEmpty()) {
        ShowRegistrationForm = true;
        IsInvited = false;
        return;
      }

      IsInvited = true;
      InviteeUserInfo = DbHelper.AbleUser.GetUserByInviteCode(inviteCode);

      if (InviteeUserInfo == null) {

        IsInvalidCode = true;

      } else if (InviteeUserInfo.IsRegistered) {

        IsAlreadyClaimed = true;
      }
    }

    private void SetInvitedByInfo() {
      // Note must set both InvitedByUserInfo and InvitedByCompanyName.

      InvitedByUserInfo = null;
      InvitedByCompanyName = string.Empty;

      if (InviteeUserInfo == null) return;

      // Get InvitedBy user if it was assigned.
      if (InviteeUserInfo.InvitedByUserId != null) {
        InvitedByUserInfo = DbHelper.AbleUser.GetBasicInfoById(InviteeUserInfo.InvitedByUserId.Value, DbHelper.AbleUser.RegisteredFilter.Any);
        InvitedByCompanyName = InvitedByUserInfo.ClientCompanyName;
      }

      if (InviteeUserInfo.IsAbleCoach) {
        // Invitee is a Partner - use their OrgId organisation name.

        InvitedByCompanyName = InviteeUserInfo.OrgName;

      } else if (InviteeUserInfo.IsAbleClient) {
        // Invitee is a Client - Use default logic.

        InvitedByCompanyName = GetClientInvitedByCompanyName();

      } else if (InviteeUserInfo.IsParticipant) {

        // Invitee is Participant/Leader - use invitee's company name.
        InvitedByCompanyName = InviteeUserInfo.LatestCoacheeInfo?.CompanyName;
      }

      // Fallbacks if above fail.
      if (InvitedByUserInfo == null) {
        InvitedByUserInfo = DbHelper.AbleUser.GetBasicInfoById(ConfigHelper.UserId.Automation, DbHelper.AbleUser.RegisteredFilter.Any);
      }
      if (InvitedByCompanyName.IsNullOrEmpty()) {
        InvitedByCompanyName = InvitedByUserInfo.OrgName;
      }
    }

    private string GetClientInvitedByCompanyName() {
      // https://app.clickup.com/t/86cuw7h59 - see update 20/03.
      // For Clients, don't use ClientCompany, use Org (original descr was misunderstood).
      // Use these, in order of priority:
      // 1. If invited user's ClientCompany is set, use that.
      // 2. If invited user is a quote contact, use most recent matching quote owner's Org.
      // 3. If invited user is from Project Access, use most recent project quote owner's Org.

      System.Collections.Generic.List<DbHelper.AbleQuotes.QuoteInfo> quoteList;
      DbHelper.AbleQuotes.QuoteInfo latestQuote;
      DbHelper.AbleUser.AbleUserBasicInfo quoteOwner;

      // If invited user's ClientCompany is set, use that.
      if (InviteeUserInfo.ClientCompanyName != null) {
        return InviteeUserInfo.ClientCompanyName;
      }
      // If invited user is a quote contact, or if not then has project access.
      quoteList = DbHelper.AbleQuotes.GetQuotesByContact(InviteeUserInfo.UserId);
      if (quoteList == null) {
        // 3. If invited user is from Project Access, use most recent project quote owner's Org.
        quoteList = DbHelper.AbleQuotes.GetQuotesByProjectAccess(InviteeUserInfo.UserId);
      }
      if (quoteList != null && quoteList.Count != 0) {
        // Get latest quote in list from above.
        latestQuote = quoteList.OrderByDescending(q => q.CreatedUtc).FirstOrDefault();
        // Return quote owner's Org.
        quoteOwner = DbHelper.AbleUser.GetBasicInfoById(latestQuote.OwnerUserId, DbHelper.AbleUser.RegisteredFilter.Any);
        if (quoteOwner != null) {
          return quoteOwner.OrgName;
        }
      }
      // Default to inviter's Org.
      return InvitedByUserInfo.OrgName;
    }

    void CheckForm(AjaxSubmitHelper ajax) {

      var formValues = new FormValues();

      formValues.FirstName = ajax.CheckFieldPlainText(FormFields.FirstName, "First Name", true);
      formValues.LastName = ajax.CheckFieldPlainText(FormFields.LastName, "Last Name", true);

      if (IsInvited) {

        // Use same company name as inviter.
        formValues.CompanyName = InvitedByUserInfo.OrgName;

        // Don't allow invited user to change email addr.
        formValues.EmailAddress = InviteeUserInfo.EmailAddress;

      } else {

        var selectedUserRole = WebHelper.GetFormValue(FormFields.UserRole);
        // Get Selected user role
        if (Enum.TryParse<ConfigHelper.UserRole>(selectedUserRole, out var parsedUserRole)) {
          formValues.UserRole = parsedUserRole;
        } else {
          ajax.AddBadField(FormFields.UserRole, "Please select an account type.");
        }

        formValues.CompanyName = ajax.CheckFieldPlainText(FormFields.CompanyName, "Company Name", true);

        if (!ajax.BadFieldExists(FormFields.CompanyName) && !formValues.CompanyName.IsNullOrEmpty()) {

          if (parsedUserRole == ConfigHelper.UserRole.Coach) {
            // For Partners, check for same Org (Provider) company name.
            if (DbHelper.TenantOrg.TenantOrgNameExists(formValues.CompanyName)) {
              ajax.AddBadField(FormFields.CompanyName, "Company name is already in use.");
            }
          } else {
            // For Leaders or Clients, check for same ClientCompany company name.
            if (DbHelper.ClientCompanies.CompanyNameExists(formValues.CompanyName)) {
              ajax.AddBadField(FormFields.CompanyName, "Company name is already in use.");
            }
          }
        }

        formValues.EmailAddress = ajax.CheckFieldEmail(FormFields.EmailAddress, "Email Address", true);
        if (!ajax.BadFieldExists(FormFields.EmailAddress) && DbHelper.AbleUser.GetUserByEmailOrNull(formValues.EmailAddress, DbHelper.AbleUser.RegisteredFilter.Any) != null) {
          ajax.AddBadField(FormFields.EmailAddress,
            $"<div><p>Email address is already registered.</p><p>Please try <a href=\"{PathHelper.Pages.ResetPassword()}\"><b>Resetting your password</b></a>.</p></div>");
        }

        formValues.SelfRegisteredAsRoleId = SessionHelper.GetUserRoleId(formValues.UserRole);
      }

      formValues.Password1 = ajax.CheckFieldRaw(FormFields.Password1, "Password", true);
      formValues.Password2 = ajax.CheckFieldRaw(FormFields.Password2, "Confirmed Password", true);

      if (!ajax.BadFieldExists(FormFields.Password1) && formValues.Password1.Length < DbHelper.AbleUser.PASSWORD_MIN_LENGTH) {
        ajax.AddBadField(FormFields.Password1, $"Password must be at least {DbHelper.AbleUser.PASSWORD_MIN_LENGTH} characters.");
      }

      if (!ajax.BadFieldExists(FormFields.Password1) && (!formValues.Password1.RegexIsMatch("[a-zA-Z]") || !formValues.Password1.RegexIsMatch("[0-9]"))) {
        ajax.AddBadField(FormFields.Password1, "Password must contain both letters and numbers.");
      }

      if (!ajax.BadFieldExists(FormFields.Password2) && formValues.Password1 != formValues.Password2) {
        ajax.AddBadField(FormFields.Password2, "Passwords much match.");
      }

      if (ajax.BadFieldCount > 0) return;

      // Form is valid, try to register the user.
      bool registeredOk = false;

      try {
        DbHelper.Common.UsingTransaction(trans => {
          if (IsInvited) {
            registeredOk = DbHelper.AbleUser.RegisterExistingUser(
              trans: trans,
              userBasicInfo: InviteeUserInfo,
              passwordPlainText: formValues.Password1
            );
          } else {
            registeredOk = DbHelper.AbleUser.RegisterNewUser(
              trans: trans,
              firstName: formValues.FirstName,
              lastName: formValues.LastName,
              emailAddress: formValues.EmailAddress,
              password: formValues.Password1,
              newCompanyName: formValues.CompanyName,
              isCoach: formValues.UserRole == ConfigHelper.UserRole.Coach,
              isClient: formValues.UserRole == ConfigHelper.UserRole.Client,
              isParticipant: formValues.UserRole == ConfigHelper.UserRole.Leader,
              selfRegisteredAsRoleId: formValues.SelfRegisteredAsRoleId
            );
          }
          if (formValues.UserRole == ConfigHelper.UserRole.Coach) {
            var coach = DbHelper.AlbertCoaches.GetCoachInfo(trans, formValues.EmailAddress);
            if (coach != null) DbHelper.AlbertCoaches.UpdateIsPartnerActive(trans, coach);
          }
          return true;
        });
      } catch (Exception ex) {
        // Track exception in Application Insights
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("UserRegistration")
          .FromSession()
          .WithProperty("IsInvited", IsInvited)
          .WithPageUrl(SystemWeb.RequestRawUrl)
          .Track();

        ajax.AddDialogMessage("There was a problem creating your account.<br/>Please try again later.<br/>" + (ConfigHelper.IsDevServer ? ex.ToString().Wrap("<pre>", "</pre>") : ""));
        return;
      }
      if (!registeredOk) {
        ajax.AddDialogMessage("A problem occurred creating your account.<br/>Please try again later.");
        return;
      }

      // Log new user in.
      SessionHelper.TryLogin(formValues.EmailAddress, formValues.Password1, out _);

      // Track registration event with user identification
      var userInfo = SessionHelper.GetUserInfoOrNull();
      var userRole = SessionHelper.GetUserRole();
      var externalUserId = userRole.ToExternalUserId(userInfo.UserGuid);
      var deviceId = SessionHelper.GetSessionGuid().ToAmplitudeDeviceId();

      if (externalUserId.HasValue) {
        var eventProperties = new Dictionary<string, object> {
          { AmplitudePropertyConstants.UserRole, IsInvited ? "invited" : formValues.UserRole.ToString() },
          { AmplitudePropertyConstants.RegistrationSource, IsInvited ? "invited" : "self_signup" }
        };

        var userPropertyOperations = new List<AmplitudeUserProperty> {
          new AmplitudeUserProperty(AmplitudeUserPropertyConstants.Email, userInfo?.EmailAddress, AmplitudePropertyOperation.Set)
        };

        SendEvent(
          amplitude => amplitude.TrackEvent(
            externalUserId.Value,
            deviceId,
            AmplitudeEventConstants.UserRegistration,
            eventProperties,
            userPropertyOperations
          ),
          operationName: "Register_UserRegistration",
          eventName: AmplitudeEventConstants.UserRegistration,
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["IsInvited"] = IsInvited,
            ["UserRole"] = IsInvited ? "invited" : formValues.UserRole.ToString(),
            ["RegistrationSource"] = IsInvited ? "invited" : "self_signup"
          }
        );
      }

      // Set track event name for user registration.
      SessionHelper.SetLoginRequiredRedirectedFrom(SessionHelper.LoginRequiredRedirectedFrom.Registration);

      // Send Intercom event for user signup
      SendEvent(
        intercom => intercom.UserSignedUp()
          .FromSession()
          .WithUserRole(IsInvited ? "invited" : formValues.UserRole.ToString())
          .WithCompanyName(formValues.CompanyName ?? "")
          .WithIsInvited(IsInvited),
        operationName: "Register_UserSignedUp",
        requestRawUrl: SystemWeb.RequestRawUrl,
        telemetryProperties: new Dictionary<string, object> {
          ["IsInvited"] = IsInvited,
          ["UserRole"] = IsInvited ? "invited" : formValues.UserRole.ToString(),
          ["CompanyName"] = formValues.CompanyName ?? ""
        }
      );

      // If there is an after-login redirect url, and it includes an invite code, remove the invite code.
      string afterLoginRedirectUrl = SessionHelper.GetLoginRedirectUrl();
      if (!afterLoginRedirectUrl.IsNullOrEmpty()) {
        afterLoginRedirectUrl = Regex.Replace(afterLoginRedirectUrl, $@"\&{PathHelper.AbleUrlKeys.UserInviteCode}={ConfigHelper.UserInviteCodeRegex}", "", RegexOptions.IgnoreCase);
        SessionHelper.SetLoginRedirectUrl(afterLoginRedirectUrl);
        ajax.SetRedirectUrl(afterLoginRedirectUrl);
        return;
      }

      // If current url contains ProgramId, when completing registration, redirect to Program Participants page.
      if (ProgramInfo != null) {
        ajax.SetRedirectUrl(PathHelper.Pages.ProgramParticipants(ProgramInfo.ProgramJobId));
        return;
      }

      // Otherwise, redirect to the page shown after logging in.
      ajax.SetRedirectUrl(PathHelper.Pages.Home(), "Thank you for registering with Able!");
    }
  }
}
