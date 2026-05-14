using System;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  using static DbHelper.Common;

  public partial class DbHelper : HelperBase<DbHelper> {

    public class ProgramSummary {

      public static string GetSelectColumns() {

        return @"

          ps.ProgramJobId,
          ps.LastUpdatedUtc,

          ps.PaxTotal,
          ps.PaxHasCoaching,
          ps.PaxAssignedCoach,
          ps.PaxSessionsAllocated,
          ps.PaxSessionsCompleted,

          ps.WorkshopsTotal,
          ps.WorkshopsCompleted,

          ps.Eval_All_AnsSum,
          ps.Eval_All_AnsCount,
          ps.Eval_All_AnsAvg,

          ps.Eval_Workshops_AnsSum,
          ps.Eval_Workshops_AnsCount,
          ps.Eval_Workshops_AnsAvg,

          ps.Eval_Coaching_AnsSum,
          ps.Eval_Coaching_AnsCount,
          ps.Eval_Coaching_AnsAvg,

          ps.Eval_EndProgram_AnsSum,
          ps.Eval_EndProgram_AnsCount,
          ps.Eval_EndProgram_AnsAvg,
          ps.Eval_EndProgram_CompletedCount,

          ps.PrePost360_Pre_ScheduledUtc,
          ps.PrePost360_Pre_ClosedUtc,
          ps.PrePost360_Pre_AnsSum,
          ps.PrePost360_Pre_AnsCount,
          ps.PrePost360_Pre_AnsAvg,
          ps.PrePost360_Pre_CompletedCount,

          ps.PrePost360_Post_ScheduledUtc,
          ps.PrePost360_Post_ClosedUtc,
          ps.PrePost360_Post_AnsSum,
          ps.PrePost360_Post_AnsCount,
          ps.PrePost360_Post_AnsAvg,
          ps.PrePost360_Post_CompletedCount,

          ps.ComponentsCount,
          ps.ComponentsCompleted,

          ps.RevenueTotal,
          ps.RevenueCompleted,
          ps.CostTotal,
          ps.ProfitTotal
        ";
      }

      public static int UpdateProgramSummaries(int? programIdOrNullForAll = null) {

        string sql = @"

          INSERT INTO al_ProgramSummary (ProgramJobId, LastUpdatedUtc)
          SELECT ij.JobId, '1970-01-01'
          FROM id_Job ij
          LEFT OUTER JOIN al_ProgramSummary ps ON ps.ProgramJobId = ij.JobId
          WHERE ps.ProgramJobId IS NULL;

          WITH lu_all
          AS
          ( -- Latest update times of various parts of programs - pax, sessions, workshops, etc.
            SELECT ij.JobId AS ProgramJobId, ij.LastUpdatedUTC
            FROM id_Job ij
            WHERE ij.IsAbleProgram = 1
              AND (@ProgramJobId IS NULL OR ij.JobId = @ProgramJobId)
            UNION
            SELECT ac.ProgramJobId, MAX(ac.LastUpdatedUtc)
            FROM al_Coachees ac
            WHERE (@ProgramJobId IS NULL OR ac.ProgramJobId = @ProgramJobId)
            GROUP BY ac.ProgramJobId
            UNION
            SELECT sp.AbleProgramJobId, MAX(sp.Completed)
            FROM sv_360_Participants sp
            WHERE (@ProgramJobId IS NULL AND sp.AbleProgramJobId > 0 OR sp.AbleProgramJobId = @ProgramJobId)
            GROUP BY sp.AbleProgramJobId
            UNION
            SELECT we.ProgramJobId, MAX(we.LastUpdatedUtc)
            FROM ev_WorkshopEvent we
            WHERE (@ProgramJobId IS NULL AND we.ProgramJobId > 0 OR we.ProgramJobId = @ProgramJobId)
            GROUP BY we.ProgramJobId
            UNION
            SELECT ci.ProgramJobId, MAX(ci.LastUpdatedUtc)
            FROM al_ConsultingItems ci
            WHERE (@ProgramJobId IS NULL AND ci.ProgramJobId > 0 OR ci.ProgramJobId = @ProgramJobId)
            GROUP BY ci.ProgramJobId
            UNION
            SELECT ci.ProgramJobId, MAX(ci.AbleUpdatedUtc)
            FROM al_ProgramCostItems ci
            WHERE (@ProgramJobId IS NULL AND ci.ProgramJobId > 0 OR ci.ProgramJobId = @ProgramJobId)
            GROUP BY ci.ProgramJobId
          ),
          upd_pj
          AS
          ( -- Select only those that need updating - update time is later than existing summary data.
            SELECT lu.ProgramJobId
            FROM lu_all lu
            INNER JOIN al_ProgramSummary ps ON ps.ProgramJobId = lu.ProgramJobId
            WHERE lu.ProgramJobId = @ProgramJobId
               OR ( @ProgramJobId IS NULL AND (lu.LastUpdatedUtc > ps.LastUpdatedUtc OR @NoDateCheck = 1) )
            GROUP BY lu.ProgramJobId
          ),
          pax_stats
          AS
          (
            SELECT pj.ProgramJobId,
              COUNT(*) AS PaxTotal,
              COUNT(NULLIF(ac.CoachingTypeId, 1)) AS HasCoaching, -- excl ""No""
              COUNT(NULLIF(ac.CoachUserId, 233)) AS AssignedCoach, -- excl Unassigned
              SUM(ac.SessionsAllocated) AS SessionsAllocated,
              SUM(ac.SessionsCompleted) AS SessionsCompleted
            FROM upd_pj pj
            INNER JOIN al_Coachees ac ON ac.ProgramJobId = pj.ProgramJobId
            WHERE ac.DeletedUtc IS NULL
            GROUP BY pj.ProgramJobId
          ),
          ws_stats
          AS
          (
            SELECT pj.ProgramJobId,
              COUNT(*) AS WorkshopsTotal,
              COUNT(IIF(we.EndDateUtc < SYSUTCDATETIME(), 1, NULL)) AS WorkshopsCompleted
            FROM upd_pj pj
            INNER JOIN ev_WorkshopEvent we ON we.ProgramJobId = pj.ProgramJobId
            WHERE we.WorkshopStatusId = 1 -- only Confirmed
            GROUP BY pj.ProgramJobId
          ),
          eval_stats
          AS
          (
            SELECT pj.ProgramJobId,

              --COUNT(sp.PartId) AS All_CompletedCount,
              --COUNT(DISTINCT IIF(sv.ClonedFromSvId IN (4407, 4798), sp.PartId, NULL)) AS Coaching_CompletedCount,
              --COUNT(DISTINCT IIF((sv.ClonedFromSvId IN (4796, 13752, 4793, 13753) OR sv.WorkshopEventId IS NOT NULL), sp.PartId, NULL)) AS Workshops_CompletedCount,
              COUNT(DISTINCT IIF(sv.ClonedFromSvId IN (4793, 13753, 4407), sp.PartId, NULL)) AS EndProgram_CompletedCount

            FROM upd_pj pj
            INNER JOIN sv_360_Participants sp ON sp.AbleProgramJobId = pj.ProgramJobId
            INNER JOIN al_Coachees ac ON ac.CoacheeId = sp.AbleCoacheeId
            INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
            WHERE sp.Completed IS NOT NULL
              AND ac.DeletedUtc IS NULL
            GROUP BY pj.ProgramJobId
          ),
          eval_scores
          AS
          (
            SELECT pj.ProgramJobId,

              SUM(sc.ValueScore) AS All_AnsSum,
              COUNT(sc.ValueScore) AS All_AnsCount,
              COUNT(DISTINCT sp.PartId) AS All_CompletedCount,

              SUM(IIF(sv.ClonedFromSvId IN (4407, 4798) AND rqh.RptQnGrpHeadingId IN (16, 17, 18, 20), sc.ValueScore, NULL)) AS Coaching_AnsSum,
              COUNT(IIF(sv.ClonedFromSvId IN (4407, 4798) AND rqh.RptQnGrpHeadingId IN (16, 17, 18, 20), sc.ValueScore, NULL)) AS Coaching_AnsCount,

              SUM(IIF((sv.ClonedFromSvId IN (4796, 13752, 4793, 13753) OR sv.WorkshopEventId IS NOT NULL) AND rqh.RptQnGrpHeadingId IN (16, 18, 52), sc.ValueScore, NULL)) AS Workshops_AnsSum,
              COUNT(IIF((sv.ClonedFromSvId IN (4796, 13752, 4793, 13753) OR sv.WorkshopEventId IS NOT NULL) AND rqh.RptQnGrpHeadingId IN (16, 18, 52), sc.ValueScore, NULL)) AS Workshops_AnsCount,

              SUM(IIF(sv.ClonedFromSvId IN (4793, 13753, 4407) AND rqh.RptQnGrpHeadingId IN (16, 17), sc.ValueScore, NULL)) AS EndProgram_AnsSum,
              COUNT(IIF(sv.ClonedFromSvId IN (4793, 13753, 4407) AND rqh.RptQnGrpHeadingId IN (16, 17), sc.ValueScore, NULL)) AS EndProgram_AnsCount

            FROM upd_pj pj
            INNER JOIN sv_360_Participants sp ON sp.AbleProgramJobId = pj.ProgramJobId
            INNER JOIN al_Coachees ac ON ac.CoacheeId = sp.AbleCoacheeId
            INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
            INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
            INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
            INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
            INNER JOIN al_RptQnGrpHgGblQns rqh ON rqh.GblQuestionId = sq.GblQuestionId
            WHERE sp.Completed IS NOT NULL
              AND ac.DeletedUtc IS NULL
              AND sq.GblAnswerTypeId = @EvalGblAnswerTypeId
            GROUP BY pj.ProgramJobId
          ),
          pp_stats
          AS
          (
            SELECT pj.ProgramJobId,
              MAX(IIF(sp.PrePost360ForCoachee = 1, sc.ScheduledStartDateUtc, NULL)) AS Pre_ScheduledUtc,
              MAX(IIF(sp.PrePost360ForCoachee = 1, DATEADD(HOUR, -8, sc.IntakeCloseDate), NULL)) AS Pre_ClosedUtc,
              --COUNT(IIF(sp.PrePost360ForCoachee = 1, sp.PartId, NULL)) AS Pre_PartCount,
              COUNT(IIF(sp.PrePost360ForCoachee = 1, sp.PartId, NULL)) AS Pre_CompletedCount,
              MAX(IIF(sp.PrePost360ForCoachee = 2, sc.ScheduledStartDateUtc, NULL)) AS Post_ScheduledUtc,
              MAX(IIF(sp.PrePost360ForCoachee = 2, DATEADD(HOUR, -8, sc.IntakeCloseDate), NULL)) AS Post_ClosedUtc,
              --COUNT(IIF(sp.PrePost360ForCoachee = 2, sp.PartId, NULL)) AS Post_PartCount,
              COUNT(IIF(sp.PrePost360ForCoachee = 2, sp.PartId, NULL)) AS Post_CompletedCount
            FROM upd_pj pj
            INNER JOIN sv_360_Participants sp ON sp.AbleProgramJobId = pj.ProgramJobId
            INNER JOIN sv_Survey sv ON sv.sv_id = sp.SurveyId
            INNER JOIN sv_360_Codes sc ON sc.CodeId = sp.IntakeCodeId
            WHERE sp.PrePost360ForCoachee > 0
              AND sp.IsSelf = 1
              AND sp.PrePost360ForCoachee > 0
            GROUP BY pj.ProgramJobId
          ),
          pp_scores
          AS
          (
            SELECT pj.ProgramJobId,
              SUM(IIF(sp.PrePost360ForCoachee = 1, sc.ValueScore, NULL)) AS Pre_AnsSum,
              COUNT(IIF(sp.PrePost360ForCoachee = 1, sc.ValueScore, NULL)) AS Pre_AnsCount,
              SUM(IIF(sp.PrePost360ForCoachee = 2, sc.ValueScore, NULL)) AS Post_AnsSum,
              COUNT(IIF(sp.PrePost360ForCoachee = 2, sc.ValueScore, NULL)) AS Post_AnsCount
            FROM upd_pj pj
            INNER JOIN sv_360_Participants sp ON sp.AbleProgramJobId = pj.ProgramJobId
            INNER JOIN al_Coachees ac ON ac.CoacheeId = sp.AbleCoacheeId
            INNER JOIN sv_Survey sv ON sp.SurveyId = sv.sv_id
            INNER JOIN sv_Answers sa ON sa.ParticipantId = sp.PartId
            INNER JOIN sv_360_Questions sq ON sq.QuestionId = sa.QuestionId
            INNER JOIN sv_360_Codes sc ON sc.CodeId = sa.CodeId
            WHERE sp.Completed IS NOT NULL
              AND ac.DeletedUtc IS NULL
              AND sp.PrePost360ForCoachee > 0
              AND sq.GblAnswerTypeId = sv.PrimaryGblAnswerTypeId
              AND sq.RptQnGroupId = 1
            GROUP BY pj.ProgramJobId
          ),
          com_all
          AS
          (
            SELECT pj.ProgramJobId,
              COUNT(*) AS AllCount,
              COUNT(IIF(com.CompletedDateUtc < SYSUTCDATETIME(), 1, NULL)) AS CompletedCount
            FROM upd_pj pj
            INNER JOIN al_Component com ON com.ProgramJobId = pj.ProgramJobId
            GROUP BY pj.ProgramJobId
          ),
          com_revenue
          AS
          (
            SELECT pj.ProgramJobId,
              COUNT(*) AS AllCount,
              SUM(com.ComponentPrice) AS AllSum,
              COUNT(IIF(com.CompletedDateUtc < SYSUTCDATETIME(), 1, NULL)) AS CompletedCount,
              SUM(IIF(com.CompletedDateUtc < SYSUTCDATETIME(), com.ComponentPrice, NULL)) AS CompletedSum
            FROM upd_pj pj
            INNER JOIN al_Component com ON com.ProgramJobId = pj.ProgramJobId
            WHERE com.ComponentPrice > 0
            GROUP BY pj.ProgramJobId
          ),
          com_cost
          AS
          (
            SELECT pj.ProgramJobId,
              --COUNT(*) AS AllCount,
              --COUNT(IIF(com.CompletedDateUtc < SYSUTCDATETIME(), 1, NULL)) AS CompletedCount,
              SUM(com.ComponentCost) AS AllSum,
              SUM(IIF(com.CompletedDateUtc < SYSUTCDATETIME(), com.ComponentCost, NULL)) AS CompletedSum,
              SUM(ISNULL(SIGN(COALESCE(com.CoacheeId, com.WorkshopEventId, com.ConsultingItemId)) * com.ComponentPrice, 0)) AS PriceSum_Delivery
              -- Individual if needed:
              -- SUM(IIF(com.CoacheeId IS NOT NULL, com.ComponentPrice, NULL)) AS PriceSum_Sess,
              -- SUM(IIF(com.WorkshopEventId IS NOT NULL, com.ComponentPrice, NULL)) AS PriceSum_Work,
              -- SUM(IIF(com.ConsultingItemId IS NOT NULL, com.ComponentPrice, NULL)) AS PriceSum_Cons
            FROM upd_pj pj
            INNER JOIN al_Component com ON com.ProgramJobId = pj.ProgramJobId
            GROUP BY pj.ProgramJobId
          )
          -- All results needed for program summary.
          UPDATE ps

          SET ps.LastUpdatedUtc = SYSUTCDATETIME()

            , ps.PaxTotal = ISNULL(pax_stats.PaxTotal, 0)
            , ps.PaxHasCoaching = ISNULL(pax_stats.HasCoaching, 0)
            , ps.PaxAssignedCoach = ISNULL(pax_stats.AssignedCoach, 0)
            , ps.PaxSessionsAllocated = ISNULL(pax_stats.SessionsAllocated, 0)
            , ps.PaxSessionsCompleted = ISNULL(pax_stats.SessionsCompleted, 0)

            , ps.WorkshopsTotal = ISNULL(ws_stats.WorkshopsTotal, 0)
            , ps.WorkshopsCompleted = ISNULL(ws_stats.WorkshopsCompleted, 0)

            , ps.Eval_All_AnsSum = ISNULL(eval_scores.All_AnsSum, 0)
            , ps.Eval_All_AnsCount = ISNULL(eval_scores.All_AnsCount, 0)
            , ps.Eval_Workshops_AnsSum = ISNULL(eval_scores.Workshops_AnsSum, 0)
            , ps.Eval_Workshops_AnsCount = ISNULL(eval_scores.Workshops_AnsCount, 0)
            , ps.Eval_Coaching_AnsSum = ISNULL(eval_scores.Coaching_AnsSum, 0)
            , ps.Eval_Coaching_AnsCount = ISNULL(eval_scores.Coaching_AnsCount, 0)
            , ps.Eval_EndProgram_AnsSum = ISNULL(eval_scores.EndProgram_AnsSum, 0)
            , ps.Eval_EndProgram_AnsCount = ISNULL(eval_scores.EndProgram_AnsCount, 0)
            , ps.Eval_EndProgram_CompletedCount = ISNULL(eval_stats.EndProgram_CompletedCount, 0)

            , ps.PrePost360_Pre_ScheduledUtc = pp_stats.Pre_ScheduledUtc
            , ps.PrePost360_Pre_ClosedUtc = pp_stats.Pre_ClosedUtc
            , ps.PrePost360_Pre_CompletedCount = ISNULL(pp_stats.Pre_CompletedCount, 0)
            , ps.PrePost360_Post_ScheduledUtc = pp_stats.Post_ScheduledUtc
            , ps.PrePost360_Post_ClosedUtc = pp_stats.Post_ClosedUtc
            , ps.PrePost360_Post_CompletedCount = ISNULL(pp_stats.Post_CompletedCount, 0)

            , ps.PrePost360_Pre_AnsSum = ISNULL(pp_scores.Pre_AnsSum, 0)
            , ps.PrePost360_Pre_AnsCount = ISNULL(pp_scores.Pre_AnsCount, 0)
            , ps.PrePost360_Post_AnsSum = ISNULL(pp_scores.Post_AnsSum, 0)
            , ps.PrePost360_Post_AnsCount = ISNULL(pp_scores.Post_AnsCount, 0)

            , ps.ComponentsCount = ISNULL(com_all.AllCount, 0)
            , ps.ComponentsCompleted = ISNULL(com_all.CompletedCount, 0)

            , ps.RevenueTotal = ISNULL(com_revenue.AllSum, 0)
            , ps.RevenueCompleted = ISNULL(com_revenue.CompletedSum, 0)

            , ps.CostTotal = ISNULL(com_cost.AllSum, 0)
                           + ISNULL(com_cost.PriceSum_Delivery * ij.Partner_DeliveryPercentage, 0)
                           + ISNULL(com_cost.PriceSum_Delivery * ij.Partner_SalesDeliveryPercentage, 0)
                           + ISNULL(com_cost.PriceSum_Delivery * ij.Partner_PLCPercentage, 0)

            , ps.ProfitTotal = ps.RevenueTotal - ps.CostTotal

          FROM al_ProgramSummary ps
          INNER JOIN upd_pj pj ON pj.ProgramJobId = ps.ProgramJobId
          INNER JOIN id_Job ij ON ij.JobId = ps.ProgramJobId
          LEFT OUTER JOIN pax_stats ON pax_stats.ProgramJobId = pj.ProgramJobId
          LEFT OUTER JOIN ws_stats ON ws_stats.ProgramJobId = pj.ProgramJobId
          LEFT OUTER JOIN com_all ON com_all.ProgramJobId = pj.ProgramJobId
          LEFT OUTER JOIN com_revenue ON com_revenue.ProgramJobId = pj.ProgramJobId
          LEFT OUTER JOIN com_cost ON com_cost.ProgramJobId = pj.ProgramJobId
          LEFT OUTER JOIN eval_stats ON eval_stats.ProgramJobId = pj.ProgramJobId
          LEFT OUTER JOIN eval_scores ON eval_scores.ProgramJobId = pj.ProgramJobId
          LEFT OUTER JOIN pp_stats ON pp_stats.ProgramJobId = pj.ProgramJobId
          LEFT OUTER JOIN pp_scores ON pp_scores.ProgramJobId = pj.ProgramJobId;

          SELECT @@ROWCOUNT;";

        return GetScalarQueryInt(sql,
          NewSqlParameter("EvalGblAnswerTypeId", ConfigHelper.GblAnsTypeId_Eval),
          NewSqlParameter("WorkshopStatusId_Confirmed", WorkshopStatus.Ids.Confirmed),
          NewSqlParameter("ProgramJobId", programIdOrNullForAll),
          NewSqlParameter("NoDateCheck", programIdOrNullForAll != null) // Force update for program whether due or not.
        );
      }

      public class SummaryData {

        public int ProgramJobId { get; set; }
        public DateTime LastUpdatedUtc { get; set; }

        public int PaxTotal { get; private set; }
        public int PaxHasCoaching { get; private set; }
        public int PaxAssignedCoach { get; private set; }
        public int PaxSessionsAllocated { get; private set; }
        public int PaxSessionsCompleted { get; private set; }

        public int WorkshopsTotal { get; private set; }
        public int WorkshopsCompleted { get; private set; }

        public int Eval_All_AnsSum { get; private set; }
        public int Eval_All_AnsCount { get; private set; }
        public decimal? Eval_All_AnsAvg { get; private set; }

        public int Eval_Workshops_AnsSum { get; private set; }
        public int Eval_Workshops_AnsCount { get; private set; }
        public decimal? Eval_Workshops_AnsAvg { get; private set; }

        public int Eval_Coaching_AnsSum { get; private set; }
        public int Eval_Coaching_AnsCount { get; private set; }
        public decimal? Eval_Coaching_AnsAvg { get; private set; }

        public int Eval_EndProgram_AnsSum { get; private set; }
        public int Eval_EndProgram_AnsCount { get; private set; }
        public int Eval_EndProgram_CompletedCount { get; private set; }
        public decimal? Eval_EndProgram_AnsAvg { get; private set; }

        public DateTime? PrePost360_Pre_ScheduledUtc { get; private set; }
        public DateTime? PrePost360_Pre_ClosedUtc { get; private set; }
        public int PrePost360_Pre_AnsSum { get; private set; }
        public int PrePost360_Pre_AnsCount { get; private set; }
        public int PrePost360_Pre_CompletedCount { get; private set; }
        public decimal? PrePost360_Pre_AnsAvg { get; private set; }

        public DateTime? PrePost360_Post_ScheduledUtc { get; private set; }
        public DateTime? PrePost360_Post_ClosedUtc { get; private set; }
        public int PrePost360_Post_AnsSum { get; private set; }
        public int PrePost360_Post_AnsCount { get; private set; }
        public int PrePost360_Post_CompletedCount { get; private set; }
        public decimal? PrePost360_Post_AnsAvg { get; private set; }

        public decimal? PrePost360_Delta =>
          PrePost360_Pre_AnsAvg == null || PrePost360_Post_AnsAvg == null
          ? null
          : (PrePost360_Post_AnsAvg - PrePost360_Pre_AnsAvg);

        public int ComponentsCount { get; private set; }
        public int ComponentsCompleted { get; private set; }

        public decimal RevenueTotal { get; private set; }
        public decimal RevenueCompleted { get; private set; }
        public decimal CostTotal { get; private set; }
        public decimal ProfitTotal { get; private set; }

        public SummaryData() { }

        public SummaryData(SqlDataReader dr) {

          if (dr.IsDBNull("ProgramJobId")) return;

          ProgramJobId = dr.GetInt("ProgramJobId");
          LastUpdatedUtc = dr.GetDateTime("LastUpdatedUtc");

          PaxTotal = dr.GetIntOrNull("PaxTotal") ?? 0;
          PaxHasCoaching = dr.GetIntOrNull("PaxHasCoaching") ?? 0;
          PaxAssignedCoach = dr.GetIntOrNull("PaxAssignedCoach") ?? 0;
          PaxSessionsAllocated = dr.GetIntOrNull("PaxSessionsAllocated") ?? 0;
          PaxSessionsCompleted = dr.GetIntOrNull("PaxSessionsCompleted") ?? 0;

          WorkshopsTotal = dr.GetIntOrNull("WorkshopsTotal") ?? 0;
          WorkshopsCompleted = dr.GetIntOrNull("WorkshopsCompleted") ?? 0;

          Eval_All_AnsSum = dr.GetIntOrNull("Eval_All_AnsSum") ?? 0;
          Eval_All_AnsCount = dr.GetIntOrNull("Eval_All_AnsCount") ?? 0;
          Eval_All_AnsAvg = dr.GetDecimalOrNull("Eval_All_AnsAvg") ?? 0;

          Eval_Workshops_AnsSum = dr.GetIntOrNull("Eval_Workshops_AnsSum") ?? 0;
          Eval_Workshops_AnsCount = dr.GetIntOrNull("Eval_Workshops_AnsCount") ?? 0;
          Eval_Workshops_AnsAvg = dr.GetDecimalOrNull("Eval_Workshops_AnsAvg") ?? 0;

          Eval_Coaching_AnsSum = dr.GetIntOrNull("Eval_Coaching_AnsSum") ?? 0;
          Eval_Coaching_AnsCount = dr.GetIntOrNull("Eval_Coaching_AnsCount") ?? 0;
          Eval_Coaching_AnsAvg = dr.GetDecimalOrNull("Eval_Coaching_AnsAvg") ?? 0;

          Eval_EndProgram_AnsSum = dr.GetIntOrNull("Eval_EndProgram_AnsSum") ?? 0;
          Eval_EndProgram_AnsCount = dr.GetIntOrNull("Eval_EndProgram_AnsCount") ?? 0;
          Eval_EndProgram_CompletedCount = dr.GetIntOrNull("Eval_EndProgram_CompletedCount") ?? 0;
          Eval_EndProgram_AnsAvg = dr.GetDecimalOrNull("Eval_EndProgram_AnsAvg");

          PrePost360_Pre_ScheduledUtc = dr.GetDateTimeOrNull("PrePost360_Pre_ScheduledUtc");
          PrePost360_Pre_ClosedUtc = dr.GetDateTimeOrNull("PrePost360_Pre_ClosedUtc");
          PrePost360_Pre_AnsSum = dr.GetIntOrNull("PrePost360_Pre_AnsSum") ?? 0;
          PrePost360_Pre_AnsCount = dr.GetIntOrNull("PrePost360_Pre_AnsCount") ?? 0;
          PrePost360_Pre_CompletedCount = dr.GetIntOrNull("PrePost360_Pre_CompletedCount") ?? 0;
          PrePost360_Pre_AnsAvg = dr.GetDecimalOrNull("PrePost360_Pre_AnsAvg") ?? 0;

          PrePost360_Post_ScheduledUtc = dr.GetDateTimeOrNull("PrePost360_Post_ScheduledUtc");
          PrePost360_Post_ClosedUtc = dr.GetDateTimeOrNull("PrePost360_Post_ClosedUtc");
          PrePost360_Post_AnsSum = dr.GetIntOrNull("PrePost360_Post_AnsSum") ?? 0;
          PrePost360_Post_AnsCount = dr.GetIntOrNull("PrePost360_Post_AnsCount") ?? 0;
          PrePost360_Post_CompletedCount = dr.GetIntOrNull("PrePost360_Post_CompletedCount") ?? 0;
          PrePost360_Post_AnsAvg = dr.GetDecimalOrNull("PrePost360_Post_AnsAvg");

          ComponentsCount = dr.GetIntOrNull("ComponentsCount") ?? 0;
          ComponentsCompleted = dr.GetIntOrNull("ComponentsCompleted") ?? 0;

          RevenueTotal = dr.GetDecimalOrNull("RevenueTotal") ?? 0;
          RevenueCompleted = dr.GetDecimalOrNull("RevenueCompleted") ?? 0;
          CostTotal = dr.GetDecimalOrNull("CostTotal") ?? 0;
          ProfitTotal = dr.GetDecimalOrNull("ProfitTotal") ?? 0;
        }

        public void Add(SummaryData addData) {

          PaxTotal += addData.PaxTotal;
          PaxHasCoaching += addData.PaxHasCoaching;
          PaxAssignedCoach += addData.PaxAssignedCoach;
          PaxSessionsAllocated += addData.PaxSessionsAllocated;
          PaxSessionsCompleted += addData.PaxSessionsCompleted;

          WorkshopsTotal += addData.WorkshopsTotal;
          WorkshopsCompleted += addData.WorkshopsCompleted;

          Eval_All_AnsSum += addData.Eval_All_AnsSum;
          Eval_All_AnsCount += addData.Eval_All_AnsCount;
          Eval_All_AnsAvg = GetAvg(Eval_All_AnsSum, Eval_All_AnsCount);

          Eval_Workshops_AnsSum += addData.Eval_Workshops_AnsSum;
          Eval_Workshops_AnsCount += addData.Eval_Workshops_AnsCount;
          Eval_Workshops_AnsAvg = GetAvg(Eval_Workshops_AnsSum, Eval_Workshops_AnsCount);

          Eval_Coaching_AnsSum += addData.Eval_Coaching_AnsSum;
          Eval_Coaching_AnsCount += addData.Eval_Coaching_AnsCount;
          Eval_Coaching_AnsAvg = GetAvg(Eval_Coaching_AnsSum, Eval_Coaching_AnsCount);

          Eval_EndProgram_AnsSum += addData.Eval_EndProgram_AnsSum;
          Eval_EndProgram_AnsCount += addData.Eval_EndProgram_AnsCount;
          Eval_EndProgram_CompletedCount += addData.Eval_EndProgram_CompletedCount;
          Eval_EndProgram_AnsAvg = GetAvg(Eval_EndProgram_AnsSum, Eval_EndProgram_AnsCount);

          PrePost360_Pre_ScheduledUtc = Min(PrePost360_Pre_ScheduledUtc, addData.PrePost360_Pre_ScheduledUtc);
          PrePost360_Pre_ClosedUtc = Max(PrePost360_Pre_ClosedUtc, addData.PrePost360_Pre_ClosedUtc);
          PrePost360_Pre_AnsSum += addData.PrePost360_Pre_AnsSum;
          PrePost360_Pre_AnsCount += addData.PrePost360_Pre_AnsCount;
          PrePost360_Pre_CompletedCount += addData.PrePost360_Pre_CompletedCount;
          PrePost360_Pre_AnsAvg = GetAvg(PrePost360_Pre_AnsSum, PrePost360_Pre_AnsCount);

          PrePost360_Post_ScheduledUtc = Min(PrePost360_Post_ScheduledUtc, addData.PrePost360_Post_ScheduledUtc);
          PrePost360_Post_ClosedUtc = Max(PrePost360_Post_ClosedUtc, addData.PrePost360_Post_ClosedUtc);
          PrePost360_Post_AnsSum += addData.PrePost360_Post_AnsSum;
          PrePost360_Post_AnsCount += addData.PrePost360_Post_AnsCount;
          PrePost360_Post_CompletedCount += addData.PrePost360_Post_CompletedCount;
          PrePost360_Post_AnsAvg = GetAvg(PrePost360_Post_AnsSum, PrePost360_Post_AnsCount);

          ComponentsCount += addData.ComponentsCount;
          ComponentsCompleted += addData.ComponentsCompleted;

          RevenueTotal += addData.RevenueTotal;
          RevenueCompleted += addData.RevenueCompleted;
          CostTotal += addData.CostTotal;
          ProfitTotal += addData.ProfitTotal;
        }

        // Simple functions to add/compare values when accumulating totals for the parent.

        private decimal? GetAvg(int? EvalAllAnsSum, int? EvalAllAnsCount) {
          if (EvalAllAnsSum.GetValueOrDefault(0) <= 0 || EvalAllAnsCount.GetValueOrDefault(0) <= 0) return null;
          return (decimal)EvalAllAnsSum / EvalAllAnsCount;
        }

        private int? Add(int? n1, int? n2) {
          if (n1 == null && n2 == null) return null;
          if (n1 == null) return n2;
          if (n2 == null) return n1;
          return n1 + n2;
        }

        private int Add(int n1, int n2) {
          return n1 + n2;
        }

        private decimal? Add(decimal? n1, decimal? n2) {
          if (n1 == null && n2 == null) return null;
          if (n1 == null) return n2;
          if (n2 == null) return n1;
          return n1 + n2;
        }

        private decimal Add(decimal n1, decimal n2) {
          return n1 + n2;
        }

        private DateTime? Min(DateTime? date1, DateTime? date2) {
          if (date1 == null && date2 == null) return null;
          if (date1 == null) return date2;
          if (date2 == null) return date1;
          return date1 < date2 ? date1 : date2;
        }

        private DateTime? Max(DateTime? date1, DateTime? date2) {
          if (date1 == null && date2 == null) return null;
          if (date1 == null) return date2;
          if (date2 == null) return date1;
          return date1 > date2 ? date1 : date2;
        }

      }
    }
  }
}
