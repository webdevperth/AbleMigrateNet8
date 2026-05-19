using System.Collections.Generic;
using System.Text;
using Integral.Web.PortalSite.Reports;

namespace Integral.Web.PortalSite.ViewComponents {

  using static DbHelper.Common;

  // Model for the OrgRpt_HeatMap ViewComponent. Mirrors the public API of the
  // legacy UserControls/OrgRpt_HeatMap.ascx.cs codebehind so the .cshtml view
  // can call the same helper methods used by the original <% %> blocks.
  public class OrgRpt_HeatMapModel {

    public DbHelper.OrgReportsCached.ReportData reportData;
    public int categoryDimensionNo;
    public string includeDimCodes;
    public List<HeatMapRow> HeatMapData;

    public string GetRowTitle(HeatMapRow heatMapRow) {
      int dashPos = heatMapRow.RowTitle.IndexOf("- ");
      if (dashPos > 0) return heatMapRow.RowTitle.Substring(dashPos + 2);
      return heatMapRow.RowTitle;
    }

    public string GetRowResultCells(HeatMapRow heatMapRow) {

      if (heatMapRow.CellValues == null) {
        return "<td class=\"nocells\"></td>";
      }

      string[] cellValues = heatMapRow.CellValues.Split(',');
      var html = new StringBuilder();

      foreach (string valueString in cellValues) {
        bool validValue = int.TryParse(valueString, out int value);
        html.Append("<td class=\"data ");
        string valueClass;
        if (!validValue) {
          valueClass = "empty";
        } else {
          valueClass = "val" + (value / 10).ToString(); // val-1, val0, val1, ... val10
        }
        html.Append(valueClass);
        html.Append("\">");
        html.Append(validValue ? value.ToString() : "&nbsp;");
        html.Append("</td>");
      }

      return html.ToString();
    }

    public string GetSectionTitleCells() {

      var html = new StringBuilder();
      includeDimCodes = string.Empty; // Dimension codes to include in the heatmap.

      Query($@"

        SELECT dc.Code, dc.Value AS SectionName, COUNT(q.QuestionId) AS QnCount
        FROM sv_360_Questions q
          INNER JOIN sv_360_Codes dc ON dc.Code = q.Dimension{categoryDimensionNo}
          INNER JOIN sv_360_AnswerTypes dt ON dc.AnswerTypeId = dt.AnswerTypeId
          INNER JOIN sv_360_Dimensions d ON dt.AnswerTypeId = d.AnswerTypeId
        WHERE q.SurveyId = @SurveyId
          AND q.GblAnswerTypeId = @GblAnswerTypeId
          AND dt.SurveyId = @SurveyId
          AND d.DimensionNo = @Dimension
          AND (@Dimension <> 1 OR dc.Code > 1) -- Don't include IOI
          GROUP BY dc.Code, dc.Value
          HAVING COUNT(q.QuestionId) > @MinQuestions
          ORDER BY dc.Code",

        dr => {
          if (includeDimCodes.Length > 0) {
            includeDimCodes += ",";
          }
          includeDimCodes += dr.GetInt("Code").ToString();
          html.Append("<td class=\"colTitle\">");
          html.Append(dr.GetString("SectionName").Replace("/", " / ").HTMLEncode());
          html.Append("</td>");
        },

        NewSqlParameter("SurveyId", reportData.SurveyInfo.SurveyId),
        NewSqlParameter("Dimension", categoryDimensionNo),
        NewSqlParameter("GblAnswerTypeId", DbHelper.OrgSurveys.Standard_GblAnswerTypeId),
        NewSqlParameter("MinQuestions", OrgReports.Min_Questions_Per_Category)
      );

      return html.ToString();
    }

