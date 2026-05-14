using System;
using System.Collections.Generic;
using System.Text;
using static Integral.Web.DbHelper.Common;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class Consulting : AppCode.PageBaseClasses.ProgramPageBase {

    public bool IsNewItem = false;
    public List<DbHelper.ConsultingItems.ConsultingItemInfo> ConsultingItemList;  // For list display.
    public DbHelper.ConsultingItems.ConsultingItemInfo ConsultingItemInfo = null; // For details display.
    public List<DbHelper.AbleQuotes.QuoteItemForList> QuoteItemsForList;
    public bool IsLimitedEdit, IsReadOnly;
    public bool CanAddConsulting, CanSetQuoteItem, CanMoveToProgram, CanDeleteConsulting;
    public bool CanViewTotalRevenue, CanViewPartnerRevenue, CanViewAllDeliveryTeamRevenue, CanNavigateFromConsultingTable;
    public bool ConsultingListVisible = false;
    public bool ConsultingFormVisible = false;
    private List<DbHelper.AlbertCoaches.AlbertCoachInfo> PartnerList;

    public class FormFields {
      public const string ConsultingItemId = "ConsultingItemId";
      public const string ProgramJobId = "ProgramJobId";
      public const string ConsultantUserId = "ConsultantUserId";
      public const string ItemTitle = "ItemTitle";
      public const string Description = "Description";
      public const string ConsultingTypeId = "ConsultingTypeId";
      public const string RevenueAmount = "RevenueAmount";
      public const string CompletionDateLocal = "CompletionDateLocal";
      public const string MoveToProgramJobId = "MoveToProgramJobId";
      public const string QuoteItemId = "QuoteItemId";
    }

    class FormValues {
      public int ConsultingItemId;
      public int ProgramJobId;
      public int? ConsultantUserId;
      public string ItemTitle;
      public string Description;
      public int? ConsultingTypeId;
      public decimal RevenueAmount;
      public DateTime? CompletionDateLocal;
      public int? MoveToProgramJobId;
      public TimeZoneInfo timeZoneInfo = ConfigHelper.DefaultTimeZoneInfo;
      public int? QuoteItemId;
    }

    public class AjaxAction {
      public const string UpdateItem = "UpdateItem";
      public const string DeleteItem = "DeleteItem";
    }

    protected void Page_Load(object sender, EventArgs e) {

      string urlConsultingItemStr = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ConsultingItemId);
      int? urlConsultingItemId = urlConsultingItemStr.ToIntOrNull();
      IsNewItem = urlConsultingItemStr == PathHelper.AbleUrlValues.IdNew;

      CanAddConsulting = SessionHelper.AppAccess.Programs.Consulting.CanAdd(ProgramInfo);
      CanSetQuoteItem = SessionHelper.AppAccess.Programs.CanSetQuoteItem(ProgramInfo);
      CanMoveToProgram = !IsNewItem && SessionHelper.AppAccess.Programs.CanMoveToProgram(ProgramInfo);
      CanNavigateFromConsultingTable = SessionHelper.AppAccess.Programs.Consulting.CanNavigateFromConsultingTable(ProgramInfo);
      PartnerList = DbHelper.AlbertCoaches.GetCoachInfoList(true, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);

      CanViewTotalRevenue = SessionHelper.AppAccess.Programs.Revenue.CanViewTotalRevenue(ProgramInfo);
      CanViewAllDeliveryTeamRevenue = SessionHelper.AppAccess.Programs.Revenue.CanViewAllDeliveryTeamRevenue(ProgramInfo);

      if (IsNewItem) { // New consulting item.

        if (!CanAddConsulting) { // No access to adding new item.

          RespondNoAccessOrRedirect();
          return;

        } else { // Allowed to add new item.

          ConsultingItemInfo = new DbHelper.ConsultingItems.ConsultingItemInfo();

          QuoteItemsForList = DbHelper.AbleQuotes.GetQuoteItemsForList(ProgramInfo.ProgramJobNumber, false,
            DbHelper.ProgramComponents.KeyColumnEnum.ConsultingItemId, urlConsultingItemId, ProgramInfo.ProgramJobId);

          if (SystemWeb.IsHttpPost) {

            AjaxSubmitHelper.Process(ajax => {
              if (ajax.Action == AjaxAction.UpdateItem) {
                bool success = UpdateItem(ajax, out string returnMessage);
                if (success) {
                  ajax.SetRedirectUrl(PathHelper.Pages.Consulting_List(ProgramInfo.ProgramJobId), returnMessage, AjaxSubmitHelper.PageMessageType.SuccessToast);
                } else {
                  ajax.AddDialogMessage(returnMessage);
                }
              }
              return;
            });
            WebHelper.EndRequest();
            return;

          } else {

            PageTitle = "Add Consulting Item";
            ConsultingFormVisible = true;

          }
        }
        return;

      } else { // Not a new item.

        if (urlConsultingItemId == null) { // No consulting item id given.

          if (SystemWeb.IsHttpPost) {
            AjaxSubmitHelper.Process(ajax => {
              ajax.RespondNoAccessToFunction();
            });
            return;
          } else {
            // A GET with no item id means show the list.
            ShowList();
          }
          return;

        } else { // Consulting item id given.

          // Get consulting item data.
          // Note including current Program ensures user has access to the given item id in the url.
          try {
            ConsultingItemInfo = DbHelper.ConsultingItems.GetConsultingItemInfo(ProgramInfo.ProgramJobId, (int)urlConsultingItemId);
          } catch (Exception) { }

          if (ConsultingItemInfo == null) { // Item not found.

            RespondNoAccessOrRedirect();
            return;

          } else { // Item found.

            // Get permissions to view, edit & delete.

            if (!SessionHelper.AppAccess.Programs.Consulting.CanView(ProgramInfo, ConsultingItemInfo)) {
              // Not allowed to view.
              RespondNoAccessOrRedirect();
              return;
            }

            IsReadOnly = SessionHelper.AppAccess.Programs.Consulting.ReadOnly(ProgramInfo, ConsultingItemInfo);
            IsLimitedEdit = SessionHelper.AppAccess.Programs.Consulting.LimitedEdit(ProgramInfo, ConsultingItemInfo);
            CanDeleteConsulting = SessionHelper.AppAccess.Programs.Consulting.CanDelete(ProgramInfo, ConsultingItemInfo);

            QuoteItemsForList = DbHelper.AbleQuotes.GetQuoteItemsForList(ProgramInfo.ProgramJobNumber, false,
              DbHelper.ProgramComponents.KeyColumnEnum.ConsultingItemId, urlConsultingItemId, ProgramInfo.ProgramJobId);

            if (SystemWeb.IsHttpPost) { // Ajax post.

              // Process submitted form.
              AjaxSubmitHelper.Process(ajax => {

                if (IsReadOnly) {

                  ajax.RespondNoAccessToFunction(); // Read-only, no update or delete.
                  return;

                } else {

                  string returnMessage = "";
                  bool success = false;

                  if (ajax.Action == AjaxAction.UpdateItem) {
                    success = UpdateItem(ajax, out returnMessage);
                    return;
                  } else if (ajax.Action == AjaxAction.DeleteItem) {
                    if (!CanDeleteConsulting) {
                      ajax.RespondNoAccessToFunction(); // No deletion allowed if limited access.
                      return;
                    } else {
                      success = DeleteItem(ajax, out returnMessage);
                    }
                    return;
                  }

                  if (success) {
                    ajax.SetRedirectUrl(PathHelper.Pages.Consulting_List(ProgramInfo.ProgramJobId), returnMessage, AjaxSubmitHelper.PageMessageType.SuccessToast);
                  } else {
                    ajax.AddDialogMessage(returnMessage);
                  }
                }
              });
              WebHelper.EndRequest();
              return;

            } else { // Not a POST

              // Show form.
              PageTitle = "Update Consulting Item";
              ConsultingFormVisible = true;

            }
          }
        }
      }
    }

    // If POST, return fail status, otherwise redirect back to the list.
    void RespondNoAccessOrRedirect() {
      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {
          ajax.RespondNoAccessToFunction();
        });
        return;
      } else {
        WebHelper.Redirect(PathHelper.Pages.Consulting_List()); // Not allowed to add new item.
        return;
      }
    }

    void ShowList() {

      PageTitle = "Consulting Items";
      ConsultingListVisible = true;
      CanViewPartnerRevenue = SessionHelper.AppAccess.Programs.Revenue.CanViewPartnerRevenue(ProgramInfo);

      if (SessionHelper.AppAccess.Programs.Consulting.CanViewAllInProgram(ProgramInfo)) {
        // List all items in the Program.
        ConsultingItemList = DbHelper.ConsultingItems.GetItemsInProgram(ProgramInfo.ProgramJobId);
      } else {
        // List only items where user is the consultant.
        ConsultingItemList = DbHelper.ConsultingItems.GetItemsInProgram(ProgramInfo.ProgramJobId, userInfo.UserId);
      }
    }

    public string GetQuoteItemOptionsHtml() {

      string html = "<option value=\"\">[select quote item]</option>";

      foreach (var item in QuoteItemsForList) {

        bool isSelected = item.QuoteItemId == ConsultingItemInfo.ComponentQuoteInfo?.QuoteItemId;
        decimal unallocated = item.TotalFunds - item.TotalRevenue;
        string optionText = item.CategoryName + " - " + WebHelper.HtmlToText(item.Description);

        html += "<option ";
        if (isSelected) html += "selected ";
        html += " value=\"" + item.QuoteItemId + "\">";
        if (unallocated == 0) {
          html += optionText.LimitLengthTo(90, " ...").HTMLEncode();
        } else {
          html += optionText.LimitLengthTo(70, " ...").HTMLEncode() + " (" + unallocated.ToString("C") + " unallocated)";
        }
        html += "</option>";
      }
      return html;
    }

    public string GetConsultantName() {
      if (ConsultingItemInfo.ConsultantUserId == null) return "Unassigned";
      return ConsultingItemInfo.ConsultantFullName;
    }

    public string GetConsultantDropdownHtml() {

      return WebHelper.GetPartnerDropdown(new WebHelper.PartnerDropdownInfo() {
        PartnerInfoList = PartnerList,
        FormName = FormFields.ConsultantUserId,
        LabelText = "Consultant:",
        InputCols = 3,
        SelectedPartnerUserId = ConsultingItemInfo.ConsultantUserId,
        CanViewHiddenPartners = CanViewHiddenPartners,
        CanViewInactivePartners = CanViewInactivePartners,
        IncludeUnassignedUser = true
      });
    }

    public string GetConsultingTypeOptions() {

      var html = new StringBuilder();
      html.Append("<option value=\"\">[Not Set]</option>");

      Query(@"
        SELECT ConsultingTypeId, ConsultingTypeName
        FROM al_ConsultingTypes
        ORDER BY ConsultingTypeName",
        dr => {
          html.Append("<option ");
          if (dr.GetInt("ConsultingTypeId") == ConsultingItemInfo.ConsultingTypeId) {
            html.Append("selected ");
          }
          html.Append("value=\"" + dr.GetInt("ConsultingTypeId") + "\">" + dr.GetString("ConsultingTypeName").HTMLEncode() + "</option>");
        }
      );

      return html.ToString();
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

    bool UpdateItem(AjaxSubmitHelper ajax, out string returnMessage) {

      returnMessage = "";

      // Form validation.

      var formValues = new FormValues();

      formValues.ConsultingItemId = ajax.CheckFieldID(FormFields.ConsultingItemId, "Item ID", false, "Invalid Item ID");
      formValues.ProgramJobId = ProgramInfo.ProgramJobId;
      formValues.Description = ajax.CheckFieldRegex(FormFields.Description, "Description", AppHelper.Regex.GeneralText, false, "Use only text characters in Description.");
      formValues.CompletionDateLocal = ajax.GetDatePickerDateUnspecified(FormFields.CompletionDateLocal, "Completion Date", false, "Please provide a completion date.");
      formValues.ConsultantUserId = ajax.CheckFieldID(FormFields.ConsultantUserId, "Consultant", false, "Please choose a Consultant.");
      formValues.ConsultingTypeId = ajax.CheckFieldIDOrNull(FormFields.ConsultingTypeId, "Consulting Type", false, "");

      if (!IsLimitedEdit) {
        // Required fields during full access.
        formValues.ItemTitle = ajax.CheckFieldRegex(FormFields.ItemTitle, "Title", AppHelper.Regex.GeneralText, true, "Use only text characters in Title.");
        formValues.RevenueAmount = ajax.CheckFieldDecimal(FormFields.RevenueAmount, "Amount", false, null, null, true, "Please provide a valid amount.") ?? 0;
      }

      if (CanSetQuoteItem) {
        formValues.QuoteItemId = ajax.CheckFieldIDOrNull(FormFields.QuoteItemId, "Quote Item", true, "");
      }
      if (CanMoveToProgram) {
        formValues.MoveToProgramJobId = ajax.CheckFieldIDOrNull(FormFields.MoveToProgramJobId, "Program", false, "");
      }

      if (ajax.BadFieldCount > 0) return false;

      if (CanSetQuoteItem && formValues.QuoteItemId != null) {

        // Get selected quote item.

        var selectedQuoteItem = QuoteItemsForList.Find(qi => qi.QuoteItemId == (int)formValues.QuoteItemId);

        if (selectedQuoteItem == null) {
          ajax.AddBadField(FormFields.QuoteItemId, "Invalid Quote Item");
          return false;
        }

        // Ensure item price doesn't exceed available funds for selected quote item (quote item funds minus sum of all components attached to it).
        decimal existingRevenue = IsNewItem ? selectedQuoteItem.TotalRevenue : selectedQuoteItem.TotalRevenueIgnoredComponent;
        decimal allocatedRevenue = formValues.RevenueAmount;

        if (existingRevenue + allocatedRevenue > selectedQuoteItem.TotalFunds) {
          ajax.AddDialogMessage("Allocation exceeds quote item amount for"
            + "<br/>" + selectedQuoteItem.ProductTitle.HTMLEncode()
            + "<br/>Available Funds: <b>" + (selectedQuoteItem.TotalFunds - existingRevenue).ToString("C") + "</b>"
            + "<br/>Tried to assign: <b>" + allocatedRevenue.ToString("C") + "</b>");
          return false;
        }
      }

      if (ajax.BadFieldCount > 0) return false;

      // Validation done, copy form values to workshop object.
      ApplyFormValues(formValues);

      // Update db.

      if (IsNewItem) {
        if (!UpdateDb_AddNew(ajax, out returnMessage)) return false;
      } else {
        if (!UpdateDb_Existing(ajax, formValues, out returnMessage)) return false;
      }

      return true;
    }

    // Assign form values to DTO, excluding limited-access fields.
    void ApplyFormValues(FormValues formValues) {

      if (!IsLimitedEdit) {
        ConsultingItemInfo.ItemTitle = formValues.ItemTitle;
        ConsultingItemInfo.ItemAmount = formValues.RevenueAmount;
        ConsultingItemInfo.ConsultantUserId = formValues.ConsultantUserId;
        ConsultingItemInfo.ConsultingTypeId = formValues.ConsultingTypeId;
      }

      if (CanSetQuoteItem) {
        ConsultingItemInfo.ComponentQuoteInfo.QuoteItemId = formValues.QuoteItemId;
      }
      ConsultingItemInfo.ProgramJobId = ProgramInfo.ProgramJobId;
      ConsultingItemInfo.Description = formValues.Description;
      ConsultingItemInfo.CompletionDateUtc = formValues.CompletionDateLocal.ToUniversalTimeOrNull(null);
    }

    bool UpdateDb_AddNew(AjaxSubmitHelper ajax, out string returnMessage) {

      returnMessage = "";

      try {
        DbHelper.ConsultingItems.AddConsultingItem(ConsultingItemInfo);
      } catch (Exception ex) {
        ajax.AddDialogMessage("Error adding new Item: " + ex.Message);
        return false;
      }

      return true;
    }

    bool UpdateDb_Existing(AjaxSubmitHelper ajax, FormValues formValues, out string returnMessage) {

      returnMessage = "";

      try {
        DbHelper.ConsultingItems.UpdateConsultingItem(ConsultingItemInfo);
      } catch (Exception ex) {
        if (ConfigHelper.IsDevServer) {
          ajax.AddDialogMessage("Error updating Item: " + ex.ToString());
        } else {
          ajax.AddDialogMessage("Unfortunately there was a problem updating this item.<br>The issue has been raised, please try again later.");
        }
        return false;
      }

      // Moving item to another program?
      // Note can only move within the same project (i.e. same JobNumber)
      if (CanMoveToProgram) {
        if (formValues.MoveToProgramJobId != null) {
          var moveToJob = DbHelper.AblePrograms.GetProgramInfoOrNull((int)formValues.MoveToProgramJobId);
          if (moveToJob != null && moveToJob.ProgramJobNumber == ConsultingItemInfo.JobNumber) {
            bool movedOk = DbHelper.ConsultingItems.MoveItemToProgram(ConsultingItemInfo.ConsultingItemId, moveToJob.ProgramJobId);
            if (!movedOk) returnMessage = "Item updated, but unable to move to other Program.";
          }
        }
      }

      return true;
    }

    bool DeleteItem(AjaxSubmitHelper ajax, out string returnMessage) {

      returnMessage = "";

      try {
        DbHelper.ConsultingItems.DeleteConsultingItem(null, ConsultingItemInfo.ConsultingItemId);
      } catch (Exception ex) {
        ajax.AddDialogMessage("Can't delete consulting item at this time.", ex);
        return false;
      }

      return true;
    }

  }
}
