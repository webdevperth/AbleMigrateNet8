using System;
using System.Collections.Generic;
using System.Linq;

namespace Integral.Web {

  using static DbHelper.Common;

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Reports {

      public class Coachee {

        public class InteractionsForMonth {

          public DateTime MonthStart { get; private set; }
          public List<Interaction> Interactions { get; private set; }

          public InteractionsForMonth(DateTime monthStart) {
            MonthStart = monthStart;
            Interactions = new List<Interaction>();
          }

          public class Interaction {
            public int InteractionTypeId { get; private set; }
            public int ChartOrder { get; private set; }
            public int TotalMinutes { get; private set; }
            public Interaction(int interactionTypeId, int chartOrder, int totalMinutes) {
              InteractionTypeId = interactionTypeId;
              ChartOrder = chartOrder;
              TotalMinutes = totalMinutes;
            }
          }
        }

        public static List<InteractionsForMonth> GetInteractionsByMonth(int coacheeId, DateTime fromDate, DateTime toDate) {

          var result = new List<InteractionsForMonth>();

          Query($@"

            WITH Coachees AS (
              SELECT ac.CoacheeId, ac.UserId, ac.ProgramJobId
              FROM al_Coachees ac
              WHERE ac.CoacheeId = @CoacheeId
            ),

            Interactions AS (

              SELECT
                aim.SentUtc AS ActivityUtc,
                IIF(aim.IsFromAI = 0, @ItId_MessageSent, @ItId_MessageReceived) AS InteractionTypeId,
                NULL AS DurationMinutes
              FROM al_AIMessage aim
              INNER JOIN Coachees ac ON ac.UserId = aim.UserId
              WHERE aim.SentUtc BETWEEN @FromDate AND @ToDate

              UNION

              SELECT
                we.StartDateUtc AS ActivityUtc,
                @ItId_WorkshopAttended AS InteractionTypeId,
                SUM(DATEDIFF(MINUTE, we.StartDateUtc, we.EndDateUtc)) AS DurationMinutes
              FROM ev_WorkshopEvent we
              INNER JOIN Coachees ac ON ac.ProgramJobId = we.ProgramJobId -- Multiply workshops by coachees
              WHERE we.StartDateUtc BETWEEN @FromDate AND @ToDate
                AND we.WorkshopStatusId = @WorkshopStatusId_Confirmed
              GROUP BY we.StartDateUtc

              UNION

              SELECT
                cs.ApptDateUTC AS ActivityUtc,
                @ItId_CoachingSession AS InteractionTypeId,
                cs.DurationMins AS DurationMinutes
              FROM id_CoachingSession cs
              INNER JOIN Coachees ac ON ac.CoacheeId = cs.AbleCoacheeId
              WHERE cs.ApptDateUTC BETWEEN @FromDate AND @ToDate

              UNION

              SELECT
                uc.ViewedUtc AS ActivityUtc,
                @ItId_ContentView AS InteractionTypeId,
                NULL AS DurationMinutes
              FROM al_UserContent uc
              INNER JOIN Coachees ac ON ac.UserId = uc.UserId
              WHERE uc.ViewedUtc BETWEEN @FromDate AND @ToDate

              UNION

              SELECT
                sp.Completed AS ActivityUtc,
                IIF(sp.IsSelf = 1, @ItId_Completed360Self, @ItId_Completed360Rater) AS InteractionTypeId,
                NULL AS DurationMinutes
              FROM sv_360_Participants sp
              INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
              WHERE sp.AbleCoacheeId = @CoacheeId
                AND sv.SurveyTypeCode = '360'
                AND sp.Completed BETWEEN @FromDate AND @ToDate

              UNION

              SELECT
                sp.Completed AS ActivityUtc,
                CASE sv.SurveyTypeCode
                  WHEN 'pulse' THEN @ItId_CompletedPulse
                  WHEN 'intake' THEN @ItId_CompletedIntake
                  WHEN 'eval' THEN @ItId_CompletedEval
                  WHEN 'devplan' THEN @ItId_CompletedDevPlan
                  ELSE NULL
                END AS InteractionTypeId,
                NULL AS DurationMinutes
              FROM sv_360_Participants sp
              INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
              WHERE sp.AbleCoacheeId = @CoacheeId
                AND sv.SurveyTypeCode <> '360'
                AND sp.Completed BETWEEN @FromDate AND @ToDate

            )

            SELECT
              YEAR(i.ActivityUtc) AS Year,
              MONTH(i.ActivityUtc) AS Month,
              it.ChartOrder,
              i.InteractionTypeId,
              SUM(ISNULL(it.MinutesMultiplier, i.DurationMinutes)) AS TotalMinutes
            FROM Interactions i
            INNER JOIN al_InteractionType it ON it.InteractionTypeId = i.InteractionTypeId
            GROUP BY YEAR(i.ActivityUtc), MONTH(i.ActivityUtc), it.ChartOrder, i.InteractionTypeId
            ORDER BY YEAR(i.ActivityUtc), MONTH(i.ActivityUtc), it.ChartOrder, i.InteractionTypeId;",

            dr => {

              DateTime monthStart = new DateTime(dr.GetInt("Year"), dr.GetInt("Month"), 1);

              // Check if period from db row is same as latest one from the list of months.
              // If not, or if no month items exist yet, create a new month item and add it to the collection.
              InteractionsForMonth thisMonth;
              if (result.Count > 0 && result.Last().MonthStart == monthStart) {
                thisMonth = result.Last(); // We're still in the same month as the latest item.
              } else {
                thisMonth = new InteractionsForMonth(monthStart);
                result.Add(thisMonth); // Add a new month item.
              }

              // Add interaction to this month.
              thisMonth.Interactions.Add(new InteractionsForMonth.Interaction(
                dr.GetInt("InteractionTypeId"),
                dr.GetInt("ChartOrder"),
                dr.GetInt("TotalMinutes")));

            },

            NewSqlParameter("CoacheeId", coacheeId),
            NewSqlParameter("FromDate", fromDate),
            NewSqlParameter("ToDate", toDate),

            NewSqlParameter("WorkshopStatusId_Confirmed", WorkshopStatus.Ids.Confirmed),
            NewSqlParameter("ItId_MessageSent", Interactions.InteractionTypeIds.MessageSent),
            NewSqlParameter("ItId_MessageReceived", Interactions.InteractionTypeIds.MessageReceived),
            NewSqlParameter("ItId_WorkshopAttended", Interactions.InteractionTypeIds.WorkshopAttended),
            NewSqlParameter("ItId_CoachingSession", Interactions.InteractionTypeIds.CoachingSession),
            NewSqlParameter("ItId_CompletedPulse", Interactions.InteractionTypeIds.CompletedPulse),
            NewSqlParameter("ItId_CompletedIntake", Interactions.InteractionTypeIds.CompletedIntake),
            NewSqlParameter("ItId_CompletedEval", Interactions.InteractionTypeIds.CompletedEval),
            NewSqlParameter("ItId_CompletedDevPlan", Interactions.InteractionTypeIds.CompletedDevPlan),
            NewSqlParameter("ItId_ContentView", Interactions.InteractionTypeIds.ContentView),
            NewSqlParameter("ItId_Completed360Self", Interactions.InteractionTypeIds.Completed360Self),
            NewSqlParameter("ItId_Completed360Rater", Interactions.InteractionTypeIds.Completed360Rater)
          );

          return result;
        }

        public class MonthlyProgress {

          public string SurveyTypeCode { get; private set; }
          public string ProgressChartLineColor { get; private set; }
          public int Year { get; private set; }
          public int Month { get; private set; }
          public int MonthIndex { get; private set; }
          public decimal ScoreAvg { get; private set; }

          public MonthlyProgress(string surveyTypeCode, string progressChartLineColor, int year, int month, int monthIndex, decimal scoreAvg) {
            SurveyTypeCode = surveyTypeCode;
            ProgressChartLineColor = progressChartLineColor;
            Year = year;
            Month = month;
            MonthIndex = monthIndex;
            ScoreAvg = scoreAvg;
          }
        }

        public static List<MonthlyProgress> GetMonthlyProgress(int coacheeId, DateTime startDate, DateTime endDate) {

          var result = new List<MonthlyProgress>();

          Query($@"
            SELECT
              sv.SurveyTypeCode, st.ProgressChartLineColor,
              YEAR(sp.Completed) AS Year, MONTH(sp.Completed) AS Month,
              DATEDIFF(MONTH, @StartDate, sp.Completed) AS MonthIndex,
              AVG(sc.ValueScore) AS ScoreAvg
            FROM sv_360_Participants sp
            INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
            INNER JOIN al_SurveyType st ON st.SurveyTypeCode = sv.SurveyTypeCode
            INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
            INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
            INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
            WHERE sp.AbleCoacheeId = @CoacheeId
              AND sp.Completed BETWEEN @StartDate AND @EndDate
              AND sq.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
              AND st.ProgressChartLineColor IS NOT NULL
            GROUP BY
              sv.SurveyTypeCode, st.ProgressChartLineColor,
              YEAR(sp.Completed), MONTH(sp.Completed), DATEDIFF(MONTH, @StartDate, sp.Completed)
            HAVING AVG(sc.ValueScore) IS NOT NULL
            ORDER BY sv.SurveyTypeCode, YEAR(sp.Completed), MONTH(sp.Completed);",

            dr => {
              result.Add(new MonthlyProgress(
                surveyTypeCode: dr.GetString("surveyTypeCode"),
                progressChartLineColor: dr.GetString("ProgressChartLineColor"),
                year: dr.GetInt("Year"),
                month: dr.GetInt("Month"),
                monthIndex: dr.GetInt("MonthIndex"),
                scoreAvg: dr.GetDecimal("ScoreAvg")
              ));
            },

            NewSqlParameter("StartDate", startDate),
            NewSqlParameter("EndDate", endDate),
            NewSqlParameter("CoacheeId", coacheeId)
          );

          return result;
        }

      }
    }
  }
}
