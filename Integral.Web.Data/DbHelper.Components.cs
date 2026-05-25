using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Integral.Web.Services;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class ProgramComponents {

      private const string TblPfx = "prc";

      public enum KeyColumnEnum {
        WorkshopEventId = 1,
        ConsultingItemId = 2,
        ProgramCostItemId = 3,
        CoachingSessionId = 4,
        CoacheeId = 5,
        UserSubscriptionId = 6
      }

      public enum ComponentTypeEnum { CoachingSession, Workshop, ConsultingItem, CostItem }

      public static string GetComponentQuoteCrossApply_SelectItems => @" apc.QuoteItemId, apc.LockedDateUtc, apc.QuotePublicGuid, apc.QuoteItemDescription ";
      public static string GetComponentQuoteCrossApply(string whereClause) {
        // The target is to easily get Component and Quote info for given items. (i.e. Coaching session, workshop, consulting item, cost item)
        return $@"
          CROSS APPLY (
            SELECT apc.QuoteItemId, apc.LockedDateUtc, q.QuotePublicGuid, qi.ItemDescription as QuoteItemDescription
            FROM al_Component apc
            LEFT OUTER JOIN al_QuoteItem qi ON qi.QuoteItemId = apc.QuoteItemId
            LEFT OUTER JOIN al_Quote q ON q.QuoteId = qi.QuoteId
            WHERE {whereClause}
          ) AS apc";
      }

      public static ComponentTypeEnum GetComponentTypeFromIds(
        int? CoachingSessionId,
        int? WorkshopEventId,
        int? ConsultingItemId,
        int? ProgramCostItemId) {

        if (CoachingSessionId != null) return ComponentTypeEnum.CoachingSession;
        if (WorkshopEventId != null) return ComponentTypeEnum.Workshop;
        if (ConsultingItemId != null) return ComponentTypeEnum.ConsultingItem;
        if (ProgramCostItemId != null) return ComponentTypeEnum.CostItem;

        throw new ApplicationException("Can't determine component type.");
      }

      // Every row links to the component types Workshop, Consulting Item, Cost Item and Coaching Session.
      // Only one of those values should be non-null per row (i.e. each row links to just one of those).
      // For all rows except those linking to Cost Item, PartnerUserId is non-null.
      // Every row links the component to its ProgramJobId.
      // When components are add, edited & deleted (workshops, consulting items, etc) the related ProgramComponent must be maintained.
      // When LockedDateUtc is not null, that component is "locked" and cannot be edited or deleted.

      public static ComponentInfo GetComponentInfoOrNull(int componentId) {
        var cmp = GetComponentListPaged(1, "",
          $"{TblPfx}.ComponentId = @ComponentId", "", null, null, null,
          Common.NewSqlParameter("ComponentId", componentId));
        if (cmp.InfoList.Count == 0) return null;
        return cmp.InfoList[0];
      }

      public static List<ComponentInfo> GetForCoachee(string coacheeEmail, int programJobId) {
        var cmp = GetComponentListPaged(null, "",
          $"ac.EmailAddress = @CoacheeEmail AND ac.ProgramJobId = @ProgramJobId",
          null, null, null, null,
          Common.NewSqlParameter("CoacheeEmail", coacheeEmail),
          Common.NewSqlParameter("ProgramJobId", programJobId));
        return cmp.InfoList;
      }

      public static List<ComponentInfo> GetForCoachee(int coacheeId) {
        var cmp = GetComponentListPaged(null, "",
          $"{TblPfx}.CoacheeId = @CoacheeId",
          $"{TblPfx}.SessionNumber", null, null, null,
          Common.NewSqlParameter("CoacheeId", coacheeId));
        return cmp.InfoList;
      }

      public static List<ComponentInfo> GetForProgram(int programJobId) {
        var cmp = GetComponentListPaged(null, "",
          $"{TblPfx}.ProgramJobId = @ProgramJobId", "", null, null, null,
          Common.NewSqlParameter("ProgramJobId", programJobId));
        return cmp.InfoList;
      }

      public static List<ComponentInfo> GetForQuoteItem(int quoteItemId) {
        var cmp = GetComponentListPaged(null, "",
          $"{TblPfx}.QuoteItemId = @QuoteItemId", "", null, null, null,
          Common.NewSqlParameter("QuoteItemId", quoteItemId));
        return cmp.InfoList;
      }

      public static List<ComponentInfo> GetForQuoteId(int quoteId) {
        var cmp = GetComponentListPaged(null, "",
          $"qi.QuoteId = @QuoteId", "", null, null, null,
          Common.NewSqlParameter("QuoteId", quoteId));
        return cmp.InfoList;
      }

      private static ComponentListPaged GetComponentListPaged(
        int? topOrNullForAll,
        string sqlExtraJoins,
        string sqlWhereConditions,
        string sqlOrderBy,
        int? offsetRows, int? fetchRows,
        Action<ComponentInfo, SqlDataReader> processRowAction,
        params SqlParameter[] sqlWhereParams
      ) {

        var infoPaged = new ComponentListPaged();
        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);
        string sqlOffset = "";
        if (sqlTop.IsNullOrEmpty() && !sqlOrderBy.IsNullOrEmpty() && offsetRows >= 0 && fetchRows > 0) {
          infoPaged.OffsetRows = offsetRows;
          infoPaged.FetchRows = fetchRows;
          sqlOffset = $" OFFSET {offsetRows} ROWS FETCH NEXT {fetchRows} ROWS ONLY";
        }

        string sql = $@"
          SELECT {sqlTop}
            {TblPfx}.ComponentId, {TblPfx}.ProgramJobId, {TblPfx}.CoacheeId,
            {TblPfx}.SessionNumber, {TblPfx}.CoachingSessionId, {TblPfx}.WorkshopEventId, {TblPfx}.ConsultingItemId, {TblPfx}.ProgramCostItemId, {TblPfx}.UserSubscriptionId,
            {TblPfx}.ComponentPrice, {TblPfx}.ComponentCost, {TblPfx}.PartnerUserId, {TblPfx}.CompletedDateUtc, {TblPfx}.LockedDateUtc,
            {TblPfx}.QuoteItemId, qi.QuoteId,
            {TblPfx}.InvoiceItemId, ii.InvoiceId, i.InvoiceNumber,
            pli.PLPeriodItemId, pli.PLPeriodId,
            plp.EndDateUtc AS PLPeriodDate,
            pri.PayRunId, pri.PayrunDate, IIF (pri.PayRunItemId IS NULL, 0, 1) AS HasPayrunItems,
            q.XeroTaxType,
            j.JobName as ProgramName,
            ac.FirstName as CoacheeFirstName, ac.LastName as CoacheeLastName,
            w.WorkshopTitle,
            ci.ItemTitle as ConsultingItemTitle,
            pci.Description as CostItemTitle

          FROM al_Component {TblPfx}

          OUTER APPLY (
            SELECT TOP 1 pri.PayRunItemId, pri.PayRunId, pr.ProcessedDateUtc as PayrunDate
            FROM id_PayRunItems pri
            LEFT JOIN id_PayRun pr ON pr.PayRunId = pri.PayRunId
            WHERE pri.ComponentId = {TblPfx}.ComponentId
          ) AS pri

          INNER JOIN id_Job j ON j.JobId = {TblPfx}.ProgramJobId
          LEFT OUTER JOIN id_CoachingSession cs ON cs.CoachingSessionId = {TblPfx}.CoachingSessionId
          LEFT OUTER JOIN al_Coachees ac ON ac.CoacheeId = cs.AbleCoacheeId
          LEFT OUTER JOIN ev_WorkshopEvent w ON w.WorkshopEventId = {TblPfx}.WorkshopEventId
          LEFT OUTER JOIN al_ConsultingItems ci ON ci.ConsultingItemId = {TblPfx}.ConsultingItemId
          LEFT OUTER JOIN al_ProgramCostItems pci ON pci.ProgramCostItemId = {TblPfx}.ProgramCostItemId

          LEFT OUTER JOIN al_QuoteItem qi ON qi.QuoteItemId = {TblPfx}.QuoteItemId
          LEFT OUTER JOIN al_Quote q ON q.QuoteId = qi.QuoteId
          LEFT OUTER JOIN al_InvoiceItem ii ON ii.InvoiceItemId = {TblPfx}.InvoiceItemId
          LEFT OUTER JOIN al_Invoice i ON i.InvoiceId = ii.InvoiceId
          LEFT OUTER JOIN al_PLPeriodItem pli ON pli.ComponentId = {TblPfx}.ComponentId
          LEFT OUTER JOIN al_PLPeriod plp ON plp.PLPeriodId = pli.PLPeriodId
          {sqlExtraJoins.EmptyIfNull()}
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}
          {sqlOffset}";

        if (ConfigHelper.IsDevServer) infoPaged.SqlText = sql;
        int lastComponentId = 0;

        Common.Query(sql,
          dr => {
            var ci = new ComponentInfo(
              dr.GetInt("ComponentId"),
              dr.GetInt("ProgramJobId"),
              dr.GetIntOrNull("CoacheeId"),
              dr.GetIntOrNull("SessionNumber"),
              dr.GetIntOrNull("CoachingSessionId"),
              dr.GetIntOrNull("WorkshopEventId"),
              dr.GetIntOrNull("ConsultingItemId"),
              dr.GetIntOrNull("ProgramCostItemId"),
              dr.GetIntOrNull("UserSubscriptionId"),
              dr.GetDecimal("ComponentPrice"),
              dr.GetDecimal("ComponentCost"),
              dr.GetIntOrNull("PartnerUserId"),
              dr.GetDateTimeOrNull("CompletedDateUtc"),
              dr.GetIntOrNull("QuoteItemId"),
              dr.GetIntOrNull("QuoteId"),
              dr.GetIntOrNull("InvoiceItemId"),
              dr.GetIntOrNull("InvoiceId"),
              dr.GetString("InvoiceNumber"),
              dr.GetDateTimeOrNull("LockedDateUtc"),
              dr.GetIntOrNull("PLPeriodItemId"),
              dr.GetIntOrNull("PLPeriodId"),
              dr.GetDateTimeOrNull("PLPeriodDate"),
              dr.GetIntOrNull("PayRunId"),
              dr.GetDateTimeOrNull("PayrunDate"),
              dr.GetBoolFromInt("HasPayrunItems"),
              XeroTaxType.GetGSTApplicableFromInvoiceTaxTypeOrNull(dr.GetString("XeroTaxType")) ?? false,
              dr.GetString("ProgramName"),
              dr.GetString("CoacheeFirstName"),
              dr.GetString("CoacheeLastName"),
              dr.GetString("WorkshopTitle"),
              dr.GetString("ConsultingItemTitle"),
              dr.GetString("CostItemTitle")
            );
            if (lastComponentId == ci.ComponentId) {
              throw new ApplicationException($"Recordset contains duplicate component ID ({ci.ComponentId}). Maybe more than one Pay Run item pointing to this component ID?");
            }
            lastComponentId = ci.ComponentId;
            if (processRowAction != null) {
              processRowAction(ci, dr);
            }
            infoPaged.InfoList.Add(ci);
          },
          sqlWhereParams
        );
        return infoPaged;
      }

      public class SessionComponentInfo {

        public int SessionNumber { get; protected set; }
        public int? SessionId { get; protected set; }
        public DateTime? CompletedUtc { get; protected set; }
        public int? DurationMins { get; protected set; }
        public decimal? ComponentPrice { get; protected set; }
        public int? QuoteItemId { get; protected set; }
        public bool IsLocked { get; protected set; }

        protected SessionComponentInfo() { }

        public SessionComponentInfo(int sessionNumber, int? sessionId, DateTime? completedUtc, int? durationMins, decimal? componentPrice, int? quoteItemId, bool isLocked) {
          SessionNumber = sessionNumber;
          SessionId = sessionId;
          CompletedUtc = completedUtc;
          DurationMins = durationMins;
          ComponentPrice = componentPrice;
          QuoteItemId = quoteItemId;
          IsLocked = isLocked;
        }
      }

      public static List<SessionComponentInfo> GetSessionComponentsForCoachee(int coacheeId) {

        var componentInfo = new List<SessionComponentInfo>();

        Common.Query($@"

          -- Fist get SessionsAllocated for the coachee.
          DECLARE @SessionsAllocated INT = (
            SELECT ac.SessionsAllocated
            FROM al_Coachees ac
            WHERE ac.CoacheeId = @CoacheeId
          );
          -- Generate sequential SessionNumbers from 1 to SessionsAllocated.
          WITH sn
          AS (
            SELECT TOP (ISNULL(@SessionsAllocated, 0)) ROW_NUMBER() OVER (ORDER BY t1.object_id) AS SessionNumber
            FROM sys.all_objects t1
          )
          -- Get components matching the right SessionNumber in order.
          SELECT sn.SessionNumber, cmp.ComponentId, cmp.CoachingSessionId, cs.ApptDateUTC, cs.DurationMins, cmp.ComponentPrice, cmp.QuoteItemId, cmp.LockedDateUtc
          FROM sn
          LEFT OUTER JOIN al_Component cmp ON cmp.CoacheeId = @CoacheeId AND cmp.SessionNumber = sn.SessionNumber
          LEFT OUTER JOIN id_CoachingSession cs ON cs.CoachingSessionId = cmp.CoachingSessionId
          ORDER BY sn.SessionNumber",

          dr => {
            componentInfo.Add(new SessionComponentInfo(
              dr.GetInt("SessionNumber"),
              dr.GetIntOrNull("CoachingSessionId"),
              dr.GetDateTimeOrNull("ApptDateUtc"),
              dr.GetIntOrNull("DurationMins"),
              dr.GetDecimalOrNull("ComponentPrice"),
              dr.GetIntOrNull("QuoteItemId"),
              dr.GetDateTimeOrNull("LockedDateUtc") != null));
          },
          Common.NewSqlParameter("CoacheeId", coacheeId));

        return componentInfo;
      }

      public static void UpsertUserSubscription(SqlTransaction trans,
        int userSubscriptionId, int programJobId, decimal? componentPrice, DateTime? completedDateUtc, int? quoteItemId) {
        UpsertNonCoaching(trans,
          KeyColumnEnum.UserSubscriptionId, userSubscriptionId, programJobId, null, componentPrice, completedDateUtc, quoteItemId);
      }

      public static void UpsertWorkshop(SqlTransaction trans,
        int workshopEventId, int programJobId, int? keyFacilitatorUserId, decimal? workshopRevenue, DateTime? startDateUtc, int? quoteItemId) {
        UpsertNonCoaching(trans,
          KeyColumnEnum.WorkshopEventId, workshopEventId, programJobId, keyFacilitatorUserId, workshopRevenue, startDateUtc, quoteItemId);
      }

      public static void UpsertConsultingItem(SqlTransaction trans,
        int consultingItemId, int programJobId, int? consultantUserId, decimal? itemAmount, DateTime? completionUtc, int? quoteItemId) {
        UpsertNonCoaching(trans,
          KeyColumnEnum.ConsultingItemId, consultingItemId, programJobId, consultantUserId, itemAmount, completionUtc, quoteItemId);
      }

      public static void UpsertCostItem(SqlTransaction trans,
        int costItemId, int programJobId, decimal? unitCost, decimal? unitPrice, decimal itemQuantity, DateTime? costIncurredUtc, int? quoteItemId) {
        UpsertNonCoaching(trans,
          KeyColumnEnum.ProgramCostItemId, costItemId, programJobId, null,
          unitPrice == null ? null : (unitPrice * itemQuantity),
          costIncurredUtc,
          quoteItemId,
          true,
          unitCost == null ? null : (unitCost * itemQuantity));
      }

      public class UpdateSessionComponentsInfo {

        public int CoacheeIdToUpdate { get; private set; }
        public int SessionsAllocated { get; private set; }
        public int ProgramJobId { get; private set; }
        public int CoachUserId { get; private set; }
        internal List<SessionComponentInfo> sessionComponentList = new List<SessionComponentInfo>();

        internal class SessionComponentInfo {
          public int SessionNumberToUpdate { get; private set; }
          public decimal? SessionRevenue { get; private set; }
          public int? SessionQuoteItemId { get; private set; }
          public SessionComponentInfo(int sessionNumberToUpdate, decimal? sessionRevenue, int? sessionQuoteItemId) {
            this.SessionNumberToUpdate = sessionNumberToUpdate;
            this.SessionRevenue = sessionRevenue;
            this.SessionQuoteItemId = sessionQuoteItemId;
          }
        }

        public UpdateSessionComponentsInfo(AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {
          Init(coacheeInfo.CoacheeId, coacheeInfo.ProgramJobId.Value, coacheeInfo.CoachUserId, coacheeInfo.UserActivity.SessionsAllocated);
        }

        public UpdateSessionComponentsInfo(int coacheeIdToUpdate, int programJobId, int coachUserId, int sessionsAllocated) {
          Init(coacheeIdToUpdate, programJobId, coachUserId, sessionsAllocated);
        }

        private void Init(int coacheeIdToUpdate, int programJobId, int coachUserId, int sessionsAllocated) {
          this.CoacheeIdToUpdate = coacheeIdToUpdate;
          this.ProgramJobId = programJobId;
          this.CoachUserId = coachUserId;
          this.SessionsAllocated = sessionsAllocated;
        }

        public void AddSessionToUpdate(int sessionNumber, decimal? sessionRevenue, int? sessionQuoteItemId) {
          sessionComponentList.Add(new SessionComponentInfo(sessionNumber, sessionRevenue, sessionQuoteItemId));
        }
      }

      // Update multiple session components.
      // Call this when updating more than one session at once.
      // The reason is to make sure that 2 updates are done in tandem:
      //  - first update to set all components to 0 revenue,
      //  - second update to set the correct revenues.
      // This avoids triggering the "al_Component.CheckComponentFunds" db trigger if any single allocation breaks a rule before all are done.
      // Must be performed within a transaction to ensure rollback of the first update if second fails.
      public static void UpdateSessionComponents(SqlTransaction trans, ProgramComponents.UpdateSessionComponentsInfo componentsInfo) {

        if (trans == null || trans.Connection == null) throw new ArgumentException("Must be called within a transaction.");
        if (componentsInfo == null) throw new ArgumentException("componentsInfo is null");
        if (componentsInfo.sessionComponentList != null && componentsInfo.SessionsAllocated < componentsInfo.sessionComponentList.Count) throw new ArgumentException("sessionsAllocated cannot be less than sessions to update.");

        if (!componentsInfo.sessionComponentList.IsNullOrEmpty()) {
          // Update existing or add new components.
          // Initially set all components to 0 revenue, then update again with proper revenue.
          // This avoids the over-allocation db trigger when changing revenue and quote items for multiple sessions at a time.
          try {
            foreach (var component in componentsInfo.sessionComponentList) {
              UpsertSessionComponent(trans,
                componentsInfo.CoacheeIdToUpdate, component.SessionNumberToUpdate,
                componentsInfo.ProgramJobId, componentsInfo.CoachUserId, 0, component.SessionQuoteItemId);
            }
            foreach (var component in componentsInfo.sessionComponentList) {
              UpsertSessionComponent(trans,
                componentsInfo.CoacheeIdToUpdate, component.SessionNumberToUpdate,
                componentsInfo.ProgramJobId, componentsInfo.CoachUserId, component.SessionRevenue, component.SessionQuoteItemId);
            }
          } catch (Exception ex) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation(nameof(UpdateSessionComponents))
              .WithOperationContext("UpsertComponents")
              .WithProperty(DalApplicationInsightsConstants.CoacheeId, componentsInfo?.CoacheeIdToUpdate)
              .WithProperty(DalApplicationInsightsConstants.CoachUserId, componentsInfo?.CoachUserId)
              .WithProperty(DalApplicationInsightsConstants.ProgramJobId, componentsInfo?.ProgramJobId)
              .Track();

            if (trans != null && trans.Connection != null) trans.Rollback(); // Ensure rollback if any problem.
            throw;
          }
        }
        // Remove excess components if any.
        DeleteUnallocatedSessions(trans, componentsInfo.CoacheeIdToUpdate, componentsInfo.SessionsAllocated);
      }

      public static void UpdateSingleSessionComponent(SqlTransaction trans,
        int coacheeIdToUpdate, int programJobId, int coachUserId, int sessionsAllocated,
        int sessionNumberToUpdate, decimal? sessionRevenue, int? quoteItemId) {

        var updateComponentsInfo = new UpdateSessionComponentsInfo(coacheeIdToUpdate, programJobId, coachUserId, sessionsAllocated);
        updateComponentsInfo.AddSessionToUpdate(sessionNumberToUpdate, sessionRevenue, quoteItemId);

        UpdateSessionComponents(trans, updateComponentsInfo);
      }

      // Upsert session component basic settings.
      private static void UpsertSessionComponent(SqlTransaction trans,
        int coacheeIdToUpdate, int sessionNumberToUpdate,
        int programJobId, int coachUserId, decimal? sessionRevenue, int? quoteItemId) {

        // Upsert pattern as per this "updates more likely" scenario: https://sqlperformance.com/2020/09/locking/upsert-anti-pattern
        Common.GetNonQueryInt(trans, $@"

          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          INSERT al_Component (CoacheeId,  SessionNumber,  ProgramJobId,  PartnerUserId,  ComponentPrice,  QuoteItemId)
          SELECT               @CoacheeId, @SessionNumber, @ProgramJobId, @PartnerUserId, @ComponentPrice, @QuoteItemId
          WHERE NOT EXISTS (
            SELECT NULL FROM al_Component c WITH (UPDLOCK, SERIALIZABLE)
            WHERE CoacheeId = @CoacheeId
            AND SessionNumber = @SessionNumber);

          IF @@ROWCOUNT = 0
          BEGIN
            UPDATE al_Component WITH (UPDLOCK, SERIALIZABLE) SET
              RowUpdatedUtc = GETUTCDATE(),
              ProgramJobId = @ProgramJobId,
              PartnerUserId = @PartnerUserId,
              ComponentPrice = @ComponentPrice,
              QuoteItemId = @QuoteItemId
            WHERE CoacheeId = @CoacheeId
              AND SessionNumber = @SessionNumber
              AND LockedDateUtc IS NULL;
          END

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

          Common.NewSqlParameter("CoacheeId", coacheeIdToUpdate),
          Common.NewSqlParameter("SessionNumber", sessionNumberToUpdate),
          Common.NewSqlParameter("ProgramJobId", programJobId),
          Common.NewSqlParameter("PartnerUserId", coachUserId),
          Common.NewSqlParameter("ComponentPrice", sessionRevenue),
          Common.NewSqlParameter("QuoteItemId", quoteItemId)
        );
      }

      public static int DeleteUnallocatedSessions(SqlTransaction trans, int coacheeId, int sessionsAllocated) {

        return Common.GetNonQueryInt(trans, $@"

          DELETE FROM al_Component
          WHERE CoacheeId = @CoacheeId
          AND SessionNumber > @SessionsAllocated
          AND CoachingSessionId IS NULL
          AND LockedDateUtc IS NULL",

          Common.NewSqlParameter("CoacheeId", coacheeId),
          Common.NewSqlParameter("SessionsAllocated", sessionsAllocated));
      }

      public static bool UpdateCoachingSessions_PartnerUserId(SqlTransaction trans, int coacheeId, int? coachUserId) {

        return Common.GetNonQueryInt(trans, $@"

          UPDATE al_Component SET
            RowUpdatedUtc = GETUTCDATE(),
            PartnerUserId = @PartnerUserId
          WHERE CoacheeId = @CoacheeId
            AND SessionNumber IS NOT NULL
            AND PartnerUserId = @Unassigned_UserId",

          Common.NewSqlParameter("CoacheeId", coacheeId),
          Common.NewSqlParameter("PartnerUserId", coachUserId),
          Common.NewSqlParameter("Unassigned_UserId", ConfigHelper.UserId.Unassigned)

        ) > 0;
      }

      // Add session info to component.
      public static bool AttachCoachingSession(SqlTransaction trans,
        int coacheeId, int sessionNumber, // <-- find by these
        int coachingSessionId, int? coachUserId, DateTime? startDateUtc) { // <-- assign these

        return Common.GetNonQueryInt(trans, $@"

          UPDATE al_Component SET
            RowUpdatedUtc = GETUTCDATE(),
            CoachingSessionId = @CoachingSessionId,
            PartnerUserId = @PartnerUserId,
            CompletedDateUtc = @CompletedDateUtc
          WHERE CoacheeId = @CoacheeId
            AND SessionNumber = @SessionNumber
            AND LockedDateUtc IS NULL",

          Common.NewSqlParameter("CoacheeId", coacheeId),
          Common.NewSqlParameter("SessionNumber", sessionNumber),
          Common.NewSqlParameter("CoachingSessionId", coachingSessionId),
          Common.NewSqlParameter("PartnerUserId", coachUserId),
          Common.NewSqlParameter("CompletedDateUtc", startDateUtc)

        ) == 1;
      }

      public static bool DetachCoachingSession(SqlTransaction trans, int coachingSessionId) {

        return Common.GetNonQueryInt(trans, $@"

          UPDATE al_Component SET
            RowUpdatedUtc = GETUTCDATE(),
            CoachingSessionId = NULL,
            PartnerUserId = NULL,
            CompletedDateUtc = NULL
          WHERE CoachingSessionId = @CoachingSessionId
            AND LockedDateUtc IS NULL",

          Common.NewSqlParameter("CoachingSessionId", coachingSessionId)

        ) == 1;
      }

      // Insert or update non-coaching component info.
      // Note InvoiceItemId is not updated here, it is handled separately.
      private static ComponentInfo UpsertNonCoaching(SqlTransaction trans,
        KeyColumnEnum keyColumn, int keyColumnValue,
        int programJobId, int? partnerUserId, decimal? componentPrice, DateTime? completedDateUtc, int? quoteItemId,
        bool updateComponentCost = false, decimal? componentCost = null) {

        var component = new ComponentInfo() {
          ProgramJobId = programJobId,
          PartnerUserId = partnerUserId,
          QuoteItemId = quoteItemId,
          ComponentPrice = componentPrice,
          ComponentCost = componentCost,
          CompletedDateUtc = completedDateUtc
        };

        // Note this function only handles components for workshops, consulting and cost items.
        switch (keyColumn) {
          case KeyColumnEnum.WorkshopEventId:
            component.WorkshopEventId = keyColumnValue;
            break;
          case KeyColumnEnum.ConsultingItemId:
            component.ConsultingItemId = keyColumnValue;
            break;
          case KeyColumnEnum.ProgramCostItemId:
            component.ProgramCostItemId = keyColumnValue;
            break;
          case KeyColumnEnum.UserSubscriptionId:
            component.UserSubscriptionId = keyColumnValue;
            break;
          default:
            throw new ArgumentException($"KeyColumnEnum.{keyColumn} not allowed here.");
        }

        // Note: UPDLOCK, SERIALIZABLE requires a transaction for scope, so if trans is null, add a transaction to SQL.
        // Ref: https://sqlperformance.com/2020/09/locking/upsert-anti-pattern

        Common.GetScalarQueryInt(trans, $@"

          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          INSERT al_Component (
            RowUpdatedUtc, ProgramJobId, PartnerUserId,
            WorkshopEventId, ConsultingItemId, ProgramCostItemId, UserSubscriptionId,
            ComponentPrice, ComponentCost, CompletedDateUtc,  QuoteItemId)
          SELECT
            @RowUpdatedUtc, @ProgramJobId, @PartnerUserId,
            @WorkshopEventId, @ConsultingItemId, @ProgramCostItemId, @UserSubscriptionId,
            @ComponentPrice, @ComponentCost, @CompletedDateUtc, @QuoteItemId
          WHERE NOT EXISTS (
            SELECT 1 FROM al_Component c WITH (UPDLOCK, SERIALIZABLE)
            WHERE {keyColumn} = @KeyColumnValue);

          IF @@ROWCOUNT = 0
          BEGIN
            UPDATE al_Component SET
              RowUpdatedUtc = @RowUpdatedUtc,
              ProgramJobId = @ProgramJobId,
              PartnerUserId = @PartnerUserId,
              CoacheeId = @CoacheeId,
              SessionNumber = @SessionNumber,
              CoachingSessionId = @CoachingSessionId,
              WorkshopEventId = @WorkshopEventId,
              ConsultingItemId = @ConsultingItemId,
              ProgramCostItemId = @ProgramCostItemId,
              UserSubscriptionId = @UserSubscriptionId,
              ComponentPrice = @ComponentPrice,
              {(updateComponentCost ? "ComponentCost = @ComponentCost," : "")}
              CompletedDateUtc = @CompletedDateUtc,
              QuoteItemId = @QuoteItemId
            WHERE {keyColumn} = @KeyColumnValue
              AND LockedDateUtc IS NULL;
          END

          SELECT @@ROWCOUNT; -- Will return 1 if successful.

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

          Common.NewSqlParameter("KeyColumnValue", keyColumnValue),
          Common.NewSqlParameter("RowUpdatedUtc", DateTime.UtcNow),
          Common.NewSqlParameter("ProgramJobId", component.ProgramJobId),
          Common.NewSqlParameter("PartnerUserId", component.PartnerUserId),
          Common.NewSqlParameter("CoacheeId", component.CoacheeId),
          Common.NewSqlParameter("SessionNumber", component.SessionNumber),
          Common.NewSqlParameter("CoachingSessionId", component.CoachingSessionId),
          Common.NewSqlParameter("WorkshopEventId", component.WorkshopEventId),
          Common.NewSqlParameter("ConsultingItemId", component.ConsultingItemId),
          Common.NewSqlParameter("ProgramCostItemId", component.ProgramCostItemId),
          Common.NewSqlParameter("UserSubscriptionId", component.UserSubscriptionId),
          Common.NewSqlParameter("ComponentPrice", component.ComponentPrice),
          Common.NewSqlParameter("ComponentCost", component.ComponentCost),
          Common.NewSqlParameter("CompletedDateUtc", component.CompletedDateUtc),
          Common.NewSqlParameter("QuoteItemId", component.QuoteItemId)

        );

        return component;
      }

      private static void DeleteComponent(SqlTransaction trans, KeyColumnEnum keyColumn, int keyColumnValue) {
        // Not do not delete if component is locked.
        Common.GetNonQueryInt(trans, $@"

          {(trans == null ? "BEGIN TRANSACTION;" : "")}

          IF EXISTS
          (
            SELECT 1
            FROM al_Component WITH (UPDLOCK, ROWLOCK, SERIALIZABLE)
            WHERE {keyColumn} = @KeyColumnValue
            AND LockedDateUtc IS NOT NULL
          )
          BEGIN
            IF @@trancount > 0
              ROLLBACK TRANSACTION;
            RAISERROR ('Component is locked.', 11, 1);
          END
          ELSE
          BEGIN
            DELETE FROM al_Component
            WHERE {keyColumn} = @KeyColumnValue;
          END;

          {(trans == null ? "COMMIT TRANSACTION;" : "")}",

          Common.NewSqlParameter("KeyColumnValue", keyColumnValue)
        );
      }

      public static void DeleteWorkshop(SqlTransaction trans, int workshopEventId) {
        DeleteComponent(trans, KeyColumnEnum.WorkshopEventId, workshopEventId);
      }
      public static void DeleteConsulting(SqlTransaction trans, int consultingItemId) {
        DeleteComponent(trans, KeyColumnEnum.ConsultingItemId, consultingItemId);
      }
      public static void DeleteCostItem(SqlTransaction trans, int costItemId) {
        DeleteComponent(trans, KeyColumnEnum.ProgramCostItemId, costItemId);
      }

      public class PriceTotals {
        public decimal QuoteItemQuantity { get; internal set; }
        public decimal QuoteItemTotalPrice { get; internal set; }
        public decimal ComponentCount { get; internal set; }
        public decimal ComponentTotalPrice { get; internal set; }
      }

      // Get any orphaned components, where there are existing sessions
      // but the components that correspond to the CoacheeId + SessionNumber
      // do not have the CoachingSessionId set.
      public static List<UnassignedSessionIds> GetUnassignedSessionIds() {

        var result = new List<UnassignedSessionIds>();

        Common.Query($@"
          SELECT ac.CoacheeId, cs.CoachingSessionId, cs.SessionNumber, cs.ApptDateUTC, cmp.ComponentId
          FROM al_Coachees ac
          CROSS APPLY (
            SELECT cs.CoachingSessionId, cs.ApptDateUTC,
              ROW_NUMBER() OVER (ORDER BY cs.ApptDateUTC) AS SessionNumber
            FROM id_CoachingSession cs
            WHERE cs.AbleCoacheeId = ac.CoacheeId
          ) AS cs
          INNER JOIN al_Component cmp ON cmp.CoacheeId = ac.CoacheeId AND cmp.SessionNumber = cs.SessionNumber AND cmp.CoachingSessionId IS NULL",
          dr => {
            result.Add(new UnassignedSessionIds() {
              CoacheeId = dr.GetInt("CoacheeId"),
              CoachingSessionId = dr.GetInt("CoachingSessionId"),
              ApptDateUTC = dr.GetDateTime("ApptDateUTC"),
              SessionNumber = dr.GetInt("SessionNumber"),
              ComponentId = dr.GetInt("ComponentId")
            });
          }
        );

        return result;
      }

      public static bool UpdateComponentInvoiceItem(SqlTransaction trans, int componentId, int? invoiceItem) {

        return Common.GetNonQueryInt(trans, $@"

          UPDATE al_Component SET
            InvoiceItemId = @InvoiceItemId
          WHERE ComponentId = @ComponentId",

          Common.NewSqlParameter("ComponentId", componentId),
          Common.NewSqlParameter("InvoiceItemId", invoiceItem)

        ) == 1;
      }

      public static int? GetFirstFreeSessionNumber(int coacheeId) {

        return Common.GetScalarQueryIntOrNull(null, @"
          SELECT TOP 1 cmp.SessionNumber
          FROM al_Component cmp
          WHERE cmp.CoacheeId = @CoacheeId
            AND cmp.SessionNumber > 0
            AND cmp.CoachingSessionId IS NULL
          ORDER BY cmp.SessionNumber",
          Common.NewSqlParameter("CoacheeId", coacheeId)
        );
      }

      public class ComponentQuoteInfo {
        public int? QuoteItemId { get; set; }
        public bool IsComponentLocked { get; internal set; }
        public Guid? QuotePublicGuid { get; internal set; }
        public string QuoteItemDescriptionHtml { get; internal set; }
        public ComponentQuoteInfo() { }
        public ComponentQuoteInfo(SqlDataReader dr) {
          this.QuoteItemId = dr.GetIntOrNull("QuoteItemId");
          this.IsComponentLocked = dr.GetDateTimeOrNull("LockedDateUtc") != null;
          this.QuotePublicGuid = dr.GetGuidOrNull("QuotePublicGuid");
          this.QuoteItemDescriptionHtml = dr.GetString("QuoteItemDescription");
        }
      }

      public class UnassignedSessionIds {
        public int CoacheeId { get; internal set; }
        public int CoachingSessionId { get; internal set; }
        public int SessionNumber { get; internal set; }
        public DateTime ApptDateUTC { get; internal set; }
        public int ComponentId { get; internal set; }
      }

      public static ComponentInfo NewSessionComponent(
        int programJobId,
        int coacheeId,
        int sessionNumber,
        int? coachingSessionId,
        int? coachUserId,
        DateTime? sessionStartUtc,
        decimal? sessionPrice,
        int? quoteItemId
      ) {
        return new ComponentInfo() {
          ProgramJobId = programJobId,
          CoacheeId = coacheeId,
          SessionNumber = sessionNumber,
          CoachingSessionId = coachingSessionId,
          PartnerUserId = coachUserId,
          CompletedDateUtc = sessionStartUtc,
          ComponentPrice = sessionPrice,
          QuoteItemId = quoteItemId
        };
      }

      public class ComponentListPaged : InfoListPaged<ComponentInfo> { }

      public class ComponentInfo {
        public int ComponentId { get; internal set; }
        public int ProgramJobId { get; internal set; }
        public int? CoacheeId { get; internal set; }
        public int? SessionNumber { get; internal set; }
        public int? CoachingSessionId { get; internal set; }
        public int? WorkshopEventId { get; internal set; }
        public int? ConsultingItemId { get; internal set; }
        public int? ProgramCostItemId { get; internal set; }
        public int? UserSubscriptionId { get; internal set; }
        public decimal? ComponentPrice { get; internal set; }
        public decimal? ComponentCost { get; internal set; }
        public int? PartnerUserId { get; internal set; }
        public DateTime? CompletedDateUtc { get; internal set; }
        public int? QuoteItemId { get; internal set; }
        public int? QuoteId { get; internal set; }
        public int? InvoiceItemId { get; internal set; }
        public int? InvoiceId { get; internal set; }
        public string InvoiceNumber { get; internal set; }
        public DateTime? LockedDateUtc { get; internal set; }
        public int? PLPeriodItemId { get; internal set; }
        public int? PLPeriodId { get; internal set; }
        public DateTime? PLPeriodDate { get; internal set; }
        public int? PayRunId { get; internal set; }
        public DateTime? PayrunDate { get; internal set; }
        public bool HasPayrunItems { get; internal set; }
        public bool GSTApplicable { get; internal set; }
        public string ProgramName { get; internal set; }
        public string CoacheeFirstName { get; internal set; }
        public string CoacheeLastName { get; internal set; }
        public string WorkshopTitle { get; internal set; }
        public string ConsultingItemTitle { get; internal set; }
        public string CostItemTitle { get; internal set; }

        public ComponentInfo() { }

        public ComponentInfo(
          int componentId,
          int programJobId,
          int? coacheeId,
          int? sessionNumber,
          int? coachingSessionId,
          int? workshopEventId,
          int? consultingItemId,
          int? programCostItemId,
          int? userSubscriptionId,
          decimal? componentPrice,
          decimal? componentCost,
          int? partnerUserId,
          DateTime? completedDateUtc,
          int? quoteItemId,
          int? quoteId,
          int? invoiceItemId,
          int? invoiceId,
          string invoiceNumber,
          DateTime? lockedDateUtc,
          int? plPeriodItemId,
          int? plPeriodId,
          DateTime? pLPeriodDate,
          int? payRunId,
          DateTime? payrunDate,
          bool hasPayrunItems,
          bool gstApplicable,
          string programName,
          string coacheeFirstName,
          string coacheeLastName,
          string workshopTitle,
          string consultingItemTitle,
          string costItemTitle
        ) {
          this.ComponentId = componentId;
          this.CoacheeId = coacheeId;
          this.SessionNumber = sessionNumber;
          this.ProgramJobId = programJobId;
          this.CoachingSessionId = coachingSessionId;
          this.WorkshopEventId = workshopEventId;
          this.ConsultingItemId = consultingItemId;
          this.ProgramCostItemId = programCostItemId;
          this.UserSubscriptionId = userSubscriptionId;
          this.ComponentPrice = componentPrice;
          this.ComponentCost = componentCost;
          this.PartnerUserId = partnerUserId;
          this.CompletedDateUtc = completedDateUtc;
          this.QuoteItemId = quoteItemId;
          this.QuoteId = quoteId;
          this.InvoiceItemId = invoiceItemId;
          this.InvoiceId = invoiceId;
          this.InvoiceNumber = invoiceNumber;
          this.LockedDateUtc = lockedDateUtc;
          this.PLPeriodItemId = plPeriodItemId;
          this.PLPeriodId = plPeriodId;
          this.PLPeriodDate = pLPeriodDate;
          this.PayRunId = payRunId;
          this.PayrunDate = payrunDate;
          this.HasPayrunItems = hasPayrunItems;
          this.GSTApplicable = gstApplicable;
          this.ProgramName = programName;
          this.CoacheeFirstName = coacheeFirstName;
          this.CoacheeLastName = coacheeLastName;
          this.WorkshopTitle = workshopTitle;
          this.ConsultingItemTitle = consultingItemTitle;
          this.CostItemTitle = costItemTitle;
        }
        public string XeroTaxType => DbHelper.XeroTaxType.GetInvoiceTaxTypeFromGSTApplicable(this.GSTApplicable);
      }

    }
  }
}
