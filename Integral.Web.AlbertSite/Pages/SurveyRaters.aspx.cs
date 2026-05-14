using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class SurveyRaters : AppCode.PageBaseClasses.LoggedInPageBase {

    public string urlSurveyUID = "";
    public string urlPartUID = "";
    public string invitedIconHtml = WebHelper.Icon.Check.AddClass("icon-color-green icon-invited").ToString();
    public DbHelper.AlbertSurveys.SurveyInfo SurveyInfo;
    public DbHelper.Participants.ParticipantInfo SelfParticipantInfo;
    public List<DbHelper.Participants.ParticipantInfo> PartList;
    public List<DbHelper.Participants.PastRaterInfo> PastRatersForParticipant;
    public bool HasPastRaters, CanInviteRaters;
    public bool ErrorVisible = false;
    public bool ContentVisible = false;

    public class FormFields {
      public const string FirstName = "FirstName";
      public const string LastName = "LastName";
      public const string Email = "Email";
      public const string PastRaterId = "PastRaterId";
    }

    public class AjaxAction {
      public const string InviteNewRater = "InviteNewRater";
      public const string InvitePastRaters = "InvitePastRaters";
    }

    public class DataAttr {
      public const string AddingType = "at";
    }

    protected void Page_Load(object sender, EventArgs e) {

      urlSurveyUID = DbHelper.AlbertSurveys.GetValidUniqueId(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SurveyUId));
      urlPartUID = DbHelper.Participants.GetValidUniqueId(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PartUId));

      if (urlSurveyUID == "" || urlPartUID == "") {
        ShowError();
        return;
      }

      // Find survey.
      SurveyInfo = DbHelper.AlbertSurveys.GetSurveyInfo(urlSurveyUID, urlPartUID);
      if (SurveyInfo == null) { // survey UID not correct
        ShowError();
        return;
      }

      // Get coachee this survey is for.
      if (SurveyInfo.FoundParticipantBrief == null || SurveyInfo.FoundParticipantBrief.CoacheeId == null) {
        // No coachee. Shouldn't happen.
        ShowError();
        return;
      }

      var coacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo((int)SurveyInfo.FoundParticipantBrief.CoacheeId);

      // Find self participant.
      SelfParticipantInfo = DbHelper.Participants.GetParticipantInfo(null, SurveyInfo.SurveyId, SurveyInfo.FoundParticipantBrief.PartId);
      if (SelfParticipantInfo == null || SelfParticipantInfo.SurveyId != SurveyInfo.SurveyId) { // Not found or doesn't belong to survey.
        ShowError();
        return;
      }

      // Does the survey participant belong to this user?
      if (SurveyInfo.FoundParticipantBrief.UserId != SessionHelper.GetUserIdOrNull()) {
        ShowError();
        return;
      }

      // If self-only, go back to "thank you" page.
      if (SurveyInfo.FeedbackOption == DbHelper.AlbertSurveys.FeedbackOptionEnum.NoRaters) {
        WebHelper.Redirect(PathHelper.Pages.SurveyCompleted(urlSurveyUID, urlPartUID));
        return;
      }

      var raterList = DbHelper.Participants.GetRaterList(SurveyInfo.SurveyId, SelfParticipantInfo.IntakeCode, SelfParticipantInfo.PartId);
      if (raterList?.Participants == null) {
        PartList = new List<DbHelper.Participants.ParticipantInfo>();
      } else {
        PartList = raterList.Participants;
      }

      // Get Past users
      PastRatersForParticipant = DbHelper.Participants.GetPastRatersForParticipant(coacheeInfo.EmailAddress, coacheeInfo.UserId);

      if (!PastRatersForParticipant.IsNullOrEmpty()) {

        if (!PartList.IsNullOrEmpty()) {
          var ratersAlreadyInvited = PartList.Select(p => p.Email).ToList();
          PastRatersForParticipant.RemoveAll(x => ratersAlreadyInvited.Contains(x.Email));
        }

        // Remove Repeated emails
        var seenEmails = new HashSet<string>();
        PastRatersForParticipant = PastRatersForParticipant.Where(rater => seenEmails.Add(rater.Email)).ToList();

        HasPastRaters = PastRatersForParticipant.Count > 0;
      }

      CanInviteRaters = !(SurveyInfo.IsStrictRaterLimits && SurveyInfo.RatersSuggestedMax != null && PartList.Count >= SurveyInfo.RatersSuggestedMax);

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          if (ajax.Action == AjaxAction.InviteNewRater) {

            InviteNewRater(ajax, coacheeInfo);

          } else if (ajax.Action == AjaxAction.InvitePastRaters) {

            InvitePastRater(ajax, coacheeInfo);
          }
        });
        return;
      }

      ContentVisible = true;

    }

    void ShowError() {
      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {
          ajax.AddDialogMessage("Can't find participant or survey.");
        });
        return;
      } else {
        ErrorVisible = true;
      }
    }

    public void InviteNewRater(AjaxSubmitHelper ajax, DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

      string firstName = ajax.CheckFieldRegex(FormFields.FirstName, "First Name", AppHelper.Regex.GeneralText, true, "Please provide the rater's full name.");
      string lastName = ajax.CheckFieldRegex(FormFields.LastName, "Last Name", AppHelper.Regex.GeneralText, true, "Please provide the rater's full name.");
      string emailAddr = ajax.CheckFieldRegex(FormFields.Email, "Email", AppHelper.Regex.Email, true, "Please provide a valid email address.");

      if (ajax.BadFieldCount != 0) return;

      // Get coach & project info for signoff & sender.
      var coachInfo = DbHelper.AbleUser.GetUserByIdOrNull(coacheeInfo.CoachUserId, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);
      var projectInfo = DbHelper.Projects.GetProjectInfoOrNull(coacheeInfo.ProgramJobNumber);

      // Add rater to survey.
      var raterInfo = new DbHelper.Participants.AddRaterToSurveyInfo(SurveyInfo, SelfParticipantInfo, coacheeInfo, firstName, lastName, emailAddr);

      try {
        DbHelper.Participants.AddRaterToSurvey(raterInfo);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation(nameof(InviteNewRater))
          .FromSession()
          .WithProperty(ApplicationInsightsConstants.SurveyId, SurveyInfo.SurveyId)
          .WithProperty(ApplicationInsightsConstants.SurveyUID, urlSurveyUID)
          .WithProperty(ApplicationInsightsConstants.PartUID, urlPartUID)
          .WithProperty(ApplicationInsightsConstants.RaterEmail, emailAddr)
          .Track();
        ajax.AddDialogMessage("Rater cannot be added at this time, please try again later.");
        return;
      }

      // Send email.
      bool emailSent = SendInviteToRater(raterInfo, projectInfo, coachInfo, coacheeInfo);

      if (emailSent) {
        DbHelper.Participants.UpdateFirstInvitationSent(raterInfo.NewPartId, DateTime.UtcNow);
      }

      ajax.SetReloadPage();
    }

    public void InvitePastRater(AjaxSubmitHelper ajax, DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

      bool isInvitingPastRaters = false;
      var pastRatersSelected = WebHelper.GetFormValue(FormFields.PastRaterId).Split(',').ToList();
      if (pastRatersSelected != null && pastRatersSelected.Count > 0) {
        // If any of the emails don't exist in PastRatersForParticipant
        if (HasPastRaters && pastRatersSelected.All(r => PastRatersForParticipant.Exists(e => e.Email == r))) {
          isInvitingPastRaters = true;
        }
      }

      if (ajax.BadFieldCount != 0) {
        return;
      }

      var ratersToInvite = new List<DbHelper.Participants.PastRaterInfo>();
      if (isInvitingPastRaters) {
        // Create a new list where the email in PastRatersForParticipant matches any email in pastRatersSelected
        ratersToInvite = PastRatersForParticipant.Where(p => pastRatersSelected.Contains(p.Email)).ToList();
      }

      // Get coach & project info for signoff & sender.
      var coachInfo = DbHelper.AbleUser.GetUserByIdOrNull(coacheeInfo.CoachUserId, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);
      var projectInfo = DbHelper.Projects.GetProjectInfoOrNull(coacheeInfo.ProgramJobNumber);

      List<string> ratersSent = new List<string>();
      List<string> ratersError = new List<string>();

      foreach (var rater in ratersToInvite) {
        // Add rater to survey.
        var raterInfo = new DbHelper.Participants.AddRaterToSurveyInfo(SurveyInfo, SelfParticipantInfo, coacheeInfo, rater.FirstName, rater.LastName, rater.Email);

        try {
          DbHelper.Participants.AddRaterToSurvey(raterInfo);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation(nameof(InvitePastRater))
            .FromSession()
            .WithProperty(ApplicationInsightsConstants.SurveyId, SurveyInfo.SurveyId)
            .WithProperty(ApplicationInsightsConstants.SurveyUID, urlSurveyUID)
            .WithProperty(ApplicationInsightsConstants.PartUID, urlPartUID)
            .WithProperty(ApplicationInsightsConstants.RaterEmail, rater.Email)
            .WithProperty(ApplicationInsightsConstants.RaterName, $"{rater.FirstName} {rater.LastName}")
            .Track();
          ratersError.Add($"{rater.FirstName} {rater.LastName}");
          continue;
        }

        // Send email.
        bool emailSent = SendInviteToRater(raterInfo, projectInfo, coachInfo, coacheeInfo);

        if (emailSent) {
          DbHelper.Participants.UpdateFirstInvitationSent(raterInfo.NewPartId, DateTime.UtcNow);
          ratersSent.Add($"{rater.FirstName} {rater.LastName}");
        }
      }

      string msg = "";
      if (!ratersSent.IsNullOrEmpty()) msg += "Invite sent to: " + ratersSent.Join(", ");
      if (!ratersError.IsNullOrEmpty()) msg += "<br/>Couldn't send invite to: " + ratersError.Join(", ");

      ajax.AddSuccessDialog(msg);
      ajax.SetReloadPage();
    }

    private bool SendInviteToRater(
      DbHelper.Participants.AddRaterToSurveyInfo raterInfo,
      DbHelper.Projects.ProjectInfo projectInfo,
      DbHelper.AbleUser.AbleUserInfo coachUserInfo,
      DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

      return AlbertEmails.SendRaterInvitationEmail(
        projectInfo, SurveyInfo,
        raterInfo.NewPartId,
        raterInfo.NewPartUID,
        projectInfo?.ComputedSenderEmailName.ValueIfNullOrEmpty(SurveyInfo.ComputedSenderEmailName),
        projectInfo?.ComputedSenderEmailAddress.ValueIfNullOrEmpty(SurveyInfo.ComputedSenderEmailAddress),
        new AlbertEmails.Addressee(raterInfo),
        new AlbertEmails.Addressee(SelfParticipantInfo),
        new AlbertEmails.Addressee(coachUserInfo),
        coacheeInfo.CompanyName,
      false, false);
    }

    public string GetUserActionsHtml() {

      var userActions = new StringBuilder();

      if (SessionHelper.AppAccess.Surveys.CanViewReports(SurveyInfo)) {

        userActions.Append(WebHelper.GetParticipantActionCard(new WebHelper.ParticipantActionCard(
        headerText: "View Your Report",
        descriptionText: "Explore your survey results and gain valuable insights to shape your leadership development.",
        actionText: "View Report",
        iconPath: PathHelper.Images.SurveyIcon(),
        iconClass: WebHelper.Icon.ActionCardIconClass.Intake,
        linkUrl: PathHelper.Reports.CoacheeSurvey(null, SurveyInfo),
        targetNewTab: WebHelper.TargetNewTab.No)));
      }

      if (SessionHelper.AppAccess.PageAccess.CanAccessDevelopmentPlan()) {

        userActions.Append(WebHelper.GetParticipantActionCard(new WebHelper.ParticipantActionCard(
        headerText: "Set Your Leadership Goals",
        descriptionText: "Create a personalised development plan with clear, actionable steps to enhance your growth and effectiveness.",
        actionText: "Create Plan",
        iconPath: PathHelper.Images.GoalIcon(),
        iconClass: WebHelper.Icon.ActionCardIconClass.DevPlan,
        linkUrl: PathHelper.Pages.DevelopmentPlan(),
        targetNewTab: WebHelper.TargetNewTab.No)));
      }

      userActions.Append(WebHelper.GetShareSurveyButtonHtml(SurveyInfo, WebHelper.ShareSurveyButtonTypeEnum.ActionCard));

      if (userActions.ToString().IsNullOrEmpty()) {
        return string.Empty;
      }

      return $@"
        <div class=""actions-container w100p"">
          <button class=""btn btn-primary btn-next float-right"">Next</button>
          <div class=""mb10 mt10 action-cards-container action-cards-container-row"">
            {userActions}
          </div>
          <button class=""btn btn-secondary display-none btn-prev"">Back</button>
        </div>";
    }

  }
}
