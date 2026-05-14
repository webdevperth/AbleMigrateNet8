using System;
using System.Collections.Generic;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Reports {

      public class SurveyViewer {

        public class SurveyListItem {
          public int IntakeCodeId { get; internal set; }
          public string IntakeName { get; internal set; }
          public DateTime IntakeCloseDateLocal { get; internal set; }
          public string ReportType { get; internal set; }
          public string SurveyTitle { get; internal set; }
          public string SurveyPublicTitle { get; internal set; }
        }

        public static List<SurveyListItem> GetSurveyList(List<int> companyIdList, List<int> reportTypeIdList) {

          var svList = new List<SurveyListItem>();

          if (companyIdList.IsNullOrEmpty() || reportTypeIdList.IsNullOrEmpty()) return svList;

          // Able surveys (company id via coachee) union with Jarvis surveys (company id via intake job).
          Common.Query(@"

          WITH
          cmpIds AS ( SELECT Value AS CompanyId FROM STRING_SPLIT(@CompanyIdString, ',') ),
          rtIds  AS ( SELECT Value AS ReportTypeId FROM STRING_SPLIT(@ReportTypeIdString, ',') )

          SELECT sc.CodeId AS IntakeCodeId, rt.ReportTypeName, sc.IntakeCloseDate, sc.Value AS IntakeName, sv.sv_title, sv.sv_360GroupName
          FROM sv_Survey sv
          INNER JOIN cmpIds ON cmpIds.CompanyId = sv.SvCompanyId
          INNER JOIN sv_360_AnswerTypes sat ON sat.SurveyId = sv.sv_id
          INNER JOIN sv_360_Codes sc ON sat.AnswerTypeId = sc.AnswerTypeId
          INNER JOIN sv_ReportType rt ON sv.sv_ReportType = rt.ReportTypeCode
          INNER JOIN rtIds ON rt.ReportTypeId = rtIds.ReportTypeId
          WHERE sat.AnswerTypeDescr = 'date'
            AND sv.SurveyViewerQuestionCount > 0
            AND sc.IntakeTotalCompleted > 0
          GROUP BY sc.CodeId, rt.ReportTypeName, sc.IntakeCloseDate, sc.Value, sv.sv_title, sv.sv_360GroupName

          ORDER BY sc.IntakeCloseDate DESC;",
            dr => {
              svList.Add(
                new SurveyListItem() {
                  IntakeCodeId = dr.GetInt("IntakeCodeId"),
                  IntakeName = dr.GetString("IntakeName"),
                  IntakeCloseDateLocal = dr.GetDateTime("IntakeCloseDate"),
                  ReportType = dr.GetString("ReportTypeName"),
                  SurveyTitle = dr.GetString("sv_title"),
                  SurveyPublicTitle = dr.GetString("sv_360GroupName")
                }
              );
            },
            Common.NewSqlParameter("CompanyIdString", string.Join(",", companyIdList)),
            Common.NewSqlParameter("ReportTypeIdString", string.Join(",", reportTypeIdList))
          );

          return svList;
        }

      }
    }
  }
}
