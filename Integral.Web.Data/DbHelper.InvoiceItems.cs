using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class InvoiceItems {

      private const string TblPfx = "pci";
      private const string PrjTblPfx = "prj";
      private const string InvTblPfx = "inv";
      private const string DefaultXeroTaxType = "INPUT";

      public const int DescriptionMaxLength = 200;

      // In most of these methods, caller must ensure access to the ProgramId is valid for purpose.

      // Get invoice item by id.
      public static InvoiceItemInfo GetInvoiceItemInfo(int invoiceItemId) {

        return GetInvoiceItemInfo(
          $"{TblPfx}.InvoiceItemId = @InvoiceItemId",
          Common.NewSqlParameter("@InvoiceItemId", invoiceItemId)
        );
      }

      // Get item by id, ensuring it is part of specific program.
      public static InvoiceItemInfo GetInvoiceItemInfo(int invoiceItemId, int projectId) {

        return GetInvoiceItemInfo(
          $"{TblPfx}.InvoiceItemId = @InvoiceItemId AND {TblPfx}.ProjectId = @ProjectId",
          Common.NewSqlParameter("@InvoiceItemId", invoiceItemId),
          Common.NewSqlParameter("@ProjectId", projectId)
        );
      }

      // Get invoice items in an invoice.
      public static List<InvoiceItemInfo> GetItemsInInvoice(SqlTransaction trans, int invoiceId) {

        return GetInvoiceItemInfoList(trans, null, "",
          $"{TblPfx}.InvoiceId = @InvoiceId",
          $"{TblPfx}.ProjectId, {TblPfx}.CreatedUtc",
          Common.NewSqlParameter("@InvoiceId", invoiceId)
        );
      }

      private static InvoiceItemInfo GetInvoiceItemInfo(string sqlWhereConditions, params SqlParameter[] sqlWhereParams) {

        var invoiceItemList = GetInvoiceItemInfoList(null, 1, "", sqlWhereConditions, "", sqlWhereParams);
        if (invoiceItemList.Count == 0) return null;
        else return invoiceItemList[0];
      }

      private static List<InvoiceItemInfo> GetInvoiceItemInfoList(
        SqlTransaction trans,
        int? topOrNullForAll,
        string sqlExtraJoins,
        string sqlWhereConditions,
        string sqlOrderBy,
        params SqlParameter[] sqlWhereParams) {

        var InvoiceItemList = new List<InvoiceItemInfo>();

        string sqlTop = topOrNullForAll == null ? "" : ("TOP " + topOrNullForAll);
        string sql = $@"
          SELECT {sqlTop}
            {TblPfx}.InvoiceItemId, {TblPfx}.CreatedUtc, {TblPfx}.InvoiceId, {TblPfx}.ProjectId,
            {TblPfx}.Description, {TblPfx}.UnitPrice, {TblPfx}.Quantity, {TblPfx}.XeroTaxType, {TblPfx}.QuoteId,
            {PrjTblPfx}.JobNumber, {PrjTblPfx}.ProjectName, {InvTblPfx}.InvoiceNumber,
            q.QuotePublicGuid
          FROM al_InvoiceItem {TblPfx}
          INNER JOIN al_Project {PrjTblPfx} ON {PrjTblPfx}.ProjectId = {TblPfx}.ProjectId
          LEFT OUTER JOIN al_Quote q ON q.QuoteId = {TblPfx}.QuoteId AND q.DeletedUtc IS NULL
          LEFT OUTER JOIN al_Invoice {InvTblPfx} ON {InvTblPfx}.InvoiceId = {TblPfx}.InvoiceId
          {sqlExtraJoins.EmptyIfNull()}
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}";

        Common.Query(trans, sql,
          dr => {
            InvoiceItemList.Add(new InvoiceItemInfo(
              dr.GetInt("InvoiceItemId"),
              dr.GetDateTime("CreatedUtc"),
              dr.GetString("Description"),
              dr.GetDecimal("UnitPrice"),
              dr.GetDecimal("Quantity"),
              XeroTaxType.GetGSTApplicableFromInvoiceTaxTypeOrNull(dr.GetString("XeroTaxType")) ?? false,
              dr.GetIntOrNull("InvoiceId"),
              dr.GetString("InvoiceNumber"),
              dr.GetInt("ProjectId"),
              dr.GetString("JobNumber"),
              dr.GetString("ProjectName"),
              dr.GetIntOrNull("QuoteId"),
              dr.GetGuidOrNull("QuotePublicGuid")
            ));
          },
          sqlWhereParams
        );

        return InvoiceItemList;
      }

      public class InvoiceItemsByQuote {
        public string JobNumber;
        public List<Quote> Quotes = new List<Quote>();
        public class Quote {
          public int QuoteId;
          public Guid QuoteGuid;
          public string QuoteName;
          public DateTime CreatedUtc;
          public decimal QuoteItemTotal;
          public DateTime? ClientAcceptedUtc;
          public decimal? ClientAcceptedAmt;
          public decimal InvoiceItemsAmt;
          public decimal InvoicedAmt;
          public decimal PaidAmountInvoices;
          public decimal ComponentsAmt;
          public decimal DeliveredAmt;
          public decimal AssignedToInvoiceItem;
          public bool IsAccepted => ClientAcceptedAmt != null;
          public List<Invoice> Invoices = new List<Invoice>();
          public class Invoice {
            public int? InvoiceId;
            public string InvoiceDescription;
            public string XeroContactId;
            public string XeroInvoiceUID;
            public string InvoiceNumber;
            public DateTime CreatedUtc;
            public DateTime? PaidUtc;
            public decimal InvoiceTotal;
            public List<InvoiceItem> InvoiceItems = new List<InvoiceItem>();
            public class InvoiceItem {
              public int InvoiceItemId;
              public DateTime CreatedUtc;
              public int ProjectId;
              public string Description;
              public decimal UnitPrice;
              public decimal Quantity;
              public bool GSTApplicable;
              public decimal InvoiceItemTotalComponentAllocated;
            }
          }
        }
        public void IterateInvoiceItems(Action<Quote, Quote.Invoice, Quote.Invoice.InvoiceItem> invoiceItemAction) {
          foreach (var quote in Quotes) {
            foreach (var invoice in quote.Invoices) {
              foreach (var invItem in invoice.InvoiceItems) {
                invoiceItemAction(quote, invoice, invItem);
              }
            }
          }
        }
      }

      public static InvoiceItemsByQuote GetInvoiceItemsByQuote(string jobNumber) {

        var result = new InvoiceItemsByQuote();
        result.JobNumber = jobNumber;
        InvoiceItemsByQuote.Quote quote = null;
        InvoiceItemsByQuote.Quote.Invoice invoice = null;
        InvoiceItemsByQuote.Quote.Invoice.InvoiceItem invoiceItem = null;

        Common.Query(@"
          SELECT
            q.JobNumber, q.QuoteId, q.QuotePublicGuid, q.ClientAcceptedUtc, q.ClientAcceptedAmount, q.CreatedUtc AS QuoteCreatedUtc,
            IIF(q.ProjectName <> '', q.ProjectName, prj.ProjectName) AS QuoteName,
            qi.QuoteItemTotal,
            qii.InvoiceItemsAmt, qii.InvoicedAmt, qii.PaidAmountInvoices,
            cmp.ComponentsAmt, cmp.DeliveredAmt, cmp.AssignedToInvoiceItem,
            i.InvoiceId, i.Description AS InvDescription, i.XeroContactId, i.XeroInvoiceUID, i.InvoiceNumber,
            i.CreatedUtc AS InvCreatedUtc, i.PaidUtc AS InvPaidUtc,
            ii.InvoiceItemId, ii.CreatedUtc AS InvItemCreatedUtc, ii.ProjectId, ii.Description AS InvItemDescription, ii.UnitPrice, ii.Quantity, ii.XeroTaxType,
            ISNULL(iicmp.SumComponentPrice, 0) AS InvoiceItemTotalComponentAllocated
          FROM al_Quote q
          INNER JOIN al_Project prj ON prj.JobNumber = q.JobNumber
          CROSS APPLY (
            SELECT SUM(qi.UnitPrice * qi.Quantity) AS QuoteItemTotal
            FROM al_QuoteItem qi
            WHERE qi.QuoteId = q.QuoteId
          ) AS qi
          CROSS APPLY (
            SELECT
              SUM(qii.UnitPrice * qii.Quantity) AS InvoiceItemsAmt,
              SUM(IIF(qii.InvoiceId IS NULL, 0, qii.UnitPrice * qii.Quantity)) AS InvoicedAmt,
              SUM(CASE WHEN ai.PaidUtc IS NULL THEN 0 ELSE (qii.UnitPrice * qii.Quantity) END) as PaidAmountInvoices
            FROM al_InvoiceItem qii
            INNER JOIN al_Invoice ai ON qii.InvoiceId = ai.InvoiceId
            WHERE qii.QuoteId = q.QuoteId
          ) AS qii
          CROSS APPLY (
            SELECT
              SUM(cmp.ComponentPrice) AS ComponentsAmt,
              SUM(IIF(cmp.CompletedDateUtc < GETUTCDATE(), cmp.ComponentPrice, 0)) AS DeliveredAmt,
              SUM(IIF(cmp.InvoiceItemId IS NOT NULL, cmp.ComponentPrice, 0)) AS AssignedToInvoiceItem
            FROM al_Component cmp
            INNER JOIN al_QuoteItem cqi ON cmp.QuoteItemId = cqi.QuoteItemId
            WHERE cqi.QuoteId = q.QuoteId
          ) AS cmp
          CROSS APPLY (
            SELECT SUM(cmp.ComponentPrice) AS SumComponentPrice
            FROM al_InvoiceItem ii
            LEFT JOIN al_Component cmp ON cmp.InvoiceItemId = ii.InvoiceItemId
            WHERE ii.QuoteId = q.QuoteId
          ) AS iicmp
          LEFT OUTER JOIN al_InvoiceItem ii ON ii.QuoteId = q.QuoteId
          LEFT OUTER JOIN al_Invoice i ON ii.InvoiceId = i.InvoiceId
          WHERE q.JobNumber = @JobNumber
            AND q.DeletedUtc IS NULL
          ORDER BY q.CreatedUtc, q.QuoteId, i.CreatedUtc, i.InvoiceId, ii.CreatedUtc, ii.InvoiceItemId;",
          dr => {
            int quoteId = dr.GetInt("QuoteId");
            if (quote == null || quoteId != quote.QuoteId) {
              quote = new InvoiceItemsByQuote.Quote() {
                QuoteId = quoteId,
                QuoteGuid = dr.GetGuid("QuotePublicGuid"),
                QuoteName = dr.GetString("QuoteName"),
                CreatedUtc = dr.GetDateTime("QuoteCreatedUtc"),
                QuoteItemTotal = dr.GetDecimal("QuoteItemTotal"),
                ClientAcceptedUtc = dr.GetDateTimeOrNull("ClientAcceptedUtc"),
                ClientAcceptedAmt = dr.GetDecimalOrNull("ClientAcceptedAmount"),
                InvoiceItemsAmt = dr.GetDecimalOrNull("InvoiceItemsAmt") ?? 0,
                InvoicedAmt = dr.GetDecimalOrNull("InvoicedAmt") ?? 0,
                PaidAmountInvoices = dr.GetDecimalOrDefault("PaidAmountInvoices", 0),
                ComponentsAmt = dr.GetDecimalOrNull("ComponentsAmt") ?? 0,
                AssignedToInvoiceItem = dr.GetDecimalOrDefault("AssignedToInvoiceItem", 0),
                DeliveredAmt = dr.GetDecimalOrNull("DeliveredAmt") ?? 0
              };
              result.Quotes.Add(quote);
            }
            int? invoiceId = dr.GetIntOrNull("InvoiceId");
            if (invoice == null || invoice.InvoiceId != invoiceId) {
              if (invoiceId == null) {
                invoice = new InvoiceItemsByQuote.Quote.Invoice();
              } else {
                invoice = new InvoiceItemsByQuote.Quote.Invoice() {
                  InvoiceId = invoiceId,
                  InvoiceNumber = dr.GetString("InvoiceNumber"),
                  InvoiceDescription = dr.GetString("InvDescription"),
                  CreatedUtc = dr.GetDateTime("InvCreatedUtc"),
                  PaidUtc = dr.GetDateTimeOrNull("InvPaidUtc"),
                  XeroContactId = dr.GetString("XeroContactId"),
                  XeroInvoiceUID = dr.GetString("XeroInvoiceUID"),
                  InvoiceTotal = 0
                };
              }
              quote.Invoices.Add(invoice);
            }
            if (dr.IsDBNull("InvoiceItemId")) return;
            int invoiceItemId = dr.GetInt("InvoiceItemId");
            if (invoiceItem == null || invoiceItem.InvoiceItemId != invoiceItemId) {
              invoiceItem = new InvoiceItemsByQuote.Quote.Invoice.InvoiceItem() {
                CreatedUtc = dr.GetDateTime("InvItemCreatedUtc"),
                InvoiceItemId = invoiceItemId,
                Description = dr.GetString("InvItemDescription"),
                ProjectId = dr.GetInt("ProjectId"),
                UnitPrice = dr.GetDecimal("UnitPrice"),
                Quantity = dr.GetDecimal("Quantity"),
                GSTApplicable = XeroTaxType.GetGSTApplicableFromInvoiceTaxTypeOrNull(dr.GetString("XeroTaxType")) ?? false,
                InvoiceItemTotalComponentAllocated = dr.GetDecimal("InvoiceItemTotalComponentAllocated")
              };
              invoice.InvoiceItems.Add(invoiceItem);
              invoice.InvoiceTotal += invoiceItem.UnitPrice * invoiceItem.Quantity;
            }
          },
          Common.NewSqlParameter("JobNumber", jobNumber),
          Common.NewSqlParameter("@WorkshopStatus_Confirmed", WorkshopStatus.WorkshopStatus_Confirmed.WorkshopStatusId),
          Common.NewSqlParameter("@WorkshopStatus_Cancelled", WorkshopStatus.WorkshopStatus_Cancelled.WorkshopStatusId));
        return result;
      }

      public class ItemsNoInvoiceOrQuote {
        public int InvoiceItemId;
        public string Description;
        public decimal UnitPrice;
        public decimal Quantity;
        public int? InvoiceId;
        public string InvoiceNumber;
        public int? QuoteId;
        public string QuoteName;
      }

      public static List<ItemsNoInvoiceOrQuote> GetItemsNoInvoiceOrQuote(string jobNumber) {

        var result = new List<ItemsNoInvoiceOrQuote>();

        Common.Query(@"
          SELECT
            ii.InvoiceItemId, ii.Description AS ItemDescription, ii.UnitPrice, ii.Quantity,
            ii.InvoiceId, i.InvoiceNumber,
            ii.QuoteId, q.ProjectName AS QuoteName
          FROM al_InvoiceItem ii
          INNER JOIN al_Project prj ON prj.ProjectId = ii.ProjectId
          LEFT OUTER JOIN al_Invoice i ON i.InvoiceId = ii.InvoiceId
          LEFT OUTER JOIN al_Quote q ON q.QuoteId = ii.QuoteId AND q.DeletedUtc IS NULL
          WHERE prj.JobNumber = @JobNumber
            AND (ii.QuoteId IS NULL OR ii.InvoiceId IS NULL)
          ORDER BY ISNULL(ii.InvoiceId, ii.QuoteId)",
          dr => {
            var item = new ItemsNoInvoiceOrQuote() {
              InvoiceItemId = dr.GetInt("InvoiceItemId"),
              Description = dr.GetString("ItemDescription"),
              UnitPrice = dr.GetDecimal("UnitPrice"),
              Quantity = dr.GetDecimal("Quantity"),
              InvoiceId = dr.GetIntOrNull("InvoiceId"),
              InvoiceNumber = dr.GetString("InvoiceNumber"),
              QuoteId = dr.GetIntOrNull("QuoteId"),
              QuoteName = dr.GetString("QuoteName")
            };
            result.Add(item);
          },
          Common.NewSqlParameter("JobNumber", jobNumber)
        );

        return result;
      }

      public static int AddInvoiceItemToProgram(
        int projectId, string invDescription, decimal invUnitPrice,
        decimal invQuantity, bool invGSTApplies, int? quoteId) {

        return Common.GetScalarQueryInt(@"
          INSERT INTO al_InvoiceItem (ProjectId, Description, UnitPrice, Quantity, XeroTaxType, QuoteId)
          OUTPUT INSERTED.InvoiceItemId
          VALUES (@ProjectId, @Description, @UnitPrice, @Quantity, @XeroTaxType, @QuoteId)",
          Common.NewSqlParameter("@ProjectId", projectId),
          Common.NewSqlParameter("@Description", invDescription),
          Common.NewSqlParameter("@UnitPrice", invUnitPrice),
          Common.NewSqlParameter("@Quantity", invQuantity),
          Common.NewSqlParameter("@QuoteId", quoteId),
          Common.NewSqlParameter("@XeroTaxType", XeroTaxType.GetInvoiceTaxTypeFromGSTApplicable(invGSTApplies))
        );
      }

      // Update InvoiceIds for given items.
      public static int UpdateInvoiceIds(SqlTransaction trans, int invoiceId, string jobNumber, List<int> updateItemIds) {
        // Only update if component is either not present or not locked.

        return Common.GetNonQueryInt(trans, $@"

          UPDATE ii
          SET ii.InvoiceId = @InvoiceId
          FROM al_InvoiceItem ii
          INNER JOIN al_Project prj ON prj.ProjectId = ii.ProjectId
          WHERE InvoiceItemId IN({updateItemIds.ToStringList()})
            AND prj.JobNumber = @JobNumber",

          Common.NewSqlParameter("InvoiceId", invoiceId),
          Common.NewSqlParameter("JobNumber", jobNumber));
      }

      public static bool UpdateInvoiceItem(InvoiceItemInfo invoiceItemInfo, bool reload = false) {

        if (invoiceItemInfo == null) throw new ArgumentException("ci is null.");

        int updatedId = Common.GetScalarQueryInt(@"

          UPDATE al_InvoiceItem
          SET InvoiceId = @InvoiceId,
              ProjectId = @ProjectId,
              Description = @Description,
              UnitPrice = @UnitPrice,
              Quantity = @Quantity,
              XeroTaxType = @XeroTaxType,
              QuoteId = @QuoteId
          OUTPUT INSERTED.InvoiceItemId
          WHERE InvoiceItemId = @InvoiceItemId",

          Common.NewSqlParameter("@InvoiceItemId", invoiceItemInfo.InvoiceItemId),

          Common.NewSqlParameter("@InvoiceId", invoiceItemInfo.InvoiceId),
          Common.NewSqlParameter("@ProjectId", invoiceItemInfo.ProjectId),
          Common.NewSqlParameter("@Description", invoiceItemInfo.Description),
          Common.NewSqlParameter("@UnitPrice", invoiceItemInfo.UnitPrice),
          Common.NewSqlParameter("@Quantity", invoiceItemInfo.Quantity),
          Common.NewSqlParameter("@QuoteId", invoiceItemInfo.QuoteId),
          Common.NewSqlParameter("@XeroTaxType", XeroTaxType.GetInvoiceTaxTypeFromGSTApplicable(invoiceItemInfo.GSTApplicable)));

        if (updatedId != invoiceItemInfo.InvoiceItemId) return false;
        InvoiceItemInfo ciReloaded;
        if (reload) {
          ciReloaded = GetInvoiceItemInfo(invoiceItemInfo.ProjectId, invoiceItemInfo.InvoiceItemId);
          if (ciReloaded == null) return false;
          invoiceItemInfo = ciReloaded;
        }
        return true;
      }

      public static bool UpdateInvoiceDate(int invoiceItemId, DateTime invoiceDateUtc) {
        int recordsAffected = Common.GetNonQueryInt(
          "UPDATE al_InvoiceItem SET CreatedUtc = @CreatedUtc WHERE InvoiceItemId = @InvoiceItemId",
          Common.NewSqlParameter("@CreatedUtc", invoiceDateUtc),
          Common.NewSqlParameter("@InvoiceItemId", invoiceItemId));
        return recordsAffected == 1;
      }

      public static bool UpdateQuoteId(int invoiceItemId, int? quoteId) {
        // Only update if component is either not present or not locked.

        return Common.GetNonQueryInt(@"

          UPDATE ii
          SET ii.QuoteId = @QuoteId
          FROM al_InvoiceItem ii
          LEFT OUTER JOIN al_Component cmp ON cmp.InvoiceItemId = ii.InvoiceItemId
          WHERE ii.InvoiceItemId = @InvoiceItemId
            AND cmp.LockedDateUtc IS NULL;",

          Common.NewSqlParameter("@QuoteId", quoteId),
          Common.NewSqlParameter("@InvoiceItemId", invoiceItemId)

        ) == 1;
      }

      public static bool DeleteInvoiceItemInProject(int projectId, int invoiceItemId) {
        // Only delete if component is either not present or not locked.

        return Common.GetScalarQueryInt(@"

          BEGIN TRANSACTION;

          UPDATE al_Component
          SET InvoiceItemId = NULL
          WHERE InvoiceItemId = @InvoiceItemId
            AND LockedDateUtc IS NULL;

          DELETE ii
          FROM al_InvoiceItem ii
          LEFT OUTER JOIN al_Component cmp ON cmp.InvoiceItemId = ii.InvoiceItemId
          WHERE ii.InvoiceItemId = @InvoiceItemId
            AND ii.ProjectId = @ProjectId
            AND cmp.LockedDateUtc IS NULL;

          SELECT @@ROWCOUNT;

          COMMIT TRANSACTION;",

          Common.NewSqlParameter("@ProjectId", projectId),
          Common.NewSqlParameter("@InvoiceItemId", invoiceItemId)

        ) == 1;
      }

      public static List<InvoiceItemsAmount> GetInvoiceItemsAmounts(string jobNumber) {

        var result = new List<InvoiceItemsAmount>();

        Common.Query($@"
          SELECT
            ii.InvoiceItemId, ii.InvoiceId, ii.ProjectId, ii.Description, ii.QuoteId, ii.UnitPrice, ii.Quantity, (ii.UnitPrice * ii.Quantity) as InvoiceItemTotal,
            ISNULL(cmp.AllocatedAmount, 0) AS AllocatedAmount,
            ((ii.UnitPrice * ii.Quantity) - ISNULL(cmp.AllocatedAmount, 0)) as UnallocatedAmount
          FROM al_InvoiceItem ii
          OUTER APPLY (
            SELECT SUM(cmp.ComponentPrice) AS AllocatedAmount
            FROM al_Component cmp
            WHERE cmp.InvoiceItemId = ii.InvoiceItemId
          ) AS cmp
          LEFT JOIN al_Quote quo on quo.QuoteId = ii.QuoteId
          WHERE quo.JobNumber = @JobNumber",
          dr => {
            result.Add(new InvoiceItemsAmount(
              dr.GetInt("InvoiceItemId"),
              dr.GetIntOrNull("InvoiceId"),
              dr.GetInt("ProjectId"),
              dr.GetString("Description"),
              dr.GetIntOrNull("QuoteId"),
              dr.GetDecimalOrDefault("UnitPrice", 0),
              dr.GetDecimalOrDefault("Quantity", 0),
              dr.GetDecimalOrDefault("InvoiceItemTotal", 0),
              dr.GetDecimalOrDefault("AllocatedAmount", 0),
              dr.GetDecimalOrDefault("UnallocatedAmount", 0)
            ));
          },
          Common.NewSqlParameter("JobNumber", jobNumber)
        );

        return result;
      }

      public class InvoiceItemsAmount {
        public int InvoiceItemId { get; private set; }
        public int? InvoiceId { get; private set; }
        public int ProjectId { get; private set; }
        public string Description { get; private set; }
        public int? QuoteId { get; private set; }
        public decimal UnitPrice { get; private set; }
        public decimal Quantity { get; private set; }
        public decimal InvoiceItemTotal { get; private set; }
        public decimal AllocatedAmount { get; private set; }
        public decimal UnallocatedAmount { get; private set; }
        public InvoiceItemsAmount(
          int invoiceItemId,
          int? invoiceId,
          int projectId,
          string description,
          int? quoteId,
          decimal unitPrice,
          decimal quantity,
          decimal invoiceItemTotal,
          decimal allocatedAmount,
          decimal unallocatedAmount
        ) {
          this.InvoiceItemId = invoiceItemId;
          this.InvoiceId = invoiceId;
          this.ProjectId = projectId;
          this.Description = description;
          this.QuoteId = quoteId;
          this.UnitPrice = unitPrice;
          this.Quantity = quantity;
          this.InvoiceItemTotal = invoiceItemTotal;
          this.AllocatedAmount = allocatedAmount;
          this.UnallocatedAmount = unallocatedAmount;
        }
      }

      public class InvoiceItemInfo {

        public int InvoiceItemId { get; internal set; }
        public DateTime CreatedUtc { get; private set; }
        public string Description { get; private set; }
        public decimal UnitPrice { get; private set; }
        public decimal Quantity { get; private set; }
        public bool GSTApplicable { get; set; }
        public int? InvoiceId { get; private set; }
        public int? QuoteId { get; private set; }
        public Guid? QuoteGuid { get; private set; }
        public string InvoiceNumber { get; private set; }
        public int ProjectId { get; private set; }
        public string JobNumber { get; private set; }
        public string ProjectName { get; private set; }

        public InvoiceItemInfo() { }

        public InvoiceItemInfo(
          int invoiceItemId,
          DateTime createdUtc,
          string description,
          decimal unitPrice,
          decimal quantity,
          bool gstApplicable,
          int? invoiceId,
          string invoiceNumber,
          int projectId,
          string jobNumber,
          string projectName,
          int? quoteId,
          Guid? quoteGuid
        ) {
          this.InvoiceItemId = invoiceItemId;
          this.CreatedUtc = createdUtc;
          this.Description = description;
          this.UnitPrice = unitPrice;
          this.Quantity = quantity;
          this.GSTApplicable = gstApplicable;

          this.InvoiceId = invoiceId;
          this.InvoiceNumber = invoiceNumber;

          this.ProjectId = projectId;
          this.JobNumber = jobNumber;
          this.ProjectName = projectName;

          this.QuoteId = quoteId;
          this.QuoteGuid = quoteGuid;
        }

        public string XeroTaxType => DbHelper.XeroTaxType.GetInvoiceTaxTypeFromGSTApplicable(this.GSTApplicable);
      }

    }
  }
}

