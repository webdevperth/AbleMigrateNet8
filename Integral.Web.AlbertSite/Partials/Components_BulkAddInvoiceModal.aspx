<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Components_BulkAddInvoiceModal.aspx.cs" Inherits="Integral.Web.PortalSite.Page_Partials.Components_BulkAddInvoiceModal" %>

<%@ Import Namespace="Integral.Web" %>

<div class="container-fluid">

  <% if (QuoteInfo == null) { %>
    <p>No data found.</p>
  <% } else { %>

    <% if (ComponentsList != null) { %>

      <div class="">

        <form class="form-horizontal label-narrow" method="post" action="#" onsubmit="return false;">
          <%= WebHelper.GetTextDisplayRow("Quote:", 8, QuoteInfo.QuoteTitle.IsNullOrEmpty() ? QuoteInfo.ProjectName : QuoteInfo.QuoteTitle) %>
          <%= WebHelper.GetTextDisplayRow("Unassigned Components:", 8, ComponentsList.Count.ToString()) %>
          <%= WebHelper.GetSelectRow("Invoice Item:", FormFields.InvoiceItemId, 8, GetInvoiceItemOptions()) %>
          <%= WebHelper.GetSelectRow("Component Type:", FormFields.TypeToAllocateEnum, 8, GetTypeToAllocateOptions()) %>
          <%= WebHelper.GetSelectRow("Item Completion:", FormFields.ItemCompletionEnum, 8, GetItemCompletionOptions()) %>
          <%= WebHelper.GetSelectRow("Proceed type:", FormFields.ProceedTypeEnum, 8, GetProceedTypeOptions()) %>

          <div class="w100c mb20 mt20">
            <button type="button" class="btn btn-primary float-right mb15 btnCalculate">Calculate</button>

            <div class="mb10 mt20" id="dvCalculationResults">
              <h3>Results:</h3>
              <div class="table-responsive w100p mt10 bulkaddinvoice">
                <table class="table" id="tblCalculationResult">
                  <thead>
                    <tr>
                      <th>Component</th>
                      <th>Date Completed</th>
                      <th>Price</th>
                    </tr>
                  </thead>
                  <tbody>

                  </tbody>
                </table>
              </div>
            </div>
          </div>

          <div class="modal-footer-buttons">
            <div><button type="button" class="btn btn-secondary btnClose">Cancel</button></div>
            <div>
              <button type="button" class="btn btn-primary btnSaveInvoice">Update</button>
            </div>
          </div>
          <br />
        </form>
      </div>

    <% } %>
  <% } %>

</div>

<% var scriptId = new Random((int)DateTime.Now.Ticks).Next(100000); %>

