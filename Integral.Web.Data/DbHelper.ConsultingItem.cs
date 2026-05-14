using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class ConsultingItems {

      // In most of these methods, caller must ensure access to the ProgramId is valid for purpose.

      public static ConsultingItemInfo GetConsultingItemInfo(int consultingItemId) {
        return GetConsultingItemInfo(
          "ci.ConsultingItemId = @ConsultingItemId",
          Common.NewSqlParameter("@ConsultingItemId", consultingItemId)
        );
      }

      // Get item by id, ensuring it is part of intended program.
      public static ConsultingItemInfo GetConsultingItemInfo(int programJobId, int consultingItemId) {
        return GetConsultingItemInfo(
          "ci.ProgramJobId = @ProgramJobId AND ci.ConsultingItemId = @ConsultingItemId",
          Common.NewSqlParameter("@ProgramJobId", programJobId),
          Common.NewSqlParameter("@ConsultingItemId", consultingItemId)
        );
      }

      // Return items in program.
      public static List<ConsultingItemInfo> GetItemsInProgram(int programJobId, int? forUserOnlyOrNullForAll = null) {
        return GetConsultingItemInfoList(null, "",
          @"ci.ProgramJobId = @ProgramJobId
            AND (@ConsultantUserId IS NULL OR ci.ConsultantUserId = @ConsultantUserId)",
          "ISNULL(ci.CompletionDateUtc, '2100-01-01')", // blank dates at bottom of list.
          Common.NewSqlParameter("@ProgramJobId", programJobId),
          Common.NewSqlParameter("@ConsultantUserId", forUserOnlyOrNullForAll)
        );
      }

      private static ConsultingItemInfo GetConsultingItemInfo(string sqlWhereConditions, params SqlParameter[] sqlWhereParams) {
        var ConsultingItemList = GetConsultingItemInfoList(1, "", sqlWhereConditions, "", sqlWhereParams);
        if (ConsultingItemList.Count == 0) return null;
        else return ConsultingItemList[0];
      }

      public static List<ConsultingItemInfo> GetUpcomingConsultingForCoach(int coachUserId, DateTime earliestItemDateUtc) {
        return GetConsultingItemInfoList(null, "",
          @"ci.ConsultantUserId = @CoachUserId
            AND (ci.CompletionDateUtc IS NULL OR ci.CompletionDateUtc >= @EarliestItemDateUtc)
            AND NOT EXISTS(SELECT NULL FROM id_PayRunItems pri WHERE pri.ConsultingItemId = ci.ConsultingItemId)",
          "ISNULL(ci.CompletionDateUtc, '2100-01-01')", // blank dates at bottom of list.
          Common.NewSqlParameter("CoachUserId", coachUserId),
          Common.NewSqlParameter("EarliestItemDateUtc", earliestItemDateUtc));
      }

      private static List<ConsultingItemInfo> GetConsultingItemInfoList(
        int? topOrNullForAll,
        string sqlExtraJoins,
        string sqlWhereConditions,
        string sqlOrderBy,
        params SqlParameter[] sqlWhereParams) {

        var ConsultingItemList = new List<ConsultingItemInfo>();

        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);
        string sql = $@"
          SELECT {sqlTop}
            ci.ConsultingItemId, ci.ProgramJobId, ci.ConsultantUserId, ci.CreatedUtc, ci.ItemTitle, ci.Description,
            ci.ItemAmount, ci.CompletionDateUtc, ci.ToBeBilled, ci.ConsultingTypeId,
            j.JobNumber, j.JobName, j.CompanyId, sc.CompanyName, prj.ProjectName,

                                              j.Partner_DeliveryPercentage,
            j.Partner_UserId AS SalesUserId,  j.Partner_SalesDeliveryPercentage,
            j.LeadConsultantUserId,           j.Partner_PLCPercentage,

            u.FirstName AS ConsultantFirstName, u.LastName AS ConsultantLastName, u.Email AS ConsultantEmail,
            {DbHelper.ProgramComponents.GetComponentQuoteCrossApply_SelectItems}
          FROM al_ConsultingItems ci
          INNER JOIN id_Job j ON j.JobId = ci.ProgramJobId
          LEFT OUTER JOIN al_Project prj ON prj.JobNumber = j.JobNumber
          LEFT OUTER JOIN sv_SurveyCompany sc ON sc.SvCompanyId = j.CompanyId
          LEFT OUTER JOIN sv_User u ON u.UserId = ci.ConsultantUserId
          {DbHelper.ProgramComponents.GetComponentQuoteCrossApply("apc.ConsultingItemId = ci.ConsultingItemId")}
          {sqlExtraJoins.EmptyIfNull()}
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        Common.Query(sql,
          dr => {
            ConsultingItemList.Add(new ConsultingItemInfo(
              dr.GetInt("ConsultingItemId"),
              dr.GetDateTime("CreatedUtc"),
              dr.GetString("ItemTitle"),
              dr.GetString("Description"),
              dr.GetIntOrNull("ConsultingTypeId"),
              dr.GetDecimal("ItemAmount"),
              dr.GetBoolFromInt("ToBeBilled"),
              dr.GetDateTimeOrNull("CompletionDateUtc"),
              dr.GetInt("ProgramJobId"),
              dr.GetString("JobNumber"),
              dr.GetString("JobName"),
              dr.GetString("ProjectName"),
              dr.GetIntOrNull("CompanyId"),
              dr.GetString("CompanyName"),
              dr.GetIntOrNull("ConsultantUserId"),
              dr.GetString("ConsultantFirstName"),
              dr.GetString("ConsultantLastName"),
              dr.GetString("ConsultantEmail"),
              dr.GetDecimalOrNull("Partner_DeliveryPercentage"),
              dr.GetIntOrNull("SalesUserId"),
              dr.GetDecimalOrNull("Partner_SalesDeliveryPercentage"),
              dr.GetIntOrNull("LeadConsultantUserId"),
              dr.GetDecimalOrNull("Partner_PLCPercentage"),
              new DbHelper.ProgramComponents.ComponentQuoteInfo(dr)
            ));
          },
          sqlWhereParams
        );

        return ConsultingItemList;
      }

      public class NewConsultingItemInfo {
        public int ConsultingItemId { get; internal set; }
      }

      // Add consulting item row.
      // The new ConsultingItemId si returned in the given ConsultingItemInfo object.
      public static void AddConsultingItem(ConsultingItemInfo ci) {

        // Use transaction to wrap item and component updates.
        Common.UsingTransaction(trans => {

          ci.ConsultingItemId = Common.GetScalarQueryInt(trans, @"
            INSERT INTO al_ConsultingItems (ProgramJobId, ConsultantUserId, ItemTitle, Description, ConsultingTypeId, ItemAmount, ToBeBilled, CompletionDateUtc)
            OUTPUT INSERTED.ConsultingItemId
            VALUES (@ProgramJobId, @ConsultantUserId, @ItemTitle, @Description, @ConsultingTypeId, @ItemAmount, @ToBeBilled, @CompletionDateUtc)",
            Common.NewSqlParameter("@ProgramJobId", ci.ProgramJobId),
            Common.NewSqlParameter("@ConsultantUserId", ci.ConsultantUserId),
            Common.NewSqlParameter("@ItemTitle", ci.ItemTitle, 200),
            Common.NewSqlParameter("@Description", ci.Description, 4000),
            Common.NewSqlParameter("@ConsultingTypeId", ci.ConsultingTypeId),
            Common.NewSqlParameter("@ItemAmount", ci.ItemAmount),
            Common.NewSqlParameter("@ToBeBilled", ci.ToBeBilled),
            Common.NewSqlParameter("@CompletionDateUtc", ci.CompletionDateUtc));

          ProgramComponents.UpsertConsultingItem(trans,
            ci.ConsultingItemId, ci.ProgramJobId,
            ci.ConsultantUserId, ci.ItemAmount, ci.CompletionDateUtc, ci.ComponentQuoteInfo.QuoteItemId);

          return true; // Commit transaction.
        });
      }

      public static bool UpdateConsultingItem(ConsultingItemInfo ci, bool reload = false) {

        if (ci == null) throw new ArgumentException("ci is null.");

        int updatedId = Common.GetScalarQueryInt(@"
          UPDATE al_ConsultingItems
          SET ItemTitle = @ItemTitle,
              Description = @Description,
              ConsultingTypeId = @ConsultingTypeId,
              ItemAmount = @ItemAmount,
              ToBeBilled = @ToBeBilled,
              ConsultantUserId = @ConsultantUserId,
              CompletionDateUtc = @CompletionDateUtc,
              LastUpdatedUtc = SYSUTCDATETIME()
          OUTPUT INSERTED.ConsultingItemId
          WHERE ProgramJobId = @ProgramJobId
            AND ConsultingItemId = @ConsultingItemId",
          Common.NewSqlParameter("@ProgramJobId", ci.ProgramJobId),
          Common.NewSqlParameter("@ConsultingItemId", ci.ConsultingItemId),
          Common.NewSqlParameter("@ItemTitle", ci.ItemTitle, 200),
          Common.NewSqlParameter("@Description", ci.Description, 4000),
          Common.NewSqlParameter("@ConsultingTypeId", ci.ConsultingTypeId),
          Common.NewSqlParameter("@ItemAmount", ci.ItemAmount),
          Common.NewSqlParameter("@ToBeBilled", ci.ToBeBilled),
          Common.NewSqlParameter("@ConsultantUserId", ci.ConsultantUserId),
          Common.NewSqlParameter("@CompletionDateUtc", ci.CompletionDateUtc));
        if (updatedId != ci.ConsultingItemId) return false;
        ConsultingItemInfo ciReloaded;
        if (reload) {
          ciReloaded = GetConsultingItemInfo(ci.ProgramJobId, ci.ConsultingItemId);
          if (ciReloaded == null) return false;
          ci = ciReloaded;
        }
        ProgramComponents.UpsertConsultingItem(
          null, ci.ConsultingItemId, ci.ProgramJobId, ci.ConsultantUserId,
          ci.ItemAmount, ci.CompletionDateUtc, ci.ComponentQuoteInfo.QuoteItemId);
        return true;
      }

      public static void DeleteConsultingItem(SqlTransaction trans, int consultingItemId) {
        // Only delete if component is not locked.

        Common.UsingTransaction(trans, _ => {

          ProgramComponents.DeleteConsulting(trans, consultingItemId); // Deletes only if not locked.

          // Delete if component was deleted.
          Common.GetNonQueryInt(trans, $@"

            IF NOT EXISTS (SELECT 1 FROM al_Component cmp WHERE cmp.ConsultingItemId = @ConsultingItemId)
            BEGIN

              UPDATE ij
                SET ij.LastUpdatedUtc = SYSUTCDATETIME()
              FROM id_Job ij
              INNER JOIN al_ConsultingItems ci ON ci.ProgramJobId = ij.JobId
              WHERE ci.ConsultingItemId = @ConsultingItemId;

              DELETE FROM al_ConsultingItems WHERE ConsultingItemId = @ConsultingItemId;

            END;",

            Common.NewSqlParameter("@ConsultingItemId", consultingItemId)
          );

          return true;
        });
      }

      public static bool MoveItemToProgram(int consultingItemId, int toProgramJobId) {

        var fromConsultingItem = GetConsultingItemInfo(consultingItemId);
        if (fromConsultingItem == null) return false;

        int fromProgramJobId = fromConsultingItem.ProgramJobId;

        return Common.GetScalarQueryInt(null, $@"

          BEGIN TRANSACTION;

          -- Only update if no locked components (or no components at all) exist.
          IF NOT EXISTS (SELECT 1 FROM al_Component cmp WHERE ConsultingItemId = @ConsultingItemId AND LockedDateUtc IS NOT NULL)
          AND EXISTS(SELECT 1 FROM id_Job WHERE JobId = @ToProgramJobId)
          BEGIN

            UPDATE al_Component
            SET ProgramJobId = @ToProgramJobId
            WHERE ConsultingItemId = @ConsultingItemId;

            UPDATE al_ConsultingItems
            SET ProgramJobId = @ToProgramJobId,
                LastUpdatedUtc = SYSUTCDATETIME()
            WHERE ConsultingItemId = @ConsultingItemId;

            UPDATE id_Job
              SET LastUpdatedUtc = SYSUTCDATETIME()
            WHERE JobId = @FromProgramJobId
               OR JobId = @ToProgramJobId;

          END;

          SELECT @@ROWCOUNT;

          COMMIT TRANSACTION;",

          Common.NewSqlParameter("ConsultingItemId", consultingItemId),
          Common.NewSqlParameter("FromProgramJobId", fromProgramJobId),
          Common.NewSqlParameter("ToProgramJobId", toProgramJobId)

        ) > 0;
      }

      public class ConsultingItemInfo {

        public int ConsultingItemId { get; internal set; }
        public DateTime? CreatedUtc { get; private set; }
        public string ItemTitle { get; set; }
        public string Description { get; set; }
        public int? ConsultingTypeId { get; set; }
        public decimal ItemAmount { get; set; }
        public bool ToBeBilled { get; set; }
        public DateTime? CompletionDateUtc { get; set; }
        public int ProgramJobId { get; set; }
        public string JobNumber { get; private set; }
        public string JobName { get; private set; }
        public string ProjectName { get; private set; }
        public int? CompanyId { get; set; }
        public string CompanyName { get; private set; }
        public int? ConsultantUserId { get; set; }
        public string ConsultantFirstName { get; private set; }
        public string ConsultantLastName { get; private set; }
        public string ConsultantEmail { get; private set; }
        public decimal? ProgramDeliveryPercentage { get; internal set; }
        public int? ProgramSalesUserId { get; internal set; }
        public decimal? ProgramSalesPercentage { get; internal set; }
        public int? ProgramPLCUserId { get; internal set; }
        public decimal? ProgramPLCPercentage { get; internal set; }
        public DbHelper.ProgramComponents.ComponentQuoteInfo ComponentQuoteInfo { get; set; }

        public ConsultingItemInfo() {
          ComponentQuoteInfo = new DbHelper.ProgramComponents.ComponentQuoteInfo();
        }

        public ConsultingItemInfo(
          int consultingItemId,
          DateTime? createdUtc,
          string itemTitle,
          string description,
          int? consultingTypeId,
          decimal itemAmount,
          bool toBeBilled,
          DateTime? completionDateUtc,
          int programJobId,
          string jobNumber,
          string jobName,
          string projectName,
          int? companyId,
          string companyName,
          int? consultantUserId,
          string consultantFirstName,
          string consultantLastName,
          string consultantEmail,
          decimal? programDeliveryPercentage,
          int? programSalesUserId, decimal? programSalesPercentage,
          int? programPLCUserId, decimal? programPLCPercentage,
          DbHelper.ProgramComponents.ComponentQuoteInfo componentQuoteInfo
        ) {
          this.ConsultingItemId = consultingItemId;
          this.CreatedUtc = createdUtc;
          this.ItemTitle = itemTitle;
          this.Description = description;
          this.ConsultingTypeId = consultingTypeId;
          this.ItemAmount = itemAmount;
          this.ToBeBilled = toBeBilled;
          this.CompletionDateUtc = completionDateUtc;
          this.ProgramJobId = programJobId;
          this.JobName = jobName;
          this.ProjectName = projectName;
          this.JobNumber = jobNumber;
          this.CompanyId = companyId;
          this.CompanyName = companyName;
          this.ConsultantUserId = consultantUserId;
          this.ConsultantFirstName = consultantFirstName;
          this.ConsultantLastName = consultantLastName;
          this.ConsultantEmail = consultantEmail;
          this.ProgramDeliveryPercentage = programDeliveryPercentage;
          this.ProgramSalesUserId = programSalesUserId;
          this.ProgramSalesPercentage = programSalesPercentage;
          this.ProgramPLCUserId = programPLCUserId;
          this.ProgramPLCPercentage = programPLCPercentage;
          this.ComponentQuoteInfo = componentQuoteInfo != null ? componentQuoteInfo : new DbHelper.ProgramComponents.ComponentQuoteInfo();
        }
        public string ConsultantFullName => $"{ConsultantFirstName} {ConsultantLastName}".Trim();
      }

    }
  }
}

