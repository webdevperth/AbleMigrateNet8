using System;
using System.Collections.Generic;
using System.Linq;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class CoacheeSurveySummaryReport : AppCode.PageBaseClasses.CoacheeInfoBase {

    public class PartialIDs {
      public const string SummaryScore = "partial_Overview";
      public const string Categories = "partial_Categories";
      public const string QuestionDetail = "partial_QuestionDetail";
      public const string QuestionFocus = "partial_QuestionFocus";
      public const string QuestionPrePost = "partial_QuestionPrePost";
      public const string Comments = "partial_Comments";
    }

    public class FormFields {
      public const string SurveyToView = "SurveyToView";
      public const string SendWebReport = "SendWebReport";
      public const string SendPDFReport = "SendPDFReport";
    }

    public class PageFlagsC {
      public bool IsNoSurveys { get; set; }
      public bool IsNoSelfResponse { get; set; }
      public bool ShowReport { get; set; }
    }

    public PageFlagsC PageFlags = new PageFlagsC();

    public string UrlSelectedSurveyUId, UrlSelectedPartUId, AICoachLongFormText;
    public List<DbHelper.AlbertSurveys.SurveyInfo> SurveyList;
    public bool IsRatersOnly;
    public int FoundIntakeId;
    public DateTime SurveyCompletedUtc;

    public bool HasPreSurvey, HasOpenText, Hide360ReportNorms;
    public string ReportInformationHtml;
    public int PreSurveyPartId, PreSurveyIntakeId;
    public string PreSurveyUId, PreSurveyPartUId;
    public DateTime PreSurveyCompletedUtc;
    public bool CanViewSurveySelector;
    public bool CanShowDevPlanSlideout;
    public DbHelper.DevelopmentPlans.PlanInfo LatestDevPlan;

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Analytics: " + CoacheeInfo.GetFullName();
      PageTitle_Mobile = CoacheeInfo.GetFullName();

      // Get survey & participant ID which is a string of 2 UIDs separated by "-".
      PathHelper.Pages.GetCoacheeSurveyUIDs(out UrlSelectedSurveyUId, out UrlSelectedPartUId);

      CanViewSurveySelector = IsViewingSharedSurvey ? IsViewingSharedSurvey : SessionHelper.AppAccess.Reports.CoacheeSurveySummaryReport.CanViewSurveySelector();

      CanShowDevPlanSlideout = CoacheeInfo.UserId != null && IsViewingSharedSurvey ? false : SessionHelper.AppAccess.Reports.CoacheeSurveySummaryReport.CanShowDevPlanSlideout(CoacheeInfo);

      SurveyList = DbHelper.AlbertSurveys.GetSurveyInfoListForUser(
        CoacheeInfo.UserId ?? 0, UrlSelectedSurveyUId, UrlSelectedPartUId,
        DbHelper.AlbertSurveys.OnlyViewerCompatible.Yes,
        SessionHelper.IsUserRoleLeader ? DbHelper.AlbertSurveys.OnlyWhereLeaderCanView360Report.Yes : DbHelper.AlbertSurveys.OnlyWhereLeaderCanView360Report.No,
        out var surveyInfoFound);

      // Remove incomplete surveys.
      SurveyList.RemoveAll(s => !s.IsRatersOnly && (s.SelfsCompleted ?? 0) == 0 || s.IsRatersOnly && (s.RatersCompleted ?? 0) == 0);

      if (SurveyList.IsNullOrEmpty()) {
        PageFlags.IsNoSurveys = true;  // Show "no surveys".
        return;
      }

      if (UrlSelectedSurveyUId.IsNullOrEmpty()) {
        surveyInfoFound = SurveyList[0]; // No survey uid given, default selected to first in list.
      } else if (surveyInfoFound == null) {
        return; // Survey ID not found or survey is not compatible.
      }

      if (!SessionHelper.AppAccess.Surveys.CanViewReports(ProgramInfo, CoacheeInfo, surveyInfoFound) && !IsViewingSharedSurvey) {
        return;
      }

      FoundIntakeId = surveyInfoFound.IntakeCodeId;
      IsRatersOnly = surveyInfoFound.IsRatersOnly;
      HasOpenText = surveyInfoFound.HasOpenText;
      Hide360ReportNorms = surveyInfoFound.Hide360ReportNorms;
      ReportInformationHtml = surveyInfoFound.ReportInformationHtml ?? surveyInfoFound.ReportType.ReportInformationHtml;

      if (IsRatersOnly == false && surveyInfoFound.FoundParticipantBrief?.CompletedUtc == null) {
        PageFlags.IsNoSelfResponse = true;  // Show "no response".
        return;
      }

      // All ok to show report components.
      PageFlags.ShowReport = true;
      AICoachLongFormText = GetAICoachLongFormText(surveyInfoFound.FoundParticipantBrief?.PartId ?? 0);

      if (surveyInfoFound?.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Standard360) {
        DbHelper.AlbertSurveys.GetFirstCompletedPreSurvey(
          DbHelper.AlbertSurveys.GetPreSurveyBy.UserId,
          surveyInfoFound, CoacheeInfo,
          out HasPreSurvey, out PreSurveyUId, out PreSurveyIntakeId,
          out PreSurveyPartUId, out PreSurveyPartId, out PreSurveyCompletedUtc);
      }

      // Get latest dev plan survey or create one.
      if (CanShowDevPlanSlideout) {
        List<DbHelper.DevelopmentPlans.PlanInfo> devPlans = null;
        try {
          devPlans = DbHelper.DevelopmentPlans.GetPlansForUser(CoacheeInfo.UserId.Value, DbHelper.DevelopmentPlans.GetPlansStatus.Any);
        } catch (Exception) {
          CanShowDevPlanSlideout = false;
        }
        if (CanShowDevPlanSlideout) {
          if (!devPlans.IsNullOrEmpty()) {
            LatestDevPlan = devPlans.OrderBy(d => d.CreatedUtc).Last(); // Use the latest one.
          } else {
            // Create new dev plan for user.
            int newSurveyId = 0;
            try {
              newSurveyId = DbHelper.DevelopmentPlans.CreateDevPlanSurvey(SessionHelper.GetUserInfoOrNull());
            } catch (Exception) { }
            if (newSurveyId == 0) {
              CanShowDevPlanSlideout = false;
            } else {
              LatestDevPlan = null;
              try {
                LatestDevPlan = DbHelper.DevelopmentPlans.GetPlanSurvey(newSurveyId);
              } catch (Exception) { }
              if (LatestDevPlan == null) {
                CanShowDevPlanSlideout = false;
              }
            }
          }
        }
      }
    }

    public string GetSurveyListOptions() {
      string html = "";
      foreach (var surveyListItem in SurveyList) {
        html += "<option" +
          $" data-svuid=\"{surveyListItem.SurveyUID}\"" +
          $" data-partuid=\"{surveyListItem.FoundParticipantBrief.PartUniqueId}\"" +
          $" {(UrlSelectedSurveyUId == surveyListItem.SurveyUID && UrlSelectedPartUId == surveyListItem.FoundParticipantBrief.PartUniqueId ? "selected" : "")}" +
          $" value=\"{surveyListItem.SurveyUID}-{surveyListItem.FoundParticipantBrief.PartUniqueId}\">" +
          $"{GetSurveyListOptionHtml(surveyListItem)}</option>";
      }
      return html;
    }

    public string GetSurveySelectorInfoHtml() {

      if (IsViewingSharedSurvey) {

        var currentSurveyInfo = SurveyList.Find(x => x.SurveyUID == UrlSelectedSurveyUId && x.FoundParticipantBrief.PartUniqueId == UrlSelectedPartUId);
        return $"<p>{GetSurveyListOptionHtml(currentSurveyInfo)}</p>";

      } else {

        return WebHelper.GetSelect(FormFields.SurveyToView, GetSurveyListOptions());
      }
    }

    string GetSurveyListOptionHtml(DbHelper.AlbertSurveys.SurveyInfo surveyListItem) {
      DateTime? dt = surveyListItem.SentDateUtc ?? surveyListItem.ScheduledStartDateUtc ?? surveyListItem.CreatedUtc;
      return
        WebHelper.DisplayDate(SessionHelper.UtcToUserTime(dt)).EnsureEndsWith(": ", StringExt.Ensure.IfNotBlank)
        + surveyListItem.SurveyName.HTMLEncode()
        //+ (surveyListItem.SelfsCompleted > 0 ? " &check; " : "")
        + (surveyListItem.FeedbackOption == DbHelper.AlbertSurveys.FeedbackOptionEnum.NoRaters ? "" : " (" + surveyListItem.RatersCompleted + " / " + surveyListItem.RatersInvited + ")")
        + (surveyListItem.SurveyId == SurveyList[0].SurveyId ? " (latest)" : "");
    }

    private string GetAICoachLongFormText(int surveyParticipantId) {
      return DbHelper.Common.GetScalarQuery(@"
        SELECT AICoachLongFormText
        FROM sv_ParticipantAICoachSummary
        WHERE ParticipantId = @ParticipantId",
        DbHelper.Common.NewSqlParameter("ParticipantId", surveyParticipantId)
      ).ToStringOrNull();
    }

  }
}
