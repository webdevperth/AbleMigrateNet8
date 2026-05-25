using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Integral.Web.Services;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class ReportTypes {

      private static List<ReportTypeInfo> reportTypeList;
      private static Dictionary<int, ReportTypeInfo> reportTypeById;
      private static Dictionary<string, ReportTypeInfo> reportTypeByDbValue;
      private readonly static object lockObj = new object();

      static ReportTypes() {
        lock (lockObj) {
          if (reportTypeList?.Count > 0) return;
          ReadReportTypes();
        }
      }

      static void ReadReportTypes() {

        // TODO: Thread safety lock in case we need to call this while running to refresh the list.

        reportTypeList = new List<ReportTypeInfo>();
        reportTypeById = new Dictionary<int, ReportTypeInfo>();
        reportTypeByDbValue = new Dictionary<string, ReportTypeInfo>();

        AddReportTypesFromDb();

      }

      static void AddReportTypesFromDb() {

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {

          string sql = @"
            SELECT ReportTypeId, st_code, ReportTypeCode, ReportTypeName, ReportPathJarvisFolder, HasCoacheeOnlineReport, ReportInformationHtml
            FROM sv_ReportType
            ORDER BY ReportTypeCode";

          using (var cmd = new SqlCommand(sql, conn)) {
            conn.Open();
            try {
              using (SqlDataReader dr = cmd.ExecuteReader()) {
                while (dr.Read()) {
                  var ct = new ReportTypeInfo(
                    dr.GetInt("ReportTypeId"),
                    dr.GetString("st_code"),
                    dr.GetString("ReportTypeCode"),
                    dr.GetString("ReportTypeName"),
                    dr.GetString("ReportPathJarvisFolder"),
                    dr.GetBoolFromInt("HasCoacheeOnlineReport"),
                    dr.GetString("ReportInformationHtml")
                  );
                  reportTypeList.Add(ct);
                  reportTypeById.Add(ct.ReportTypeId, ct);
                  reportTypeByDbValue.Add(ct.ReportTypeCode.ToLower(), ct);
                }
              }
            } catch (Exception ex) {
              var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
              telemetry?.Exception(ex)
                .WithOperation(nameof(AddReportTypesFromDb))
                .WithOperationContext("ReportTypes")
                .Track();
              throw;
            }
          }
        }
      }

      public static List<ReportTypeInfo> GetReportTypeList() {
        return reportTypeList;
      }

      public static ReportTypeInfo GetReportTypeByIdOrNull(int? reportTypeId) {
        if (reportTypeId == null || !reportTypeById.ContainsKey((int)reportTypeId)) return null;
        return reportTypeById[(int)reportTypeId];
      }
      public static ReportTypeInfo GetReportTypeByDbValue(string reportTypeDbValue) {
        if (reportTypeDbValue == null || !reportTypeByDbValue.ContainsKey(reportTypeDbValue.ToLower())) return null;
        return reportTypeByDbValue[reportTypeDbValue.ToLower()];
      }

      // Get all report types of surveys with global category questions.
      public static List<ReportTypeForCompanyInfo> GetReportTypesForSurveyViewer() {

        var rtList = new List<ReportTypeForCompanyInfo>();

        Common.Query($@"
          SELECT rt.ReportTypeId, rt.ReportTypeCode, rt.ReportTypeName
          FROM sv_ReportType rt
          INNER JOIN sv_Survey sv ON rt.ReportTypeCode = sv.sv_ReportType
          INNER JOIN sv_360_Questions sq ON sv.sv_id = sq.SurveyId
          INNER JOIN al_RptQnGrpHgGblQns gqh ON sq.GblQuestionId = gqh.GblQuestionId
          GROUP BY rt.ReportTypeId, rt.ReportTypeCode, rt.ReportTypeName
          ORDER BY rt.ReportTypeName",
          dr => {
            var rt = new ReportTypeForCompanyInfo() {
              ReportTypeId = dr.GetInt("ReportTypeId"),
              ReportTypeCode = dr.GetString("ReportTypeCode"),
              ReportTypeName = dr.GetString("ReportTypeName")
            };
            rtList.Add(rt);
          }
        );

        return rtList;
      }

      public class ReportTypeForCompanyInfo {
        public int ReportTypeId { get; internal set; }
        public string ReportTypeCode { get; internal set; }
        public string ReportTypeName { get; internal set; }
        public int IntakeCount { get; internal set; }
      }

      public class ReportTypeInfo {

        public int ReportTypeId { get; private set; }
        public string StCode { get; private set; }
        public string ReportTypeCode { get; private set; }
        public string ReportTypeName { get; private set; }
        public string ReportPathJarvisFolder { get; private set; }
        public bool IsAble { get; private set; }
        public bool IsIOS { get; private set; }
        public bool HasCoacheeOnlineReport { get; private set; }
        public string ReportInformationHtml { get; private set; }

        public ReportTypeInfo(
          int reportTypeId,
          string stCode,
          string reportTypeCode,
          string reportTypeName,
          string reportPathJarvisFolder,
          bool hasCoacheeOnlineReport,
          string reportInformationHtml
        ) {
          this.ReportTypeId = reportTypeId;
          this.StCode = stCode;
          this.ReportTypeCode = reportTypeCode;
          this.ReportTypeName = reportTypeName;
          this.ReportPathJarvisFolder = !reportPathJarvisFolder.IsNullOrEmpty() ? reportPathJarvisFolder : reportTypeCode;
          this.IsAble = reportTypeCode.Equals(ConfigHelper.ReportTypes.Able360, StringComparison.OrdinalIgnoreCase);
          this.IsIOS = reportTypeCode.Equals(ConfigHelper.ReportTypes.IOS, StringComparison.OrdinalIgnoreCase);
          this.HasCoacheeOnlineReport = hasCoacheeOnlineReport;
          this.ReportInformationHtml = reportInformationHtml;
        }
      }

    }
  }
}

