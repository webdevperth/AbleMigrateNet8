using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class ProgramOverview : AppCode.PageBaseClasses.ProgramPageBase {

    public List<DbHelper.AlbertCoachees.AlbertCoacheeInfo> CoacheeList;
    public List<DbHelper.WorkshopEvents.WorkshopEventInfo> WorkshopList;
    public List<DbHelper.ConsultingItems.ConsultingItemInfo> ConsultingItemList;
    public List<DbHelper.ProgramCostItems.ProgramCostItemInfo> CostItemList;

    public decimal ProgramExpectedRevenue = 0;
    public decimal CoacheeTotalRevenue = 0;
    public decimal WorkshopTotalRevenue = 0;
    public decimal ConsultingTotalRevenue = 0;
    public decimal CostItemTotalRevenue = 0;
    public decimal AllTotalRevenue = 0;
    public decimal OutstandingAmount = 0;
    public bool CanViewTotalRevenue, CanViewAllDeliveryTeamRevenue, CanViewPartnerRevenue, CanNavigateFromTables, CanViewParticipants;

    public DbHelper.AblePrograms.ProgramOverviewEvalScores EvalScores;
    public DbHelper.AblePrograms.PrePostSurveyState PrePostSurveyState;
    public DbHelper.AblePrograms.ProgramOverviewPrePostScores PrePostScores;

    public const string PrePostLinkButtonID = "btnPrePostLink";

    public string colHidePartner = "", colHideSales = "", colHidePLC = "";

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Program Overview";

      if (ProgramInfo.Partner_SalesDeliveryPercentage.GetValueOrDefault(0) == 0) colHideSales = "displaynone";
      if (ProgramInfo.Partner_PLCPercentage.GetValueOrDefault(0) == 0) colHidePLC = "displaynone";

      ProgramExpectedRevenue = ProgramInfo.ProgramExpectedRevenue.GetValueOrDefault(0);
      CanViewParticipants = SessionHelper.AppAccess.Programs.CanViewProgramParticipants(ProgramInfo) && !SessionHelper.IsUserRoleClient;
      EvalScores = DbHelper.AblePrograms.GetProgramOverviewEvalScores(ProgramInfo.ProgramJobId);
      PrePostScores = DbHelper.AblePrograms.GetProgramOverviewPrePostScores(ProgramInfo.ProgramJobId);

      CoacheeList = DbHelper.AlbertCoachees.GetCoacheesForProgramOverview(ProgramInfo.ProgramJobId);
      CoacheeTotalRevenue = 0;
      if (CoacheeList != null) foreach (var item in CoacheeList) CoacheeTotalRevenue += item.CoachingRevenue.GetValueOrDefault(0);

      WorkshopList = DbHelper.WorkshopEvents.GetWorkshopsInProgram(ProgramInfo.ProgramJobId);
      WorkshopTotalRevenue = 0;
      if (WorkshopList != null) foreach (var item in WorkshopList) WorkshopTotalRevenue += item.WorkshopRevenue.GetValueOrDefault(0);

      ConsultingItemList = DbHelper.ConsultingItems.GetItemsInProgram(ProgramInfo.ProgramJobId);
      ConsultingTotalRevenue = 0;
      if (ConsultingItemList != null) foreach (var item in ConsultingItemList) ConsultingTotalRevenue += item.ItemAmount;

      CostItemList = DbHelper.ProgramCostItems.GetItemsInProgram(ProgramInfo.ProgramJobId);
      CostItemTotalRevenue = 0;
      if (CostItemList != null) foreach (var item in CostItemList) CostItemTotalRevenue += (item.UnitPrice * item.Quantity);

      AllTotalRevenue = CoacheeTotalRevenue + WorkshopTotalRevenue + ConsultingTotalRevenue;
      OutstandingAmount = ProgramExpectedRevenue - AllTotalRevenue;

      CanViewTotalRevenue = SessionHelper.AppAccess.Programs.Revenue.CanViewTotalRevenue(ProgramInfo);
      CanViewAllDeliveryTeamRevenue = SessionHelper.AppAccess.Programs.Revenue.CanViewAllDeliveryTeamRevenue(ProgramInfo);
      CanViewPartnerRevenue = SessionHelper.AppAccess.Programs.Revenue.CanViewPartnerRevenue(ProgramInfo);

      CanNavigateFromTables = SessionHelper.AppAccess.Programs.CanNavigateFromOverviewTables();

      PrePostSurveyState = DbHelper.AblePrograms.GetPrePostSurveyState(ProgramInfo.ProgramJobId);
    }

    public string GetRowAttribute(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {
      // TODO: AppAccess.Participants.CanViewParticipantProfile needs to take CoacheeListItem, which needs to have relevant tenant info.
      if (SessionHelper.AppAccess.Participants.CanViewParticipantProfile(coacheeInfo)) {
        return $"data-rowlink-id=\"{coacheeInfo.CoacheeId}\"";
      } else {
        return WebHelper.GetSlideoutTriggerDataAttributes("Participant Details", PathHelper.Partials.ParticipantSlideoutPanel(coacheeInfo.CoacheeId));
      }
    }

    public string GetPrePostSurveyBox() {

      string titleText = "Pre-Post Impact";
      string titleTooltipText = "The score displayed is the average of all surveys (Self and Raters).";
      string linkUrl = null;
      string itemLinkClass = "btn btn-sm btn-primary";
      var scores = new List<WebHelper.OverviewBoxScore>();

      // Note the order of the below conditions matters. Checks are in order of:
      // Pre-Survey | Post-Survey
      // ------------------------
      // Complete   | Complete
      // Complete   | Incomplete
      // Complete   | Not Created
      // Incomplete | Created (will be scheduled)
      // Incomplete | Not Created
      // Not Created

      if (PrePostScores.SurveyScorePre != null && PrePostScores.SurveyScorePost != null) {
        // Both pre and post completed, show scores.

        linkUrl = PathHelper.Pages.ProgramSkillsViewer(ProgramInfo);
        scores.Add(new WebHelper.OverviewBoxScore(PrePostScores.SurveyScorePre.Value.ToString("0.0"), "Pre"));
        scores.Add(new WebHelper.OverviewBoxScore(PrePostScores.SurveyScorePost.Value.ToString("0.0"), "Post"));
        scores.Add(new WebHelper.OverviewBoxScore((PrePostScores.SurveyScorePost.Value - PrePostScores.SurveyScorePre.Value).ToString("0.0"), "Impact"));

      } else if (PrePostScores.SurveyScorePre != null && PrePostSurveyState.PostProgramSurveyExists) {
        // Pre completed, post not yet completed.

        linkUrl = PathHelper.Pages.ProgramSkillsViewer(ProgramInfo);
        scores.Add(new WebHelper.OverviewBoxScore(PrePostScores.SurveyScorePre.Value.ToString("0.0"), "Pre"));
        scores.Add(new WebHelper.OverviewBoxScore(null, "Awaiting Post-Program Completion"));

      } else if (PrePostScores.SurveyScorePre != null && !PrePostSurveyState.PostProgramSurveyExists) {
        // Pre completed, post does not exist.

        linkUrl = PathHelper.Pages.ProgramSkillsViewer(ProgramInfo);
        scores.Add(new WebHelper.OverviewBoxScore(PrePostScores.SurveyScorePre.Value.ToString("0.0"), "Pre"));
        scores.Add(new WebHelper.OverviewBoxScore(null, "Schedule Post-Program Survey", itemLinkClass, PathHelper.Pages.ProgramSendSurvey(ProgramInfo.ProgramJobId, ConfigHelper.TemplateSurveyIds.NewPostProgramSurvey)));

      } else if (PrePostSurveyState.PreProgramSurveyExists && PrePostSurveyState.PostProgramSurveyExists) {
        // Pre exists but not completed, post exists (completed or not).

        scores.Add(new WebHelper.OverviewBoxScore(null, "Awaiting Pre-Program Completion"));

      } else if (PrePostSurveyState.PreProgramSurveyExists && !PrePostSurveyState.PostProgramSurveyExists) {
        // Pre exists but not completed, post does not exist.

        scores.Add(new WebHelper.OverviewBoxScore(null, "Schedule Post-Program Survey", itemLinkClass, PathHelper.Pages.ProgramSendSurvey(ProgramInfo.ProgramJobId, ConfigHelper.TemplateSurveyIds.NewPostProgramSurvey)));

      } else if (scores.Count == 0) {
        // No surveys exist, add initial message.

        scores.Add(new WebHelper.OverviewBoxScore(null, "Schedule Pre-Program Survey", itemLinkClass, PathHelper.Pages.ProgramSendSurvey(ProgramInfo.ProgramJobId, ConfigHelper.TemplateSurveyIds.NewPreProgramSurvey)));
      }

      return WebHelper.GetOverviewScoreBox(titleText, titleTooltipText, null, linkUrl, scores.ToArray());
    }

    public string GetParticipantRowLinkUrl() {
      return PathHelper.Pages.CoacheeEdit(PathHelper.CoacheeTabEnum.summary, null);
    }

    public bool GetDeliveryType(DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {
      if (coacheeInfo.CoachingTypeId == null) return false;
      return coacheeInfo.GetCoachingType().SessionTypeInPerson;
    }

    public string GetProjectLeadDisplay() {

      if (ProgramInfo?.LeadConsultantUserId == null) return string.Empty;
      var leadInfo = DbHelper.AbleUser.GetUserByIdOrNull(ProgramInfo.LeadConsultantUserId.Value, DbHelper.AbleUser.RegisteredFilter.Any);

      return WebHelper.GetOverviewUserBox("Project Lead", leadInfo);
    }

    public string GetWorkshopEvalScore(DbHelper.WorkshopEvents.WorkshopEventInfo workshop) {
      if (workshop.DisableEvals || ProjectInfo.WorkshopSessionEvalSurveyDisabled || ProjectInfo.WorkshopAndProgramEvalSurveyDisabled) {
        return "<small>Disabled</small>";
      }
      if (workshop.LastEvalSentUtc == null) { // Not yet sent.
        return "";
      }
      if (workshop.HasEvalScore) { // Score exists.
        return $@"<a href=""{PathHelper.Reports.EvalViewer(workshop)}"" class=""action-button align-center"">"
          + (workshop.EvalScoreSum.GetValueOrDefault(0) / (decimal)workshop.EvalScoreCount).ToString("0.0")
          + "</a>";
      } else {
        return $"<small>Sent<br/>{WebHelper.DisplayDate(workshop.GetLastEvalSentLocal())}</small>";
      }
    }

  }
}