<script id="<%= scriptId %>">

  (function ($) {

    var dialogRef = null;
    var modalBody = null;
    var modalForm = null;
    var SelectedComponentIds = [];
    var $btnSaveInvoice, $btnCalculate, $btnClose;

    $(document).ready(function () {
      // Find modal objects based on the random ID given to this script tag.
      var scripts = document.getElementsByTagName("script");
      for (var i = scripts.length - 1; i >= 0; i--) {
        if (scripts[i].id == <%= scriptId %>) {
          var thisScript = $(scripts[i]);
          var modalDialog = thisScript.closest(".modal-dialog");
          dialogRef = modalDialog.data("<%= WebHelper.DataAttrName.DialogRef %>");
          modalBody = modalDialog.find(".modal-body");
          modalForm = modalBody.find("form");
          break;
        }
      }

      $btnSaveInvoice = modalForm.find(".btnSaveInvoice");
      $btnCalculate = modalForm.find(".btnCalculate");
      $btnClose = modalForm.find(".btnClose");
      CleanUpCalculationForms();
      $btnSaveInvoice.click(UpdateInvoiceItem)
      $btnClose.click(function () { dialogRef.close(); });
      $btnCalculate.click(CalculateComponents);

      $(this).keyup(function (e) {
        if (e.keyCode == 27) {
          modalForm.find('select[name="<%= FormFields.InvoiceItemId %>"]').select2("close");
        }
      });

    });

    function CleanUpCalculationForms() {
      modalForm.find("#dvCalculationResults").hide();
      $btnSaveInvoice.hide();
      $btnCalculate.removeClass("btn-secondary").addClass("btn-primary");
      $btnCalculate.text("Calculate");
    }

    function ComponentsListCheck(allocatableComponents) {
      if (!allocatableComponents.length) {
        common_InfoDialog('No allocatable components found for the selected values.');
        CleanUpCalculationForms();
        return false;
      }
      return true;
    }

    function CalculateComponents() {
      var invoiceItemValue = modalForm.find('select[name="<%= FormFields.InvoiceItemId %>"] option:selected').data('iu');

      if (invoiceItemValue == null || invoiceItemValue === undefined) {
        CleanUpCalculationForms();
        common_InfoDialog("Please select an invoice item.");
        return;
      }

      var typeToAllocateValue = modalForm.find('select[name="<%= FormFields.TypeToAllocateEnum %>"]').val();
      var itemCompletionValue = modalForm.find('select[name="<%= FormFields.ItemCompletionEnum %>"]').val();
      var proceedTypeValue = modalForm.find('select[name="<%= FormFields.ProceedTypeEnum %>"]').val();

      var componentList = <%= jsonComponentsList %>;

      // Filter the list of components based on the type to allocate
      var allocatableComponents = componentList.filter(function(component) {
        switch (typeToAllocateValue) {
          case '<%= ComponentTypeEnum.All %>':
            // If 'All', no filtering is needed.
            return true;
          case '<%= ComponentTypeEnum.Workshop %>':
            return component['WorkshopEventId'] != null;
          case '<%= ComponentTypeEnum.ConsultingItem %>':
            return component['ConsultingItemId'] != null;
          case '<%= ComponentTypeEnum.CostItem %>':
            return component['ProgramCostItemId'] != null;
          case '<%= ComponentTypeEnum.CoachingSession %>':
            return component['CoachingSessionId'] != null || component['CoacheeId'] != null;
          default:
            return false;
        }
      });

      // Check if there's any results after the filter.
      if (!ComponentsListCheck(allocatableComponents)) {
        return; // Stop execution if ComponentsListCheck returns false
      }

      // Filter the list of components based on the item completion
      var completionComponents = allocatableComponents.filter(function(component) {
        switch (itemCompletionValue) {
          case '<%= CompletionTypeEnum.AllItems %>':
            // If 'AllItems', no further filtering based on completion date
            return true;
          case '<%= CompletionTypeEnum.PastDate %>':
            // If 'PastDate', keep only components with a completion date in the past
            if (component['CompletedDateUtc']) {
              var currentDate = new Date();
              var completedDate = new Date(component['CompletedDateUtc']);
              currentDate.setHours(0, 0, 0, 0);
              completedDate.setHours(0, 0, 0, 0);
              return completedDate < currentDate;
            }
            return false;
          case '<%= CompletionTypeEnum.WithAnyDate %>':
            // If 'WithAnyDate', keep components where the completion date is not null
            return component['CompletedDateUtc'] != null;
          default:
            return false;
        }
      });

      // Check if there's any results after the filter.
      if (!ComponentsListCheck(completionComponents)) {
        return;
      }

      // Filter the list of components based on the proceed type
      var sumComponentPrices = completionComponents.reduce(function(accumulator, component) {
            return accumulator + parseFloat(component['ComponentPrice']);
      }, 0);

      if (proceedTypeValue === '<%= ProceedTypeEnum.AllItems %>') {
        if (invoiceItemValue < sumComponentPrices) {
          common_InfoDialog(`The available amount in invoice item is $${invoiceItemValue} and the sum of components is $${sumComponentPrices}, can't assign all components, change your factor selection and re-calculate.`);
          CleanUpCalculationForms();
          return;
        } else {
          PopulateComponentsTable(completionComponents);
        }
      } else if (proceedTypeValue === '<%= ProceedTypeEnum.BestEffortToAssign %>') {
        var bestEffortComponents = calculateBestEffortToAssign(completionComponents, invoiceItemValue);

        // Check if there's any results after the filter.
        if (!ComponentsListCheck(bestEffortComponents)) {
          return;
        }

        PopulateComponentsTable(bestEffortComponents);
      }

      modalForm.find("#dvCalculationResults").show();
      $btnCalculate.removeClass("btn-primary").addClass("btn-secondary");
      $btnCalculate.text("Calculate Again");
      $btnSaveInvoice.show();
    }

    function calculateBestEffortToAssign(components, maxTotalPrice) {
      let assignedComponents = [];
      let currentTotal = 0;

      for (let component of components) {
        let componentPrice = parseFloat(component['ComponentPrice']);
        if (currentTotal + componentPrice <= maxTotalPrice) {
            assignedComponents.push(component);
            currentTotal += componentPrice;
        } else {
            break; // Stop adding components if the next one would exceed maxTotalPrice
        }
      }
      return assignedComponents;
    }

    function PopulateComponentsTable(componentList){
      var $tableBody = modalForm.find('#tblCalculationResult tbody');
      $tableBody.empty(); // Clear existing table body
      let totalComponentPrice = 0;
      SelectedComponentIds = []; // Clear the list of selected components

      // Append each component to the table
      componentList.forEach(function(component) {
        var dateCompleted = component['CompletedDateUtc'] ? new Date(component['CompletedDateUtc']).toLocaleDateString() : 'N/A';
        var componentPrice = parseFloat(component['ComponentPrice']);
        totalComponentPrice += componentPrice; // Add to total

        var row = `
          <tr>
            <td>${getComponentType(component)}</td>
            <td>${dateCompleted}</td>
            <td>${component['ComponentPrice'].toFixed(2)}</td>
          </tr>`;
        $tableBody.append(row);

        // Add the component ID to the list of selected components
        SelectedComponentIds.push(component['ComponentId']);
      });

      // Append a row for the total
      var totalRow = `
        <tr style="background-color: #edf3f7; font-weight: bolder;">
          <td colspan="2" class="text-right">Total:</td>
          <td>$${totalComponentPrice.toFixed(2)}</td>
        </tr>`;
      $tableBody.append(totalRow);
    }

    function getComponentType(component) {
      if (component['CoachingSessionId'] != null || component['CoacheeId'] != null) return 'Coaching Session';
      if (component['WorkshopEventId'] != null) return 'Workshop';
      if (component['ConsultingItemId'] != null) return 'Consulting Item';
      if (component['ProgramCostItemId'] != null) return 'Cost Item';
      return 'Unknown'; // Default case if none of the IDs are set
    }

    function UpdateInvoiceItem(evt) {

      AjaxSubmit({
        form: modalForm,
        action: "<%= AjaxAction.Update %>",
        data: {
          "<%= FormFields.SelectedComponentIds %>": SelectedComponentIds
        },
        url: "<%= PathHelper.Partials.Components_BulkAddInvoiceModal(ProjectInfo.JobNumber, QuoteInfo.PublicGuid) %>",
        onSuccess: function (jqXHR, data) { },
        onFail: function (jqXHR, data) { },
        onError: function (jqXHR, textStatus, errorThrown) { },
        onAlways: function (data_or_jqXHR, textStatus, jqXHR_or_errorThrown) { }
      });
    }


  })(jQuery);

</script>
