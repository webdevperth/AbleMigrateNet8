using System;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Integral.Web.PortalSite {

  public partial class ResetPassword : System.Web.UI.Page {

    public class FormFields {
      public const string EmailAddress = "email";
      public const string Password = "password";
      public const string Password2 = "password2";
    }

    public string _UrlResetCode;
    public string _UserFirstName;
    public string _UserEmailAddress;

    public bool Reset1Visible = false;
    public bool Reset2Visible = false;
    public bool CodeNotFoundVisible = false;

    protected void Page_Load(object sender, EventArgs e) {

      Guid urlResetGuid;
      _UrlResetCode = "" + WebHelper.GetQueryStringValue("code");
      bool resetGuidOk = Guid.TryParse(_UrlResetCode, out urlResetGuid);

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {
          if (resetGuidOk) {
            SaveNewPassword(ajax, urlResetGuid);
          } else {
            SendResetPasswordEmail(ajax);
          }
        });

        return;
      }

      if (resetGuidOk) {
        CheckGuidAndGetNewPassword(urlResetGuid); // As for new password.
      } else {
        if (_UrlResetCode.IsNullOrEmpty()) {
          Reset1Visible = true; // Initial page asking for email address.
        } else {
          CodeNotFoundVisible = true;
        }
      }
    }

    public string GetPostUrl(string newResetCode = null) {
      string resetCode;
      if (newResetCode != null) {
        resetCode = newResetCode;
      } else if (!_UrlResetCode.IsNullOrEmpty())
        resetCode = _UrlResetCode;
      else {
        resetCode = "";
      }
      return PathHelper.Pages.ResetPassword(resetCode, true);
    }

    void SendResetPasswordEmail(AjaxSubmitHelper ajax) {

      string userEmail = ajax.CheckFieldEmail(FormFields.EmailAddress, "Email Address", true, "Please enter a valid email address");

      if (ajax.BadFieldCount > 0) return;

      // Find user.
      var userInfo = DbHelper.AbleUser.GetUserByEmailOrNull(userEmail.TrimWhitespace(), DbHelper.AbleUser.RegisteredFilter.Any);

      if (userInfo == null) {
        ajax.AddBadField(FormFields.EmailAddress, "No account with that email address.");
        return;
      }

      if (userInfo.IsSoftDeleted) {
        ajax.SetRedirectUrl(PathHelper.Pages.Home());
        return;
      }

      if (!SessionHelper.AppAccess.Users.CanResetPassword(userInfo)) {
        ajax.AddDialogMessage("No account found.<br/>Please contact your coach or email <a href=\"mailto:coordination@integral.global\">coordination@integral.global</a>");
        return;
      }

      if (!userInfo.IsRegistered && !userInfo.IsParticipant) {
        // If they haven't completed their registration yet, re-send invitation.
        AlbertEmails.TrySendUserInvite(userInfo, null, false);
        ajax.AddDialogMessage($@"
          <p>Hi {userInfo.FirstName}!</p>
          <p>Instructions on how to register have been sent to your email address.</p>
          <p>You will be able to set your password during registration.</p>");
        return;
      }

      // Create password reset.
      string newResetCode = DbHelper.AbleUser.CreatePasswordReset(userInfo.UserId).ToString();

      // Email user. If on staging, make sure it's sent to the requester.
      if (ConfigHelper.IsStagingServer) EmailHelper.SetRecipientOverrideAddressForRequest(userInfo.GetFullName(), userInfo.EmailAddress);
      AlbertEmails.SendGenericEmail(
        null,
        "Your Password Reset Link", $@"
        Hi {userInfo.FirstName},<br/>
        <br/>
        Please click the link below to finish resetting your password.<br/>
        <br/>
        <a href=""{GetPostUrl(newResetCode)}"">{GetPostUrl(newResetCode)}</a><br/>
        <br/>
        All the best,<br/>
        The team at Able<br/>.",
        false,
        new MailAddress(userInfo.EmailAddress, userInfo.GetFullName())
      );
    }

    void CheckGuidAndGetNewPassword(Guid resetGuid) {

      var userInfo = DbHelper.AbleUser.GetPasswordResetUserInfoOrNull(resetGuid);

      if (userInfo == null) {
        CodeNotFoundVisible = true;
        return;
      }

      _UserFirstName = userInfo.FirstName;
      _UserEmailAddress = userInfo.EmailAddress;

      Reset2Visible = true;
    }

    void SaveNewPassword(AjaxSubmitHelper ajax, Guid resetGuid) {

      var userInfo = DbHelper.AbleUser.GetPasswordResetUserInfoOrNull(resetGuid);

      if (userInfo == null) {
        ajax.AddDialogMessage("Can't find this password reset code.<br/>Please try the link again from your reset email.");
        return;
      }

      string password = ajax.CheckFieldRegex("password", "Password", "^.+$", true, "Please enter a vaid password.");
      string password2 = ajax.CheckFieldRegex("password2", "Password", "^.+$", true, "Please confirm the password.");

      if (ajax.BadFieldCount > 0) return;

      if (password.Length < DbHelper.AbleUser.PASSWORD_MIN_LENGTH) {
        ajax.AddBadField("password", $"Password must be at least {DbHelper.AbleUser.PASSWORD_MIN_LENGTH} characters.");
        return;
      }

      if (!Regex.IsMatch(password, "[0-9]")) {
        ajax.AddBadField("password", "Password must include at least one number (0-9).");
        return;
      }

      if (!Regex.IsMatch(password, "[" + DbHelper.AbleUser.PASSWORD_REQUIRED_SYMBOLS + "]")) {
        ajax.AddBadField("password", "Include at least one of the symbols shown below.");
        return;
      }

      if (Regex.IsMatch(password, "[^a-zA-Z0-9" + DbHelper.AbleUser.PASSWORD_REQUIRED_SYMBOLS + "]")) {
        ajax.AddBadField("password", "Please use only letters, numbers and basic symbols.");
        return;
      }

      if (password != password2) {
        ajax.AddBadField("password2", "Passwords do not match. Enter the same password in both boxes.");
        return;
      }

      // All good, save the new password.
      DbHelper.AbleUser.UpdatePassword(null, userInfo, password);
      DbHelper.AbleUser.DeletePasswordReset(userInfo.UserId);

      // If user is currently unregistered (e.g. hasn't used their invite link),
      // they can become registered here by doing a password reset.
      if (userInfo.RegisteredUtc == null) {
        DbHelper.AbleUser.UpdateRegisteredDate(null, userInfo, DateTime.UtcNow);
      }

      SessionHelper.TryLogin(userInfo.EmailAddress, password, out var user);
      if (user != null) {
        ajax.AddSuccessDialog("Your password has been reset.");
        ajax.SetRedirectUrl("/");
      }

    }

  }
}
