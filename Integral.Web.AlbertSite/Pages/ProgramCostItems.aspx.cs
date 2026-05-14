using System;
using System.Collections.Generic;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class ProgramCostItems : AppCode.PageBaseClasses.ProgramPageBase {

    public bool IsNewCostItem = false;
    public int? UrlProgramCostItemId = null;
    public bool disableDelete = false;
    public bool IsReadOnly = false, CanEditCostAndQty = false;
    public const string disableDeleteMsg = "Can't delete Cost item if ...";
    public bool CanAddCostItem, CanEditCostItem, CanDeleteCostItem, CanSubmitPOtoXero, CanEditUnitPrice;
    public bool ProgramCostItemFormVisible = false;
    public bool ProgramCostItemListVisible = false;

    public List<DbHelper.ProgramCostItems.ProgramCostItemInfo> ProgramCostItemList = null;
    public DbHelper.ProgramCostItems.ProgramCostItemInfo CostItemInfo = null;

    public class FormFields {
      public const string ProgramCostItemId = "ProgramCostItemId";
      public const string ProgramJobId = "ProgramJobId";
      public const string XeroPurchaseOrderId = "XeroPurchaseOrderId";
      public const string NewXeroPurchaseOrderNumber = "NewXeroPurchaseOrderNumber";
      public const string NewXeroPurchaseOrderName = "NewXeroPurchaseOrderName";
      public const string NewXeroPurchaseOrderContactId = "NewXeroPurchaseOrderContactId";
      public const string Description = "Description";
      public const string Notes = "Notes";
      public const string Quantity = "Quantity";
      public const string UnitCost = "UnitCost";
      public const string UnitPrice = "UnitPrice";
      public const string GSTApplicable = "GSTApplicable";
      public const string BillToClient = "BillToClient";
      public const string CostIncurred = "CostIncurred";
      public const string SelectedItemIds = "SelectedItemIds";
      public const string ChkItemId = "ChkItemId";
      public const string MoveToProgramJobId = "MoveToProgramJobId";
      public const string QuoteItemId = "QuoteItemId";
    }

    class FormValues {
      public int ProgramCostItemId;
      public int ProgramJobId;
      public int XeroPurchaseOrderId;
      public string NewXeroPurchaseOrderNumber;
      public string NewXeroPurchaseOrderName;
      public int NewXeroPurchaseOrderContactId;
      public string Description;
      public string Notes;
      public decimal Quantity;
      public decimal UnitCost;
      public decimal UnitPrice;
      public bool GSTApplicable;
      public bool BillToClient;
      public DateTime? CostIncurredLocal;
      public string SelectedItemIds;
      public int? MoveToProgramJobId;
      public TimeZoneInfo timeZoneInfo = ConfigHelper.DefaultTimeZoneInfo;
      public int? QuoteItemId;
    }

    public class AjaxAction {
      public const string UpdateItem = "UpdateItem";
      public const string DeleteItem = "DeleteItem";
      public const string AssignPO = "AssignPO";
    }

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Program Cost Items";

      CanAddCostItem = SessionHelper.AppAccess.Programs.CanAddCostItem(ProgramInfo);
      CanSubmitPOtoXero = SessionHelper.AppAccess.Programs.CanSubmitPOtoXero(ProgramInfo);

      // Handle submitting the PO.
      if (SystemWeb.IsHttpPost && WebHelper.GetAjaxaction() == AjaxAction.AssignPO) {
        AjaxSubmitHelper.Process(ajax => {
          if (!CanSubmitPOtoXero) {
            ajax.RespondNoAccessToFunction();
          } else {
            AssignPurchaseOrder(ajax);
          }
        });
        return;
      }

      // Handle CostItem operations, or show main page.

      // Get CostItemId or 'new'.
      WebHelper.TryGetQueryStringIdOrNew(PathHelper.AbleUrlKeys.ProgramCostItemId,
        out UrlProgramCostItemId, out IsNewCostItem);

      // If not doing anything with CostItems, show main page.
      if (!SystemWeb.IsHttpPost && UrlProgramCostItemId == null && !IsNewCostItem) {
        ShowPage();
        return;
      }

      if (IsNewCostItem) {

        if (!CanAddCostItem) {
          WebHelper.Redirect(FallbackUrl);
          return;
        }

        CostItemInfo = new DbHelper.ProgramCostItems.ProgramCostItemInfo();

      } else if (UrlProgramCostItemId != null) {

        CostItemInfo = DbHelper.ProgramCostItems.GetCostItemInfo(UrlProgramCostItemId.Value, ProgramInfo.ProgramJobId);

        if (CostItemInfo == null) {
          WebHelper.Redirect(FallbackUrl);
          return;
        }
      }

      // Permissions pertaining to cost item.
      CanEditCostItem = SessionHelper.AppAccess.Programs.CanEditCostItem(ProgramInfo, CostItemInfo);
      CanEditCostAndQty = SessionHelper.AppAccess.Programs.CanEditCostAndQty(ProgramInfo, CostItemInfo);
      CanDeleteCostItem = SessionHelper.AppAccess.Programs.CanDeleteCostItem(ProgramInfo, CostItemInfo);
      CanEditUnitPrice = SessionHelper.AppAccess.Programs.CanOverwriteUnitPrice(ProjectInfo, ProgramInfo);

      if (!SystemWeb.IsHttpPost) {
        ShowForm();
        return;
      }

      // User is submitting form to add or update cost item, or is deleting a cost item.
      AjaxSubmitHelper.Process(ajax => {

        if (ajax.Action == AjaxAction.UpdateItem) {

          if (IsNewCostItem) {
            if (!CanAddCostItem) {
              ajax.RespondNoAccessToFunction();
              return;
            }
          } else {
            if (!CanEditCostItem && !CanEditUnitPrice) {
              ajax.RespondNoAccessToFunction();
              return;
            }
          }

          UpdateItem(ajax, UrlProgramCostItemId);
          return;

        } else if (ajax.Action == AjaxAction.DeleteItem) {

          if (!CanDeleteCostItem) {
            ajax.RespondNoAccessToFunction();
            return;
          }

          DeleteItem(ajax, (int)UrlProgramCostItemId);
          return;
        }
      });
    }

    void ShowPage() {

      PageTitle = "Cost Items";
      ProgramCostItemList = DbHelper.ProgramCostItems.GetItemsInProgram(ProgramInfo.ProgramJobId);
      ProgramCostItemListVisible = true;
    }

    public string GetCostIncurredDate() {
      if (CostItemInfo.CostIncurredUtc == null) return "";
      var localTime = (DateTime)(CostItemInfo.CostIncurredUtc.UtcToTZOrNull(ConfigHelper.DefaultTimeZoneInfo));
      return localTime.ToString("d MMM yyyy");
    }

    public string GetItemCheckBox(DbHelper.ProgramCostItems.ProgramCostItemInfo costItem) {
      if (costItem.XeroPurchaseOrderId != null) return "";
      return WebHelper.CustomCheckBox(FormFields.ChkItemId + "_" + costItem.ProgramCostItemId, costItem.ProgramCostItemId.ToString(), false, null);
    }

    public string GetDescriptionLink(DbHelper.ProgramCostItems.ProgramCostItemInfo costItem) {
      string html = costItem.Description.HTMLEncode();
      if (costItem.XeroPurchaseOrderId == null) {
        html += "<a title=\"Edit Cost Item\" class=\"ml5\" href=\""
          + PathHelper.Pages.ProgramCostItems_Edit(ProgramInfo.ProgramJobId, costItem.ProgramCostItemId)
          + "\">" + WebHelper.Icon.Edit + "</a>";
      }
      return html;
    }

    void ShowForm() {

      if (IsNewCostItem) {
        PageTitle = "Add Cost Item";
      } else {
        PageTitle = "Update Cost Item";
      }
      ProgramCostItemFormVisible = true;

    }

    public string GetXeroPurchaseOrderName() {
      return (CostItemInfo.XeroPurchaseOrderName + " (" + CostItemInfo.XeroPurchaseOrderNumber + ")").HTMLEncode();
    }

    public string GetNextPONumber() {
      // Hacky.. return next PO number as row count + 1.
      return "PO" + (DbHelper.XeroPurchaseOrders.GetRowCount() + 1).ToString("00000");
    }

    public string GetXeroContactOptionsHtml() {

      var xeroContacts = DbHelper.XeroContacts.GetXeroContacts();
      return WebHelper.GetXeroContactOptions(false, xeroContacts);
    }

    public string GetQuoteItemOptions() {

      string html = "<option value=\"\">[select quote item]</option>";

      var quoteItemsForList = DbHelper.AbleQuotes.GetQuoteItemsForList(ProgramInfo.ProgramJobNumber, false,
        DbHelper.ProgramComponents.KeyColumnEnum.ProgramCostItemId, UrlProgramCostItemId, ProgramInfo.ProgramJobId);

      foreach (var item in quoteItemsForList) {

        bool isSelected = item.QuoteItemId == CostItemInfo?.ComponentQuoteInfo?.QuoteItemId;
        decimal unallocated = item.TotalFunds - item.TotalRevenue;
        string optionText = item.CategoryName + " - " + WebHelper.HtmlToText(item.Description);

        html += "<option ";
        if (isSelected) html += "selected ";
        html += " value=\"" + item.QuoteItemId + "\">";
        if (unallocated == 0) {
          html += optionText.HTMLEncode();
        } else {
          html += optionText.LimitLengthTo(50, "...").HTMLEncode() + " (" + unallocated.ToString("C") + " unallocated)";
        }
        html += "</option>";
      }
      return html;
    }

    public string GetMoveToProgramOptions() {
      string optionsHtml = "<option value=\"\">[Move Workshop to Another Program]</option>";
      var programs = DbHelper.AblePrograms.GetProjectProgramsList(ProgramInfo.ProgramJobNumber);
      foreach (var program in programs) {
        if (program.ProgramJobId != ProgramInfo.ProgramJobId) {
          optionsHtml += WebHelper.GetSelectOptionHtml(program.ProgramJobId.ToString(), program.JobName, "");
        }
      }
      return optionsHtml;
    }

    // Assign all CostItems in this Program to the selected (or new) Purchase Order.
    // If new PO, create it in the db first.
    void AssignPurchaseOrder(AjaxSubmitHelper ajax) {

      var formValues = new FormValues();
      int newPurchaseOrderId;
      DbHelper.XeroPurchaseOrders.PurchaseOrderInfo po;

      // Add PO to db
      formValues.NewXeroPurchaseOrderNumber = ajax.CheckFieldRegex(FormFields.NewXeroPurchaseOrderNumber, "Purchase Order Number", AppHelper.Regex.GeneralText, true, "Use only plain text for Purchase Order Number");
      formValues.NewXeroPurchaseOrderName = ajax.CheckFieldRegex(FormFields.NewXeroPurchaseOrderName, "Purchase Order Name", AppHelper.Regex.GeneralText, true, "Use only plain text for Purchase Order Name");
      formValues.NewXeroPurchaseOrderContactId = ajax.CheckFieldID(FormFields.NewXeroPurchaseOrderContactId, "Xero Contact", true, "Invalid Xero Contact");
      if (ajax.BadFieldCount > 0) return;

      // Selected item IDs.
      formValues.SelectedItemIds = ajax.CheckFieldRegex(FormFields.SelectedItemIds, "Selected items", @"^[0-9]+(?:,[0-9]+)*$", false, "Select one or more items to include.");
      if (ajax.BadFieldCount > 0 || formValues.SelectedItemIds.IsNullOrEmpty()) {
        ajax.AddDialogMessage("Select one or more Cost Items to include.");
        return;
      }

      // Find contact id
      Guid? xeroContactUID = null;
      try {
        xeroContactUID = DbHelper.XeroPurchaseOrders.GetXeroContactGuidOrNull(formValues.NewXeroPurchaseOrderContactId);
      } catch (Exception e) {
        ajax.AddDialogMessage("Failed to find Xero Contact. " + (ConfigHelper.IsDevServer ? e.ToString() : ""));
        return;
      }
      if (xeroContactUID == null) {
        ajax.AddDialogMessage("Can't find Xero Contact.");
        return;
      }

      try {
        newPurchaseOrderId = DbHelper.XeroPurchaseOrders.AddPurchaseOrder(
          new DbHelper.XeroPurchaseOrders.PurchaseOrderInfo(
            formValues.NewXeroPurchaseOrderNumber,
            formValues.NewXeroPurchaseOrderName,
            formValues.NewXeroPurchaseOrderContactId
          )
        );
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("CompleteSelectedItems_AddPurchaseOrder")
          .FromSession()
          .WithProperty("PONumber", formValues.NewXeroPurchaseOrderNumber)
          .WithProperty("POName", formValues.NewXeroPurchaseOrderName)
          .WithProperty("JobNumber", ProgramInfo?.ProgramJobNumber)
          .Track();
        ajax.AddDialogMessage("Failed to add new PO. " + (ConfigHelper.IsDevServer ? ex.ToString() : ""));
        return;
      }
      formValues.XeroPurchaseOrderId = newPurchaseOrderId;

      try {
        po = DbHelper.XeroPurchaseOrders.GetPurchaseOrderInfo(formValues.XeroPurchaseOrderId);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("CompleteSelectedItems_GetPurchaseOrder")
          .FromSession()
          .WithProperty("XeroPurchaseOrderId", formValues.XeroPurchaseOrderId)
          .WithProperty("JobNumber", ProgramInfo?.ProgramJobNumber)
          .Track();
        ajax.AddDialogMessage("Failed to find Purchase Order. " + (ConfigHelper.IsDevServer ? ex.ToString() : ""));
        return;
      }
      if (po == null) {
        ajax.AddDialogMessage("Can't find selected Purchase Order.");
        return;
      }

      var itemIdList = formValues.SelectedItemIds.ToIntList();
      int updatedCostItems = 0;
      try {
        updatedCostItems = DbHelper.ProgramCostItems.UpdatePurchaseOrderIds(ProgramInfo.ProgramJobId, formValues.XeroPurchaseOrderId, itemIdList);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("CompleteSelectedItems_UpdatePurchaseOrderIds")
          .FromSession()
          .WithProperty("XeroPurchaseOrderId", formValues.XeroPurchaseOrderId)
          .WithProperty("ItemCount", itemIdList.Count)
          .WithProperty("JobNumber", ProgramInfo?.ProgramJobNumber)
          .Track();
        ajax.AddDialogMessage("Failed to assign Purchase Order IDs.");
        return;
      }
      if (updatedCostItems == 0) {
        ajax.AddDialogMessage("Can't assign Purchase Order IDs.");
        return;
      }

      if (!SendEventGridMessage(po)) {
        ajax.AddDialogMessage("Failed to send EventGrid message.");
        return;
      }

      // EventGrid succeeded so update sync date.
      DbHelper.XeroPurchaseOrders.UpdateLatestZeroSyncTime(po.XeroPurchaseOrderId);

      ajax.SetRedirectUrl(PathHelper.Pages.ProgramCostItems_List(ProgramInfo.ProgramJobId), "Purchase Order Completed.", AjaxSubmitHelper.PageMessageType.SuccessToast);
    }

    bool SendEventGridMessage(DbHelper.XeroPurchaseOrders.PurchaseOrderInfo po) {

      var eventGridData = new Integrations.EventGrid.PurchaseOrder.PurchaseOrderInfo(
        DateTime.UtcNow, po.XeroPurchaseOrderId, po.PurchaseOrderNumber, po.XeroContactUID, ProgramInfo.ProgramJobNumber);

      var costItems = DbHelper.ProgramCostItems.GetItemsInPurchaseOrder(po.XeroPurchaseOrderId);
      foreach (var ci in costItems) {
        eventGridData.AddLineItem(new Integrations.EventGrid.PurchaseOrder.LineItem(ci.Description, ci.Quantity, ci.UnitCost, ci.XeroTaxType));
      }
      var postResult = Integrations.EventGrid.PurchaseOrder.PostNewPurchaseOrder(eventGridData);

      return postResult.Success;
    }

    void UpdateItem(AjaxSubmitHelper ajax, int? updateItemId) {

      // Form validation.

      var formValues = new FormValues();
      formValues.ProgramCostItemId = ajax.CheckFieldID(FormFields.ProgramCostItemId, "Item ID", false, "Invalid Item ID");
      formValues.ProgramJobId = ProgramInfo.ProgramJobId;
      formValues.Description = ajax.CheckFieldRegex(FormFields.Description, "Description", AppHelper.Regex.GeneralText, true, "Use only text characters Description.");
      formValues.Notes = ajax.CheckFieldRegex(FormFields.Notes, "Notes", AppHelper.Regex.GeneralText, false, "Use only text characters in Notes.");

      formValues.CostIncurredLocal = ajax.GetDatePickerDateUnspecified(FormFields.CostIncurred, "Cost Incurred", false, "Please provide a valid date.");
      formValues.GSTApplicable = ajax.CheckFieldBool(FormFields.GSTApplicable, "1");

      // Removed from UI. If new set to false, otherwise use existing value.
      formValues.BillToClient = IsNewCostItem ? false : CostItemInfo.BillToClient;

      if (CanEditCostAndQty) {
        formValues.Quantity = ajax.CheckFieldDecimal(FormFields.Quantity, "Quantity", false, 0, null, true, "Please provide a valid Quantity.") ?? 0;
        formValues.UnitCost = ajax.CheckFieldDecimal(FormFields.UnitCost, "Unit Cost", false, null, null, true, "Please provide a valid amount.") ?? 0;

        if (CanEditUnitPrice) {
          formValues.UnitPrice = ajax.CheckFieldDecimal(FormFields.UnitPrice, "Unit Price", false, null, null, true, "Please provide a valid amount.") ?? 0;
        } else {
          formValues.UnitPrice = formValues.UnitCost * (1 + ProjectInfo.DefaultCostItemMarkupPercent.GetValueOrDefault(ConfigHelper.DefaultCostItemMarkupPercent));
        }

      } else {
        formValues.Quantity = CostItemInfo.Quantity;
        formValues.UnitCost = CostItemInfo.UnitCost;
        formValues.UnitPrice = CostItemInfo.UnitPrice;
      }

      if (SessionHelper.AppAccess.Programs.CanSetQuoteItem(ProgramInfo)) {
        formValues.QuoteItemId = ajax.CheckFieldIDOrNull(FormFields.QuoteItemId, "Quote Item", false, "");
        if (formValues.UnitPrice != 0 && formValues.QuoteItemId == null) {
          ajax.AddBadField(FormFields.QuoteItemId, "Quote Item is Required if Price is not zero.");
        }
      }

      if (ajax.BadFieldCount > 0) return;

      if (formValues.QuoteItemId != null) {

        var quoteItemsForList = DbHelper.AbleQuotes.GetQuoteItemsForList(ProgramInfo.ProgramJobNumber, false,
          DbHelper.ProgramComponents.KeyColumnEnum.ProgramCostItemId, UrlProgramCostItemId, ProgramInfo.ProgramJobId);

        var selectedQuoteItem = quoteItemsForList.Find(qi => qi.QuoteItemId == (int)formValues.QuoteItemId);

        if (selectedQuoteItem == null) {
          ajax.AddBadField(FormFields.QuoteItemId, "Invalid Quote Item");
          return;
        }

        // Ensure item price doesn't exceed available funds for selected quote item (quote item funds minus sum of all components attached to it).
        decimal existingRevenue = IsNewCostItem ? selectedQuoteItem.TotalRevenue : selectedQuoteItem.TotalRevenueIgnoredComponent;
        decimal allocatedRevenue = formValues.UnitPrice * formValues.Quantity;

        if (existingRevenue + allocatedRevenue > selectedQuoteItem.TotalFunds) {
          ajax.AddDialogMessage("Allocation exceeds quote item amount for"
            + "<br/>" + selectedQuoteItem.ProductTitle.HTMLEncode()
            + "<br/>Available Funds: <b>" + (selectedQuoteItem.TotalFunds - existingRevenue).ToString("C") + "</b>"
            + "<br/>Tried to assign: <b>" + allocatedRevenue.ToString("C") + "</b>");
          return;
        }
      }

      if (IsNewCostItem) {
        UpdateDb_AddNew(ajax, formValues);
        return;
      }

      // Update item.

      DbHelper.ProgramCostItems.ProgramCostItemInfo costItemInfo;

      try {
        costItemInfo = DbHelper.ProgramCostItems.GetCostItemInfo((int)updateItemId, ProgramInfo.ProgramJobId);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("UpdateItem_GetCostItemInfo")
          .FromSession()
          .WithProperty("ItemId", updateItemId)
          .WithProperty("ProgramJobId", ProgramInfo.ProgramJobId)
          .WithProperty("JobNumber", ProgramInfo?.ProgramJobNumber)
          .Track();
        ajax.AddDialogMessage("Error finding Item to update: " + ex.Message);
        return;
      }
      if (costItemInfo == null) {
        ajax.AddDialogMessage("Can't find Item to update.");
        return;
      }
      if (costItemInfo.ProgramJobId != ProgramInfo.ProgramJobId) {
        ajax.AddDialogMessage("Can't identify the Workshop to update.");
        return;
      }
      if (costItemInfo.ComponentQuoteInfo.IsComponentLocked) {
        ajax.AddDialogMessage("Item locked, cannot update.");
        return;
      }

      if (!UpdateDb_Existing(ajax, costItemInfo, formValues)) return;

      // Moving workshop to another program?
      // Note can only move within the same project (i.e. same JobNumber)
      if (!IsNewCostItem && SessionHelper.AppAccess.Programs.CanMoveToProgram(ProgramInfo)) {
        formValues.MoveToProgramJobId = ajax.CheckFieldIDOrNull(FormFields.MoveToProgramJobId, "Program", false, "");
        if (formValues.MoveToProgramJobId != null) {
          var moveToJob = DbHelper.AblePrograms.GetProgramInfoOrNull((int)formValues.MoveToProgramJobId);
          if (moveToJob != null && moveToJob.ProgramJobNumber == costItemInfo.ProgramJobNumber) {
            bool movedOk = DbHelper.ProgramCostItems.MoveItemToProgram(costItemInfo.ProgramCostItemId, moveToJob.ProgramJobId);
            if (!movedOk) {
              ajax.AddDialogMessage("Item updated, but unable to move to other Program.");
              return;
            }
          }
        }
      }

      ajax.SetRedirectUrl(PathHelper.Pages.ProgramCostItems_List());
    }

    bool DeleteItem(AjaxSubmitHelper ajax, int itemId) {

      if (IsNewCostItem || !CanAddCostItem) {
        ajax.AddDialogMessage("Deleting not allowed.");
        return false;
      }

      DbHelper.ProgramCostItems.ProgramCostItemInfo itemInfo;
      try {
        itemInfo = DbHelper.ProgramCostItems.GetCostItemInfo(itemId, ProgramInfo.ProgramJobId);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("DeleteItem_GetCostItemInfo")
          .FromSession()
          .WithProperty("ItemId", itemId)
          .WithProperty("ProgramJobId", ProgramInfo.ProgramJobId)
          .WithProperty("JobNumber", ProgramInfo?.ProgramJobNumber)
          .Track();
        ajax.AddDialogMessage("Error finding Item to delete: " + ex.Message);
        return false;
      }
      if (itemInfo == null) {
        ajax.AddDialogMessage("Can't find Item to delete.");
        return false;
      }
      if (itemInfo.ComponentQuoteInfo.IsComponentLocked) {
        ajax.AddDialogMessage("Item locked, cannot delete.");
        return false;
      }

      try {
        DbHelper.ProgramCostItems.DeleteCostItem(null, itemId);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("DeleteItem_DeleteCostItem")
          .FromSession()
          .WithProperty("ItemId", itemId)
          .WithProperty("ProgramJobId", ProgramInfo.ProgramJobId)
          .WithProperty("JobNumber", ProgramInfo?.ProgramJobNumber)
          .Track();
        ajax.AddDialogMessage("Can't delete cost item at this time.", ex);
        return false;
      }

      if (ajax.MessagesExist()) return false;
      ajax.SetRedirectUrl(PathHelper.Pages.ProgramCostItems_List(ProgramInfo.ProgramJobId));
      return true;
    }

    bool UpdateDb_AddNew(AjaxSubmitHelper ajax, FormValues formValues) {

      if (!IsNewCostItem || !CanAddCostItem) {
        ajax.AddDialogMessage("Add not allowed.");
        return false;
      }

      var componentQuoteInfo = new DbHelper.ProgramComponents.ComponentQuoteInfo();
      componentQuoteInfo.QuoteItemId = formValues.QuoteItemId;

      var newItemInfo = new DbHelper.ProgramCostItems.ProgramCostItemInfo(
        0, ProgramInfo.ProgramJobId, "",
        DateTime.UtcNow, DateTime.UtcNow, null,
        null, "", "",
        formValues.Description,
        formValues.Notes,
        formValues.Quantity,
        formValues.UnitCost,
        formValues.UnitPrice,
        formValues.GSTApplicable,
        formValues.CostIncurredLocal.ToUniversalTimeOrNull(null),
        formValues.BillToClient,
        null,
        componentQuoteInfo);

      try {
        DbHelper.ProgramCostItems.AddCostItem(newItemInfo);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("UpdateDb_AddNew_AddCostItem")
          .FromSession()
          .WithProperty("ProgramJobId", ProgramInfo.ProgramJobId)
          .WithProperty("Description", formValues.Description)
          .WithProperty("JobNumber", ProgramInfo?.ProgramJobNumber)
          .Track();
        ajax.AddDialogMessage("Error adding new Item: " + ex.Message);
        return false;
      }
      if (newItemInfo.ProgramCostItemId == 0) {
        ajax.AddDialogMessage("Unknown error adding new Item.");
        return false;
      }
      ajax.SetRedirectUrl(PathHelper.Pages.ProgramCostItems_List(ProgramInfo.ProgramJobId), "New Item Added.", AjaxSubmitHelper.PageMessageType.SuccessToast);
      return true;
    }

    bool UpdateDb_Existing(AjaxSubmitHelper ajax, DbHelper.ProgramCostItems.ProgramCostItemInfo costItemInfo, FormValues formValues) {

      costItemInfo.Description = formValues.Description;
      costItemInfo.Notes = formValues.Notes;

      costItemInfo.CostIncurredUtc = formValues.CostIncurredLocal.ToUniversalTimeOrNull(null);
      costItemInfo.Quantity = formValues.Quantity;
      costItemInfo.UnitCost = formValues.UnitCost;
      costItemInfo.UnitPrice = formValues.UnitPrice;
      costItemInfo.GSTApplicable = formValues.GSTApplicable;
      costItemInfo.BillToClient = formValues.BillToClient;

      if (SessionHelper.AppAccess.Programs.CanSetQuoteItem(ProgramInfo)) {
        costItemInfo.ComponentQuoteInfo.QuoteItemId = formValues.QuoteItemId;
      }

      try {
        DbHelper.ProgramCostItems.UpdateCostItem(costItemInfo);
      } catch (Exception ex) {
        ajax.AddDialogMessage("Error updating Item: " + ex.Message);
        return false;
      }
      ajax.SetRedirectUrl(PathHelper.Pages.ProgramCostItems_List(ProgramInfo.ProgramJobId), "Item Updated.", AjaxSubmitHelper.PageMessageType.SuccessToast);
      return true;
    }

    public string GetCostItemMarkupPercent() {

      decimal markupPercent = (ProjectInfo.DefaultCostItemMarkupPercent ?? ConfigHelper.DefaultCostItemMarkupPercent) * 100;

      string markupTooltipHtml = WebHelper.GetIconTooltip(
        iconType: WebHelper.ActionButtonTypeEnum.info,
        tooltipTitle: $"Cost Item Markup Percentage is {markupPercent:0.00}%",
        tooltipText: null);

      return WebHelper.GetCurrencyInput(
        labelHtml: "Unit Price:",
        inputName: FormFields.UnitPrice,
        value: CostItemInfo.UnitPrice,
        decimalPlaces: 2,
        inputCols: 2,
        rightHtml: CanEditUnitPrice ? string.Empty : markupTooltipHtml,
        isReadOnly: !(CanEditCostItem || CanEditUnitPrice));
    }

  }
}
