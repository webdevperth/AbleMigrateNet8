using Microsoft.Data.SqlClient;
using System;

namespace Integral.Web.PortalSite.AppCode.PageBaseClasses {

  public class SkillsViewerPartialBase : LoggedInPageModel {

    protected DbHelper.Reports.NormEnum BenchmarkType = DbHelper.Reports.NormEnum.Global;
    protected DbHelper.Reports.SkillsViewer.SurveyStats SurveyStats;
    protected string BenchmarkDisplayName;

    protected override void InitializePage() {

      base.InitializePage();

      if (WebHelper.IsRequestExiting()) return;

      var projectJobNumber = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ProjectJobNumber);
      var programJobIds = WebHelper.GetQueryStringIntList(PathHelper.AbleUrlKeys.ProgramJobId, null);

      if (projectJobNumber.IsNullOrEmpty()) {
        RespondMessageAndEnd("Project Number not provided.");
        return;
      }

      ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(projectJobNumber);

      if (!SessionHelper.AppAccess.Insights.CanViewSkillsViewer(ProjectInfo)) {
        RespondMessageAndEnd("No access to report.");
        return;
      }

      if (ProjectInfo == null) {
        RespondMessageAndEnd($"Project {projectJobNumber.HTMLEncode()} Not Found.");
        return;
      }

      var surveyStatsList = DbHelper.Reports.SkillsViewer.GetSurveyStatsByType(projectJobNumber, programJobIds);

      if (surveyStatsList.IsNullOrEmpty()) {
        RespondMessageAndEnd("Could not find any results, try expanding the selection.");
        return;
      }

      if (surveyStatsList.Count > 1) {
        RespondMessageAndEnd("Results contain incompatible surveys, try narrowing the selection...");
        return;
      }

      SurveyStats = surveyStatsList[0];

      if (!Enum.TryParse(WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.SurveyViewerBenchmark), true, out BenchmarkType)) {
        BenchmarkType = DbHelper.Reports.NormEnum.Global;
      }
      if (BenchmarkType == DbHelper.Reports.NormEnum.Org) {
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
        DbHelper.Common.NewSqlParameter("SurveyTypeCode", SurveyStats.SurveyTypeCode),
        DbHelper.Common.NewSqlParameter("RptQnGroupId", SurveyStats.RptQnGroupId),
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
