using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class ProgramCostItems {

      private const string TblPfx = "pci";
      private const string DefaultXeroAccountCode = "50033";
      private const string DefaultXeroTaxType = "INPUT";

      // In most of these methods, caller must ensure access to the ProgramId is valid for purpose.

      // Get item by id, optionally ensuring it is part of intended program.
      public static ProgramCostItemInfo GetCostItemInfo(int costItemId) {
        return GetCostItemInfo(
          $"{TblPfx}.ProgramCostItemId = @ProgramCostItemId",
          Common.NewSqlParameter("@ProgramCostItemId", costItemId)
        );
      }

      // Get item by id, ensuring it is part of intended program.
      public static ProgramCostItemInfo GetCostItemInfo(int costItemId, int programJobId) {
        return GetCostItemInfo(
          $"{TblPfx}.ProgramCostItemId = @ProgramCostItemId AND {TblPfx}.ProgramJobId = @ProgramJobId",
          Common.NewSqlParameter("@ProgramCostItemId", costItemId),
          Common.NewSqlParameter("@ProgramJobId", programJobId)
        );
      }

      // Return items in program.
      public static List<ProgramCostItemInfo> GetItemsInProgram(int programJobId, bool? billToClientValue = null) {
        string sqlAndBillToClient = "";
        if (billToClientValue != null) sqlAndBillToClient = $" AND {TblPfx}.BillToClient = {((bool)billToClientValue ? "1" : "0")}";
        return GetCostItemInfoList(null, "",
          $"{TblPfx}.ProgramJobId = @ProgramJobId {sqlAndBillToClient}",
          $"{TblPfx}.CostIncurredUtc DESC",
          Common.NewSqlParameter("@ProgramJobId", programJobId)
        );
      }

      // Return items in purchase order.
      public static List<ProgramCostItemInfo> GetItemsInPurchaseOrder(int xeroPurchaseOrderId) {
        return GetCostItemInfoList(null, "",
          $"{TblPfx}.XeroPurchaseOrderId = @XeroPurchaseOrderId",
          $"{TblPfx}.CostIncurredUtc DESC",
          Common.NewSqlParameter("@XeroPurchaseOrderId", xeroPurchaseOrderId)
        );
      }

      private static ProgramCostItemInfo GetCostItemInfo(string sqlWhereConditions, params SqlParameter[] sqlWhereParams) {
        var costItemList = GetCostItemInfoList(1, "", sqlWhereConditions, "", sqlWhereParams);
        if (costItemList.Count == 0) return null;
        else return costItemList[0];
      }

      private static List<ProgramCostItemInfo> GetCostItemInfoList(
        int? topOrNullForAll,
        string sqlExtraJoins,
        string sqlWhereConditions,
        string sqlOrderBy,
        params SqlParameter[] sqlWhereParams) {

        var CostItemList = new List<ProgramCostItemInfo>();

        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);
        string sql = $@"
          SELECT {sqlTop}
            {TblPfx}.ProgramCostItemId, {TblPfx}.ProgramJobId, {TblPfx}.AbleCreatedUtc, {TblPfx}.AbleUpdatedUtc,
            {TblPfx}.XeroPurchaseOrderId, {TblPfx}.Description, {TblPfx}.Notes, {TblPfx}.Quantity, {TblPfx}.XeroAccountCode,
            {TblPfx}.UnitCost, {TblPfx}.UnitPrice, {TblPfx}.XeroTaxType, {TblPfx}.CostIncurredUtc, {TblPfx}.BillToClient, {TblPfx}.LastXeroSyncUtc,
            j.JobNumber, j.JobName,
            xpo.PurchaseOrderName, xpo.PurchaseOrderNumber,
            {DbHelper.ProgramComponents.GetComponentQuoteCrossApply_SelectItems}
          FROM al_ProgramCostItems {TblPfx}
          INNER JOIN id_Job j ON j.JobId = {TblPfx}.ProgramJobId
          LEFT OUTER JOIN al_XeroPurchaseOrders xpo ON xpo.XeroPurchaseOrderId = {TblPfx}.XeroPurchaseOrderId
          {DbHelper.ProgramComponents.GetComponentQuoteCrossApply($"apc.ProgramCostItemId = {TblPfx}.ProgramCostItemId")}
          {sqlExtraJoins.EmptyIfNull()}
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddRange(sqlWhereParams);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                CostItemList.Add(new ProgramCostItemInfo(
                  dr.GetInt("ProgramCostItemId"),
                  dr.GetInt("ProgramJobId"),
                  dr.GetString("JobNumber"),
                  dr.GetDateTime("AbleCreatedUtc"),
                  dr.GetDateTime("AbleUpdatedUtc"),
                  dr.GetString("XeroAccountCode"),
                  dr.GetIntOrNull("XeroPurchaseOrderId"),
                  dr.GetString("PurchaseOrderName"),
                  dr.GetString("PurchaseOrderNumber"),
                  dr.GetString("Description"),
                  dr.GetString("Notes"),
                  dr.GetDecimal("Quantity"),
                  dr.GetDecimal("UnitCost"),
                  dr.GetDecimal("UnitPrice"),
                  XeroTaxType.GetGSTApplicableFromCostTaxTypeOrNull(dr.GetString("XeroTaxType")) ?? false,
                  dr.GetDateTimeOrNull("CostIncurredUtc"),
                  dr.GetBoolFromInt("BillToClient"),
                  dr.GetDateTimeOrNull("LastXeroSyncUtc"),
                  new DbHelper.ProgramComponents.ComponentQuoteInfo(dr)
                ));
              }
            }
          }
        }
        return CostItemList;
      }

      public class NewCostItemInfo {
        public int ProgramCostItemId { get; internal set; }
      }

      public static int AddCostItem(ProgramCostItemInfo ci) {

        int newCostItemId;

        newCostItemId = Common.GetScalarQueryInt(@"
          INSERT INTO al_ProgramCostItems (
                  ProgramJobId, XeroPurchaseOrderId, CostIncurredUtc, Description, Quantity, UnitCost, UnitPrice, XeroTaxType,
                  BillToClient, Notes, AbleCreatedUtc, AbleUpdatedUtc, LastXeroSyncUtc, XeroAccountCode)
          OUTPUT INSERTED.ProgramCostItemId
          VALUES (@ProgramJobId, @XeroPurchaseOrderId, @CostIncurredUtc, @Description, @Quantity, @UnitCost, @UnitPrice, @XeroTaxType,
                  @BillToClient, @Notes, @AbleCreatedUtc, @AbleUpdatedUtc, @LastXeroSyncUtc, @XeroAccountCode)",
          Common.NewSqlParameter("@ProgramJobId", ci.ProgramJobId),
          Common.NewSqlParameter("@AbleCreatedUtc", DateTime.UtcNow),
          Common.NewSqlParameter("@AbleUpdatedUtc", DateTime.UtcNow),
          Common.NewSqlParameter("@XeroPurchaseOrderId", ci.XeroPurchaseOrderId),
          Common.NewSqlParameter("@XeroAccountCode", ci.XeroAccountCode),
          Common.NewSqlParameter("@Description", ci.Description),
          Common.NewSqlParameter("@Notes", ci.Notes),
          Common.NewSqlParameter("@Quantity", ci.Quantity),
          Common.NewSqlParameter("@UnitCost", ci.UnitCost),
          Common.NewSqlParameter("@UnitPrice", ci.UnitPrice),
          Common.NewSqlParameter("@XeroTaxType", XeroTaxType.GetCostTaxTypeFromGSTApplicable(ci.GSTApplicable)),
          Common.NewSqlParameter("@CostIncurredUtc", ci.CostIncurredUtc),
          Common.NewSqlParameter("@BillToClient", ci.BillToClient),
          Common.NewSqlParameter("@LastXeroSyncUtc", ci.LastXeroSyncUtc));

        ci.ProgramCostItemId = newCostItemId;
        ProgramComponents.UpsertCostItem(
          null, ci.ProgramCostItemId, ci.ProgramJobId, ci.UnitCost,
          ci.UnitPrice, ci.Quantity, ci.CostIncurredUtc, ci.ComponentQuoteInfo.QuoteItemId);
        return newCostItemId;
      }

      // Update PO Ids for all CIs in Program.
      public static int UpdatePurchaseOrderIds(int programJobId, int xeroPurchaseOrderId, List<int> updateItemIds) {
        return Common.GetNonQueryInt(
          $@"UPDATE al_ProgramCostItems
                SET XeroPurchaseOrderId = @XeroPurchaseOrderId
              WHERE ProgramJobId = @ProgramJobId
                AND ProgramCostItemId IN({updateItemIds.ToStringList()})",
          Common.NewSqlParameter("ProgramJobId", programJobId),
          Common.NewSqlParameter("XeroPurchaseOrderId", xeroPurchaseOrderId));
      }

      public static bool UpdateCostItem(ProgramCostItemInfo ci, bool setUpdateTime = true, bool reload = false) {

        if (ci == null) throw new ArgumentException("ci is null.");

        int updatedId = Common.GetScalarQueryInt(@"
          UPDATE al_ProgramCostItems
          SET AbleUpdatedUtc = SYSUTCDATETIME(),
              XeroPurchaseOrderId = @XeroPurchaseOrderId,
              Description = @Description,
              Notes = @Notes,
              Quantity = @Quantity,
              UnitCost = @UnitCost,
              UnitPrice = @UnitPrice,
              XeroTaxType = @XeroTaxType,
              CostIncurredUtc = @CostIncurredUtc,
              BillToClient = @BillToClient,
              LastXeroSyncUtc = @LastXeroSyncUtc
          OUTPUT INSERTED.ProgramCostItemId
          WHERE ProgramJobId = @ProgramJobId
            AND ProgramCostItemId = @ProgramCostItemId",

          Common.NewSqlParameter("@ProgramJobId", ci.ProgramJobId),
          Common.NewSqlParameter("@ProgramCostItemId", ci.ProgramCostItemId),

          Common.NewSqlParameter("@XeroPurchaseOrderId", ci.XeroPurchaseOrderId),
          Common.NewSqlParameter("@Description", ci.Description),
          Common.NewSqlParameter("@Notes", ci.Notes),
          Common.NewSqlParameter("@Quantity", ci.Quantity),
          Common.NewSqlParameter("@UnitCost", ci.UnitCost),
          Common.NewSqlParameter("@UnitPrice", ci.UnitPrice),
          Common.NewSqlParameter("@XeroTaxType", XeroTaxType.GetCostTaxTypeFromGSTApplicable(ci.GSTApplicable)),
          Common.NewSqlParameter("@CostIncurredUtc", ci.CostIncurredUtc),
          Common.NewSqlParameter("@BillToClient", ci.BillToClient),
          Common.NewSqlParameter("@LastXeroSyncUtc", ci.LastXeroSyncUtc));

        if (updatedId != ci.ProgramCostItemId) return false;
        ProgramCostItemInfo ciReloaded;
        if (reload) {
          ciReloaded = GetCostItemInfo(ci.ProgramJobId, ci.ProgramCostItemId);
          if (ciReloaded == null) return false;
          ci = ciReloaded;
        }
        ProgramComponents.UpsertCostItem(
          null, ci.ProgramCostItemId, ci.ProgramJobId, ci.UnitCost,
          ci.UnitPrice, ci.Quantity, ci.CostIncurredUtc, ci.ComponentQuoteInfo.QuoteItemId);
        return true;
      }

      public static void DeleteCostItem(SqlTransaction trans, int costItemId) {
        // Only delete if component is not locked.

        Common.UsingTransaction(trans, _ => {

          ProgramComponents.DeleteCostItem(trans, costItemId); // Deletes only if not locked.

          // Delete if component was deleted.
          Common.GetNonQueryInt(trans, $@"
            IF NOT EXISTS (SELECT 1 FROM al_Component cmp WHERE cmp.ProgramCostItemId = @ProgramCostItemId)
            BEGIN

              UPDATE ij
                SET ij.LastUpdatedUtc = SYSUTCDATETIME()
              FROM id_Job ij
              INNER JOIN al_ProgramCostItems ci ON ci.ProgramJobId = ij.JobId
              WHERE ci.ProgramCostItemId = @ProgramCostItemId;

              DELETE FROM al_ProgramCostItems WHERE ProgramCostItemId = @ProgramCostItemId;

            END;",

            Common.NewSqlParameter("@ProgramCostItemId", costItemId)
          );

          return true;
        });
      }

      public static bool MoveItemToProgram(int costItemId, int toProgramJobId) {

        var fromCostItem = GetCostItemInfo(costItemId);
        if (fromCostItem == null) return false;

        int fromProgramJobId = fromCostItem.ProgramJobId;

        return Common.GetScalarQueryInt(null, $@"

          BEGIN TRANSACTION;

          -- Only update if no locked components (or no components at all) exist.
          IF NOT EXISTS (SELECT 1 FROM al_Component cmp WHERE ProgramCostItemId = @ProgramCostItemId AND LockedDateUtc IS NOT NULL)
          AND EXISTS(SELECT 1 FROM id_Job WHERE JobId = @ToProgramJobId)
          BEGIN

            UPDATE al_Component
            SET ProgramJobId = @ToProgramJobId
            WHERE ProgramCostItemId = @ProgramCostItemId;

            UPDATE al_ProgramCostItems
            SET ProgramJobId = @ToProgramJobId,
                AbleUpdatedUtc = SYSUTCDATETIME()
            WHERE ProgramCostItemId = @ProgramCostItemId;

            UPDATE id_Job
              SET LastUpdatedUtc = SYSUTCDATETIME()
            WHERE JobId = @FromProgramJobId
               OR JobId = @ToProgramJobId;

          END;

          SELECT @@ROWCOUNT;

          COMMIT TRANSACTION;",

          Common.NewSqlParameter("ProgramCostItemId", costItemId),
          Common.NewSqlParameter("FromProgramJobId", fromProgramJobId),
          Common.NewSqlParameter("ToProgramJobId", toProgramJobId)

        ) > 0;
      }

      public class ProgramCostItemInfo {

        public int ProgramCostItemId { get; internal set; }
        public int ProgramJobId { get; private set; }
        public string ProgramJobNumber { get; private set; }
        public DateTime AbleCreatedUtc { get; private set; }
        public DateTime AbleUpdatedUtc { get; set; }
        public int? XeroPurchaseOrderId { get; set; }
        public string XeroPurchaseOrderName { get; private set; }
        public string XeroPurchaseOrderNumber { get; private set; }
        public string Description { get; set; }
        public string Notes { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal UnitPrice { get; set; }
        public bool GSTApplicable { get; set; }
        public string XeroAccountCode { get; private set; }
        public DateTime? CostIncurredUtc { get; set; }
        public bool BillToClient { get; set; }
        public DateTime? LastXeroSyncUtc { get; set; }
        public DbHelper.ProgramComponents.ComponentQuoteInfo ComponentQuoteInfo { get; set; }

        public ProgramCostItemInfo() {
          this.ComponentQuoteInfo = new DbHelper.ProgramComponents.ComponentQuoteInfo();
        }

        public ProgramCostItemInfo(
          int programCostItemId,
          int programJobId,
          string programJobNumber,
          DateTime ableCreatedUtc,
          DateTime ableUpdatedUtc,
          string xeroAccountCodeOrNullForDefault,
          int? xeroPurchaseOrderId,
          string xeroPurchaseOrderName,
          string xeroPurchaseOrderNumber,
          string description,
          string notes,
          decimal quantity,
          decimal unitCost,
          decimal unitPrice,
          bool gstApplicable,
          DateTime? costIncurredUtc,
          bool billToClient,
          DateTime? lastXeroSyncUtc,
          DbHelper.ProgramComponents.ComponentQuoteInfo componentQuoteInfo
        ) {
          this.ProgramCostItemId = programCostItemId;
          this.ProgramJobId = programJobId;
          this.ProgramJobNumber = programJobNumber;
          this.AbleCreatedUtc = ableCreatedUtc;
          this.AbleUpdatedUtc = ableUpdatedUtc;
          this.XeroAccountCode = xeroAccountCodeOrNullForDefault.ValueIfNullOrEmpty(DefaultXeroAccountCode);
          this.XeroPurchaseOrderId = xeroPurchaseOrderId;
          this.XeroPurchaseOrderName = xeroPurchaseOrderName;
          this.XeroPurchaseOrderNumber = xeroPurchaseOrderNumber;
          this.Description = description;
          this.Notes = notes;
          this.Quantity = quantity;
          this.UnitCost = unitCost;
          this.UnitPrice = unitPrice;
          this.GSTApplicable = gstApplicable;
          this.CostIncurredUtc = costIncurredUtc;
          this.BillToClient = billToClient;
          this.LastXeroSyncUtc = lastXeroSyncUtc;
          this.ComponentQuoteInfo = componentQuoteInfo != null ? componentQuoteInfo : new DbHelper.ProgramComponents.ComponentQuoteInfo();
        }

        public string XeroTaxType => DbHelper.XeroTaxType.GetCostTaxTypeFromGSTApplicable(this.GSTApplicable);
      }

    }
  }
}

