using System;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class ParticipantCompleted : System.Web.UI.Page {

    public string urlSurveyUID = "";
    public string urlPartUID = "";
    public bool CanInviteRaters = false;
    public bool ErrorVisible = false;
    public bool ContentVisible = false;
    public DbHelper.AlbertSurveys.SurveyInfo surveyInfo;
    public DbHelper.Participants.ParticipantInfo partInfo;

    protected void Page_Load(object sender, EventArgs e) {

      urlSurveyUID = DbHelper.AlbertSurveys.GetValidUniqueId(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SurveyUId));
      urlPartUID = DbHelper.Participants.GetValidUniqueId(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PartUId));

      if (urlSurveyUID == "" || urlPartUID == "") {
        ShowError();
        return;
      }

      // Find survey.
      surveyInfo = DbHelper.AlbertSurveys.GetSurveyInfo(urlSurveyUID, urlPartUID);
      if (surveyInfo == null) { // survey UID not correct
        ShowError();
        return;
      }

      // Find participant.
      partInfo = DbHelper.Participants.GetParticipantInfo(null, surveyInfo.SurveyId, urlPartUID);
      if (partInfo == null || partInfo.SurveyId != surveyInfo.SurveyId) { // Not found or doesn't belong to survey.
        ShowError();
        return;
      }

      ContentVisible = true;
      CanInviteRaters = partInfo.IsSelf && surveyInfo.FeedbackOption != DbHelper.AlbertSurveys.FeedbackOptionEnum.NoRaters;

      if (CanInviteRaters) {
        SessionHelper.SetNextPageMessageType(AjaxSubmitHelper.PageMessageType.SuccessDialog);
        SessionHelper.SetNextPageMessageText("Thank you for your participation!");
        WebHelper.Redirect(PathHelper.Pages.GetSurveyCompletedURL(partInfo, surveyInfo));
        return;
      }

      // TODO: If participant isn't completed, redirect to last submitted page.

    } // Page_Load

    void ShowError() {
      ErrorVisible = true;
    }


  } // class
}
