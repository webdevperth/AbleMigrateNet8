using Microsoft.Data.SqlClient;
using System;

namespace Integral.Web.PortalSite.AppCode.PageBaseClasses {

  public class CompanyReportPartialBase : CompanyInfoBase {

    protected PathHelper.SurveyViewerBenchmarkEnum BenchmarkType = PathHelper.SurveyViewerBenchmarkEnum.Global;
    protected DbHelper.Reports.Company.SurveyStats SurveyStats;
    protected string BenchmarkDisplayName;

    protected override void Page_Init(object sender, EventArgs e) {

      if (WebHelper.IsRequestExiting()) return;

      base.Page_Init(sender, e);

      if (!SessionHelper.AppAccess.Companies.CanViewOrganisationCapabilities(CompanyInfo)) {
        RespondMessageAndEnd("No access to report.");
        return;
      }

      // The GblAnswerTypeId of the surveys to retrieve.
      // May be null, which implies there is only 1 type so the specific ID does not matter.
      // Null is also allowed for backward compatibility, for any legacy usage which doesn't provide the GblAnswerTypeId.
      int? urlGblAnswerTypeId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.GblAnswerTypeId);

      var surveyStatsList = DbHelper.Reports.Company.GetSurveyStatsByType(
        companyIdOrNullForAll: CompanyInfo.CompanyId,
        projectJobNumbersOrNullForAll: SessionHelper.IsUserRoleAdmin ? null : SessionHelper.GetUserInfoOrNull().ProjectAccessForJobNumbers.Join(","),
        programJobIdsOrNullForAll: null,
        gblAnswerTypeIdOrNullForAll: urlGblAnswerTypeId,
        surveyTypeCode: ConfigHelper.SurveyTypeCodes.Able360,
        onlySurveysWithRptQnGroupId: ConfigHelper.RptQnGroupId_SkillsViewer);

      if (surveyStatsList.IsNullOrEmpty()) {
        RespondMessageAndEnd("Could not find any results, try expanding the selection.");
        return;
      }

      SurveyStats = surveyStatsList[0];

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
      return new Microsoft.Data.SqlClient.SqlParameter[] {
        DbHelper.Common.NewSqlParameter("SvCompanyId", SurveyStats.SvCompanyId),
        DbHelper.Common.NewSqlParameter("SampleSurveyId", SurveyStats.SampleSurveyId),
        DbHelper.Common.NewSqlParameter("RptQnGroupId", SurveyStats.RptQnGroupId),
        DbHelper.Common.NewSqlParameter("SurveyTypeCode", SurveyStats.SurveyTypeCode),
        DbHelper.Common.NewSqlParameter("GblAnswerTypeId", SurveyStats.GblAnswerTypeId)
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
