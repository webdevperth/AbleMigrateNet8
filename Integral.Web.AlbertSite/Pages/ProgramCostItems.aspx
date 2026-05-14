<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ProgramCostItems.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ProgramCostItems"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <% if (ProgramCostItemListVisible) { %>

    <% if (CanSubmitPOtoXero) { %>

      <form class="form-horizontal" id="formNewPO" action="#" method="post" onsubmit="return false;">
        <input type="hidden" name="<%= PathHelper.FormKeys.AjaxAction %>" value="<%= AjaxAction.AssignPO %>" />
        <input type="hidden" name="<%= FormFields.SelectedItemIds %>" value="" />

        <div class="mb5">New PO Number:</div>
        <div class="mb20"><input class="form-control" type="text" name="<%= FormFields.NewXeroPurchaseOrderNumber %>" size="10" value="<%= GetNextPONumber() %>" /></div>

        <div class="mt20 mb5">New PO Name:</div>
        <div class="mb20"><input class="form-control" type="text" name="<%= FormFields.NewXeroPurchaseOrderName %>" size="40" /></div>

        <div class="mt20 mb5">Xero Contact:</div>
        <div class="flex gap30 flex-align-center flex-wrap">
          <div class="flex0">
            <%= WebHelper.GetSelect(new WebHelper.SelectInfo(
                    FormFields.NewXeroPurchaseOrderContactId,
                    GetXeroContactOptionsHtml())) %>
          </div>
          <div class="flex1">
            <button class="btn btn-primary" id="btnAssignPO">Submit Purchase Order To Xero</button>
          </div>
        </div>

      </form>

    <% } %>

    <% if (CanAddCostItem) { %>
      <div class="content-action-bar mt20">
        <div class="right">
          <a href="<%= PathHelper.Pages.ProgramCostItems_Add(ProgramInfo.ProgramJobId) %>" class="btn btn-primary">Add Cost Item</a>
        </div>
      </div>
    <% } %>

    <div class="table-responsive">
      <table class="tblProgramCostItems table table-bordered table-rowlink" data-rowlink-url="<%= PathHelper.Pages.ProgramCostItems_Edit(ProgramInfo.ProgramJobId, null) %>">
        <thead>
          <tr>
            <% if (CanSubmitPOtoXero) { %>
            <th class="type-checkbox">Add to PO</th>
            <% } %>
            <th class="type-description">Description / Link</th>
            <th class="type-xeronumber">Xero PO Number</th>
            <th class="type-qty">Qty</th>
            <th class="type-money">Unit Cost</th>
            <th class="type-money">Unit Price</th>
            <th class="type-yesno">GST</th>
            <th class="type-yesno">Bill Client</th>
            <th class="type-money">Cost Incurred</th>
          </tr>
        </thead>
        <tbody>
          <% foreach (var costItem in ProgramCostItemList) { %>
            <tr tabindex="0" class="rowData" data-rowlink-id="<%= costItem.ProgramCostItemId %>">
              <% if (CanSubmitPOtoXero) { %>
                <td class="type-checkbox"><%= GetItemCheckBox(costItem) %></td>
              <% } %>
              <td class="type-description"><%= GetDescriptionLink(costItem) %></td>
              <td class="type-xeronumber"><%= costItem.XeroPurchaseOrderNumber.HTMLEncode() %></td>
              <td class="type-qty"><%= costItem.Quantity.ToString() %></td>
              <td class="type-money"><%= costItem.UnitCost.ToString("C") %></td>
              <td class="type-money"><%= costItem.UnitPrice.ToString("C") %></td>
              <td class="type-yesno"><%= costItem.GSTApplicable ? "Yes" : "No" %></td>
              <td class="type-yesno"><%= costItem.BillToClient ? "Yes" : "No" %></td>
              <td class="type-money"><%= costItem.CostIncurredUtc.UtcToTZOrNull(ConfigHelper.DefaultTimeZoneInfo).ToString("d MMM yyyy") %></td>
            </tr>
          <% } %>
        </tbody>
      </table>
    </div>

  <% } %>

  <% if (ProgramCostItemFormVisible) { %>

    <form id="formItem" method="post" action="#" onsubmit="return false" class="form-horizontal">

      <input type="hidden" name="<%= FormFields.ProgramJobId %>" value="<%= ProgramInfo.ProgramJobId %>" />
      <input type="hidden" name="<%= FormFields.ProgramCostItemId %>" value="<%= IsNewCostItem ? "" : CostItemInfo.ProgramCostItemId.ToString() %>" />

      <%= WebHelper.GetTextInput("Description:", FormFields.Description, CostItemInfo.Description, 7, "", !CanEditCostItem) %>
      <%= WebHelper.GetTextArea("Notes:", FormFields.Notes, 2, 7, CostItemInfo.Notes, "", !CanEditCostItem) %>

      <%= WebHelper.GetTextInput("Quantity:", FormFields.Quantity, CostItemInfo.Quantity.ToString(), 2, "", !CanEditCostAndQty) %>

      <%= WebHelper.GetCurrencyInput("Unit Cost:", FormFields.UnitCost, CostItemInfo.UnitCost, 2, 2, "", !CanEditCostAndQty) %>
      <%= GetCostItemMarkupPercent() %>

      <%= WebHelper.CustomCheckBoxRow("GST Applicable:", FormFields.GSTApplicable, "1", IsNewCostItem ? true : CostItemInfo.GSTApplicable, "") %>

      <%= WebHelper.GetInputDateRow("Cost Incurred:", FormFields.CostIncurred, CostItemInfo.CostIncurredUtc.UtcToTZOrNull(), "", !CanEditCostItem) %>

      <% if (SessionHelper.AppAccess.Programs.CanSetQuoteItem(ProgramInfo)) { %>
        <%= WebHelper.GetSelectRow("Quote Item:", FormFields.QuoteItemId, 7, GetQuoteItemOptions(), IsNewCostItem ? "" :
              WebHelper.GetComponentQuoteTooltipAndLink(CostItemInfo?.ComponentQuoteInfo.QuotePublicGuid, CostItemInfo?.ComponentQuoteInfo?.QuoteItemDescription), !CanEditCostItem) %>
      <% } %>

      <% if (!IsNewCostItem && SessionHelper.AppAccess.Programs.CanMoveToProgram(ProgramInfo)) { %>
        <%= WebHelper.GetSelectRow("Move to Program:", FormFields.MoveToProgramJobId, 7, GetMoveToProgramOptions(), "", !CanEditCostItem) %>
      <% } %>

      <div class="btnholder">
        <% if (CanEditCostItem || CanEditUnitPrice) { %>
          <button type="button" class="btn btn-primary btnUpdate floatright" id="btnUpdate"><%= IsNewCostItem ? "Add New" : "Update" %> Item</button>
          <% if (!IsNewCostItem && CanDeleteCostItem) { %>
            <button type="button" class="btn btn-warning btnDelete floatleft" <%= disableDelete ? (" title=\"" + disableDeleteMsg + "\"") : "" %> id="btnDelete">Delete Item</button>
          <% } %>
        <% } %>
        <button type="button" class="btn btn-secondary btnCancel <%= !CanEditCostItem ? "floatleft" : "floatright mr20" %>"
          data-mode="cancel"><%= !CanEditCostItem ? "Back" : "Cancel" %></button>
      </div>

    </form>

  <% } %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      var btnUpdate, formItem, isNewItem, defaultCostItemMarkupPercent, canOverwriteUnitPrice;
      var btnAssignPO;
      var disableDelete, formNewPO;
      var tlTimeout = null;

      $(document).ready(function() {

        btnAssignPO = $("#btnAssignPO");
        formNewPO = $("#formNewPO");

        btnUpdate = $("#btnUpdate");
        btnDelete = $("#btnDelete");
        formItem = $("#formItem");
        isNewItem = <%= IsNewCostItem ? "true" : "false" %>;
        disableDelete = <%= disableDelete ? "true" : "false" %>;
        defaultCostItemMarkupPercent = <%= ProjectInfo.DefaultCostItemMarkupPercent.GetValueOrDefault(ConfigHelper.DefaultCostItemMarkupPercent) %>;
        canOverwriteUnitPrice = <%= CanEditUnitPrice.ToJSTrueFalse() %>;

        btnUpdate.click(UpdateItem);
        btnDelete.click(DeleteItem);
        btnAssignPO.click(AssignPO);

        $(".btnCancel").click(function (e) { history.go(-1); });

        if (isNewItem) formItem.find("input:text:not(:disabled):first").trigger("focus");

        <% if (CanEditCostAndQty) { %>
          $('input[name="<%= FormFields.UnitCost %>"]').on("keyup change", CalculateUnitPrice);
        <% } %>

      }); // ready.

      function CalculateUnitPrice() {

        if (canOverwriteUnitPrice) return;

        // Get the value of UnitCost input removing currency symbol and convert it to a float
        var unitCost = parseFloat($(this).val().replace(/[^0-9.]/g, ''));

        // Check if the unitCost is a valid number
        if (!isNaN(unitCost)) {
          // Calculate unit Price using unitCost and markup percentage if unitCost is valid
          var unitPrice = unitCost * (1 + defaultCostItemMarkupPercent);

          // Set the value of UnitPrice input to the calculated value
          $('input[name="<%= FormFields.UnitPrice %>"]').val(unitPrice);
        } else {
          // Clear the value of UnitPrice input if Cost is invalid
          $('input[name="<%= FormFields.UnitPrice %>"]').val("");
        }
      }

      function AssignPO() {

        var selectedItemIds = "";
        $('input[name^="<%= FormFields.ChkItemId %>"]:checked').each(function(i, e) {
          if (selectedItemIds.length > 0) selectedItemIds += ",";
          selectedItemIds += e.value;
        });
        $('input[name="<%= FormFields.SelectedItemIds %>"]').val(selectedItemIds);

        AjaxSubmit({ form: formNewPO });
      }

      function DeleteItem() {

        if (disableDelete) {
          common_InfoDialog("<%= disableDeleteMsg %>");
          return;
        }

        if (!confirm("Delete this Item?")) return;

        AjaxSubmit({
          form: formItem,
          action: "<%= AjaxAction.DeleteItem %>"
        });
      }

      function UpdateItem() {
        AjaxSubmit({
          form: formItem,
          action: "<%= AjaxAction.UpdateItem %>"
        });
      }

    })(jQuery);
  </script>

</asp:Content>
