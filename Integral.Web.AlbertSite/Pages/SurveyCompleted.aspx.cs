using System;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class SurveyCompleted : AppCode.PageBaseClasses.LoggedInPageBase {

    public string urlSurveyUID = "";
    public string urlPartUID = "";
    public bool CanInviteRaters = false, ShowError = false;
    public DbHelper.AlbertSurveys.SurveyInfo surveyInfo;
    public DbHelper.Participants.ParticipantInfo partInfo;

    protected void Page_Load(object sender, EventArgs e) {

      SessionHelper.SetUserRole(ConfigHelper.UserRole.Leader); // Required for survey pages.

      urlSurveyUID = DbHelper.AlbertSurveys.GetValidUniqueId(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SurveyUId));
      urlPartUID = DbHelper.Participants.GetValidUniqueId(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PartUId));

      FallbackUrl = PathHelper.Pages.ParticipantSurveys();

      if (urlSurveyUID == "" || urlPartUID == "") {
        if (SessionHelper.IsUserRoleLeader) {
          WebHelper.Redirect(FallbackUrl);
          return;
        } else {
          ShowError = true;
        }
        return;
      }

      // Find survey.
      surveyInfo = DbHelper.AlbertSurveys.GetSurveyInfo(urlSurveyUID, urlPartUID);
      if (surveyInfo == null) { // survey UID not correct
        if (SessionHelper.IsUserRoleLeader) {
          WebHelper.Redirect(FallbackUrl);
          return;
        } else {
          ShowError = true;
        }
        return;
      }

      // Find participant.
      partInfo = DbHelper.Participants.GetParticipantInfo(null, surveyInfo.SurveyId, urlPartUID);
      if (partInfo == null || partInfo.SurveyId != surveyInfo.SurveyId) { // Not found or doesn't belong to survey.
        if (SessionHelper.IsUserRoleLeader) {
          WebHelper.Redirect(FallbackUrl);
          return;
        } else {
          ShowError = true;
        }
        return;
      }

      // Does the survey participant belong to this user?
      if (surveyInfo.FoundParticipantBrief.UserId != SessionHelper.GetUserIdOrNull()) {
        if (SessionHelper.IsUserRoleLeader) {
          WebHelper.Redirect(FallbackUrl);
          return;
        } else {
          ShowError = true;
        }
        return;
      }

      SessionHelper.SetNextPageMessageType(AjaxSubmitHelper.PageMessageType.SuccessDialog);
      SessionHelper.SetNextPageMessageText("Thank you for your participation!");
      WebHelper.Redirect(PathHelper.Pages.GetSurveyCompletedURL(partInfo, surveyInfo));
      return;
    }

  } // class
}
