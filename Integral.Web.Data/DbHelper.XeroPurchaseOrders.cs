using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class XeroPurchaseOrders {

      private const string TblPfx = "xpo";
      private const string DefaultPurchaseOrderType = "ACCPAY";
      private const string DefaultLineAmountType = "Exclusive";

      // Get purchase order by id.
      public static PurchaseOrderInfo GetPurchaseOrderInfo(int xeroPurchaseOrderId) {
        return GetPurchaseOrderInfo(
          $"{TblPfx}.XeroPurchaseOrderId = @XeroPurchaseOrderId",
          Common.NewSqlParameter("@XeroPurchaseOrderId", xeroPurchaseOrderId)
        );
      }

      private static PurchaseOrderInfo GetPurchaseOrderInfo(string sqlWhereConditions, params SqlParameter[] sqlWhereParams) {
        var purchaseOrderList = GetPurchaseOrderInfoList(1, "", sqlWhereConditions, "", sqlWhereParams);
        if (purchaseOrderList.Count == 0) return null;
        else return purchaseOrderList[0];
      }

      private static List<PurchaseOrderInfo> GetPurchaseOrderInfoList(
        int? topOrNullForAll,
        string sqlExtraJoins,
        string sqlWhereConditions,
        string sqlOrderBy,
        params SqlParameter[] sqlWhereParams) {

        var purchaseOrderList = new List<PurchaseOrderInfo>();

        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);
        string sql = $@"
          SELECT {sqlTop}
            {TblPfx}.XeroPurchaseOrderId, {TblPfx}.XeroPurchaseOrderUID, {TblPfx}.XeroContactId, {TblPfx}.PurchaseOrderName, {TblPfx}.PurchaseOrderNumber, 
            {TblPfx}.IsDraft, {TblPfx}.AbleCreatedUtc, {TblPfx}.AbleUpdatedUtc, {TblPfx}.LastXeroSyncUtc, {TblPfx}.LineAmountType, {TblPfx}.DeliveryDateUtc,
            xc.XeroContactUID
          FROM al_XeroPurchaseOrders {TblPfx}
          INNER JOIN al_XeroContacts xc ON xc.XeroContactId = {TblPfx}.XeroContactId
          {sqlExtraJoins.EmptyIfNull()}
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddRange(sqlWhereParams);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                purchaseOrderList.Add(new PurchaseOrderInfo(
                  dr.GetInt("XeroPurchaseOrderId"),
                  dr.GetGuidOrNull("XeroPurchaseOrderUID"),
                  dr.GetInt("XeroContactId"),
                  dr.GetGuid("XeroContactUID"),
                  dr.GetDateTime("AbleCreatedUtc"),
                  dr.GetDateTime("AbleUpdatedUtc"),
                  dr.GetString("PurchaseOrderNumber"),
                  dr.GetString("PurchaseOrderName"),
                  dr.GetDateTimeOrNull("DeliveryDateUtc"),
                  dr.GetBoolFromInt("IsDraft"),
                  dr.GetString("LineAmountType"),
                  dr.GetDateTimeOrNull("LastXeroSyncUtc")
                ));
              }
            }
          }
        }
        return purchaseOrderList;
      }

      public static int GetRowCount() {
        return Common.GetScalarQueryInt("SELECT COUNT(*) FROM al_XeroPurchaseOrders");
      }

      public static Guid? GetXeroContactGuidOrNull(int xeroContactId) {
        return (Guid?)Common.GetScalarQuery(@"
          SELECT XeroContactUID 
          FROM al_XeroContacts 
          WHERE XeroContactId = @XeroContactId",
          Common.NewSqlParameter("@XeroContactId", xeroContactId));
      }

      public static int AddPurchaseOrder(PurchaseOrderInfo po) {

        int newXeroPurchaseOrderId;

        newXeroPurchaseOrderId = Common.GetScalarQueryInt(@"
          INSERT INTO al_XeroPurchaseOrders (
            XeroPurchaseOrderUID, XeroContactId, PurchaseOrderName, PurchaseOrderNumber, 
            IsDraft, AbleCreatedUtc, AbleUpdatedUtc, LastXeroSyncUtc, LineAmountType, DeliveryDateUtc) 
          OUTPUT INSERTED.XeroPurchaseOrderId
          VALUES (@XeroPurchaseOrderUID, @XeroContactId, @PurchaseOrderName, @PurchaseOrderNumber, 
            @IsDraft, @AbleCreatedUtc, @AbleUpdatedUtc, @LastXeroSyncUtc, @LineAmountType, @DeliveryDateUtc)",
          Common.NewSqlParameter("@XeroPurchaseOrderUID", po.XeroPurchaseOrderUID),
          Common.NewSqlParameter("@XeroContactId", po.XeroContactId),
          Common.NewSqlParameter("@PurchaseOrderNumber", po.PurchaseOrderNumber),
          Common.NewSqlParameter("@PurchaseOrderName", po.PurchaseOrderName),
          Common.NewSqlParameter("@IsDraft", po.IsDraft),
          Common.NewSqlParameter("@AbleCreatedUtc", po.AbleCreatedUtc),
          Common.NewSqlParameter("@AbleUpdatedUtc", po.AbleUpdatedUtc),
          Common.NewSqlParameter("@LastXeroSyncUtc", po.LastXeroSyncUtc),
          Common.NewSqlParameter("@LineAmountType", po.LineAmountType),
          Common.NewSqlParameter("@DeliveryDateUtc", po.DeliveryDateUtc));

        po.XeroPurchaseOrderId = newXeroPurchaseOrderId;
        return newXeroPurchaseOrderId;
      }

      public static bool UpdatePurchaseOrder(PurchaseOrderInfo po, bool setUpdateTime = true, bool reload = false) {

        if (po == null) throw new ArgumentException("po is null.");
        if (po.XeroPurchaseOrderId == 0) throw new ArgumentException("po.XeroPurchaseOrderId is zero.");

        int updatedId = Common.GetScalarQueryInt(@"
          UPDATE al_XeroPurchaseOrders SET 
            XeroPurchaseOrderUID = @XeroPurchaseOrderUID,
            XeroContactId        = @XeroContactId,
            PurchaseOrderNumber  = @PurchaseOrderNumber,
            PurchaseOrderName    = @PurchaseOrderName,
            IsDraft              = @IsDraft,
            AbleCreatedUtc       = @AbleCreatedUtc,
            AbleUpdatedUtc       = @AbleUpdatedUtc,
            LastXeroSyncUtc      = @LastXeroSyncUtc,
            LineAmountType       = @LineAmountType,
            DeliveryDateUtc      = @DeliveryDateUtc
          OUTPUT INSERTED.XeroPurchaseOrderId
          WHERE XeroPurchaseOrderId = @XeroPurchaseOrderId",

          Common.NewSqlParameter("@XeroPurchaseOrderId", po.XeroPurchaseOrderId),
          Common.NewSqlParameter("@XeroPurchaseOrderUID", po.XeroPurchaseOrderUID),
          Common.NewSqlParameter("@XeroContactId", po.XeroContactId),
          Common.NewSqlParameter("@PurchaseOrderNumber", po.PurchaseOrderNumber),
          Common.NewSqlParameter("@PurchaseOrderName", po.PurchaseOrderName),
          Common.NewSqlParameter("@IsDraft", po.IsDraft),
          Common.NewSqlParameter("@AbleCreatedUtc", po.AbleCreatedUtc),
          Common.NewSqlParameter("@AbleUpdatedUtc", DateTime.UtcNow),
          Common.NewSqlParameter("@LastXeroSyncUtc", po.LastXeroSyncUtc),
          Common.NewSqlParameter("@LineAmountType", po.LineAmountType),
          Common.NewSqlParameter("@DeliveryDateUtc", po.DeliveryDateUtc));

        if (updatedId != po.XeroPurchaseOrderId) return false;
        PurchaseOrderInfo ciReloaded;
        if (reload) {
          ciReloaded = GetPurchaseOrderInfo(po.XeroPurchaseOrderId);
          if (ciReloaded == null) return false;
          po = ciReloaded;
        }
        return true;
      }

      // Update PO Xero sync timestamp.
      public static int UpdateLatestZeroSyncTime(int xeroPurchaseOrderId) {
        return Common.UpdateSingleColumn(
          "al_XeroPurchaseOrders",
          new Common.SimpleWhereCondition("XeroPurchaseOrderId", xeroPurchaseOrderId),
          new Common.ColumnSetting("LastXeroSyncUtc", DateTime.UtcNow));
      }

      public static bool DeletePurchaseOrder(int xeroPurchaseOrderId) {
        int recordsAffected = Common.GetNonQueryInt(
          "DELETE FROM al_XeroPurchaseOrders WHERE XeroPurchaseOrderId = @XeroPurchaseOrderId",
          Common.NewSqlParameter("@XeroPurchaseOrderId", xeroPurchaseOrderId));
        return recordsAffected == 1;
      }

      public class PurchaseOrderInfo {

        public int XeroPurchaseOrderId { get; internal set; }
        public Guid? XeroPurchaseOrderUID { get; private set; }
        public int XeroContactId { get; private set; }
        public Guid XeroContactUID { get; private set; }
        public DateTime AbleCreatedUtc { get; private set; }
        public DateTime AbleUpdatedUtc { get; private set; }
        public string PurchaseOrderNumber { get; private set; }
        public string PurchaseOrderName { get; private set; }
        public DateTime? DeliveryDateUtc { get; private set; }
        public string PurchaseOrderType { get; private set; }
        public bool IsDraft { get; private set; }
        public string LineAmountType { get; private set; } // Note this is a check constraint in the db, limited to Null, "Inclusive", "Exclusive" and "NoTax".
        public DateTime? LastXeroSyncUtc { get; private set; }

        public PurchaseOrderInfo(string purchaseOrderNumber, string purchaseOrderName, int xeroContactId) {
          this.PurchaseOrderNumber = purchaseOrderNumber;
          this.PurchaseOrderName = purchaseOrderName;
          this.XeroContactId = xeroContactId;
          this.XeroContactUID = Guid.Empty;
          this.AbleCreatedUtc = DateTime.UtcNow;
          this.AbleUpdatedUtc = DateTime.UtcNow;
          this.IsDraft = false;
          this.LineAmountType = DefaultLineAmountType;
          this.PurchaseOrderType = DefaultPurchaseOrderType;
        }

        internal PurchaseOrderInfo (
          int xeroPurchaseOrderId,
          Guid? xeroPurchaseOrderUID,
          int xeroContactId,
          Guid xeroContactUID,
          DateTime ableCreatedUtc,
          DateTime ableUpdatedUtc,
          string purchaseOrderNumber,
          string purchaseOrderName,
          DateTime? deliveryDateUtc,
          bool isDraft,
          string lineAmountType,
          DateTime? lastXeroSyncUtc
        ) {
          this.XeroPurchaseOrderId = xeroPurchaseOrderId;
          this.XeroPurchaseOrderUID = xeroPurchaseOrderUID;
          this.XeroContactId = xeroContactId;
          this.XeroContactUID = xeroContactUID;
          this.AbleCreatedUtc = ableCreatedUtc;
          this.AbleUpdatedUtc = ableUpdatedUtc;
          this.PurchaseOrderNumber = purchaseOrderNumber;
          this.PurchaseOrderName = purchaseOrderName;
          this.DeliveryDateUtc = deliveryDateUtc;
          this.IsDraft = isDraft;
          this.LineAmountType = lineAmountType;
          this.LastXeroSyncUtc = lastXeroSyncUtc;
          this.PurchaseOrderType = DefaultPurchaseOrderType;
        }
      }

    }
  }
}

