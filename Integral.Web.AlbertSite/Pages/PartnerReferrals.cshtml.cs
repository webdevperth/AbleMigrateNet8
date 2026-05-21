using Integral.Web.Services;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class PartnerReferrals : AppCode.PageBaseClasses.CoachInfoBase {

    public List<DbHelper.PartnerReferrals.PartnerReferral> ReferralList;

    public class FormFields {
      public const string FirstName = "FirstName";
      public const string LastName = "LastName";
      public const string EmailAddress = "EmailAddress";
    }
    public class AjaxAction {
      public const string AddReferral = "AddReferral";
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Referrals";

      ReferralList = DbHelper.PartnerReferrals.GetAllForUserId(CoachInfo.UserId);

      if (SystemWeb.IsHttpPost) {

        if (CoachInfo.UserId == userInfo.UserId) {
          WebHelper.EndRequest(WebHelper.HttpStatusEnum.Forbidden);
          return new EmptyResult();
        }

        AjaxSubmitHelper.Process(ajax => {
          if (ajax.Action == AjaxAction.AddReferral) {
            AddReferral(ajax);
          }
        });

        WebHelper.EndRequest();
        return new EmptyResult();
      }

      return Page();
    }

    void AddReferral(AjaxSubmitHelper ajax) {

      var firstName = ajax.CheckFieldRegex(FormFields.FirstName, "First Name", AppHelper.Regex.GeneralText, true, "Please use plain text for name.");
      var lastName = ajax.CheckFieldRegex(FormFields.LastName, "Last Name", AppHelper.Regex.GeneralText, true, "Please use plain text for name.");
      var emailAddress = ajax.CheckFieldRegex(FormFields.EmailAddress, "Email Address", AppHelper.Regex.Email, true, "Please provide a valid email address.");
      if (ajax.BadFieldCount > 0) return;

      if (ReferralList.Exists(i => i.InviteeEmail.ToLower() == emailAddress.ToLower())) {
        ajax.AddBadField(FormFields.EmailAddress, "That email address is already in your list.");
        return;
      }

      var inviteeBasicInfo = new DbHelper.AbleUser.InviteeBasicInfo(
        firstName, lastName, emailAddress,
        DbHelper.AbleUser.UserRoleEnum.Coach,
        userInfo.OrgId);

      DbHelper.AbleUser.AbleUserBasicInfo inviteeUserInfo;
      try {
        inviteeUserInfo = DbHelper.AbleUser.CreateInviteeUser(null, CoachInfo, inviteeBasicInfo);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("PartnerReferrals_CreateInviteeUser")
          .WithProperty("EmailAddress", emailAddress)
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .AddExternalUserId(ExternalUserKind.Coach, ConfigHelper.UserRole.Coach.ToExternalUserId(CoachInfo.UserGuid))
          .Track();
        ajax.AddDialogMessage("Problem creating invite, please try again later.", ex);
        return;
      }

      Exception sendException = null;
      AlbertEmails.UserInviteResult sendInviteEmail = null;

      try {
        // Send invite email.
        sendInviteEmail = AlbertEmails.TrySendUserInvite(inviteeUserInfo, userInfo);

      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("PartnerReferrals_SendInviteEmail")
          .WithProperty("EmailAddress", inviteeBasicInfo.EmailAddress)
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .AddExternalUserId(ExternalUserKind.Coach, ConfigHelper.UserRole.Coach.ToExternalUserId(inviteeUserInfo.UserGuid))
          .Track();
        sendException = ex;
      }

      if (!sendInviteEmail.IsSuccessful) {
        ajax.AddDialogMessage($"Failed to send email to {inviteeBasicInfo.EmailAddress.HTMLEncode()}.", sendException);
        return;
      }

      // Send Intercom event for partner referral invitation
      var invitedExternalId = ConfigHelper.UserRole.Coach.ToExternalUserId(inviteeUserInfo.UserGuid);
      if (invitedExternalId.HasValue) {
        SendEvent(
          intercom => intercom.TeamMemberInvited()
            .FromSession()
            .WithInvitedUser(invitedExternalId.Value, inviteeUserInfo.GetFullName(), inviteeBasicInfo.EmailAddress)
            .WithInvitedRole("coach"),
          operationName: "PartnerReferrals_TeamMemberInvited",
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["InvitedUserEmail"] = inviteeBasicInfo.EmailAddress,
            ["InvitedRole"] = "coach",
            ["InvitedExternalId"] = invitedExternalId.Value
          }
        );
      }

      ajax.SetReloadPage(sendInviteEmail.Message); // Reload page to show new item.
    }

  }
}
