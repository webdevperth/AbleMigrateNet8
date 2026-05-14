using System;
using System.Collections.Generic;
using System.Linq;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class Components_BulkAddInvoiceModal : AppCode.PageBaseClasses.ProjectPageBase {

    public List<DbHelper.ProgramComponents.ComponentInfo> ComponentsList;
    public List<DbHelper.InvoiceItems.InvoiceItemsAmount> InvoiceItemsAmountsForList;
    public DbHelper.AbleQuotes.QuoteInfo QuoteInfo = null;

    public string jsonComponentsList;

    public class FormFields {
      public const string InvoiceItemId = "InvoiceItemId";
      public const string TypeToAllocateEnum = "TypeToAllocate";
      public const string ItemCompletionEnum = "ItemCompletion";
      public const string ProceedTypeEnum = "ProceedType";
      public const string SelectedComponentIds = "SelectedComponentIds";
    }

    public class FormValues {
      public int? InvoiceItemId;
      public List<int> SelectedComponentIds;
    }

    public class AjaxAction {
      public const string Update = "Update";
    }

    public class DataAttr {
      public const string InvoiceUnallocated = "iu";
      public const string TypeToAllocate = "tta";
      public const string ItemCompletion = "ic";
      public const string ProceedType = "pt";
    }

    public enum ComponentTypeEnum { All, Workshop, ConsultingItem, CostItem, CoachingSession }
    public enum CompletionTypeEnum { AllItems, PastDate, WithAnyDate }
    public enum ProceedTypeEnum { AllItems, BestEffortToAssign }

    protected void Page_Load(object sender, EventArgs e) {

      // Receive quoteUrlValue from URL
      string quoteUrlValue = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.QuoteGuid);

      if (!quoteUrlValue.IsNullOrEmpty() && quoteUrlValue != PathHelper.AbleUrlValues.IdNew) {

        Guid quoteGuid = Guid.TryParse(quoteUrlValue, out var tempGuid) ? tempGuid : Guid.Empty;
        if (quoteGuid != Guid.Empty) {

          QuoteInfo = DbHelper.AbleQuotes.GetQuoteInfoOrNull(quoteGuid);
          if (QuoteInfo != null) {
            ComponentsList = DbHelper.ProgramComponents.GetForQuoteId(QuoteInfo.QuoteId);
            if (ComponentsList != null && ComponentsList.Count > 0) {
              // Remove the components where InvoiceItemId is not null, as they don't have to be assigned.
              ComponentsList.RemoveAll(x => x.InvoiceItemId != null);
              // Order components by date (not null), then by price and then null values sorted and by smaller amount of price
              ComponentsList = ComponentsList
              .OrderBy(x => x.CompletedDateUtc.HasValue ? 0 : 1) // Prioritize non-null dates
              .ThenBy(x => x.CompletedDateUtc ?? DateTime.MaxValue) // Then sort by date, nulls at the end
              .ThenBy(x => x.ComponentPrice) // Finally, sort by price
              .ToList();
              jsonComponentsList = Newtonsoft.Json.JsonConvert.SerializeObject(ComponentsList);

              // Get Invoice Items
              InvoiceItemsAmountsForList = DbHelper.InvoiceItems.GetInvoiceItemsAmounts(ProjectInfo.JobNumber);
              if (InvoiceItemsAmountsForList.Count > 0) {
                // Remove invoice items without UnallocatedAmount allowance.
                var sortedList = InvoiceItemsAmountsForList.OrderByDescending(x => x.UnallocatedAmount).ToList();
                InvoiceItemsAmountsForList = sortedList;
              }
            }
          }
        }
      }

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          bool success = false;

          switch (PageAjaxAction) {

            case AjaxAction.Update:
              success = UpdateComponents(ajax);
              break;
          }

          if (success) {
            ajax.SetRedirectUrl(PathHelper.Pages.ProjectComponents(ProjectInfo.JobNumber));
          }
        });
      }
    }

    bool UpdateComponents(AjaxSubmitHelper ajax) {

      // Check Form Values
      var formValues = new FormValues();
      formValues.InvoiceItemId = ajax.CheckFieldIDOrNull(FormFields.InvoiceItemId, "Invoice Item", true, "You need to select an invoice item.");
      formValues.SelectedComponentIds = ajax.CheckFieldIntList(FormFields.SelectedComponentIds);

      if (formValues.SelectedComponentIds == null || formValues.SelectedComponentIds.Count == 0) {
        ajax.AddDialogMessage("No components found to update");
        return false;
      }

      var invoiceItemInfo = InvoiceItemsAmountsForList.FirstOrDefault(x => x.InvoiceItemId == formValues.InvoiceItemId);
      if (invoiceItemInfo == null) {
        ajax.AddDialogMessage("Invoice item not found, select a valid one.");
        return false;
      }

      var selectedComponentsInfo = new List<DbHelper.ProgramComponents.ComponentInfo>();
      foreach (var cmpId in formValues.SelectedComponentIds) {
        var component = ComponentsList.FirstOrDefault(x => x.ComponentId == cmpId);
        if (component == null) {
          ajax.AddDialogMessage("Component not found, try again.");
          return false;
        }

        selectedComponentsInfo.Add(component);
      }

      if (selectedComponentsInfo.Count == 0) return false;

      int updatedCmps = 0;
      try {
        foreach (var cmp in selectedComponentsInfo) {
          bool cmpUpdated = DbHelper.ProgramComponents.UpdateComponentInvoiceItem(null, cmp.ComponentId, formValues.InvoiceItemId);
          if (cmpUpdated) updatedCmps++;
        }
      } catch (Exception ex) {

        ajax.AddDialogMessage("We had a problem updating this component. Please try again later.", ex);
        throw;
      }

      if (updatedCmps == selectedComponentsInfo.Count) {
        ajax.SetRedirectUrl(PathHelper.Pages.ProjectComponents(ProjectInfo.JobNumber),
          $"{updatedCmps} Component{(updatedCmps > 1 ? "s" : "")} successfully assigned to invoice item '{invoiceItemInfo.Description}'");
        return true;
      } else {
        return false;
      }
    }

    public string GetInvoiceItemOptions() {

      string html = "<option value=\"\">[select invoice item]</option>";

      foreach (var item in InvoiceItemsAmountsForList) {

        html += "<option ";
        html += " value=\"" + item.InvoiceItemId + "\"";
        html += " data-" + DataAttr.InvoiceUnallocated + "=\"" + item.UnallocatedAmount + "\">";
        html += item.Description.LimitLengthTo(50, "...").HTMLEncode() + " (" + item.UnallocatedAmount.ToString("C") + " unallocated)";
        html += "</option>";
      }
      return html;
    }

    public string GetTypeToAllocateOptions() {
      return $@"
        <option selected value=""{ComponentTypeEnum.All}"">All</option>
        <option value=""{ComponentTypeEnum.Workshop}"">Workshop</option>
        <option value=""{ComponentTypeEnum.CoachingSession}"">Coaching Session</option>
        <option value=""{ComponentTypeEnum.ConsultingItem}"">Consulting Item</option>
        <option value=""{ComponentTypeEnum.CostItem}"">Cost Item</option>";
    }

    public string GetItemCompletionOptions() {
      return $@"
        <option selected value=""{CompletionTypeEnum.AllItems}"">All Items</option>
        <option value=""{CompletionTypeEnum.PastDate}"">With Date in the Past</option>
        <option value=""{CompletionTypeEnum.WithAnyDate}"">With Any Date</option>";
    }

    public string GetProceedTypeOptions() {
      return $@"
        <option selected value=""{ProceedTypeEnum.AllItems}"">All Items</option>
        <option value=""{ProceedTypeEnum.BestEffortToAssign}"">Best Effort (Calculation)</option>";
    }
  }
}
