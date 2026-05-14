using Integral.Web.Services;
using System;
using System.Collections.Generic;
using Integral.Web;
using Integral.Web.PortalSite.AppCode;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class ShareSurveyModal : AppCode.PageBaseClasses.LoggedInPageBase {

    public DbHelper.AlbertSurveys.SurveyInfo SurveyToShare = null;

    public class FormFields {
      public const string FirstName = "FirstName";
      public const string LastName = "LastName";
      public const string Email = "Email";
      public const string Message = "Message";
    }

    public class AjaxAction {
      public const string ShareSurvey = "ShareSurvey";
    }

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Share Survey";

      string urlSurveyUId = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SurveyUId);
      string urlPartUId = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PartUId);

      if (urlSurveyUId.IsNullOrEmpty() || urlPartUId.IsNullOrEmpty()) {
        return; // No param received
      }

      SurveyToShare = DbHelper.AlbertSurveys.GetSurveyInfo(urlSurveyUId, urlPartUId);
      if (SurveyToShare == null) {
        return;
      }

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          switch (PageAjaxAction) {

            case AjaxAction.ShareSurvey:
              if (SurveyToShare == null) {
                ajax.AddDialogMessage("Select a valid survey to share");
                ajax.SetRedirectUrl(PathHelper.Pages.ParticipantSurveys());
                return;
              }
              ShareSurvey(ajax);
              break;

          }
        });
        return;
      }
    }

    public void ShareSurvey(AjaxSubmitHelper ajax) {
      string firstName = ajax.CheckFieldRegex(FormFields.FirstName, "First Name", AppHelper.Regex.GeneralText, true, "Please enter a First Name.");
      string lastName = ajax.CheckFieldRegex(FormFields.LastName, "Last Name", AppHelper.Regex.GeneralText, true, "Please enter a Last Name.");
      string emailAddress = ajax.CheckFieldRegex(FormFields.Email, "Email Address", AppHelper.Regex.Email, true, "Please enter a valid Email Address.");
      string customEmailMsg = ajax.CheckFieldRegex(FormFields.Message, "Email Msg", AppHelper.Regex.GeneralText, false, "");

      if (SurveyToShare == null) {
        ajax.AddDialogMessage("Couldn't find survey, try again.");
        return;
      }

      if (ajax.BadFieldCount > 0) {
        return;
      }

      // Get user information to share survey with.
      var userInfoSharingWith = DbHelper.AbleUser.GetBasicInfoByEmail(null, emailAddress, DbHelper.AbleUser.RegisteredFilter.Any);

      if (userInfoSharingWith == null) {
        // Create user
        var inviteeBasicInfo = new DbHelper.AbleUser.InviteeBasicInfo(
          firstName,
          lastName,
          emailAddress,
          DbHelper.AbleUser.UserRoleEnum.Participant,
          userInfo.OrgId
        );

        try {
          userInfoSharingWith = DbHelper.AbleUser.CreateInviteeUser(null, userInfo, inviteeBasicInfo);
          if (userInfoSharingWith == null) {
            ajax.AddDialogMessage("Problem creating user, please try again later.");
            return;
          }
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("ShareSurveyModal_CreateInviteeUser")
            .FromSession()
            .WithPageUrl(Request.RawUrl)
            .WithProperty(ApplicationInsightsConstants.InviteeEmail, emailAddress)
            .WithProperty(ApplicationInsightsConstants.InviteeFirstName, firstName)
            .WithProperty(ApplicationInsightsConstants.InviteeLastName, lastName)
            .WithProperty(ApplicationInsightsConstants.SurveyId, SurveyToShare?.SurveyId)
            .Track();

          ajax.AddDialogMessage("Problem creating user, please try again later.", ex);
          return;
        }
      }

      if (userInfoSharingWith == null) {
        ajax.AddDialogMessage("Couldn't find user, please try again later.");
        return;
      }

      if (DbHelper.SurveyShare.IsSurveyAlreadySharedWithUserId(SurveyToShare.FoundParticipantBrief.PartId, userInfoSharingWith.UserId)) {
        ajax.AddDialogMessage($"You have already shared this survey with {firstName}, try a different one.");
        return;
      }

      bool emailSent = AlbertEmails.SendShareSurveyEmail(userInfo, userInfoSharingWith, SurveyToShare, customEmailMsg);
      if (emailSent) {
        // Keep shared registry
        DbHelper.SurveyShare.AddSurveyShare(userInfoSharingWith.UserId, SurveyToShare.FoundParticipantBrief.PartId);

        // Send Intercom event for survey sharing
        // Determine role of user being shared with
        var sharedWithRole = userInfoSharingWith.IsAbleAdmin ? ConfigHelper.UserRole.Admin :
                           userInfoSharingWith.IsAbleCoach ? ConfigHelper.UserRole.Coach :
                           userInfoSharingWith.IsAbleClient ? ConfigHelper.UserRole.Client :
                           userInfoSharingWith.IsParticipant ? ConfigHelper.UserRole.Leader :
                           ConfigHelper.UserRole.Unset;

        var sharedWithExternalId = sharedWithRole.ToExternalUserId(userInfoSharingWith.UserGuid);
        if (sharedWithExternalId.HasValue) {
          SendEvent(
            intercom => intercom.SurveyShared()
              .FromSession()
              .WithSurvey(SurveyToShare.SurveyId, SurveyToShare.SurveyName)
              .WithSharedWithUser(sharedWithExternalId.Value, userInfoSharingWith.EmailAddress, userInfoSharingWith.GetFullName())
              .WithSurveyType(SurveyToShare.SurveyType.ToString()),
            operationName: "ShareSurveyModal_SurveyShared",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new Dictionary<string, object> {
              ["SurveyId"] = SurveyToShare.SurveyId,
              ["SharedWithEmail"] = userInfoSharingWith.EmailAddress
            }
          );
        }
      }

      if (emailSent) {

        string redirectPath = "";
        if (SurveyToShare.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.DevelopmentPlan) {
          redirectPath = PathHelper.Pages.DevelopmentPlan();
        } else {
          redirectPath = PathHelper.Pages.ParticipantSurveys();
        }

        ajax.SetRedirectUrl(redirectPath, "Survey successfully shared.", AjaxSubmitHelper.PageMessageType.SuccessToast);
      } else {
        ajax.AddErrorToast("Couldn't share survey, please try again.");
      }
    }
  }
}
