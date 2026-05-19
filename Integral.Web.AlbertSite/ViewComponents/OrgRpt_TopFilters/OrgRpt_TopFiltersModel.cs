using System.Text;
using Microsoft.Data.SqlClient;
using Integral.Web.PortalSite.Reports;

namespace Integral.Web.PortalSite.ViewComponents {

  using static DbHelper.Common;

  // Model for the OrgRpt_TopFilters ViewComponent. Mirrors the public API of the
  // legacy UserControls/OrgRpt_TopFilters.ascx.cs codebehind so the .cshtml view
  // can call the same helper methods (GetBenchOptions / GetOrgLevelOptions /
  // GetDemographicOptions) used by the original <% %> blocks.
  public class OrgRpt_TopFiltersModel {

    public DbHelper.OrgSurveys.SurveyInfo surveyInfo;

    public string GetBenchOptions() {
      string html = "";
      foreach (var opt in OrgReports.BenchmarkOptions) {
        if (opt.OptionValue == "i" && surveyInfo.SectorId == null) continue;
        html += "<option value=\"" + opt.OptionValue + "\">" + opt.OptionTitle.HTMLEncode() + " " + OrgReports.Benchmark_Display_Text.UCaseFirstChar() + "</option>";
      }
      return html;
    }

    public string GetOrgLevelOptions() {

      // Note: This is only for a "flat" list of divisions, without "directorates".
      // For now, directorate structures are done manually in the HTML for those specific surveys.

      var html = new StringBuilder();

      Query(@"
        SELECT dc.CodeId, dc.value, COUNT(*) AS PartCount
        FROM sv_360_Codes dc
        INNER JOIN sv_360_AnswerTypes dt ON dc.AnswerTypeId = dt.AnswerTypeId
        INNER JOIN sv_360_Participants sp ON sp.SurveyId = dt.SurveyId AND sp.OrgDivisionCodeId = dc.CodeId
        WHERE dt.SurveyId = @SurveyId
          AND dt.AnswerTypeDescr = 'division'
        GROUP BY dc.CodeId, dc.value, dc.RptOrder1, dc.Code
        ORDER BY IIF(dc.RptOrder1 > 0, dc.RptOrder1, dc.Code)",
        dr => {
          string divisionName = dr.GetString("Value");
          int divisionCodeId = dr.GetInt("CodeId");
          html.Append($@"<option value=""{divisionCodeId}"">{divisionName.HTMLEncode()} ({dr.GetInt("PartCount")})</option>");
        },
        NewSqlParameter("SurveyId", surveyInfo.SurveyId)
      );

      return html.ToString();
    }

    public string GetDemographicOptions() {

      // All demographic questions and options, grouped in optgroups.

      var html = new StringBuilder();

      string sql = @"
        SELECT q.QuestionId,
          IIF(q.QuestionText <> '', q.QuestionText, q.QuestionTextFull1) AS QuestionText,
          c.CodeId,
          IIF(c.ValueAbbreviated <> '', c.ValueAbbreviated, c.Value) AS Value
        FROM sv_360_Questions q
        INNER JOIN sv_360_AnswerTypes t ON q.AnswerTypeId = t.AnswerTypeId
        INNER JOIN sv_360_Codes c ON t.AnswerTypeId = c.AnswerTypeId
        WHERE q.SurveyId = @SurveyId
          AND t.IsDemographic = 1
          AND t.AnswerTypeDescr <> 'division'
        ORDER BY q.Sort, c.Code";

      using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
        using (var cmd = new SqlCommand(sql, conn)) {
          cmd.Parameters.AddInt("@SurveyId", surveyInfo.SurveyId);
          conn.Open();
          using (SqlDataReader dr = cmd.ExecuteReader()) {
            if (dr.Read()) {
              bool hasRow = true;
              while (hasRow) {
                int questionId = dr.GetInt("QuestionId");
                string questionText = dr.GetString("QuestionText");
                html.Append("<optgroup label=\"" + questionText.HTMLEncode() + "\">");
                while (hasRow) { // Codes for this question.
                  if (dr.GetInt("QuestionId") != questionId) break; // Next question.
                  int codeId = dr.GetInt("CodeId");
                  string codeValue = dr.GetString("Value");
                  html.Append("<option value=\"" + questionId + "-" + codeId + "\">" + codeValue.HTMLEncode() + "</option>");
                  hasRow = dr.Read();
                }
                html.Append("</optgroup>");
              }
            }
          }
        }
      }
      return html.ToString();
    }

  }
}
