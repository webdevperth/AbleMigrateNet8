using Microsoft.Data.SqlClient;
using System;

namespace Integral.Web.PortalSite.AppCode.PageBaseClasses {

  public class EvalViewerPartialBase : LoggedInPageBase {

    protected PathHelper.SurveyViewerBenchmarkEnum BenchmarkType = PathHelper.SurveyViewerBenchmarkEnum.Global;
    protected DbHelper.Reports.EvalViewer.SurveyStats SurveyStats;
    protected string BenchmarkDisplayName;

    protected override void Page_Init(object sender, EventArgs e) {

      if (WebHelper.IsRequestExiting()) return;

      base.Page_Init(sender, e);

      var projectJobNumber = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ProjectJobNumber);
      var programJobIdList = WebHelper.GetQueryStringIntList(PathHelper.AbleUrlKeys.ProgramJobId);
      var intakeCodeIdList = WebHelper.GetQueryStringIntList(PathHelper.AbleUrlKeys.SurveyIntakeCodeId);

      if (projectJobNumber.IsNullOrEmpty()) {
        RespondMessageAndEnd("Project Number not provided.");
        return;
      }
      if (programJobIdList.IsNullOrEmpty()) {
        RespondMessageAndEnd("Program(s) not provided.");
        return;
      }
      if (intakeCodeIdList.IsNullOrEmpty()) {
        RespondMessageAndEnd("Survey(s) not provided.");
        return;
      }

      ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(projectJobNumber, userInfo);

      if (ProjectInfo == null) {
        RespondMessageAndEnd($"Project {projectJobNumber.HTMLEncode()} Not Found.");
        return;
      }

      if (!SessionHelper.IsUserRoleAdmin
        && !ProjectInfo.ForUser_IsInProjectAccess
        && !ProjectInfo.ForUser_IsDeliveryInProject
        && !ProjectInfo.ForUser_IsPCOrPLC) {
        RespondMessageAndEnd("No access to Project or Program.");
        return;
      }

      SurveyStats = DbHelper.Reports.EvalViewer.GetSurveyStats(projectJobNumber, programJobIdList, intakeCodeIdList, ConfigHelper.EvalType.All);

      if (SurveyStats == null || SurveyStats.IntakeCount == 0) {
        RespondMessageAndEnd("Could not find any results, try expanding the selection.");
        return;
      }

      if (!Enum.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SurveyViewerBenchmark), true, out BenchmarkType)) {
        BenchmarkType = PathHelper.SurveyViewerBenchmarkEnum.Global;
      }
      if (BenchmarkType == PathHelper.SurveyViewerBenchmarkEnum.Org) {
        BenchmarkDisplayName = "Organisation";
      } else {
        BenchmarkDisplayName = "Global";
      }

      if (SurveyStats.ScaleMinScore == 0 || SurveyStats.ScaleMaxScore == 0) {
        RespondMessageAndEnd("Can't determine scale in selected surveys.");
        return;
      }
    }

    public string GetScoreFormatted(double? scoreValue, string textIfNull = "NA") {
      return GetScoreFormatted((decimal?)scoreValue, textIfNull);
    }
    public string GetScoreFormatted(decimal? scoreValue, string textIfNull = "NA") {
      if (!scoreValue.HasValue) return textIfNull;
      return scoreValue.Value.ToString("0.0");
    }

    public SqlParameter[] GetStandardSqlParams() {
      return new SqlParameter[] {
        DbHelper.Common.NewSqlParameter("SvCompanyId", SurveyStats.SvCompanyId),
        DbHelper.Common.NewSqlParameter("ProjectId", SurveyStats.ProjectId),
        DbHelper.Common.NewSqlParameter("ProgramJobIds", SurveyStats.ProgramJobIds.ToStringList()),
        DbHelper.Common.NewSqlParameter("SampleSurveyId", SurveyStats.SampleSurveyId),
        DbHelper.Common.NewSqlParameter("RptQnGroupId", SurveyStats.RptQnGroupId),
        DbHelper.Common.NewSqlParameter("PrimaryGblAnswerTypeId", SurveyStats.PrimaryGblAnswerTypeId)
      };
    }

    public string BarWidthStyle(decimal? score) {
      return "style=\"width:" + WebHelper.GetCSSPercentFromRatio(score, SurveyStats.ScaleMaxScore) + "\"";
    }

    public string DotPosStyle(decimal? score) {
      return "style=\"left:" + WebHelper.GetCSSPercentFromRatio(score, SurveyStats.ScaleMaxScore) + "\"";
    }
  }
}
