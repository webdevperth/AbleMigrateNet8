using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class SurveyCompleted : AppCode.PageBaseClasses.LoggedInPageModel {

    public string urlSurveyUID = "";
    public string urlPartUID = "";
    public bool CanInviteRaters = false, ShowError = false;
    public DbHelper.AlbertSurveys.SurveyInfo surveyInfo;
    public DbHelper.Participants.ParticipantInfo partInfo;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      SessionHelper.SetUserRole(ConfigHelper.UserRole.Leader); // Required for survey pages.

      urlSurveyUID = DbHelper.AlbertSurveys.GetValidUniqueId(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SurveyUId));
      urlPartUID = DbHelper.Participants.GetValidUniqueId(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PartUId));

      FallbackUrl = PathHelper.Pages.ParticipantSurveys();

      if (urlSurveyUID == "" || urlPartUID == "") {
        if (SessionHelper.IsUserRoleLeader) {
          WebHelper.Redirect(FallbackUrl);
          return new EmptyResult();
        } else {
          ShowError = true;
        }
        return Page();
      }

      // Find survey.
      surveyInfo = DbHelper.AlbertSurveys.GetSurveyInfo(urlSurveyUID, urlPartUID);
      if (surveyInfo == null) { // survey UID not correct
        if (SessionHelper.IsUserRoleLeader) {
          WebHelper.Redirect(FallbackUrl);
          return new EmptyResult();
        } else {
          ShowError = true;
        }
        return Page();
      }

      // Find participant.
      partInfo = DbHelper.Participants.GetParticipantInfo(null, surveyInfo.SurveyId, urlPartUID);
      if (partInfo == null || partInfo.SurveyId != surveyInfo.SurveyId) { // Not found or doesn't belong to survey.
        if (SessionHelper.IsUserRoleLeader) {
          WebHelper.Redirect(FallbackUrl);
          return new EmptyResult();
        } else {
          ShowError = true;
        }
        return Page();
      }

      // Does the survey participant belong to this user?
      if (surveyInfo.FoundParticipantBrief.UserId != SessionHelper.GetUserIdOrNull()) {
        if (SessionHelper.IsUserRoleLeader) {
          WebHelper.Redirect(FallbackUrl);
          return new EmptyResult();
        } else {
          ShowError = true;
        }
        return Page();
      }

      SessionHelper.SetNextPageMessageType(AjaxSubmitHelper.PageMessageType.SuccessDialog);
      SessionHelper.SetNextPageMessageText("Thank you for your participation!");
      WebHelper.Redirect(PathHelper.Pages.GetSurveyCompletedURL(partInfo, surveyInfo));
      return new EmptyResult();
    }

  } // class
}
