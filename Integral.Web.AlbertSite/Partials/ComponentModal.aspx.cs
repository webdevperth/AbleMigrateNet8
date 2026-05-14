using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class ComponentModal : AppCode.PageBaseClasses.ProjectPageBase {

    public DbHelper.ProgramComponents.ComponentInfo ComponentInfo = null;
    public List<DbHelper.InvoiceItems.InvoiceItemsAmount> InvoiceItemsAmountsForList;
    public List<string> componentDetailsHtml = new List<string>();
    public bool CanUpdateComponent;

    public class FormFields {
      public const string InvoiceItemId = "InvoiceItemId";
    }

    public class FormValues {
      public int? InvoiceItemId;
    }

    public class AjaxAction {
      public const string Update = "Update";
    }

    protected void Page_Load(object sender, EventArgs e) {

      // Receive componentId from URL
      int componentId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.ComponentId) ?? 0;
      ComponentInfo = DbHelper.ProgramComponents.GetComponentInfoOrNull(componentId);

      if (ComponentInfo == null) {
        RespondNoAccessOrRedirect("Can't find component.");
        return;
      } else {
        GetComponentDetailsHtml();

        // Get invoice items with it's amounts (unassigned and assigned)
        InvoiceItemsAmountsForList = DbHelper.InvoiceItems.GetInvoiceItemsAmounts(ProjectInfo.JobNumber);
      }

      CanUpdateComponent = SessionHelper.AppAccess.Projects.CanUpdateComponent(ComponentInfo);

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          bool success = false;

          switch (PageAjaxAction) {

            case AjaxAction.Update:
              if (!CanUpdateComponent) {
                ajax.AddDialogMessage("Component cannot be updated.");
                return;
              }
              success = UpdateComponent(ajax);
              break;
          }

          if (success) {
            ajax.SetRedirectUrl(PathHelper.Pages.ProjectComponents(ProjectInfo.JobNumber));
          }
        });
      }
    }

    bool UpdateComponent(AjaxSubmitHelper ajax) {

      // Check Form Values
      var formValues = new FormValues();
      formValues.InvoiceItemId = ajax.CheckFieldIDOrNull(FormFields.InvoiceItemId, "Invoice Item", false, "");

      // If Invoice Item is not null, check if it exists in the list and that also belongs to the same quote as the current component
      if (formValues.InvoiceItemId != null && !InvoiceItemsAmountsForList.Exists(i => i.InvoiceItemId == formValues.InvoiceItemId && i.QuoteId == ComponentInfo.QuoteId)) {
        ajax.AddDialogMessage("Invalid invoice item. Please try again.");
        return false;
      }

      // If current value is the same as when Update was pressed, do not proceed to database and return true
      if (ComponentInfo.InvoiceItemId == formValues.InvoiceItemId) return true;

      try {

        return DbHelper.ProgramComponents.UpdateComponentInvoiceItem(null, ComponentInfo.ComponentId, formValues.InvoiceItemId);
      } catch (Exception ex) {

        ajax.AddDialogMessage("We had a problem updating this component. Please try again later.", ex);
        throw;
      }
    }

    void GetComponentDetailsHtml() {

      string componentPath = PathHelper.Pages.ProjectComponents(ComponentInfo);

      // Depending on the Component Type, get general details to display
      if (ComponentInfo.CoachingSessionId != null || ComponentInfo.CoacheeId != null) {

        if (ComponentInfo.CoachingSessionId == null) {

          AddComponentDetailHtml("Component Type:", "Coaching Session");

        } else {

          var sessionInfo = DbHelper.CoachingSessions.GetSessionInfoOrNull(ComponentInfo.CoachingSessionId.Value);

          AddComponentDetailHtml("Program:", sessionInfo.ProgramJobName);
          AddComponentDetailHtml("Component Type:", GetLinkHtml(componentPath, "Coaching Session"));
          AddComponentDetailHtml("Session Date:", WebHelper.DisplayDate(sessionInfo.ApptDateUTC, "-"));
          AddComponentDetailHtml("Participant: ", sessionInfo.CoacheeFirstName + " " + sessionInfo.CoacheeLastName);
        }

      } else if (ComponentInfo.WorkshopEventId != null) {

        var workshopInfo = DbHelper.WorkshopEvents.GetWorkshopInfo(ComponentInfo.WorkshopEventId.Value);

        AddComponentDetailHtml("Title:", workshopInfo.WorkshopTitle);
        AddComponentDetailHtml("Component Type:", GetLinkHtml(componentPath, "Workshop"));
        AddComponentDetailHtml("Key Facilitator:", workshopInfo.KeyFacilitatorFirstName + " " + workshopInfo.KeyFacilitatorLastName);
        AddComponentDetailHtml("Start Date:", WebHelper.DisplayDate(workshopInfo.WhenStartUtc, "-"));
        AddComponentDetailHtml("End Date:", WebHelper.DisplayDate(workshopInfo.WhenEndUtc, "-"));

      } else if (ComponentInfo.ConsultingItemId != null) {

        var consultingInfo = DbHelper.ConsultingItems.GetConsultingItemInfo(ComponentInfo.ProgramJobId, ComponentInfo.ConsultingItemId.Value);

        AddComponentDetailHtml("Title:", consultingInfo.ItemTitle);
        AddComponentDetailHtml("Component Type:", GetLinkHtml(componentPath, "Consulting"));
        AddComponentDetailHtml("Consultant:", consultingInfo.ConsultantFullName);
        AddComponentDetailHtml("Completion:", WebHelper.DisplayDate(consultingInfo.CompletionDateUtc, "-"));

      } else if (ComponentInfo.ProgramCostItemId != null) {

        var costItemInfo = DbHelper.ProgramCostItems.GetCostItemInfo(ComponentInfo.ProgramCostItemId.Value, ComponentInfo.ProgramJobId);

        AddComponentDetailHtml("Title:", costItemInfo.Description);
        AddComponentDetailHtml("Component Type:", GetLinkHtml(componentPath, "Cost Item"));
        AddComponentDetailHtml("Cost Incurred:", WebHelper.DisplayDate(costItemInfo.CostIncurredUtc, "-"));

      }
    }

    public string GetComponentDetailsTable() {

      string html = "";

      if (componentDetailsHtml.Count == 0) {
        return "<div>No details available.</div>";
      } else if (componentDetailsHtml.Count > 0) {
        html += "<div class=\"ml10\">" + componentDetailsHtml[0] + "</div>";
      }

      html += "<table class=\"table cmp-modal-table\">";
      string newRowColumns = "";

      for (int i = 1; i < componentDetailsHtml.Count; i++) {
        bool isNewRow = i % 2 == 0; // If array position is pair create new row
        newRowColumns += "<td>" + componentDetailsHtml[i] + "</td>";

        if (isNewRow) {
          html += "<tr>" + newRowColumns + "</tr>";
          newRowColumns = "";
        }
      }

      if (newRowColumns != "") html += "<tr>" + newRowColumns + "</tr>";

      html += "</table>";

      return html;
    }

    void AddComponentDetailHtml(string label, string value) {
      componentDetailsHtml.Add(WebHelper.GetTextDisplayRow(label, 12, value));
    }

    public string GetPayRun() {
      string date = WebHelper.DisplayDate(ComponentInfo.PayrunDate, "-");

      if (ComponentInfo.PayrunDate == null || ComponentInfo.PayRunId == null || ComponentInfo.PartnerUserId == null || (ComponentInfo.PayRunId == null && !SessionHelper.IsUserRoleAdmin)) return date;

      return WebHelper.GetTextDisplayRow("Payrun: ", 12, GetLinkHtml(PathHelper.Pages.CoachPayRuns(ComponentInfo.PartnerUserId.Value, ComponentInfo.PayRunId), date));
    }

    public string GetLinkHtml(string linkUrl, string linkText) {
      if (linkUrl.IsNullOrEmpty()) return linkText;
      return "<a href=\"" + linkUrl + "\">" + linkText + "</a>";
    }

    public string GetInvoiceItemOptions() {

      string html = "<option value=\"\">[select invoice item]</option>";

      foreach (var item in InvoiceItemsAmountsForList) {

        bool isSelected = false;

        if (item.InvoiceItemId == ComponentInfo.InvoiceItemId) isSelected = true; // If current invoice item is selected, set selected to true. Show even if invoice item has not balance available.
        else if (item.QuoteId != ComponentInfo.QuoteId) continue; // If current invoice item QuoteId is not equal to current component's QuoteId, do not display
        else if (item.InvoiceItemTotal < 0) continue; // If current invoice item total is less than 0, do not display
        else if (ComponentInfo.ComponentPrice > item.UnallocatedAmount) continue; // If current invoice item unallocated amount is less than current component's price, do not display

        html += "<option ";
        if (isSelected) html += "selected ";
        html += " value=\"" + item.InvoiceItemId + "\">";
        html += item.Description.LimitLengthTo(50, "...").HTMLEncode() + " (" + item.UnallocatedAmount.ToString("C") + " unallocated)";
        html += "</option>";
      }
      return html;
    }

    void RespondNoAccessOrRedirect(string message = null) {

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.RespondNoAccessToFunction(message);
        WebHelper.EndRequest();
        return;
      } else {
        WebHelper.Redirect(PathHelper.Pages.ProjectComponents(ProjectInfo.JobNumber));
        return;
      }
    }
  }
}
