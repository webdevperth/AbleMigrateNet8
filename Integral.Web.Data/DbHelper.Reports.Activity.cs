using System;
using System.Collections.Generic;
using System.Linq;

namespace Integral.Web {

  using static DbHelper.Common;

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Reports {

      public class Activity {

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
            public int TotalInteractions { get; private set; }
            public Interaction(int interactionTypeId, int chartOrder, int totalMinutes, int totalInteractions) {
              InteractionTypeId = interactionTypeId;
              ChartOrder = chartOrder;
              TotalMinutes = totalMinutes;
              TotalInteractions = totalInteractions;
            }
          }
        }

        public static List<InteractionsForMonth> GetInteractionsByMonthForProgram(int programId, DateTime startDate) {
          return GetInteractionsByMonth(InteractionsByMonthFor.Program, programId, startDate);
        }

        public static List<InteractionsForMonth> GetInteractionsByMonthForCompany(int companyId, DateTime startDate) {
          return GetInteractionsByMonth(InteractionsByMonthFor.Company, companyId, startDate);
        }

        public enum InteractionsByMonthFor { Company, Program }

        private static List<InteractionsForMonth> GetInteractionsByMonth(InteractionsByMonthFor interactionsByMonthFor, int programOrCompanyId, DateTime startDate) {

          var result = new List<InteractionsForMonth>();

          string coacheeFilterColumnName = interactionsByMonthFor == InteractionsByMonthFor.Company ? "ac.CompanyId" : "ac.ProgramJobId";

          Query($@"

            WITH Users AS (
              SELECT ac.UserId
              FROM al_Coachees ac
              WHERE {coacheeFilterColumnName} = @CoacheeFilterColumnValue
              GROUP BY ac.UserId
            ),

            Months AS (
                SELECT DISTINCT
                    DATEFROMPARTS(YEAR(ActivityDate), MONTH(ActivityDate), 1) AS MonthStart
                FROM (
                    SELECT Sentutc AS ActivityDate FROM al_AIMessage WHERE Sentutc >= @CutoffDate
                    UNION ALL
                    SELECT Completed AS ActivityDate FROM sv_360_Participants WHERE Completed >= @CutoffDate
                    UNION ALL
                    SELECT ViewedUTC AS ActivityDate FROM al_UserContent WHERE ViewedUTC >= @CutoffDate
                    UNION ALL
                    SELECT StartDateUTC AS ActivityDate FROM ev_WorkshopEvent WHERE StartDateUTC >= @CutoffDate
                    UNION ALL
                    SELECT ApptDateUTC AS ActivityDate FROM id_CoachingSession WHERE ApptDateUTC >= @CutoffDate
                ) AS Dates
            ),
            UserMonths AS (
                SELECT u.UserId, m.MonthStart
                FROM Users u
                CROSS JOIN Months m
            ),
            AIMessageCounts AS (
                SELECT
                    UserId,
                    DATEFROMPARTS(YEAR(Sentutc), MONTH(Sentutc), 1) AS MonthStart,
                    SUM(CASE WHEN IsFromAI = 0 THEN 1 ELSE 0 END) AS MessagesSent,
                    SUM(CASE WHEN IsFromAI = 1 THEN 1 ELSE 0 END) AS MessagesReceived
                FROM al_AIMessage
                WHERE Sentutc >= @CutoffDate
                GROUP BY UserId, DATEFROMPARTS(YEAR(Sentutc), MONTH(Sentutc), 1)
            ),
            WorkshopCounts AS (
                SELECT
                    c.UserId,
                    DATEFROMPARTS(YEAR(we.StartDateUTC), MONTH(we.StartDateUTC), 1) AS MonthStart,
                    COUNT(*) AS WorkshopsAttended,
                    SUM(DATEDIFF(MINUTE, we.StartDateUTC, we.EndDateUTC)) AS TotalWorkshopDuration
                FROM al_coachees c
                JOIN id_Job j ON c.ProgramJobId = j.JobId
                JOIN ev_WorkshopEvent we ON we.ProgramJobId = j.JobId
                WHERE we.StartDateUTC >= @CutoffDate
                  AND c.DeletedUtc IS NULL
                  AND we.WorkshopStatusId = @WorkshopStatusId_Confirmed
                GROUP BY c.UserId, DATEFROMPARTS(YEAR(we.StartDateUTC), MONTH(we.StartDateUTC), 1)
            ),
            CoachingSessionCounts AS (
                SELECT
                    c.UserId,
                    DATEFROMPARTS(YEAR(cs.ApptDateUTC), MONTH(cs.ApptDateUTC), 1) AS MonthStart,
                    COUNT(*) AS CoachingSessions,
                    SUM(cs.DurationMins) AS TotalCoachingSessionDuration
                FROM id_CoachingSession cs
                JOIN al_coachees c ON cs.AbleCoacheeId = c.CoacheeId
                WHERE cs.ApptDateUTC >= @CutoffDate
                GROUP BY c.UserId, DATEFROMPARTS(YEAR(cs.ApptDateUTC), MONTH(cs.ApptDateUTC), 1)
            ),
            SurveyCounts AS (
                SELECT
                    sv.UserId,
                    DATEFROMPARTS(YEAR(sv.Completed), MONTH(sv.Completed), 1) AS MonthStart, sv.isself,
                    SUM(CASE WHEN s.SurveyTypeCode = 'Pulse' THEN 1 ELSE 0 END) AS PulseSurveysCompleted,
                    SUM(CASE WHEN s.SurveyTypeCode = 'Intake' THEN 1 ELSE 0 END) AS IntakeSurveysCompleted,
                    SUM(CASE WHEN s.SurveyTypeCode = '360' and sv.IsSelf = 1 THEN 1 ELSE 0 END) AS Surveys360Completed,
                    SUM(CASE WHEN s.SurveyTypeCode = 'Eval' THEN 1 ELSE 0 END) AS EvalSurveysCompleted,
                    SUM(CASE WHEN s.SurveyTypeCode = 'DevPlan' THEN 1 ELSE 0 END) AS DevPlanSurveysCompleted,
                    SUM(CASE WHEN s.SurveyTypeCode = '360' and sv.IsSelf = 0 THEN 1 ELSE 0 END) AS Rater360Completed
                FROM sv_360_Participants sv
                JOIN sv_Survey s ON s.sv_id = sv.SurveyId
                WHERE sv.Completed >= @CutoffDate and sv.Completed IS NOT NULL --and sv.IsSelf = 1
                GROUP BY sv.UserId, DATEFROMPARTS(YEAR(sv.Completed), MONTH(sv.Completed), 1), sv.IsSelf
            ),
            ContentViewCounts AS (
                SELECT
                    UserId,
                    DATEFROMPARTS(YEAR(ViewedUTC), MONTH(ViewedUTC), 1) AS MonthStart,
                    COUNT(*) AS ContentViews
                FROM al_UserContent
                WHERE ViewedUTC >= @CutoffDate
                GROUP BY UserId, DATEFROMPARTS(YEAR(ViewedUTC), MONTH(ViewedUTC), 1)
            ),
            FinalResult AS (
                SELECT
                    um.UserId,
                    um.MonthStart,
                    COALESCE(aic.MessagesSent, 0) AS MessagesSent,
                    COALESCE(aic.MessagesReceived, 0) AS MessagesReceived,
                    COALESCE(wc.WorkshopsAttended, 0) AS WorkshopsAttended,
                    COALESCE(wc.TotalWorkshopDuration, 0) AS TotalWorkshopDuration,
                    COALESCE(csc.CoachingSessions, 0) AS CoachingSessions,
                    COALESCE(csc.TotalCoachingSessionDuration, 0) AS TotalCoachingSessionDuration,
                    COALESCE(sc.PulseSurveysCompleted, 0) AS PulseSurveysCompleted,
                    COALESCE(sc.IntakeSurveysCompleted, 0) AS IntakeSurveysCompleted,
                    COALESCE(sc.Surveys360Completed, 0) AS Surveys360Completed,
                    COALESCE(sc.Rater360Completed, 0) AS Rater360Completed,
                    COALESCE(sc.EvalSurveysCompleted, 0) AS EvalSurveysCompleted,
                    COALESCE(sc.DevPlanSurveysCompleted, 0) AS DevPlanSurveysCompleted,
                    COALESCE(cvc.ContentViews, 0) AS ContentViews
                FROM UserMonths um
                LEFT JOIN AIMessageCounts aic ON um.UserId = aic.UserId AND um.MonthStart = aic.MonthStart
                LEFT JOIN WorkshopCounts wc ON um.UserId = wc.UserId AND um.MonthStart = wc.MonthStart
                LEFT JOIN CoachingSessionCounts csc ON um.UserId = csc.UserId AND um.MonthStart = csc.MonthStart
                LEFT JOIN SurveyCounts sc ON um.UserId = sc.UserId AND um.MonthStart = sc.MonthStart
                LEFT JOIN ContentViewCounts cvc ON um.UserId = cvc.UserId AND um.MonthStart = cvc.MonthStart
            )
            ,ResultByUser AS (
              SELECT
                fr.UserId,
                fr.MonthStart,
                it.Interactiontypeid,
                it.ChartOrder,
                -- You can include 'it.name AS InteractionType' if needed
                CASE it.interactiontypeid
                    WHEN 1 THEN fr.MessagesSent
                    WHEN 2 THEN fr.MessagesReceived
                    WHEN 3 THEN fr.WorkshopsAttended
                    WHEN 4 THEN fr.CoachingSessions
                    WHEN 5 THEN fr.PulseSurveysCompleted
                    WHEN 6 THEN fr.IntakeSurveysCompleted
                    WHEN 7 THEN fr.Surveys360Completed
                    WHEN 8 THEN fr.EvalSurveysCompleted
                    WHEN 9 THEN fr.DevPlanSurveysCompleted
                    WHEN 10 THEN fr.ContentViews
                    WHEN 11 THEN fr.Rater360Completed
                    ELSE 0
                END AS Interactions,
                CASE
                    WHEN it.MinutesMultiplier IS NOT NULL THEN
                        (
                            CASE it.interactiontypeid
                                WHEN 1 THEN fr.MessagesSent
                                WHEN 2 THEN fr.MessagesReceived
                                WHEN 5 THEN fr.PulseSurveysCompleted
                                WHEN 6 THEN fr.IntakeSurveysCompleted
                                WHEN 7 THEN fr.Surveys360Completed
                                WHEN 8 THEN fr.EvalSurveysCompleted
                                WHEN 9 THEN fr.DevPlanSurveysCompleted
                                WHEN 10 THEN fr.ContentViews
                                WHEN 11 THEN fr.Rater360Completed
                                ELSE 0
                            END
                        ) * it.MinutesMultiplier
                    ELSE
                        CASE it.interactiontypeid
                            WHEN 3 THEN fr.TotalWorkshopDuration
                            WHEN 4 THEN fr.TotalCoachingSessionDuration
                            ELSE 0
                        END
                END AS TotalDurationMinutes
              FROM FinalResult fr
              CROSS JOIN al_interactiontype it
              WHERE fr.MonthStart BETWEEN @CutoffDate AND GETUTCDATE()
                AND (
                    CASE it.interactiontypeid
                        WHEN 1 THEN fr.MessagesSent
                        WHEN 2 THEN fr.MessagesReceived
                        WHEN 3 THEN fr.WorkshopsAttended
                        WHEN 4 THEN fr.CoachingSessions
                        WHEN 5 THEN fr.PulseSurveysCompleted
                        WHEN 6 THEN fr.IntakeSurveysCompleted
                        WHEN 7 THEN fr.Surveys360Completed
                        WHEN 8 THEN fr.EvalSurveysCompleted
                        WHEN 9 THEN fr.DevPlanSurveysCompleted
                        WHEN 10 THEN fr.ContentViews
                        WHEN 11 THEN fr.Rater360Completed
                        ELSE 0
                    END
                ) > 0
            )
            SELECT rbu.MonthStart, rbu.InteractionTypeId, rbu.ChartOrder, SUM(rbu.TotalDurationMinutes) AS TotalMinutes, SUM(rbu.Interactions) AS TotalInteractions
            FROM ResultByUser rbu
            GROUP BY rbu.MonthStart, rbu.ChartOrder, rbu.InteractionTypeId
            ORDER BY rbu.MonthStart, rbu.ChartOrder, rbu.InteractionTypeId;",

            dr => {

              DateTime monthStart = dr.GetDateTime("MonthStart");

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
                dr.GetInt("TotalMinutes"),
                dr.GetInt("TotalInteractions")
              ));

            },

