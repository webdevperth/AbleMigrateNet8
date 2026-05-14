using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class ProjectInvoicing : AppCode.PageBaseClasses.ProjectPageBase {

    public DbHelper.InvoiceItems.InvoiceItemsByQuote InvoiceItemsByQuote;
    public List<DbHelper.InvoiceItems.ItemsNoInvoiceOrQuote> ItemsNoInvoiceOrQuote;
    public List<DbHelper.XeroContacts.XeroContactsInfo> XeroContactsList;
    public int ItemsToInvoiceCount = 0;
    public int ItemsWithoutQuoteCount = 0;
    public bool IsReadOnly = false;
    public bool CanDeleteInvoice = false, CanSubmitInvoice = false, CanAddDeleteItems = false, CanSendInvoiceToXero;

    public class FormFields {
      public const string InvoiceId = "InvoiceId";
      public const string InvoiceDate = "InvoiceDate";

      public const string ChangeInvItemId = "ChangeInvItemId";
      public const string RemoveInvItemId = "RemoveInvItemId";
      public const string DeleteInvItemId = "DeleteInvItemId";

      public const string AssignInvItemId = "AssignInvItemId";
      public const string AssignQuoteId = "AssignQuoteId";

      public const string InvItemDate = "InvItemDate";
      public const string InvItemDescription = "InvItemDescription";
      public const string InvItemUnitPrice = "InvItemUnitPrice";
      public const string InvItemQuantity = "InvItemQuantity";
      public const string InvItemGSTApplies = "InvItemGSTApplies";
      public const string InvItemQuoteId = "InvItemQuoteId";

      public const string InvoiceOrderNumber = "InvoiceOrderNumber";
      public const string InvoiceDescription = "InvoiceDescription";
      public const string InvoiceXeroContactId = "InvoiceXeroContactId";
      public const string InvoiceSelectedItemIds = "InvoiceSelectedItemIds";
      public const string InvoiceChkItemId = "InvoiceChkItemId";
    }

    class FormValues {
      public string InvItemDescription;
      public decimal InvItemUnitPrice;
      public decimal InvItemQuantity;
      public bool InvItemGSTApplies;
      public int InvItemQuoteId;

      public string InvoiceOrderNumber;
      public string InvoiceDescription;
      public int InvoiceXeroContactId;
      public List<int> InvoiceSelectedItemIds = new List<int>();
    }

    public class AjaxAction {
      public const string ChangeInvoiceDate = "ChangeInvDate";
      public const string ChangeItemDate = "ChangeItemDate";
      public const string RemoveItemFromQuote = "RemoveItemFromQuote";
      public const string DeleteInvoice = "DeleteInvoice";
      public const string DeleteInvItem = "DeleteInvItem";
      public const string AssignItemToQuote = "AssignItemToQuote";
      public const string AddInvoiceItem = "AddInvoiceItem";
      public const string SubmitInvoice = "SubmitInvoice";
    }

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Invoicing";

      IsReadOnly = !SessionHelper.AppAccess.Projects.CanEditProject(ProjectInfo);
      CanDeleteInvoice = !IsReadOnly && SessionHelper.AppAccess.Invoices.CanDeleteInvoice();
      CanSubmitInvoice = SessionHelper.AppAccess.Invoices.CanSubmitInvoice();
      CanAddDeleteItems = SessionHelper.AppAccess.Invoices.CanAddDeleteItems(ProjectInfo);
      XeroContactsList = DbHelper.XeroContacts.GetXeroContacts();
      CanSendInvoiceToXero = SessionHelper.AppAccess.Invoices.CanSendInvoiceToXero(ProjectInfo);

      InvoiceItemsByQuote = DbHelper.InvoiceItems.GetInvoiceItemsByQuote(ProjectInfo.JobNumber);
      ItemsNoInvoiceOrQuote = DbHelper.InvoiceItems.GetItemsNoInvoiceOrQuote(ProjectInfo.JobNumber);

      ItemsWithoutQuoteCount = 0;
      ItemsToInvoiceCount = 0;
      foreach (var item in ItemsNoInvoiceOrQuote) {
        if (item.QuoteId == null) ItemsWithoutQuoteCount++;
        if (item.InvoiceId == null && item.QuoteId != null) ItemsToInvoiceCount++;
      }

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          if (PageAjaxAction == AjaxAction.AssignItemToQuote) {
            AssignItemToQuote(ajax);
          } else if (PageAjaxAction == AjaxAction.RemoveItemFromQuote) {
            if (!CanAddDeleteItems) {
              ajax.RespondNoAccessToFunction();
            } else {
              RemoveItemFromQuote(ajax);
            }
          } else if (PageAjaxAction == AjaxAction.ChangeInvoiceDate) {
            ChangeInvoiceDate(ajax);
          } else if (PageAjaxAction == AjaxAction.ChangeItemDate) {
            ChangeItemDate(ajax);
          } else if (PageAjaxAction == AjaxAction.DeleteInvItem) {
            if (!CanAddDeleteItems) {
              ajax.RespondNoAccessToFunction();
            } else {
              DeleteInvItem(ajax);
            }
          } else if (PageAjaxAction == AjaxAction.DeleteInvoice) {
            if (!CanDeleteInvoice) {
              ajax.RespondNoAccessToFunction();
            } else {
              DeleteInvoice(ajax);
            }
          } else if (PageAjaxAction == AjaxAction.AddInvoiceItem) {
            if (!CanAddDeleteItems) {
              ajax.RespondNoAccessToFunction();
            } else {
              AddInvoiceItem(ajax);
            }
          } else if (PageAjaxAction == AjaxAction.SubmitInvoice) {
            if (!CanSubmitInvoice || !CanSendInvoiceToXero) {
              ajax.RespondNoAccessToFunction();
            } else {
              SubmitInvoice(ajax);
            }
          }
        });
        return;

      } else {
        ShowList();
      }
    }

    void ShowList() {

    }

    public string GetItemCheckboxName(int itemId) {
      return FormFields.InvoiceChkItemId + "_" + itemId;
    }

    void AssignItemToQuote(AjaxSubmitHelper ajax) {

      int invoiceItemId = ajax.CheckFieldIDOrNull(FormFields.AssignInvItemId, "", false, "") ?? 0;
      if (invoiceItemId == 0) {
        ajax.AddDialogMessage("Can't find invoice item ID.");
        return;
      }

      int quoteId = ajax.CheckFieldIDOrNull(FormFields.AssignQuoteId, "", false, "") ?? 0;
      if (quoteId == 0) {
        ajax.AddDialogMessage("Can't find quote ID.");
        return;
      }

      // Check invoice item and quote both belong to the current project.
      var invItem = DbHelper.InvoiceItems.GetInvoiceItemInfo(invoiceItemId);
      var quote = DbHelper.AbleQuotes.GetQuoteInfoOrNull(quoteId);
      if (invItem == null || invItem.JobNumber != ProjectInfo.JobNumber || quote == null || quote.JobNumber != ProjectInfo.JobNumber) {
        ajax.AddDialogMessage("Invalid details - please reload page and try again.");
        return;
      }

      // Set the quote id for the item.
      DbHelper.InvoiceItems.UpdateQuoteId(invItem.InvoiceItemId, quote.QuoteId);
      ajax.SetReloadPage();

    }

    void RemoveItemFromQuote(AjaxSubmitHelper ajax) {

      int invoiceItemId = ajax.CheckFieldIDOrNull(FormFields.RemoveInvItemId, "", false, "") ?? 0;
      if (invoiceItemId == 0) {
        ajax.AddDialogMessage("Can't find invoice item ID.");
        return;
      }

      // Check invoice item and quote both belong to the current project.
      var invItem = DbHelper.InvoiceItems.GetInvoiceItemInfo(invoiceItemId);
      if (invItem == null || invItem.QuoteId == null) return; // not found or not assigned.
      var quote = DbHelper.AbleQuotes.GetQuoteInfoOrNull((int)invItem.QuoteId);
      if (invItem.JobNumber != ProjectInfo.JobNumber || quote.JobNumber != ProjectInfo.JobNumber) {
        ajax.AddDialogMessage("Invalid details - please reload page and try again.");
        return;
      }

      DbHelper.InvoiceItems.UpdateQuoteId(invItem.InvoiceItemId, null);

      ajax.SetReloadPage();
    }

    void ChangeInvoiceDate(AjaxSubmitHelper ajax) {

      int invoiceId = ajax.CheckFieldIDOrNull(FormFields.InvoiceId, "", false, "") ?? 0;
      if (invoiceId == 0) {
        ajax.AddDialogMessage("Can't find invoice ID.");
        return;
      }

      // Check invoice belongs to the current project.
      var invoiceInfo = DbHelper.Invoices.GetInvoiceInfo(invoiceId);
      if (invoiceInfo == null || invoiceInfo.ProjectId != ProjectInfo.ProjectId) { // not found or not part of current project.
        ajax.AddDialogMessage("Invalid details - please reload page and try again.");
        return;
      }

      DateTimeOffset? momentDate = ajax.GetMomentFormatDate(FormFields.InvoiceDate, "", false, "");
      if (momentDate == null) {
        ajax.AddDialogMessage("Invalid Date.");
        return;
      }

      var newDateUtc = momentDate.Value.UtcDateTime;

      DbHelper.Invoices.UpdateInvoiceDate(invoiceId, newDateUtc);

      ajax.SetReloadPage();
    }

    void ChangeItemDate(AjaxSubmitHelper ajax) {

      int invoiceItemId = ajax.CheckFieldIDOrNull(FormFields.ChangeInvItemId, "", false, "") ?? 0;
      if (invoiceItemId == 0) {
        ajax.AddDialogMessage("Can't find invoice item ID.");
        return;
      }

      // Check invoice item belongs to the current project.
      var invItem = DbHelper.InvoiceItems.GetInvoiceItemInfo(invoiceItemId);
      if (invItem == null || invItem.JobNumber != ProjectInfo.JobNumber) { // not found or not part of current project.
        ajax.AddDialogMessage("Invalid details - please reload page and try again.");
        return;
      }

      DateTimeOffset? momentDate = ajax.GetMomentFormatDate(FormFields.InvItemDate, "", false, "");
      if (momentDate == null) {
        ajax.AddDialogMessage("Invalid Date.");
        return;
      }
      var newDateUtc = momentDate.Value.UtcDateTime;

      DbHelper.InvoiceItems.UpdateInvoiceDate(invItem.InvoiceItemId, newDateUtc);

      ajax.SetReloadPage();
    }

    void DeleteInvItem(AjaxSubmitHelper ajax) {

      int invoiceItemId = ajax.CheckFieldIDOrNull(FormFields.DeleteInvItemId, "", false, "") ?? 0;
      if (invoiceItemId == 0) {
        ajax.AddDialogMessage("Can't find invoice item ID.");
        return;
      }

      // Check invoice item belongs to the current project.
      var invItem = DbHelper.InvoiceItems.GetInvoiceItemInfo(invoiceItemId);
      if (invItem == null || invItem.JobNumber != ProjectInfo.JobNumber) { // not found or not part of current project.
        ajax.AddDialogMessage("Invalid details - please reload page and try again.");
        return;
      }

      DbHelper.InvoiceItems.DeleteInvoiceItemInProject(invItem.ProjectId, invItem.InvoiceItemId);

      ajax.SetReloadPage();
    }

    void DeleteInvoice(AjaxSubmitHelper ajax) {

      int invoiceId = ajax.CheckFieldIDOrNull(FormFields.InvoiceId, "", false, "") ?? 0;

      // Check if valid invoice id.
      var invoice = DbHelper.Invoices.GetInvoiceInfo(invoiceId);
      if (invoice == null || invoice.ProjectId != ProjectInfo.ProjectId) {
        ajax.AddDialogMessage("Invoice not part of this Project."); // Only happens if form is fiddled.
        return;
      }

      // Disallow delete if paid.
      if (invoice.PaidUtc != null) {
        ajax.AddDialogMessage("Cannot delete an invoice that has been paid.");
        return;
      }

      try {
        DbHelper.Invoices.DeleteInvoice(invoice.InvoiceId, invoice.ProjectId);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("DeleteInvoice")
          .FromSession()
          .WithProperty("InvoiceId", invoice.InvoiceId)
          .WithProperty("ProjectId", invoice.ProjectId)
          .WithProperty("JobNumber", ProjectInfo.JobNumber)
          .Track();
        ajax.AddDialogMessage("Problem deleting invoice, please try again later.<br/>" + (ConfigHelper.IsDevServer ? ex.Message : ""));
        return;
      }

      ajax.SetReloadPage();
    }

    void AddInvoiceItem(AjaxSubmitHelper ajax) {

      var formValues = new FormValues();
      formValues.InvItemDescription = ajax.CheckFieldRegex(FormFields.InvItemDescription, "Invoice Description", DbHelper.InvoiceItems.DescriptionMaxLength, AppHelper.Regex.GeneralText, true, "Use only plain text for Invoice Description");
      formValues.InvItemUnitPrice = ajax.CheckFieldDecimal(FormFields.InvItemUnitPrice, "Unit Price", false, null, null, true, "Please provide a valid amount.") ?? 0;
      formValues.InvItemQuantity = ajax.CheckFieldDecimal(FormFields.InvItemQuantity, "Quantity", false, 0, null, true, "Please provide a valid Quantity.") ?? 0;
      formValues.InvItemGSTApplies = ajax.CheckFieldBool(FormFields.InvItemGSTApplies, "1");
      formValues.InvItemQuoteId = ajax.CheckFieldID(FormFields.InvItemQuoteId, "Quote", true, "Quote is required.");
      if (ajax.BadFieldCount > 0) {
        ajax.AddDialogMessage("Please provide all details, including Program and Quote.");
        return;
      }
      // Check the quote ID is valid for this project.
      if (!InvoiceItemsByQuote.Quotes.Exists(q => q.QuoteId == formValues.InvItemQuoteId)) {
        ajax.AddDialogMessage("Problem assigning to Quote. Please reload page and try again.");
        return;
      }

      DbHelper.InvoiceItems.AddInvoiceItemToProgram(
        ProjectInfo.ProjectId,
        formValues.InvItemDescription,
        formValues.InvItemUnitPrice,
        formValues.InvItemQuantity,
        formValues.InvItemGSTApplies,
        formValues.InvItemQuoteId
      );

      ajax.SetReloadPage();
    }

    // Assign all CostItems in this Program to the selected (or new) Purchase Order.
    // If new PO, create it in the db first.
    void SubmitInvoice(AjaxSubmitHelper ajax) {

      var formValues = new FormValues();
      int newInvoiceId;

      formValues.InvoiceOrderNumber = ajax.CheckFieldRegex(FormFields.InvoiceOrderNumber, "Invoice Number", DbHelper.Invoices.InvoiceNumberMaxLength, AppHelper.Regex.GeneralText, false, "Use only plain text for Client Invoice Number");
      formValues.InvoiceDescription = ajax.CheckFieldRegex(FormFields.InvoiceDescription, "Invoice Description", DbHelper.Invoices.DescriptionMaxLength, AppHelper.Regex.GeneralText, false, "Use only plain text for Invoice Description");
      formValues.InvoiceXeroContactId = ajax.CheckFieldID(FormFields.InvoiceXeroContactId, "Xero Contact", true, "Invalid Xero Contact");

      if (ajax.BadFieldCount > 0) return;

      // Ensure selected items are valid in this project.
      InvoiceItemsByQuote.IterateInvoiceItems((q, i, ii) => {
        if (i.InvoiceId != null) return; // only items without an invoice id
        if (Request.Form[GetItemCheckboxName(ii.InvoiceItemId)] != null) {
          formValues.InvoiceSelectedItemIds.Add(ii.InvoiceItemId);
        }
      });

      if (formValues.InvoiceSelectedItemIds.Count == 0) {
        ajax.AddBadField(FormFields.InvoiceSelectedItemIds, "Select one or more items to include.");
        return;
      }

      // Find contact UID
      Guid xeroContactUID = Guid.Empty;
      try {
        xeroContactUID = DbHelper.XeroPurchaseOrders.GetXeroContactGuidOrNull(formValues.InvoiceXeroContactId) ?? Guid.Empty;
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("SubmitInvoice_GetXeroContactGuid")
          .FromSession()
          .WithProperty("InvoiceXeroContactId", formValues.InvoiceXeroContactId)
          .WithProperty("JobNumber", ProjectInfo.JobNumber)
          .Track();
        ajax.AddDialogMessage("Failed to find Xero Contact. " + (ConfigHelper.IsDevServer ? ex.ToString() : ""));
        return;
      }
      if (xeroContactUID.IsEmpty()) {
        ajax.AddDialogMessage("Can't find Xero Contact.");
        return;
      }

      string currentStep = "Add Invoice";
      bool transactionOk = false;
      Exception transactionEx = null;

      try {

        transactionOk = DbHelper.Common.UsingTransaction(trans => {

          newInvoiceId = DbHelper.Invoices.AddInvoice(trans,
            new DbHelper.Invoices.InvoiceInfo(
              0, ProjectInfo.ProjectId, DateTime.UtcNow,
              formValues.InvoiceOrderNumber, null, formValues.InvoiceDescription, formValues.InvoiceXeroContactId, null)
          );

          currentStep = "Get Invoice Info";
          var invoiceInfo = DbHelper.Invoices.GetInvoiceInfo(trans, newInvoiceId);

          currentStep = "Update Invoice Ids";
          int updatedInvoiceItems = DbHelper.InvoiceItems.UpdateInvoiceIds(trans, newInvoiceId, ProjectInfo.JobNumber, formValues.InvoiceSelectedItemIds);
          if (updatedInvoiceItems == 0) {
            return false; // Rollback if nothing updated.
          }

          currentStep = "Send EventGrid Message";
          if (!SendEventGridMessage(trans, invoiceInfo, xeroContactUID, ProjectInfo.ProjectName, formValues.InvoiceOrderNumber)) {
            return false; // Rollback if message not sent.
          }

          // EventGrid succeeded so update sync date.
          DbHelper.Invoices.UpdateXeroSyncTime(trans, invoiceInfo.InvoiceId);

          return true;
        });

      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("SubmitInvoice_Transaction")
          .FromSession()
          .WithProperty("CurrentStep", currentStep)
          .WithProperty("JobNumber", ProjectInfo.JobNumber)
          .WithProperty("XeroContactId", formValues.InvoiceXeroContactId)
          .Track();
        transactionEx = ex;
        transactionOk = false;
      }

      if (!transactionOk) {
        ajax.AddDialogMessage($"Failed to {currentStep}.", transactionEx);
        return;
      }

      ajax.SetRedirectUrl(PathHelper.Pages.ProjectInvoicing(ProjectInfo.JobNumber), "Invoice Completed.", AjaxSubmitHelper.PageMessageType.SuccessToast);
    }

    bool SendEventGridMessage(SqlTransaction trans, DbHelper.Invoices.InvoiceInfo invoiceInfo, Guid xeroContactUID, string projectName, string clientOrderNumber) {

      var eventGridData = new Integrations.EventGrid.Invoice.InvoiceInfo(
        ProjectInfo.JobNumber, DateTime.UtcNow, invoiceInfo.Description,
        invoiceInfo.InvoiceId, clientOrderNumber, xeroContactUID, projectName);

      var lineItems = DbHelper.InvoiceItems.GetItemsInInvoice(trans, invoiceInfo.InvoiceId);
      foreach (var item in lineItems) {
        eventGridData.AddLineItem(new Integrations.EventGrid.Invoice.LineItem(
          item.Description, item.UnitPrice, item.Quantity, item.XeroTaxType, ProjectInfo.XeroAccountCode));
      }
      var postResult = Integrations.EventGrid.Invoice.CreateInvoice(eventGridData);

      return postResult.Success;
    }

  }
}
