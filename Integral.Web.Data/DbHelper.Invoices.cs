using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class Invoices {

      private const string TblPfx = "inv";

      public const int InvoiceNumberMaxLength = 50;
      public const int DescriptionMaxLength = 200;

      public static InvoiceInfo GetInvoiceInfo(int invoiceId) {

        return GetInvoiceInfo(null, invoiceId);
      }

      public static InvoiceInfo GetInvoiceInfo(SqlTransaction trans, int invoiceId) {

        var invoices = GetInvoiceInfoList(trans, 1,
          null,
          $"{TblPfx}.InvoiceId = @InvoiceId",
          null,
          Common.NewSqlParameter("@InvoiceId", invoiceId));

        return invoices.IsNullOrEmpty() ? null : invoices[0];
      }

      // Get invoice & ensure it is part of intended project.
      public static InvoiceInfo GetInvoiceInfo(int invoiceId, int projectId) {

        var invoices = GetInvoiceInfoList(null, 1,
          null,
          $"{TblPfx}.InvoiceId = @InvoiceId AND {TblPfx}.ProjectId = @ProjectId",
          null,
          Common.NewSqlParameter("@InvoiceId", invoiceId),
          Common.NewSqlParameter("@ProjectId", projectId));

        return invoices.IsNullOrEmpty() ? null : invoices[0];
      }

      // Get all invoices in project.
      public static List<InvoiceInfo> GetItemsInProgram(int projectId) {

        var sqlParams = new List<SqlParameter>();
        sqlParams.Add(Common.NewSqlParameter("@ProjectId", projectId));

        return GetInvoiceInfoList(null, null,
          null,
          $"{TblPfx}.ProjectId = @ProjectId",
          $"{TblPfx}.CreatedUtc DESC",
          sqlParams.ToArray()
        );
      }

      private static List<InvoiceInfo> GetInvoiceInfoList(
        SqlTransaction trans,
        int? topOrNullForAll,
        string sqlExtraJoins,
        string sqlWhereConditions,
        string sqlOrderBy,
        params SqlParameter[] sqlWhereParams) {

        var CostItemList = new List<InvoiceInfo>();

        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);
        string sql = $@"
          SELECT {sqlTop}
            {TblPfx}.InvoiceId, {TblPfx}.ProjectId, {TblPfx}.CreatedUtc, {TblPfx}.InvoiceNumber,
            {TblPfx}.XeroInvoiceUID, {TblPfx}.Description, {TblPfx}.XeroContactId, {TblPfx}.PaidUtc
          FROM al_Invoice {TblPfx}
          {sqlExtraJoins.EmptyIfNull()}
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        Common.Query(trans, sql,
          dr => {
            CostItemList.Add(new InvoiceInfo(
              dr.GetInt("InvoiceId"),
              dr.GetInt("ProjectId"),
              dr.GetDateTime("CreatedUtc"),
              dr.GetString("InvoiceNumber"),
              dr.GetGuidOrNull("XeroInvoiceUID"),
              dr.GetString("Description"),
              dr.GetIntOrNull("XeroContactId"),
              dr.GetDateTimeOrNull("PaidUtc")
            ));
          },
          sqlWhereParams
        );

        return CostItemList;
      }

      public static int AddInvoice(SqlTransaction trans, InvoiceInfo invoiceInfo) {

        int newInvoiceId;

        newInvoiceId = Common.GetScalarQueryInt(trans, @"

          INSERT INTO al_Invoice
                 (ProjectId, InvoiceNumber, XeroInvoiceUID, Description, XeroContactId, PaidUtc)
          OUTPUT INSERTED.InvoiceId
          VALUES (@ProjectId, @InvoiceNumber, @XeroInvoiceUID, @Description, @XeroContactId, @PaidUtc)",

          Common.NewSqlParameter("@ProjectId", invoiceInfo.ProjectId),
          Common.NewSqlParameter("@InvoiceNumber", invoiceInfo.InvoiceNumber),
          Common.NewSqlParameter("@XeroInvoiceUID", invoiceInfo.XeroInvoiceUID),
          Common.NewSqlParameter("@Description", invoiceInfo.Description),
          Common.NewSqlParameter("@XeroContactId", invoiceInfo.XeroContactId),
          Common.NewSqlParameter("@PaidUtc", invoiceInfo.PaidUtc));

        invoiceInfo.InvoiceId = newInvoiceId;
        return newInvoiceId;
      }

      public static bool UpdateInvoiceDate(int invoiceId, DateTime invoiceDateUtc) {
        int recordsAffected = Common.GetNonQueryInt(
          "UPDATE al_Invoice SET CreatedUtc = @CreatedUtc WHERE InvoiceId = @InvoiceId",
          Common.NewSqlParameter("@CreatedUtc", invoiceDateUtc),
          Common.NewSqlParameter("@InvoiceId", invoiceId));
        return recordsAffected == 1;
      }

      public static bool UpdateInvoice(InvoiceInfo invoiceInfo, bool reload = false) {

        if (invoiceInfo == null) throw new ArgumentException("invoiceInfo is null.");

        int updatedId = Common.GetScalarQueryInt(@"

          UPDATE al_Invoice
          SET ProjectId =        @ProjectId,
              InvoiceNumber =    @InvoiceNumber,
              XeroInvoiceUID =   @XeroInvoiceUID,
              Description =      @Description,
              XeroContactId =    @XeroContactId,
              PaidUtc =          @PaidUtc
          OUTPUT INSERTED.InvoiceId
          WHERE ProjectId = @ProjectId
            AND InvoiceId = @InvoiceId",

          Common.NewSqlParameter("@ProjectId", invoiceInfo.ProjectId),
          Common.NewSqlParameter("@InvoiceId", invoiceInfo.InvoiceId),

          Common.NewSqlParameter("@ProjectId", invoiceInfo.ProjectId),
          Common.NewSqlParameter("@InvoiceNumber", invoiceInfo.InvoiceNumber),
          Common.NewSqlParameter("@XeroInvoiceUID", invoiceInfo.XeroInvoiceUID),
          Common.NewSqlParameter("@Description", invoiceInfo.Description),
          Common.NewSqlParameter("@XeroContactId", invoiceInfo.XeroContactId),
          Common.NewSqlParameter("@PaidUtc", invoiceInfo.PaidUtc));

        if (updatedId != invoiceInfo.InvoiceId) return false;
        InvoiceInfo ciReloaded;
        if (reload) {
          ciReloaded = GetInvoiceInfo(invoiceInfo.ProjectId, invoiceInfo.InvoiceId);
          if (ciReloaded == null) return false;
          invoiceInfo = ciReloaded;
        }
        return true;
      }

      // Update PO Xero sync timestamp.
      public static int UpdateXeroSyncTime(SqlTransaction trans, int invoiceId) {

        return Common.GetNonQueryInt(trans, $@"
          UPDATE al_Invoice SET
            LastXeroSyncUtc = GETUTCDATE()
          WHERE InvoiceId = @InvoiceId",
          Common.NewSqlParameter("InvoiceId", invoiceId)
        );
      }

      // Delete invoice but not InvoiceItems - the items are disconnected from the invoice and can be deleted separately.
      public static bool DeleteInvoice(int invoiceId, int projectId) {
        Common.UsingTransaction(trans => {
          Common.GetNonQueryInt(trans,
            "UPDATE al_InvoiceItem SET InvoiceId = NULL WHERE InvoiceId = @InvoiceId",
            Common.NewSqlParameter("@InvoiceId", invoiceId));
          Common.GetNonQueryInt(trans,
            "DELETE FROM al_Invoice WHERE ProjectId = @ProjectId AND InvoiceId = @InvoiceId",
            Common.NewSqlParameter("@ProjectId", projectId),
            Common.NewSqlParameter("@InvoiceId", invoiceId));
          return true;
        });
        return true;
      }

      public class InvoiceInfo {

        public int InvoiceId { get; internal set; }
        public int ProjectId { get; private set; }
        public DateTime CreatedUtc { get; private set; }
        public string InvoiceNumber { get; private set; }
        public Guid? XeroInvoiceUID { get; private set; }
        public string Description { get; private set; }
        public int? XeroContactId { get; private set; }
        public DateTime? PaidUtc { get; private set; }

        public InvoiceInfo() { }

        public InvoiceInfo(
          int invoiceId,
          int projectId,
          DateTime createdUtc,
          string invoiceNumber,
          Guid? xeroInvoiceUID,
          string description,
          int? xeroContactId,
          DateTime? paidUtc
        ) {
          this.InvoiceId = invoiceId;
          this.ProjectId = projectId;
          this.CreatedUtc = createdUtc;
          this.InvoiceNumber = invoiceNumber;
          this.XeroInvoiceUID = xeroInvoiceUID;
          this.Description = description;
          this.XeroContactId = xeroContactId;
          this.PaidUtc = paidUtc;
        }
      }

    }
  }
}