            NewSqlParameter("CoacheeFilterColumnValue", programOrCompanyId),
            NewSqlParameter("CutoffDate", startDate),
            NewSqlParameter("WorkshopStatusId_Confirmed", WorkshopStatus.Ids.Confirmed)
          );

          return result;
        }

        // Simpler, more readable query than Jeroen's above made by chatgpt, but needs testing before replacing it.
        public static List<InteractionsForMonth> InteractionsForMonthsAlternative(int surveyCompanyId, DateTime fromDate, DateTime toDate) {

          var result = new List<InteractionsForMonth>();

          Query($@"

            WITH Coachees AS (
              SELECT ac.CoacheeId, ac.UserId, ac.ProgramJobId
              FROM al_Coachees ac
              WHERE ac.CompanyId = @CompanyId
                AND ac.DeletedUtc IS NULL
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
              WHERE sp.AbleSvCompanyId = @CompanyId
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
              WHERE sp.AbleSvCompanyId = @CompanyId
                AND sv.SurveyTypeCode <> '360'
                AND sp.Completed BETWEEN @FromDate AND @ToDate

            )

            SELECT
              YEAR(i.ActivityUtc) AS Year,
              MONTH(i.ActivityUtc) AS Month,
              it.ChartOrder,
              i.InteractionTypeId,
              SUM(ISNULL(it.MinutesMultiplier, i.DurationMinutes)) AS TotalMinutes,
              COUNT(i.ActivityUtc) AS TotalInteractions
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
                dr.GetInt("TotalMinutes"),
                dr.GetInt("TotalInteractions")
              ));

            },

            NewSqlParameter("CompanyId", surveyCompanyId),
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
      }
    }
  }
}
