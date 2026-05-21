using Integral.Integrations.Amplitude;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static Integral.Web.AmplitudeHelper;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class Index : PageModel {

    public class FormFields {
      public const string EmailAddress = "email";
      public const string Password = "password";
    }

    public IActionResult OnGet() {

      string loginRedirectUrl = SessionHelper.GetLoginRedirectUrl();
      var user = SessionHelper.GetUserInfoOrNull();

      // If user logged in, redirect.
      if (user != null) {
        if (!loginRedirectUrl.IsNullOrEmpty()) {
          WebHelper.Redirect(loginRedirectUrl);
        } else {
          WebHelper.Redirect(PathHelper.GetDefaultPostLoginURL(user));
        }
        return new EmptyResult();
      }

      // User not logged in...

      // Get invite code from post-login url if they exist.
      var inviteCode = WebHelper.GetQueryStringValueFromUrl(loginRedirectUrl, PathHelper.AbleUrlKeys.UserInviteCode, null);

      // Check if the user was redirected here to log in from a participant page.
      if (PathHelper.IsParticipantPage(loginRedirectUrl)) {
        // Get user info using userid from the redirection url's querystring.
        if (int.TryParse(WebHelper.GetQueryStringValueFromUrl(loginRedirectUrl, PathHelper.AbleUrlKeys.UserId, ""), out int urlUserId)) {
          user = DbHelper.AbleUser.GetUserByIdOrNull(urlUserId, DbHelper.AbleUser.RegisteredFilter.Any);
          if (user != null) {
            // Check if the user has IsParticipant set and isn't yet registered.
            if (user.IsParticipant && !user.IsRegistered) {
              // If no invite code, create one as if "from" the latest coachee's coach, if that exists.
              if (user.InviteCode.IsNullOrEmpty() && user.LatestCoachingInfo?.IsCoachAssigned == true) {
                int invitedByUserId = DbHelper.AbleUser.GetInvitedByUserId(user);
                DbHelper.AbleUser.UpdateInviteDetails(user, invitedByUserId);
              }
            }
            inviteCode = user?.InviteCode;
          }
        }
      }

      // If an invite code exists, redirect instead to registration (after registration it will redirect to the originally requested page)
      if (!inviteCode.IsNullOrEmpty()) {
        WebHelper.Redirect(PathHelper.Pages.RegisterInvited(inviteCode));
        return new EmptyResult();
      }

      return Page();
    }

    public IActionResult OnPost() {

      AjaxSubmitHelper.Process(ajax => {

        string loginRedirectUrl = SessionHelper.GetLoginRedirectUrl();
        string loginEmail = ajax.CheckFieldRegex(FormFields.EmailAddress, "Email", AppHelper.Regex.Email, true, "Please enter a valid email address.");
        string loginPassword = ajax.CheckFieldRegex(FormFields.Password, "Password", ".*", true, "");

        if (ajax.BadFieldCount > 0) return;

        // Check credentials.
        if (!SessionHelper.TryLogin(loginEmail, loginPassword, out _)) {
          ajax.AddBadField(FormFields.Password, "Incorrect email address or password.");
          return;
        }

        var externalUserId = SessionHelper.AsExternalUserId();
        var deviceId = SessionHelper.GetSessionGuid().ToAmplitudeDeviceId();

        if (externalUserId.HasValue) {
          SendEvent(
            amplitude => amplitude.TrackEvent(
              externalUserId.Value,
              deviceId,
              AmplitudeEventConstants.Login,
              null,  // eventProperties
              null   // userPropertyOperations
            ),
            operationName: "Login_UserLogin",
            eventName: AmplitudeEventConstants.Login,
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: null
          );
        }

        SessionHelper.SetLoginRequiredRedirectedFrom(SessionHelper.LoginRequiredRedirectedFrom.Login);

        // Check if user should go somewhere after login.
        if (!loginRedirectUrl.IsNullOrEmpty()) {
          SessionHelper.SetLoginRedirectUrl(null); // Ensure post-login redirect is cleared now.
          ajax.SetRedirectUrl(loginRedirectUrl);
        } else {
          ajax.SetRedirectUrl(PathHelper.GetDefaultPostLoginURL(SessionHelper.GetUserInfoOrNull()));
        }
      });

      return new EmptyResult();
    }

    public string GetInitialEmailAddr() {

      return WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.UserEmailAddress) ?? SessionHelper.GetLoginLastUsedEmailAddress();
    }

  }
}
