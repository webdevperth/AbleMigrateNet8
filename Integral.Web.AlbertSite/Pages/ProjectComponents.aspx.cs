using System;
using System.Collections.Generic;
using Integral.Web;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class ProjectComponents : AppCode.PageBaseClasses.ProjectPageBase {

    public bool CanAddInvoiceItem = false, CanBulkAddInvoiceComponents = false;
    public bool IsReadOnly = false;
    public List<DbHelper.InvoiceItems.InvoiceItemsByQuote.Quote> Quotes;

    public class FormFields {
      public const string ComponentId = "ComponentId";
    }

    class FormValues {
      public int ComponentId;
    }

    public class AjaxAction {
      public const string AddInvoiceItem = "AddInvoiceItem";
    }

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Quote Components";

      CanAddInvoiceItem = SessionHelper.AppAccess.Invoices.CanAddDeleteItems(ProjectInfo);
      CanBulkAddInvoiceComponents = SessionHelper.AppAccess.Invoices.CanBulkAddInvoiceComponents(ProjectInfo);
      IsReadOnly = !SessionHelper.AppAccess.Projects.CanEditProjectComponents();

      Quotes = DbHelper.InvoiceItems.GetInvoiceItemsByQuote(ProjectInfo.JobNumber).Quotes;

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          bool success = false;

          switch (PageAjaxAction) {

            case AjaxAction.AddInvoiceItem:
              if (!CanAddInvoiceItem || IsReadOnly) {
                ajax.RespondNoAccessToFunction();
                return;
              } else {
                success = AddInvoiceItem(ajax);
              }
              break;
          }

          if (success) {
            ajax.SetRedirectUrl(PathHelper.Pages.ProjectComponents(ProjectInfo.JobNumber));
          }
        });
      }
    }

    bool AddInvoiceItem(AjaxSubmitHelper ajax) {

      var formValues = new FormValues();

      formValues.ComponentId = ajax.CheckFieldID(FormFields.ComponentId, "Component", true, "Component is required.");

      var ComponentInfo = DbHelper.ProgramComponents.GetComponentInfoOrNull(formValues.ComponentId);

      if (ComponentInfo == null) {
        ajax.AddDialogMessage("Component not found. Please reload page and try again.");
        return false;
      } else if (ComponentInfo.QuoteId == null) {
        ajax.AddDialogMessage("Quote not found. Please reload page and try again.");
        return false;
      }

      try {

        // Create Invoice Item and get Id
        int InvoiceItemId = DbHelper.InvoiceItems.AddInvoiceItemToProgram(
          ProjectInfo.ProjectId,
          GetComponentDescription(ComponentInfo),
          ComponentInfo.ComponentPrice.GetValueOrDefault(0),
          1,
          ComponentInfo.GSTApplicable,
          ComponentInfo.QuoteId
        );

        if (InvoiceItemId == 0) {
          ajax.AddDialogMessage("Invoice Item not created. Please reload page and try again.");
          return false;
        }

        // Update Component with InvoiceId
        return DbHelper.ProgramComponents.UpdateComponentInvoiceItem(null, ComponentInfo.ComponentId, InvoiceItemId);

      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("AddInvoiceItem")
          .FromSession()
          .WithProperty("ComponentId", formValues.ComponentId)
          .WithProperty(ApplicationInsightsConstants.ProjectId, ProjectInfo.ProjectId)
          .WithProperty(ApplicationInsightsConstants.JobNumber, ProjectInfo.JobNumber)
          .Track();

        ajax.AddDialogMessage("We had a problem updating this component. Please try again later.", ex);
        throw;
      }
    }

    public string GetComponentDescription(DbHelper.ProgramComponents.ComponentInfo cmp) {

      string cmpDescription = "", cmpType = "", cmpTitle = "";
      string cmpProgramName = cmp.ProgramName;
      string separator = " - ";
      string cmpCompletedDate = WebHelper.DisplayDate(cmp.CompletedDateUtc.UtcToTZOrNull(null), "-") + " ";

      if (cmp.CoachingSessionId != null || cmp.CoacheeId != null) {
        cmpType = " Coaching: ";
        cmpTitle = cmp.CoachingSessionId == null ? "" : separator + cmp.CoacheeFirstName + " " + cmp.CoacheeLastName + " #" + cmp.SessionNumber;
      } else if (cmp.WorkshopEventId != null) {
        cmpType = " Workshop: ";
        cmpTitle = cmp.WorkshopTitle.IsNullOrEmptyOrWhitespace() ? "" : separator + cmp.WorkshopTitle;
      } else if (cmp.ConsultingItemId != null) {
        cmpType = " Consulting: ";
        cmpTitle = cmp.ConsultingItemTitle.IsNullOrEmptyOrWhitespace() ? "" : separator + cmp.ConsultingItemTitle;
      } else if (cmp.ProgramCostItemId != null) {
        cmpType = " Cost Item: ";
        cmpTitle = cmp.CostItemTitle.IsNullOrEmptyOrWhitespace() ? "" : separator + cmp.CostItemTitle;
      }

      cmpDescription = cmpProgramName + cmpType + cmpCompletedDate + cmpTitle;

      return cmpDescription;
    }

    public string GetComponentName(DbHelper.ProgramComponents.ComponentInfo cmp) {

      string componentPath = PathHelper.Pages.ProjectComponents(cmp);
      string componentName = "";

      if (cmp.CoachingSessionId != null || cmp.CoacheeId != null) {
        componentName = "Coaching Session";
      } else if (cmp.WorkshopEventId != null) {
        componentName = "Workshop";
      } else if (cmp.ConsultingItemId != null) {
        componentName = "Consulting";
      } else if (cmp.ProgramCostItemId != null) {
        componentName = "Cost Item";
      }

      return GetLinkHtml(componentPath, componentName);
    }

    public string GetLinkHtml(string linkUrl, string linkText) {
      if (linkUrl.IsNullOrEmpty() || IsReadOnly) return linkText;
      return "<a href=\"" + linkUrl + "\">" + linkText + "</a>";
    }

    public string GetPayRunLinkHtml(DateTime? PayrunDate, int? PayrunId, int? PartnerUserId) {

      string date = WebHelper.DisplayDate(PayrunDate.UtcToTZOrNull(null), "-");

      if (PayrunDate == null || PayrunId == null || PartnerUserId == null || (PayrunId == null && !SessionHelper.IsUserRoleAdmin)) {
        return date;
      }

      return GetLinkHtml(PathHelper.Pages.CoachPayRuns(PartnerUserId.Value, PayrunId), date);
    }

    public int GetQuoteItemPercentage(List<DbHelper.ProgramComponents.ComponentInfo> components, DbHelper.AbleQuotes.QuoteInfo.QuoteItemInfo quoteItem) {

      // Get total proce of Quote's item components
      decimal totalPrice = 0;
      foreach (var cmp in components) {
        totalPrice += cmp.ComponentPrice.GetValueOrDefault(0);
      }

      // Calculate percentage
      int percent = 0;
      if (quoteItem.PriceXQty(0) > 0) {
        percent = (int)Math.Round(totalPrice / (decimal)quoteItem.PriceXQty(0) * 100, 0, MidpointRounding.AwayFromZero);
      } else if (quoteItem.PriceXQty(0) == 0) {
        percent = 100;
      }

      if (percent == 0 && totalPrice > 0) percent = 1;
      else if (percent == 100 && totalPrice < quoteItem.PriceXQty(0)) percent = 99;

      return percent;
    }
  }
}
