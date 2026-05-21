using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Integral.Web.Services;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class Workshops : AppCode.PageBaseClasses.ProgramPageBase {

    public bool IsNewWorkshop = false;
    public bool WorkshopListVisible = false;
    public bool WorkshopFormVisible = false;
    public List<DbHelper.WorkshopEvents.WorkshopEventInfo> WorkshopEventList = null;  // For list display.
    public DbHelper.WorkshopEvents.WorkshopEventInfo WorkshopEventInfo = null;        // For details display.
    public DbHelper.AlbertCoaches.AlbertCoachInfo FacilitatorUserInfo = null;
    public List<DbHelper.AbleQuotes.QuoteItemForList> QuoteItemsForList;
    public bool CanAddWorkshop, IsLimitedEdit, IsReadOnly, CanDeleteWorkshop, CanSetQuoteItem, CanMoveToProgram, CanSeeBookingColumn, CanCopyWorkshop;
    public bool CanNavigateFromWorkshopTable, CanViewTotalRevenue, CanViewAllDeliveryTeamRevenue, CanViewPartnerRevenue;
    public const string IsVirtual_No_Text = "F2F";
    public const string IsVirtual_No_Value = "0";
    public const string IsVirtual_Yes_Text = "Digital";
    public const string IsVirtual_Yes_Value = "1";
    public decimal PartnerRevenue = 0, PLCRevenue = 0, SalesRevenue = 0;
    public List<DbHelper.WorkshopEvents.AttendanceInfo> AttendanceList;
    public int WorkshopStatus_Confirmed = DbHelper.WorkshopStatus.WorkshopStatus_Confirmed.WorkshopStatusId;
    private List<DbHelper.AlbertCoaches.AlbertCoachInfo> PartnerList;
    private List<DbHelper.CoachingSessionTypes.SessionType> SessionTypesList;

    public class TabName {
      public const string Details = "details";
      public const string Attendance = "attendance";
    }

    public class FormFields {
      public const string FriendlyWorkshopId = "FriendlyWorkshopId";
      public const string WorkshopEventId = "WorkshopEventId";
      public const string WorkshopTitle = "WorkshopTitle";
      public const string SessionTypeId = "SessionTypeId";
      public const string Location = "Location";
      public const string WorkshopStatus = "WorkshopStatus";
      public const string WorkshopRevenue = "WorkshopRevenue";
      public const string IsVirtual = "IsVirtual";
      public const string TimeZoneIdIana = "TimeZoneIdIana";
      public const string StartDate = "StartDate";
      public const string StartTime = "StartTime";
      public const string EndTime = "EndTime";
      public const string KeyFacilitatorUserId = "KeyFacilitatorUserId";
      public const string CoFacilitatorUserId = "CoFacilitatorUserId";
      public const string IsRTO = "IsRTO";
      public const string DisableEvals = "DisableEvals";
      public const string WorkshopNotes = "WorkshopNotes";
      public const string QuoteItemId = "QuoteItemId";
      public const string MoveToProgramJobId = "MoveToProgramJobId";
      public const string AddParticipantsToInvite = "AddParticipantsToInvite";
      public const string ParticipantAdditionalInfo = "ParticipantAdditionalInfo";
      public const string PartnerRevenue = "PartnerRevenue";
      public const string PLCRevenue = "PLCRevenue";
      public const string SalesRevenue = "SalesRevenue";
      public const string WorkshopAttendanceIds = "WorkshopAttendanceIds";
      public const string CopyWorkshopTitle = "CopyWorkshopTitle";
      public const string CopyWorkshopQuoteItemId = "CopyWorkshopQuoteItemId";
      public const string HideFromProgramContent = "HideFromProgramContent";
    }

    class FormValues {
      public string WorkshopTitle;
      public string Location;
      public string TimeZoneIdIana;
      public DateTime? StartDateLocal;
      public TimeSpan? StartTimeLocal;
      public TimeSpan? EndTimeLocal;
      public int KeyFacilitatorUserId;
      public int? CoFacilitatorUserId;
      public int? WorkshopStatusId;
      public int? SessionTypeId;
      public decimal? WorkshopRevenue;
      public bool IsVirtual;
      public bool IsRTO;
      public bool DisableEvals;
      public string Notes;
      public int? QuoteItemId;
      public int? MoveToProgramJobId;
      public TimeZoneInfo timeZoneInfo = ConfigHelper.DefaultTimeZoneInfo;
      public bool AddParticipantsToInvite;
      public string ParticipantAdditionalInfo;
      public bool HideFromProgramContent;
    }

    public class AjaxAction {
      public const string UpdateWorkshop = "UpdateWorkshop";
      public const string DeleteWorkshop = "DeleteWorkshop";
      public const string UpdateAttendance = "UpdateAttendance";
      public const string CopyWorkshop = "CopyWorkshop";
    }

    public class DataAttr {
      public const string WorkshopType = "wt";
    }

    public enum WorkshopTypeEnum { F2F, Digital }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      IsNewWorkshop = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.WorkshopId) == PathHelper.AbleUrlValues.IdNew;

      CanAddWorkshop = SessionHelper.AppAccess.Programs.Workshops.CanAdd(ProgramInfo);
      CanSetQuoteItem = SessionHelper.AppAccess.Programs.CanSetQuoteItem(ProgramInfo);
      CanMoveToProgram = !IsNewWorkshop && SessionHelper.AppAccess.Programs.CanMoveToProgram(ProgramInfo);
      CanNavigateFromWorkshopTable = SessionHelper.AppAccess.Programs.Workshops.CanNavigateFromWorkshopTable(ProgramInfo);
      PartnerList = DbHelper.AlbertCoaches.GetCoachInfoList(true, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);
      SessionTypesList = DbHelper.CoachingSessionTypes.SessionTypeList.FindAll(s => s.IsGroup);

      CanViewAllDeliveryTeamRevenue = SessionHelper.AppAccess.Programs.Revenue.CanViewAllDeliveryTeamRevenue(ProgramInfo);
      CanViewPartnerRevenue = SessionHelper.AppAccess.Programs.Revenue.CanViewPartnerRevenue(ProgramInfo);
      CanViewTotalRevenue = SessionHelper.AppAccess.Programs.Revenue.CanViewTotalRevenue(ProgramInfo);

      if (IsNewWorkshop) { // New workshop.

        if (!CanAddWorkshop) { // No access to adding new item.

          RespondNoAccessOrRedirect();
          return new EmptyResult();

        } else { // Allowed to add new workshop.

          WorkshopEventInfo = new DbHelper.WorkshopEvents.WorkshopEventInfo(ProgramInfo.ProgramJobId, ProgramInfo.ProgramJobNumber);
          SetNewWorkshopDefaults();

          QuoteItemsForList = DbHelper.AbleQuotes.GetQuoteItemsForList(ProgramInfo.ProgramJobNumber, false,
            DbHelper.ProgramComponents.KeyColumnEnum.WorkshopEventId, null, ProgramInfo.ProgramJobId);

          if (SystemWeb.IsHttpPost) { // Submitted new item form.

            AjaxSubmitHelper.Process(ajax => {
              var ajaxAction = "" + WebHelper.GetFormValue(PathHelper.FormKeys.AjaxAction);
              if (ajaxAction == AjaxAction.UpdateWorkshop) {
                bool success = UpdateWorkshop(ajax, out string returnMessage);
                if (success) {
                  ajax.SetRedirectUrl(PathHelper.Pages.Workshops_List(ProgramInfo.ProgramJobId), returnMessage,
                    IsNewWorkshop ? AjaxSubmitHelper.PageMessageType.SuccessDialog : AjaxSubmitHelper.PageMessageType.SuccessToast);
                } else {
                  ajax.AddDialogMessage(returnMessage);
                }
              }
            });
            return new EmptyResult();

          } else { // Display new item form.

            PageTitle = "Add Workshop";
            WorkshopFormVisible = true;

          }
        }
        return Page();

      } else { // Not a new item.

        int? urlWorkshopEventId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.WorkshopId);

        if (urlWorkshopEventId == null) { // No workshop id given.

          if (SystemWeb.IsHttpPost) {
            // Invalid condition.
            AjaxSubmitHelper.RespondNoAccessToFunction();
            return new EmptyResult();
          } else {
            // A GET with no item id means show the list.
            ShowList();
          }
          return Page();

        } else { // Workshop id given.

          // Get workshop data.
          // Note including current Program ensures user has access to the given workshop id in the url.
          try {
            WorkshopEventInfo = DbHelper.WorkshopEvents.GetWorkshopInfo(ProgramInfo, (int)urlWorkshopEventId);
          } catch (Exception ex) {

            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation("Workshops_GetWorkshopInfo")
              .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
                  .WithProperty("ProgramJobId", ProgramInfo?.ProgramJobId)
              .WithProperty("WorkshopEventId", urlWorkshopEventId)
              .Track();
          }

          if (WorkshopEventInfo == null) { // Workshop not found.

            RespondNoAccessOrRedirect();
            return new EmptyResult();

          } else { // Workshop found.

            if (!SessionHelper.AppAccess.Programs.Workshops.CanView(ProgramInfo, WorkshopEventInfo)) {
              // Not allowed to view.
              RespondNoAccessOrRedirect();
              return new EmptyResult();
            }

            IsReadOnly = SessionHelper.AppAccess.Programs.Workshops.ReadOnly(ProgramInfo, WorkshopEventInfo);
            IsLimitedEdit = SessionHelper.AppAccess.Programs.Workshops.LimitedEdit(ProgramInfo, WorkshopEventInfo);
            CanDeleteWorkshop = SessionHelper.AppAccess.Programs.Workshops.CanDelete(ProgramInfo, WorkshopEventInfo);
            CanCopyWorkshop = SessionHelper.AppAccess.Programs.Workshops.CanCopyWorkshop(ProgramInfo, WorkshopEventInfo);

            if (WorkshopEventInfo.KeyFacilitatorUserId != null) {
              FacilitatorUserInfo = DbHelper.AlbertCoaches.GetCoachInfo((int)WorkshopEventInfo.KeyFacilitatorUserId);
            }

            QuoteItemsForList = DbHelper.AbleQuotes.GetQuoteItemsForList(ProgramInfo.ProgramJobNumber, false,
              DbHelper.ProgramComponents.KeyColumnEnum.WorkshopEventId, urlWorkshopEventId, ProgramInfo.ProgramJobId);

            // Attendance
            AttendanceList = DbHelper.WorkshopEvents.GetAttendanceList(WorkshopEventInfo);

            if (SystemWeb.IsHttpPost) {

              if (PageAjaxAction == AjaxAction.UpdateAttendance) {

                AjaxSubmitHelper.Process(ajax => {
                  UpdateAttendance(ajax);
                });

              } else {

                // Process submitted form.
                AjaxSubmitHelper.Process(ajax => {

                  if (IsReadOnly && ajax.Action != AjaxAction.CopyWorkshop) {

                    ajax.RespondNoAccessToFunction(); // Read-only, no update or delete allowed.
                    return;

                  } else {

                    string returnMessage = "";
                    bool success = false;

                    switch (ajax.Action) {

                      case AjaxAction.UpdateWorkshop:
                        success = UpdateWorkshop(ajax, out returnMessage);
                        break;

                      case AjaxAction.DeleteWorkshop:
                        if (!CanDeleteWorkshop) {
                          ajax.RespondNoAccessToFunction(); // No deletion allowed if limited access.
                          return;
                        } else {
                          success = DeleteWorkshop(ajax, out returnMessage);
                        }
                        break;

                      case AjaxAction.CopyWorkshop:
                        if (!CanCopyWorkshop) {
                          ajax.RespondNoAccessToFunction(); // No copy allowed if limited access.
                          return;
                        } else {
                          CopyWorkshop(ajax, out returnMessage);
                        }
                        break;
                    }

                    if (success) {
                      ajax.SetRedirectUrl(PathHelper.Pages.Workshops_List(WorkshopEventInfo.ProgramJobId), returnMessage,
                        IsNewWorkshop ? AjaxSubmitHelper.PageMessageType.SuccessDialog : AjaxSubmitHelper.PageMessageType.SuccessToast);
                    } else {
                      ajax.AddDialogMessage(returnMessage);
                    }
                  }

                }
                );
              }
              return new EmptyResult();

            } else { // Not a POST

              // Show form.
              CalculateRevenues();
              PageTitle = "Update Workshop";
              WorkshopFormVisible = true;

            }
          }
        }
      }

      return Page();
    }

    public string GetWorkshopTableRowLinkAttr() {
      return CanNavigateFromWorkshopTable.ToValue($"data-rowlink-url=\"{PathHelper.Pages.Workshops_Edit(ProgramInfo.ProgramJobId, null)}\"", "");
    }

    public string GetViewBookingLink() {

      if (IsNewWorkshop) return "-";
      if (FacilitatorUserInfo == null) return "Facilitator Required";
      if (WorkshopEventInfo.SessionTypeId == null) return "Session Type Required";
      if (WorkshopEventInfo.KeyFacilitatorCalendlyUrlName.IsNullOrEmpty()) return "Facilitator user requires Calendly stub.";

      return
        "<a href=\""
          + GetCalendlyGroupBookingUrl(WorkshopEventInfo, true)
          + "\" target=\"_blank\">"
          + GetCalendlyGroupBookingUrl(WorkshopEventInfo, false).HTMLEncode()
          + "</a>";
    }

    public string GetCalendlyGroupBookingUrl(DbHelper.WorkshopEvents.WorkshopEventInfo we, bool urlEncodeParams) {

      return Integrations.Calendly.GetCalendlyGroupBookingUrl(
        we.KeyFacilitatorCalendlyUrlName,
        DbHelper.CoachingSessionTypes.GetSessionTypeById((int)we.SessionTypeId).CalendlyUrlSlug,
        userInfo.GetFullName(), userInfo.EmailAddress, ProjectInfo.ProjectName, we.FriendlyWorkshopId, we.WorkshopEventUID, urlEncodeParams);
    }

    public string GetListBookingLink(DbHelper.WorkshopEvents.WorkshopEventInfo we) {
      if (we.KeyFacilitatorUserId == null || we.SessionTypeId == null || we.WorkshopStatusId == WorkshopStatus_Confirmed) return "";
      return WebHelper.GetActionLink(WebHelper.ActionButtonTypeEnum.book, "cal-book-link button", "Book", "", GetCalendlyGroupBookingUrl(we, true));
    }

    public string GetSessionTypeOptions() {
      string html = "<option value=\"\">[select session type]</option>";
      foreach (var item in SessionTypesList) {
        bool isSelected = item.SessionTypeId == WorkshopEventInfo.SessionTypeId;
        string dataWorkshopType = $"data-{DataAttr.WorkshopType}=\"" + (item.InPerson ? WorkshopTypeEnum.F2F : WorkshopTypeEnum.Digital) + "\"";
        html += $"<option {(isSelected ? "selected" : "")} {dataWorkshopType} value=\"{item.SessionTypeId}\">{item.DisplayName.HTMLEncode()}</option>";
      }
      return html;
    }

    public string GetTimeZoneOptions() {

      string selected = WorkshopEventInfo?.TimeZoneIdIana.ValueIfNullOrEmpty(ConfigHelper.DefaultTimeZoneIdIana);
      var html = new StringBuilder();
      int maxLen = 0;
      foreach (var item in TimeHelper.GetIANATimeZonesForSelect()) {
        html.Append($@"<option value=""{item.Key.HTMLEncode()}"" ");
        if (item.Key.Equals(selected, StringComparison.OrdinalIgnoreCase)) html.Append("selected ");
        html.Append($">{item.Value.HTMLEncode()}</option>");
        if (item.Key.Length > maxLen) maxLen = item.Key.Length;
      }
      html.Append("<option>" + maxLen + "</option>");
      return html.ToString();
    }

    void CalculateRevenues() {
      if (WorkshopEventInfo.WorkshopRevenue != null) {
        PLCRevenue = WorkshopEventInfo.WorkshopRevenue.Value * WorkshopEventInfo.ProgramPLCPercentage.GetValueOrDefault(0);
        PartnerRevenue = WorkshopEventInfo.WorkshopRevenue.Value * WorkshopEventInfo.ProgramDeliveryPercentage.GetValueOrDefault(0);
        SalesRevenue = WorkshopEventInfo.WorkshopRevenue.Value * WorkshopEventInfo.ProgramSalesPercentage.GetValueOrDefault(0);
      }
    }

    // If POST, return fail status, otherwise redirect back to the list.
    void RespondNoAccessOrRedirect() {
      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.RespondNoAccessToFunction();
        WebHelper.EndRequest();
        return;
      } else {
        WebHelper.Redirect(PathHelper.Pages.Workshops_List()); // Not allowed to add new item.
        return;
      }
    }

    void ShowList() {

      PageTitle = "Workshops";
      WorkshopListVisible = true;

      if (SessionHelper.AppAccess.Programs.Workshops.CanViewAllInProgram(ProgramInfo)) {
        // List all items in the Program.
        WorkshopEventList = DbHelper.WorkshopEvents.GetWorkshopsInProgram(ProgramInfo.ProgramJobId);
      } else {
        // List only items where user is a facilitator.
        WorkshopEventList = DbHelper.WorkshopEvents.GetWorkshopsInProgramForUser(ProgramInfo, SessionHelper.UserInfo);
      }

      CanSeeBookingColumn = WorkshopEventList.Exists(w => w.KeyFacilitatorUserId != null && w.SessionTypeId != null && w.WorkshopStatusId != WorkshopStatus_Confirmed);
    }

    public string GetStartDateForSlideout(DbHelper.WorkshopEvents.WorkshopEventInfo workshopRowInfo) {
      return workshopRowInfo.WhenStartLocal.ToString("ddd, d MMM yyyy, h:mmtt")
        + " - " + workshopRowInfo.WhenEndLocal.ToString("h:mmtt");
    }

    private bool IsFinalWorkshop(DbHelper.WorkshopEvents.WorkshopEventInfo workshop) {
      if (WorkshopEventList.IsNullOrEmpty()) return false;
      return workshop.WorkshopEventId == WorkshopEventList.Last().WorkshopEventId;
    }

    public string GetEvalScore(DbHelper.WorkshopEvents.WorkshopEventInfo workshop) {

      if (workshop.DisableEvals
        || ProjectInfo.WorkshopSessionEvalSurveyDisabled
        || (ProjectInfo.WorkshopAndProgramEvalSurveyDisabled && IsFinalWorkshop(workshop))) {

        return "<small>Disabled</small>";
      }

      if (workshop.LastEvalSentUtc == null) { // Not yet sent.
        return "";
      }

      if (workshop.HasEvalScore) { // Score exists.
        return $@"<a href=""{PathHelper.Reports.EvalViewer(workshop)}"" class=""action-button align-center"">"
          + (workshop.EvalScoreSum.GetValueOrDefault(0) / (decimal)workshop.EvalScoreCount).ToString("0.0")
          + "</a>";
      } else {
        return $"<small>Sent<br/>{WebHelper.DisplayDate(workshop.GetLastEvalSentLocal())}</small>";
      }
    }

    public string GetEvalLink(DbHelper.WorkshopEvents.WorkshopEventInfo workshop) {
      return $@"<a href=""{PathHelper.Reports.EvalViewer(workshop, true)}"">"
        + PathHelper.Reports.EvalViewer(workshop, true)
        + "</a>";
    }

    void SetNewWorkshopDefaults() {
      var todayLocal = TimeHelper.UtcToTimeZoneId(DateTime.UtcNow, userInfo.TimeZoneIdIana).Value.Date;
      WorkshopEventInfo.SetTime(todayLocal.AddHours(9), todayLocal.AddHours(17), userInfo.TimeZoneIdIana);
      WorkshopEventInfo.FriendlyWorkshopId = "[TBA]";
    }

    public string GetStatusOptions(string labelText) {
      string selectedValue = WorkshopEventInfo.WorkshopStatusId.ToStringOrNull();
      var optionList = new List<WebHelper.ButtonGroupButton>();
      optionList.Add(new WebHelper.ButtonGroupButton("None", null));
      foreach (var status in DbHelper.WorkshopStatus.GetWorkshopStatusList())
        optionList.Add(new WebHelper.ButtonGroupButton(status.WorkshopStatusName, status.WorkshopStatusId.ToString()));
      return WebHelper.GetButtonGroup(labelText, FormFields.WorkshopStatus, optionList, selectedValue, IsReadOnly);
    }

    public string GetQuoteItemOptions() {

      string html = "<option value=\"\">[select quote item]</option>";

      foreach (var item in QuoteItemsForList) {

        bool isSelected = item.QuoteItemId == WorkshopEventInfo?.ComponentQuoteInfo?.QuoteItemId;
        decimal unallocated = item.TotalFunds - item.TotalRevenue;
        string optionText = item.CategoryName + " - " + WebHelper.HtmlToText(item.Description);

        html += "<option ";
        if (isSelected) html += "selected ";
        html += " value=\"" + item.QuoteItemId + "\">";
        if (unallocated == 0) {
          html += optionText.LimitLengthTo(80, " ...").HTMLEncode();
        } else {
          html += "(" + unallocated.ToString("C") + " unallocated) " + optionText.LimitLengthTo(60, " ...").HTMLEncode();
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

    public string GetPartnerDropdownHtml(string labelText, string formName, int inputCol, int? selectedPartnerId) {

      return WebHelper.GetPartnerDropdown(new WebHelper.PartnerDropdownInfo() {
        PartnerInfoList = PartnerList,
        FormName = formName,
        IsReadOnly = IsReadOnly || IsLimitedEdit,
        LabelText = labelText,
        InputCols = inputCol,
        SelectedPartnerUserId = selectedPartnerId,
        CanViewHiddenPartners = CanViewHiddenPartners,
        CanViewInactivePartners = CanViewInactivePartners,
        IncludeUnassignedUser = true
      });
    }

    void CopyWorkshop(AjaxSubmitHelper ajax, out string returnMessage) {

      var workshopTitle = ajax.CheckFieldRegex(FormFields.CopyWorkshopTitle, "Workshop Title", AppHelper.Regex.GeneralText, false, "Use plain characters for Workshop Title.");
      var quoteItemId = ajax.CheckFieldIDOrNull(FormFields.CopyWorkshopQuoteItemId, "Quote Item", true, "");

      if (!CanQuoteBeAssigned(quoteItemId, WorkshopEventInfo.WorkshopRevenue, out returnMessage)) {
        ajax.AddDialogMessage(returnMessage);
        return;
      }

      if (ajax.BadFieldCount > 0) return;

      if (workshopTitle.IsNullOrEmpty()) {
        ajax.AddDialogMessage("Please provide a title for the new workshop.");
        return;
      }

      var newWorkshop = new DbHelper.WorkshopEvents.NewWorkshopInfo();

      WorkshopEventInfo.WorkshopTitle = workshopTitle;
      WorkshopEventInfo.ComponentQuoteInfo.QuoteItemId = quoteItemId;
      WorkshopEventInfo.WorkshopStatusId = DbHelper.WorkshopStatus.WorkshopStatus_NotPlanned.WorkshopStatusId;

      try {
        DbHelper.WorkshopEvents.AddWorkshopEvent(WorkshopEventInfo);
      } catch (Exception ex) {

        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("CopyWorkshop")
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .WithProperty("ProgramJobId", ProgramInfo?.ProgramJobId)
          .WithProperty("WorkshopTitle", workshopTitle)
          .WithProperty("QuoteItemId", quoteItemId)
          .Track();
        ajax.AddDialogMessage("Error adding new Workshop: " + ex.Message);
        return;
      }

      ajax.SetRedirectUrl(PathHelper.Pages.Workshops_List(ProgramInfo.ProgramJobId), "Workshop copied with ID " + WorkshopEventInfo.FriendlyWorkshopId);
    }

    bool UpdateWorkshop(AjaxSubmitHelper ajax, out string returnMessage) {

      returnMessage = "";

      // Form validation.

      var formValues = new FormValues();

      if (!IsLimitedEdit) {
        formValues.WorkshopTitle = ajax.CheckFieldRegex(FormFields.WorkshopTitle, "Workshop Title", AppHelper.Regex.GeneralText, true, "Use only text characters in Workshop Title.");
        formValues.Location = ajax.CheckFieldRegex(FormFields.Location, "Location", AppHelper.Regex.GeneralText, true, "Use only text characters in Location.");
        formValues.WorkshopRevenue = ajax.CheckFieldDecimal(FormFields.WorkshopRevenue, "Workshop Revenue", false, 0, null, true, "Please provide a valid amount.");
        formValues.KeyFacilitatorUserId = ajax.CheckFieldID(FormFields.KeyFacilitatorUserId, "Key Facilitator", true, "Please choose a Key Facilitator.");
        formValues.CoFacilitatorUserId = ajax.CheckFieldIDOrNull(FormFields.CoFacilitatorUserId, "Co-Facilitator", true, "Please choose a Co-Facilitator.");
        formValues.SessionTypeId = ajax.CheckFieldIDOrNull(FormFields.SessionTypeId, "Session Type", true, "Please choose a Session Type.");
        var sessionTypeInfo = SessionTypesList.Find(x => x.SessionTypeId == formValues.SessionTypeId);
        if (sessionTypeInfo == null) {
          ajax.AddBadField(FormFields.SessionTypeId, "Please choose a Session Type.");
        } else {
          formValues.IsVirtual = !sessionTypeInfo.InPerson;
        }
      }
      formValues.WorkshopStatusId = ajax.CheckFieldIDOrNull(FormFields.WorkshopStatus, "Status", false, "");
      formValues.StartDateLocal = ajax.GetDatePickerDateUnspecified(FormFields.StartDate, "Workshop Date", false, "Please provide a date.");
      formValues.StartTimeLocal = ajax.CheckTimePickerSpan(FormFields.StartTime, "Start Time", false, "Please provide a valid Start Time.");
      formValues.EndTimeLocal = ajax.CheckTimePickerSpan(FormFields.EndTime, "End Time", false, "Please provide a valid End Time.");

      // Removed from UI. If new workshop, set to false. If existing, use existing value.
      formValues.IsRTO = IsNewWorkshop ? false : WorkshopEventInfo.IsRTO;

      formValues.DisableEvals = ajax.CheckFieldBool(FormFields.DisableEvals, WebHelper.DefaultCheckboxValue);
      formValues.AddParticipantsToInvite = ajax.CheckFieldBool(FormFields.AddParticipantsToInvite, WebHelper.DefaultCheckboxValue);
      formValues.HideFromProgramContent = ajax.CheckFieldBool(FormFields.HideFromProgramContent, WebHelper.DefaultCheckboxValue);
      formValues.ParticipantAdditionalInfo = ajax.CheckFieldRegex(FormFields.ParticipantAdditionalInfo, "Participant Additional Info", AppHelper.Regex.HTML, false, "Only use plain text characters.");
      formValues.Notes = ajax.CheckFieldRegex(FormFields.WorkshopNotes, "Notes", AppHelper.Regex.HTML, false, "Invalid Characters Detected in Notes.");

      formValues.TimeZoneIdIana = ajax.CheckFieldRegex(FormFields.TimeZoneIdIana, "Time Zone", AppHelper.Regex.GeneralText, true, "Please select a Time Zone.");

      if (!TimeHelper.ValidateIANATimeZone(ref formValues.TimeZoneIdIana, false, false)) {
        ajax.AddBadField(FormFields.TimeZoneIdIana, "Please select a Time Zone.");
        return false;
      }

      if (formValues.SessionTypeId != null) {
        if (DbHelper.CoachingSessionTypes.GetSessionTypeById((int)formValues.SessionTypeId) == null) {
          ajax.AddBadField(FormFields.SessionTypeId, "Please select a Session Type.");
          return false;
        }
      }

      var workshopStatus = DbHelper.WorkshopStatus.GetWorkshopStatusByIdOrNull(formValues.WorkshopStatusId);
      bool dateRequired = (workshopStatus != null && !workshopStatus.AllowBlankDate && !SessionHelper.IsUserRoleAdmin);
      if (dateRequired && formValues.StartDateLocal == null) ajax.AddBadField(FormFields.StartDate, "Start Date is required.");
      if (formValues.StartDateLocal != null) {
        if (formValues.StartTimeLocal == null) ajax.AddBadField(FormFields.StartTime, "Start Time is required.");
        if (formValues.EndTimeLocal == null) ajax.AddBadField(FormFields.EndTime, "End Time is required.");
        if (formValues.EndTimeLocal <= formValues.StartTimeLocal) ajax.AddBadField(FormFields.EndTime, "End Time must be after Start Time");
      }

      if (CanSetQuoteItem) {
        formValues.QuoteItemId = ajax.CheckFieldIDOrNull(FormFields.QuoteItemId, "Quote Item", true, "");
      }
      if (CanMoveToProgram) {
        formValues.MoveToProgramJobId = ajax.CheckFieldIDOrNull(FormFields.MoveToProgramJobId, "Program", false, "");
      }

      if (ajax.BadFieldCount > 0) return false;

      if (!CanQuoteBeAssigned(formValues.QuoteItemId, formValues.WorkshopRevenue, out returnMessage)) {
        ajax.AddDialogMessage(returnMessage);
        return false;
      }

      if (ajax.BadFieldCount > 0) return false;

      // Validation done, copy form values to workshop object.
      ApplyFormValues(formValues);

      // Update db.

      if (IsNewWorkshop) {
        if (!UpdateDb_AddNew(ajax, out returnMessage)) return false;
      } else {
        if (!UpdateDb_Existing(ajax, formValues, out returnMessage)) return false;
      }

      return true;
    }

    private bool CanQuoteBeAssigned(int? quoteItemId, decimal? workshopRevenue, out string returnMessage) {
      returnMessage = "";
      // Get selected quote item.

      var selectedQuoteItem = QuoteItemsForList.Find(qi => qi.QuoteItemId == (int)quoteItemId);
      if (selectedQuoteItem == null) {
        returnMessage = "Invalid Quote Item";
        return false;
      }

      // Ensure item price doesn't exceed available funds for selected quote item (quote item funds minus sum of all components attached to it).
      decimal existingRevenue = IsNewWorkshop ? selectedQuoteItem.TotalRevenue : selectedQuoteItem.TotalRevenueIgnoredComponent;
      decimal allocatedRevenue = workshopRevenue.GetValueOrDefault(0);

      if (existingRevenue + allocatedRevenue > selectedQuoteItem.TotalFunds) {
        returnMessage = "Allocation exceeds quote item amount for"
          + "<br/>" + selectedQuoteItem.ProductTitle.HTMLEncode()
          + "<br/>Available Funds: <b>" + (selectedQuoteItem.TotalFunds - existingRevenue).ToString("C") + "</b>"
          + "<br/>Tried to assign: <b>" + allocatedRevenue.ToString("C") + "</b>";
        return false;
      }

      return true;
    }

    // Assign form values to DTO, excluding limited-access fields.
    void ApplyFormValues(FormValues formValues) {

      DateTime? startLocal = null, endLocal = null;
      if (formValues.StartDateLocal != null) {
        // Assume StartTimeLocal and EndTimeLocal are not null, which is enforced during form validation.
        startLocal = (DateTime?)((DateTime)formValues.StartDateLocal).Add((TimeSpan)formValues.StartTimeLocal);
        endLocal = (DateTime?)((DateTime)formValues.StartDateLocal).Add((TimeSpan)formValues.EndTimeLocal);
      }

      if (!IsLimitedEdit) {
        WorkshopEventInfo.WorkshopTitle = formValues.WorkshopTitle;
        WorkshopEventInfo.Location = formValues.Location;
        WorkshopEventInfo.WorkshopRevenue = formValues.WorkshopRevenue;
        WorkshopEventInfo.KeyFacilitatorUserId = formValues.KeyFacilitatorUserId;
        WorkshopEventInfo.CoFacilitatorUserId = formValues.CoFacilitatorUserId;
        WorkshopEventInfo.SessionTypeId = formValues.SessionTypeId;
      }
      WorkshopEventInfo.WorkshopStatusId = formValues.WorkshopStatusId;
      WorkshopEventInfo.IsVirtual = formValues.IsVirtual;
      WorkshopEventInfo.SetTime(startLocal, endLocal, formValues.TimeZoneIdIana);
      WorkshopEventInfo.IsRTO = formValues.IsRTO;
      WorkshopEventInfo.DisableEvals = formValues.DisableEvals;
      WorkshopEventInfo.AddParticipantsToInvite = formValues.AddParticipantsToInvite;
      WorkshopEventInfo.ParticipantAdditionalInfo = formValues.ParticipantAdditionalInfo;
      WorkshopEventInfo.WorkshopNotes = formValues.Notes;
      WorkshopEventInfo.HideFromProgramContent = formValues.HideFromProgramContent;

      if (CanSetQuoteItem) {
        WorkshopEventInfo.ComponentQuoteInfo.QuoteItemId = formValues.QuoteItemId;
      }
    }

    bool UpdateDb_AddNew(AjaxSubmitHelper ajax, out string returnMessage) {

      returnMessage = "";

      try {
        DbHelper.WorkshopEvents.AddWorkshopEvent(WorkshopEventInfo);
      } catch (Exception ex) {

        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("AddWorkshopEvent")
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .WithProperty("ProgramJobId", ProgramInfo?.ProgramJobId)
          .WithProperty("WorkshopTitle", WorkshopEventInfo?.WorkshopTitle)
          .Track();
        ajax.AddDialogMessage("Error adding new Workshop: " + ex.Message);
        return false;
      }

      returnMessage = "Workshop added, new ID is: " + WorkshopEventInfo.FriendlyWorkshopId;

      string postResult = PostToEventGrid(WorkshopEventInfo.WorkshopEventId, Integrations.EventGrid.WorkshopSession.WorkshopUpdateEventType.added);
      if (!postResult.IsNullOrEmpty()) {
        returnMessage = returnMessage.AppendWithSeparator(" ", "However, could not post to EventGrid: " + postResult);
      }

      // Track workshop creation in Intercom
      SendEvent(
        intercom => {
          var builder = intercom
            .WorkshopCreated()
            .WithWorkshopId(WorkshopEventInfo.WorkshopEventId)
            .WithFriendlyWorkshopId(WorkshopEventInfo.FriendlyWorkshopId)
            .WithWorkshopTitle(WorkshopEventInfo.WorkshopTitle)
            .WithProgramId(ProgramInfo.ProgramJobId)
            .WithCompanyName(ProgramInfo.CompanyName)
            .WithLocation(WorkshopEventInfo.Location)
            .WithIsVirtual(WorkshopEventInfo.IsVirtual);

          if (WorkshopEventInfo.WhenStartUtc.HasValue) {
            // Explicitly specify UTC offset (TimeSpan.Zero) to ensure correct Unix timestamp
            builder.WithStartDateUtc(new DateTimeOffset(WorkshopEventInfo.WhenStartUtc.Value, TimeSpan.Zero));
          }

          if (WorkshopEventInfo.WorkshopRevenue.HasValue) {
            builder.WithRevenue(WorkshopEventInfo.WorkshopRevenue);
          }

          if (WorkshopEventInfo.KeyFacilitatorUserId.HasValue) {
            builder.WithKeyFacilitatorUserId(WorkshopEventInfo.KeyFacilitatorUserId);
          }

          return builder
            .WithUser(SessionHelper.UserInfo.UserId, SessionHelper.AsExternalUserId())
            .WithEmail(userInfo.EmailAddress)
            .WithOrganisation(userInfo.OrgId, userInfo.OrgName);
        },
        operationName: "Workshops_WorkshopCreated",
        requestRawUrl: SystemWeb.RequestRawUrl,
        telemetryProperties: new Dictionary<string, object> {
          ["WorkshopEventId"] = WorkshopEventInfo.WorkshopEventId,
          ["FriendlyWorkshopId"] = WorkshopEventInfo.FriendlyWorkshopId,
          ["ProgramJobId"] = ProgramInfo.ProgramJobId,
          ["CompanyName"] = ProgramInfo.CompanyName
        }
      );

      return true;
    }

    bool UpdateDb_Existing(AjaxSubmitHelper ajax, FormValues formValues, out string returnMessage) {

      returnMessage = "";

      try {
        DbHelper.WorkshopEvents.UpdateWorkshopEvent(WorkshopEventInfo);
      } catch (Exception ex) {

        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("UpdateWorkshopEvent")
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .WithProperty("ProgramJobId", ProgramInfo?.ProgramJobId)
          .WithProperty("WorkshopEventId", WorkshopEventInfo?.WorkshopEventId)
          .WithProperty("WorkshopTitle", WorkshopEventInfo?.WorkshopTitle)
          .Track();
        ajax.AddDialogMessage("Error updating Workshop: " + ex.Message);
        return false;
      }

      // Moving workshop to another program?
      // Note can only move within the same project (i.e. same JobNumber)
      if (CanMoveToProgram) {
        if (formValues.MoveToProgramJobId != null) {
          var moveToProgram = DbHelper.AblePrograms.GetProgramInfoOrNull((int)formValues.MoveToProgramJobId);
          if (moveToProgram != null && moveToProgram.ProgramJobNumber == WorkshopEventInfo.ProgramJobNumber) {
            bool movedOk = DbHelper.WorkshopEvents.MoveWorkshopToProgram(WorkshopEventInfo.WorkshopEventId, moveToProgram.ProgramJobId);
            if (!movedOk) returnMessage = "Workshop updated, but unable to move to other Program.";
          }
        }
      }

      string postResult = PostToEventGrid(WorkshopEventInfo.WorkshopEventId, Integrations.EventGrid.WorkshopSession.WorkshopUpdateEventType.updated);
      if (!postResult.IsNullOrEmpty()) {
        returnMessage = returnMessage.AppendWithSeparator(" ", "Workshop updated, but could not post to EventGrid: " + postResult);
      }

      return true;
    }

    bool DeleteWorkshop(AjaxSubmitHelper ajax, out string returnMessage) {

      returnMessage = "";
      bool deleted = false;

      try {
        deleted = DbHelper.WorkshopEvents.DeleteWorkshop(null, WorkshopEventInfo.WorkshopEventId);
      } catch (Exception ex) {

        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("DeleteWorkshop")
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .WithProperty("ProgramJobId", ProgramInfo?.ProgramJobId)
          .WithProperty("WorkshopEventId", WorkshopEventInfo?.WorkshopEventId)
          .WithProperty("WorkshopTitle", WorkshopEventInfo?.WorkshopTitle)
          .Track();
        ajax.AddDialogMessage("Can't delete workshop at this time.", ex);
        return false;
      }

      if (!deleted) {
        ajax.AddDialogMessage("Can't delete workshop, component may be locked.");
        return false;
      }

      string postResult = PostToEventGrid(WorkshopEventInfo.WorkshopEventId, Integrations.EventGrid.WorkshopSession.WorkshopUpdateEventType.deleted);

      if (!postResult.IsNullOrEmpty()) returnMessage = "Workshop deleted, but could not post to EventGrid: " + postResult;

      return true;
    }

    string PostToEventGrid(int workshopEventId, Integrations.EventGrid.WorkshopSession.WorkshopUpdateEventType eventType) {

      // Note that posting to EventGrid sends an email to the Program Contact, so only do it live.
      // Comment out the line below for testing eventgrid on dev.
      if (!ConfigHelper.IsLiveServer) return ""; // dummy ok result if not live.

      var workshopInfo = DbHelper.WorkshopEvents.GetWorkshopInfo(workshopEventId);
      if (workshopInfo == null) return null;
      var programInfo = DbHelper.AblePrograms.GetProgramInfoOrNull((int)workshopInfo.ProgramJobId);
      if (programInfo == null) return null;

      if (!ConfigHelper.IsLiveServer) {
        // For testing, replace real statuses with "test" statuses.
        if (eventType == Integrations.EventGrid.WorkshopSession.WorkshopUpdateEventType.added) eventType = Integrations.EventGrid.WorkshopSession.WorkshopUpdateEventType.added_test;
        else if (eventType == Integrations.EventGrid.WorkshopSession.WorkshopUpdateEventType.updated) eventType = Integrations.EventGrid.WorkshopSession.WorkshopUpdateEventType.updated_test;
        else if (eventType == Integrations.EventGrid.WorkshopSession.WorkshopUpdateEventType.deleted) eventType = Integrations.EventGrid.WorkshopSession.WorkshopUpdateEventType.deleted_test;
      }

      string postResult = null;
      try {
        postResult = Integrations.EventGrid.WorkshopSession.PostWorkshopUpdate(
          eventType,
          workshopInfo.WorkshopEventId,
          workshopInfo.WorkshopTitle, workshopInfo.FriendlyWorkshopId,
          programInfo.CompanyName, programInfo.ProgramJobNumber, programInfo.ProgramJobName,
          workshopInfo.WorkshopStatusName,
          workshopInfo.WhenStartLocal.ToUniversalTimeOrNull(ConfigHelper.DefaultTimeZoneInfo),
          workshopInfo.WhenEndLocal.ToUniversalTimeOrNull(ConfigHelper.DefaultTimeZoneInfo),
          workshopInfo.KeyFacilitatorFirstName, workshopInfo.Location);
      } catch (Exception ex) {

        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("Workshops_PostToEventGrid")
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .WithProperty("WorkshopEventId", workshopEventId)
          .WithProperty("EventType", eventType.ToString())
          .Track();
        // Ignore for now.
      }

      return postResult;
    }

    bool UpdateAttendance(AjaxSubmitHelper ajax) {

      var selectedCoacheeIds = ajax.CheckFieldIntList(FormFields.WorkshopAttendanceIds);

      // Remove any coachee IDs which are not part of this program (i.e. don't trust browsers!)
      var coacheeIdsInProgram = DbHelper.AblePrograms.GetCoacheesInProgram(ProgramInfo.ProgramJobId);
      selectedCoacheeIds.RemoveAll(coacheeId => !coacheeIdsInProgram.Contains(coacheeId));

      DbHelper.WorkshopEvents.UpdateAttendance(WorkshopEventInfo, selectedCoacheeIds, SessionHelper.UserInfo);
      return true;
    }

  }
}