    public List<HeatMapRow> GetHeatMapData() {
      // Return list of rows.

      var rows = new List<HeatMapRow>();

      Query($@"

        WITH qc AS (
          SELECT dc.Code
          FROM sv_360_Questions q
          INNER JOIN sv_360_Codes dc ON dc.Code = q.Dimension{categoryDimensionNo}
          INNER JOIN sv_360_AnswerTypes dt ON dc.AnswerTypeId = dt.AnswerTypeId
          INNER JOIN sv_360_Dimensions d ON dt.AnswerTypeId = d.AnswerTypeId
          WHERE q.SurveyId = @SurveyId
            AND q.GblAnswerTypeId = @GblAnswerTypeId
            AND dt.SurveyId = @SurveyId
            AND d.DimensionNo = @Dimension
            AND (@Dimension <> 1 OR dc.Code > 1) -- Don't include IOI
          GROUP BY dc.Code
          HAVING COUNT(q.QuestionId) > @MinQuestions
        )
        SELECT
          dc.Value AS DivisionName,
          (STUFF((
            SELECT ','+ CAST(dp.AvgScore AS VARCHAR(10))
            FROM sv_360_Codes sc
            INNER JOIN sv_360_AnswerTypes st ON st.AnswerTypeId = sc.AnswerTypeId
            INNER JOIN sv_360_Dimensions dim ON st.AnswerTypeId = dim.AnswerTypeId
            CROSS APPLY (
              SELECT ROUND(AVG(sac.ValueScore), 0) AS AvgScore
              FROM sv_360_Participants dp
              -- division
              INNER JOIN sv_Answers da ON da.ParticipantId = dp.PartId
              INNER JOIN sv_360_Questions dq ON dq.QuestionId = da.QuestionId
              INNER JOIN sv_360_AnswerTypes dt ON dq.AnswerTypeId = dt.AnswerTypeId
              -- report section
              INNER JOIN sv_Answers sa ON sa.ParticipantId = dp.PartId
              INNER JOIN sv_360_Questions saq ON saq.QuestionId = sa.QuestionId
              INNER JOIN sv_360_Codes sac ON sac.CodeId = sa.CodeId
              WHERE dt.SurveyId = @SurveyId
                AND dp.Completed IS NOT NULL
                AND dt.AnswerTypeDescr = 'division'
                AND da.CodeId = dc.CodeId
                AND saq.SurveyId = @SurveyId
                AND saq.Dimension{categoryDimensionNo} = sc.Code
            ) AS dp
            WHERE st.SurveyId = @SurveyId
              AND dim.DimensionNo = @Dimension
              AND sc.Code IN (SELECT Code from qc)
              AND (@Dimension <> 1 OR sc.Code > 1) -- Don't include IOI
            ORDER BY IIF(sc.RptOrder1 > 0, sc.RptOrder1, sc.Code)
          FOR XML PATH (''), TYPE, ROOT).value('root[1]', 'nvarchar(max)'), 1, 1, '')
          ) AS CellValues
        FROM sv_360_Codes dc
        INNER JOIN sv_360_AnswerTypes dt ON dt.AnswerTypeId = dc.AnswerTypeId
        WHERE dt.SurveyId = @SurveyId
          AND dt.AnswerTypeDescr = 'division'
        ORDER BY dc.Code",

        dr => {
          rows.Add(new HeatMapRow(dr.GetString("DivisionName"), dr.GetString("CellValues")));
        },

        NewSqlParameter("SurveyId", reportData.SurveyInfo.SurveyId),
        NewSqlParameter("Dimension", categoryDimensionNo),
        NewSqlParameter("GblAnswerTypeId", DbHelper.OrgSurveys.Standard_GblAnswerTypeId),
        NewSqlParameter("MinQuestions", OrgReports.Min_Questions_Per_Category)
      );

      return rows;
    }

    public string GetScoreFormatted(double? score) {
      if (score == null) return " - ";
      return ((double)score * 10).ToString("0");
    }

    public class HeatMapRow {
      public string RowTitle;
      public string CellValues;
      public HeatMapRow(string rowTitle, string cellValues) {
        this.RowTitle = rowTitle;
        this.CellValues = cellValues;
      }
    }

  }
}
