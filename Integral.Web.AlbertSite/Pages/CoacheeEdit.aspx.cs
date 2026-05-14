using Integral.Web;
using Integral.Web.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Web;
using Integral.Web.PortalSite.AppCode;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class CoacheeEdit : AppCode.PageBaseClasses.CoacheeInfoBase {

    public class SummaryTabModel {

      public DbHelper.AlbertSurveys.SurveyInfo Latest360Survey;
      public bool HasSummaryInfo;

      private Pages_Albert.CoacheeEdit Page;
      private DbHelper.AlbertCoachees.AlbertCoacheeInfo CoacheeInfo;

      public SummaryTabModel(Pages_Albert.CoacheeEdit page) {
        Page = page;
        CoacheeInfo = Page.CoacheeInfo;
        HasSummaryInfo = CoacheeInfo.UserActivity != null && (CoacheeInfo.UserActivity.Latest360IntakeCodeId != null || !CoacheeInfo.UserActivity.AISummaryText.IsNullOrEmptyOrWhitespace());
      }

    }

    public class ProfileTabModel {

      public class FormFields {
        public const string CoacheeId = "CoacheeId";
        public const string CompanyId = "CompanyId";
        public const string ProgramId = "ProgramId";
        public const string ProgramJobNumber = "ProgramJobNumber";
        public const string ProgramName = "ProgramName";
        public const string FirstName = "FirstName";
        public const string LastName = "LastName";
        public const string EmailAddress = "EmailAddress";
        public const string MobilePhone = "MobilePhone";
        public const string Send360ReportDate = "Send360ReportDate";
        public const string PauseProgram = "PauseProgram";
        public const string EndProgram = "EndProgram";
        public const string AddParticipantAfterCurrent = "AddParticipantAfterCurrent";
        public const string DateOfBirth = "DateOfBirth";
        public const string RoleTitle = "RoleTitle";
        public const string City = "City";
        public const string Country = "Country";
        public const string OrgRoleId = "OrgRoleId";
      }

      public class AjaxReturnData {
        public const string CheckEmailChangeMsg = "checkEmailChangeMsg";
      }

      private int formCompanyId = 0, formProgramJobId = 0;

      // Vars for convenience assigned from the page object.
      private readonly Pages_Albert.CoacheeEdit Page;
      private readonly HttpResponse Response;
      private readonly DbHelper.AlbertCoachees.AlbertCoacheeInfo CoacheeInfo;
      private readonly bool ShowCompanySelection, LimitedEdit, CanUpdateProfile,
        CanEditParticipantNotes, CanChangeUserIdIfEmailChanged;
      private readonly List<DbHelper.OrgRoles.OrgRolesInfo> OrgRolesInfo;

      // Constructor - just pass "this" (current page object).
      public ProfileTabModel(Pages_Albert.CoacheeEdit page) {

        Page = page;

        // Convenience vars
        Response = Page.Response;
        CoacheeInfo = Page.CoacheeInfo;
        ShowCompanySelection = Page.ShowCompanySelection;
        LimitedEdit = Page.LimitedEdit;
        CanUpdateProfile = Page.CanUpdateProfile;
        CanEditParticipantNotes = Page.CanEditParticipantNotes;
        CanChangeUserIdIfEmailChanged = Page.CanChangeUserIdIfEmailChanged;
        OrgRolesInfo = Page.OrgRolesInfo;
      }

      public string GetOrgRolesOptions() {

        string html = "<option>[Select Org Role]</option>";
        if (OrgRolesInfo == null) return html;
        foreach (var orgRole in OrgRolesInfo) {
          html += "<option";
          if (CoacheeInfo.UserActivity?.OrgRoleId != null && orgRole.OrgRoleId == CoacheeInfo.UserActivity?.OrgRoleId) html += " selected";
          html += " value=\"" + orgRole.OrgRoleId + "\">" + orgRole.RoleTitle.HTMLEncode() + "</option>";
        }
        return html;
      }

      public void CheckEmailChange(AjaxSubmitHelper ajax) {

        if (!CanUpdateProfile) return;

        // Get Coachee's user info by email
        string emailInForm = WebHelper.GetFormValue(FormFields.EmailAddress);

        if (emailInForm == CoacheeInfo.EmailAddress || emailInForm.IsNullOrEmptyOrWhitespace()) return;

        var userEmailInfo = DbHelper.AbleUser.GetUserByEmailOrNull(emailInForm, DbHelper.AbleUser.RegisteredFilter.Any);
        if (userEmailInfo == null || userEmailInfo.UserId == CoacheeInfo.UserId) return;

        if (!CanChangeUserIdIfEmailChanged) {

          ajax.AddBadField(FormFields.EmailAddress, "This email address is already in use by another user.");
          return;

        } else {

          // return ajax message
          string msg = $@"The email {emailInForm} belongs to user {userEmailInfo.GetFullName()}, are you sure you want to change it?<br/><br/>
                          Current user: {CoacheeInfo.GetFullName()} ({CoacheeInfo.EmailAddress})";
          ajax.AddReturnValue(AjaxReturnData.CheckEmailChangeMsg, msg);
          return;
        }
      }

      public string GetCompanyOptions() {

        string html = "";
        var companies = DbHelper.ClientCompanies.GetCompanyList(SessionHelper.GetUserInfoOrNull());
        foreach (var cmp in companies) {
          html += "<option";
          int companyId = cmp.CompanyId;
          if (companyId == CoacheeInfo.CompanyId) html += " selected";
          html += " value=\"" + companyId + "\">" + cmp.CompanyName.HTMLEncode() + "</option>";
        }
        return html;
      }

      public void UpdateProfile(AjaxSubmitHelper ajax) {

        // Remember coachee's current email address in case it was changed on the form.
        string originalEmailAddress = CoacheeInfo.EmailAddress;

        if (!LimitedEdit) {
          // Fields for new or existing coachees.
          CoacheeInfo.FirstName = ajax.CheckFieldRegex(FormFields.FirstName, "First Name", AppHelper.Regex.GeneralText, true, "Please enter a First Name.");
          CoacheeInfo.LastName = ajax.CheckFieldRegex(FormFields.LastName, "Last Name", AppHelper.Regex.GeneralText, true, "Please enter a Last Name.");
          CoacheeInfo.EmailAddress = ajax.CheckFieldRegex(FormFields.EmailAddress, "Email Address", AppHelper.Regex.Email, true, "Please enter a valid Email Address.");
          CoacheeInfo.MobilePhone = ajax.CheckFieldRegex(FormFields.MobilePhone, "Mobile Phone", AppHelper.Regex.Mobile, false, "Please enter a valid mobile number.");
          CoacheeInfo.UserActivity.DateOfBirth = ajax.GetDatePickerDateUnspecified(FormFields.DateOfBirth, "Date of Birth", false, "Please provide a date.");
          CoacheeInfo.UserActivity.City = ajax.CheckFieldRegex(FormFields.City, "City", AppHelper.Regex.GeneralText, false, "Please enter a City.");
          CoacheeInfo.UserActivity.Country = ajax.CheckFieldRegex(FormFields.Country, "Country", AppHelper.Regex.GeneralText, false, "Please enter a Country.");
          CoacheeInfo.UserActivity.RoleTitle = ajax.CheckFieldRegex(FormFields.RoleTitle, "Role Title", AppHelper.Regex.GeneralText, false, "Please enter a Role Title.");

          var orgRoleId = WebHelper.GetFormValue(FormFields.OrgRoleId, "0").ToIntOrDefault(0);
          if (orgRoleId != 0 && OrgRolesInfo.Exists(x => x.OrgRoleId == orgRoleId)) {
            CoacheeInfo.UserActivity.OrgRoleId = orgRoleId;
          } else if (orgRoleId == 0) {
            CoacheeInfo.UserActivity.OrgRoleId = null;
          } else {
            ajax.AddBadField(FormFields.OrgRoleId, "Select a valid Org Role.");
          }
        }

        var formValCompanyId = ajax.CheckFieldID(FormFields.CompanyId, "Company", true, "Please select a Company.");
        var formValProgramJobId = ajax.CheckFieldID(FormFields.ProgramId, "Program", true, "Please select a Program.");

        // Check and assign company & org.
        var linkedComponents = DbHelper.ProgramComponents.GetForCoachee(CoacheeInfo.CoacheeId);
        bool PaxCanBeRelocated = linkedComponents.IsNullOrEmpty() || linkedComponents.Find(c => c.LockedDateUtc != null || c.HasPayrunItems) == null;

        if (ShowCompanySelection && PaxCanBeRelocated) {
          formCompanyId = formValCompanyId;
          formProgramJobId = formValProgramJobId;

        } else {
          if (!PaxCanBeRelocated && (formValCompanyId != CoacheeInfo.CompanyId || formValProgramJobId != CoacheeInfo.ProgramJobId)) {
            ajax.AddDialogMessage($"The participant cannot be moved to another {(formValCompanyId != CoacheeInfo.CompanyId ? "Company" : "Program")} because it has components linked to it");
            return;
          }
          formCompanyId = CoacheeInfo.CompanyId.Value;
          formProgramJobId = CoacheeInfo.ProgramJobId.Value;
        }
        if (!CheckCompanySelection(ajax, formCompanyId, formProgramJobId, CoacheeInfo)) return;

        if (ajax.BadFieldCount > 0) return;

        var existingCoachee = DbHelper.AlbertCoachees.GetCoacheeInfo(CoacheeInfo.EmailAddress, formProgramJobId);
        if (existingCoachee != null) {
          ajax.AddDialogMessage("A participant with this email address already exists in this Program. Please add this participant to a different Program.");
          return;
        }

        DbHelper.AlbertCoachees.UpdateCoachee(CoacheeInfo, CanChangeUserIdIfEmailChanged);

        // Run update linked user either at creating or updating Coachee
        Page.UpdateUserLinked(CoacheeInfo);

        ajax.SetReloadPage("Profile updated.", AjaxSubmitHelper.PageMessageType.SuccessToast);
      }

      bool CheckCompanySelection(AjaxSubmitHelper ajax, int formCompanyId, int formProgramJobId, DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo) {

        DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo = null;
        DbHelper.AblePrograms.AbleProgramInfo programInfo = null;

        companyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(formCompanyId, SessionHelper.GetUserInfoOrNull());
        if (companyInfo == null) {
          ajax.AddDialogMessage("Error: CompanyId not found.");
          return false;
        }
        coacheeInfo.CompanyId = formCompanyId;
        coacheeInfo.TenantOrgId = companyInfo.OrgId;

        programInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(formProgramJobId);
        if (programInfo == null) {
          ajax.AddDialogMessage("Error: ProgramId not found.");
          return false;
        }

        // Ensure Coachee is linked to program's Job Id.
        coacheeInfo.ProgramJobId = programInfo.ProgramJobId;

        return true;
      }

    }

    public class CoachingTabModel {

      public const string SessionKeyDelimiter = "_";

      public class DataNames {
        public const string Durations = "durations";
      }

      public class FormFields {
        public const string CoachingType = "CoachingType";
        public const string MeetCoachEmailDate = "MeetCoachEmailDate";
        public const string SessionsAllocated = "SessionsAllocated";
        public const string ApplyCoachingToProgram = "ApplyCoachingToProgram";
        public const string SendMeetCoachNow = "SendMeetCoachNow";
        public const string CoachUserId = "CoachUserId";
        public const string SessList_Price = "SessListPrice";
        public const string SessList_QuoteItemId = "SessListQuoteItemId";
      }

      private class FormValues {

        public int? CoachingTypeId;
        public int CoachUserId;
        public DateTime? MeetCoachEmailUtc;
        public bool SendMeetCoachNow;
        public int SessionsAllocated;
        public bool ApplyCoachingToProgram;
        public List<SessionComponent> SessionComponents = new List<SessionComponent>();

        public class SessionComponent {

          public int SessionNumber;
          public int? SessionId;
          public decimal? Revenue;
          public int? QuoteItemId;
          public bool IsLocked;

          public SessionComponent(int sessionNumber, int? sessionId, decimal? revenue, int? quoteItemId, bool isLocked) {
            SessionNumber = sessionNumber;
            SessionId = sessionId;
            Revenue = revenue;
            QuoteItemId = quoteItemId;
            IsLocked = isLocked;
          }
        }
      }

      class SessionListItemInfo : DbHelper.ProgramComponents.SessionComponentInfo {

        public DateTime? CompletedLocal { get; set; }

        public SessionListItemInfo(
          DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
          int sessionNumber, int? sessionId, DateTime? completedUtc, int? durationMins, decimal? componentPrice, int? quoteItemId, bool isLocked) {

          SessionNumber = sessionNumber;
          SessionId = sessionId;
          CompletedUtc = completedUtc;
          CompletedLocal = TimeHelper.UtcToTimeZoneId(completedUtc, coacheeInfo.UserActivity.CoachTimeZoneIdIANA).ToDateTimeOrNull();
          DurationMins = durationMins;
          ComponentPrice = componentPrice;
          QuoteItemId = quoteItemId;
          IsLocked = isLocked;
        }
      }

      // Vars for convenience assigned from the page object.
      private readonly Pages_Albert.CoacheeEdit Page;
      private readonly DbHelper.AbleUser.AbleUserInfo UserInfo;
      private readonly DbHelper.Projects.ProjectInfo ProjectInfo;
      private readonly DbHelper.AblePrograms.AbleProgramInfo ProgramInfo;
      private readonly DbHelper.AlbertCoachees.AlbertCoacheeInfo CoacheeInfo;
      private readonly bool CanChangeCoach, CanApplyCoachingToProgram, LimitedEdit;

      // Data needed for this tab.
      public DbHelper.AlbertCoaches.AlbertCoachInfo CoachInfo;
      public List<DbHelper.CoachingSessions.AbleSessionInfo> CoachingSessionList;
      public List<DbHelper.AlbertCoaches.AlbertCoachInfo> PartnerList;
      public List<DbHelper.AbleQuotes.QuoteItemForList> QuoteItemsForList;

      public bool CanChangeLockedSessionQuoteItem, CanViewSessionOverallRevenue, CanViewSessionPartnerRevenue, CanEditSessionQuoteItem, CanAddSession, Has360WithRatersForCoachAllocation;

      public readonly int SessionFooterColSpan = 1;

      // Constructor - just pass "this" (current page object).
      public CoachingTabModel(Pages_Albert.CoacheeEdit page) {

        Page = page;

        // Convenience vars.
        UserInfo = Page.userInfo;
        ProjectInfo = Page.ProjectInfo;
        ProgramInfo = Page.ProgramInfo;
        CoacheeInfo = Page.CoacheeInfo;
        CanChangeCoach = Page.CanChangeCoach;
        CanApplyCoachingToProgram = Page.CanApplyCoachingToProgram;
        LimitedEdit = Page.LimitedEdit;

        // Data needed for tab.
        CoachInfo = DbHelper.AlbertCoaches.GetCoachInfo(CoacheeInfo.CoachUserId);
        Has360WithRatersForCoachAllocation = DbHelper.AlbertCoachees.Has360WithRatersForCoachAllocation(CoacheeInfo);
        PartnerList = DbHelper.AlbertCoaches.GetCoachInfoList(true, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);

        CoachingSessionList = Page.CoachingSessionList;
        QuoteItemsForList = DbHelper.AbleQuotes.GetQuoteItemsForList(ProgramInfo.ProgramJobNumber, false,
          DbHelper.ProgramComponents.KeyColumnEnum.CoacheeId, CoacheeInfo.CoacheeId, ProgramInfo.ProgramJobId);

        CanChangeLockedSessionQuoteItem = SessionHelper.AppAccess.Participants.CanChangeLockedSessionQuoteItem(CoacheeInfo);
        CanViewSessionOverallRevenue = SessionHelper.AppAccess.Participants.CanViewSessionOverallRevenue(ProgramInfo);
        CanViewSessionPartnerRevenue = SessionHelper.AppAccess.Participants.CanViewSessionPartnerRevenue(ProgramInfo);
        CanEditSessionQuoteItem = SessionHelper.AppAccess.Participants.CanEditSessionQuoteItem(ProgramInfo);
        CanAddSession = SessionHelper.AppAccess.Sessions.CanAdd(CoacheeInfo);
      }

      public string GetCoachingTypeOptions(string labelText) {
        var ctChosen = DbHelper.AlbertCoachingTypes.GetCoachingTypeByIdOrNull(CoacheeInfo.CoachingTypeId);
        string selectedValue = ctChosen == null ? "" : ctChosen.IntercomFieldValue;
        var buttonList = new List<WebHelper.ButtonGroupButton>();
        foreach (var ct in DbHelper.AlbertCoachingTypes.GetCoachingTypeList()) {
          if (!ct.UIHidden) {
            buttonList.Add(new WebHelper.ButtonGroupButton(ct.IntercomFieldValue, ct.IntercomFieldValue,
              new WebHelper.DataAttributes((DataNames.Durations, ct.GetDurations().ToStringList().EmptyIfNull()))
            ));
          }
        }
        return WebHelper.GetButtonGroup(labelText, FormFields.CoachingType, buttonList, selectedValue, LimitedEdit);
      }

      public string GetCoachDropdown() {

        var partnerDropdownInfo = new WebHelper.PartnerDropdownInfo() {
          PartnerInfoList = PartnerList,
          FormName = FormFields.CoachUserId,
          IsReadOnly = !CanChangeCoach,
          LabelText = "Coach:",
          InputCols = 3,
          SelectedPartnerUserId = CoacheeInfo.CoachUserId,
          CanViewHiddenPartners = SessionHelper.AppAccess.Coaches.CanViewHiddenPartners(),
          CanViewInactivePartners = SessionHelper.AppAccess.Coaches.CanViewInactivePartners(),
          IncludeUnassignedUser = true,
          CoacheeHas360WithRatersForCoachAllocation = Has360WithRatersForCoachAllocation,
          DropdownPurpose = WebHelper.PartnerDropdownPurpose.AssignCoachForParticipant
        };

        return WebHelper.GetPartnerDropdown(partnerDropdownInfo);
      }

      public string GetQuoteItemOptions() {
        string html = "<option value=\"\">[select quote item]</option>";
        foreach (var item in QuoteItemsForList) {
          html += $@"<option value=""{item.QuoteItemId}"">{item.DisplayOrder}: {item.Description.NoHTML().Left(140)} ({(item.TotalFunds - item.TotalRevenue):C})</option>";
        }
        return html;
      }

      public string GetSessionListJson() {

        var sessionComponents = DbHelper.ProgramComponents.GetSessionComponentsForCoachee(CoacheeInfo.CoacheeId);
        var sessionLItemist = new List<SessionListItemInfo>();

        foreach (var c in sessionComponents) {
          sessionLItemist.Add(new SessionListItemInfo(CoacheeInfo, c.SessionNumber, c.SessionId, c.CompletedUtc, c.DurationMins, c.ComponentPrice, c.QuoteItemId, c.IsLocked));
        }

        return JsonConvert.SerializeObject(sessionLItemist, Formatting.None);
      }

      public void UpdateCoaching(AjaxSubmitHelper ajax) {

        if (!GetFormValues(ajax, out var formValues) || ajax.HasErrors) return;

        if (!GetSessionComponents(ajax, formValues) || ajax.HasErrors) return;

        int coacheeUpdateCount = 0;
        int meetCoachEmailCount = 0;
        string responseMessage = "";
        string errorMessages = "";
        Exception updateException = null;

        if (!CheckAllocatedFunds(ajax, formValues)) return;

        DbHelper.Common.UsingTransaction(trans => {

          if (formValues.ApplyCoachingToProgram) {

            // Set multiple coachees.
            foreach (int coacheeId in Page.CoacheesInProgram) {
              var thisCoacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(coacheeId);
              if (thisCoacheeInfo != null) {
                string errorMessage = null;
                bool coacheeUpdated = false;
                bool meetCoachEmailSent = false;
                try {
                  UpdateCoachingForCoachee(trans, formValues, thisCoacheeInfo, out coacheeUpdated, out meetCoachEmailSent, out errorMessage);
                } catch (Exception ex) {
                  updateException = ex;
                  errorMessage = "Error updating coaching.";
                }
                if (!errorMessage.IsNullOrEmpty()) {
                  errorMessages += $"<br/><b>{thisCoacheeInfo.GetFullName().HTMLEncode()}</b>: {errorMessage.ValueIfNullOrEmpty("Unknown Error.")}";
                }
                if (coacheeUpdated) coacheeUpdateCount++;
                if (meetCoachEmailSent) meetCoachEmailCount++;
              }
            }

          } else {

            // Set for this coachee.
            string errorMessage = null;
            bool coacheeUpdated = false;
            bool meetCoachEmailSent = false;
            try {
              UpdateCoachingForCoachee(trans, formValues, CoacheeInfo, out coacheeUpdated, out meetCoachEmailSent, out errorMessage);
            } catch (Exception ex) {
              updateException = ex;
              errorMessage = "Error updating coaching.";
            }
            if (!errorMessage.IsNullOrEmpty()) {
              errorMessages += $"<br/><b>{CoacheeInfo.GetFullName().HTMLEncode()}</b>: {errorMessage.ValueIfNullOrEmpty("Unknown Error.")}";
            }
            if (coacheeUpdated) coacheeUpdateCount++;
            if (meetCoachEmailSent) meetCoachEmailCount++;
          }

          responseMessage += $"Coaching Updated{(coacheeUpdateCount > 1 ? $" for {coacheeUpdateCount} Participants." : "")}.";
          if (formValues.SendMeetCoachNow) responseMessage += $"<br/>{(meetCoachEmailCount == 1 ? "" : meetCoachEmailCount.ToString())} Meet Coach email".ToPlural(meetCoachEmailCount) + " sent.";
          responseMessage += errorMessages.EnsureEndsWith("<br/>", StringExt.Ensure.IfNotBlank);

          if (coacheeUpdateCount == 0 || !errorMessages.IsNullOrEmpty() || ajax.HasErrors) {
            ajax.AddDialogMessage(responseMessage, updateException); // Show exception to dev if present.
            return false; // Rollback trans.
          } else {
            ajax.AddSuccessToast(responseMessage);
            return true; // Commit trans.
          }
        });
      }

      // For every linked QuoteItem, check that allocated component prices don't exceed quote item funds.
      bool CheckAllocatedFunds(AjaxSubmitHelper ajax, FormValues formValues) {

        // Create list of *unique* quote item IDs and the revenue amount assigned to each.
        // i.e. where quote item IDs are the same, the revenue amounts are added together.
        var allocatedRevenueList = new Dictionary<int, decimal>(); // QuoteItemId(int), TotalRevenue(decimal)
        foreach (var session in formValues.SessionComponents) {
          if (session.QuoteItemId != null && session.Revenue != null) { // Only valid if both quote item AND amount are set.
            if (allocatedRevenueList.ContainsKey(session.QuoteItemId.Value)) {
              allocatedRevenueList[session.QuoteItemId.Value] += session.Revenue.Value; // Quote item in list so add to the existing revenue value.
            } else {
              allocatedRevenueList.Add(session.QuoteItemId.Value, session.Revenue.Value);
            }
          }
        }

        if (allocatedRevenueList.Count == 0) return true; // No revenue amounts submitted, so nothing to check.

        // For each quote item with revenue, check total revenue allocated against funds available for the quote item.

        foreach (var allocatedRevenueItem in allocatedRevenueList) {

          int allocatedQuoteItemId = allocatedRevenueItem.Key;
          decimal allocatedRevenue = allocatedRevenueItem.Value * (formValues.ApplyCoachingToProgram ? Page.CoacheesInProgram.Count : 1); // Multiply by number of coachees to update.

          // Get quote item info from the master list, make sure the id exists.
          var listQuoteItem = QuoteItemsForList.Find(qi => qi.QuoteItemId == allocatedQuoteItemId);
          if (listQuoteItem == null) { // Make sure QuoteItemId submitted from form is valid.
            ajax.AddDialogMessage("Invalid Quote Item. Please refresh page and try again.");
            return false;
          }

          // Get the correct existing revenue amount - either minus the current coachee, or (if setting for program) minus all coachees in the program.
          decimal existingRevenue = formValues.ApplyCoachingToProgram ? listQuoteItem.TotalRevenueIgnoredProgram : listQuoteItem.TotalRevenueIgnoredComponent;

          // Ensure allocated revenue doesn't exceed available funds for this quote item.
          if (existingRevenue + allocatedRevenue > listQuoteItem.TotalFunds) {
            ajax.AddDialogMessage("Allocation exceeds quote item amount for"
              + "<br/>" + listQuoteItem.ProductTitle.HTMLEncode()
              + "<br/>Available Funds: <b>" + (listQuoteItem.TotalFunds - existingRevenue).ToString("C") + "</b>"
              + "<br/>Tried to assign: <b>" + allocatedRevenue.ToString("C") + "</b>");
            return false;
          }
        }

        return true;
      }

      public void DeleteParticipant(AjaxSubmitHelper ajax) {

        if (!CoacheeInfo.CanDelete) {
          ajax.AddDialogMessage("This participant cannot be deleted, important related data exists.");
          return;
        }

        try {
          DbHelper.AlbertCoachees.SoftDeleteCoachee(null, CoacheeInfo.CoacheeId);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(CoacheeInfo?.UserGuid))
            .WithOperation(nameof(DeleteParticipant))
            .FromSession()
            .Track();

          ajax.AddDialogMessage("Error deleting participant, please try again later.<br/>" + (ConfigHelper.IsDevServer ? ex.Message : ""));
          return;
        }

        ajax.SetRedirectUrl(PathHelper.Pages.Coachees(), "Participant Deleted.", true);
      }

      bool GetFormValues(AjaxSubmitHelper ajax, out FormValues formValues) {

        formValues = new FormValues();

        if (!LimitedEdit) {

          // Get coaching type, ensure selection is correct and compatible with other settings.
          string coachingTypeValue = WebHelper.GetFormValue(FormFields.CoachingType);
          var dbCoachingType = DbHelper.AlbertCoachingTypes.GetCoachingTypeByIntercomValueOrNull(coachingTypeValue);
          if (dbCoachingType == null) {
            ajax.AddBadField(FormFields.CoachingType, "Incorrect Coaching Type selected." + (ConfigHelper.IsLiveServer ? "" : "coachingTypeName = " + coachingTypeValue));
            return false;
          }

          formValues.CoachingTypeId = dbCoachingType.CoachingTypeId;
          formValues.SessionsAllocated = ajax.CheckFieldInt(FormFields.SessionsAllocated, "Sessions Allocated", 0, 99, false, "Please provide the number of allocated sessions.");
          formValues.MeetCoachEmailUtc = ajax.GetDatePickerToUtc(FormFields.MeetCoachEmailDate, SessionHelper.GetSessionTimeZone(), "Meet-Coach Email Date", false, "Please provide a date.");
          formValues.SendMeetCoachNow = WebHelper.GetFormValue(FormFields.SendMeetCoachNow) == "1";

          if (CanChangeCoach) {
            int selectedCoachId = ajax.CheckFieldID(FormFields.CoachUserId, "Coach", true, "Please choose a coach.");

            if (selectedCoachId != ConfigHelper.UserId.Unassigned) {
              var selectedCoachInfo = PartnerList.Find(x => x.UserId == selectedCoachId);
              bool canBeAssignedAsParticipantCoach = SessionHelper.AppAccess.Coaches.CanBeAssignedAsCoachForParticipant(selectedCoachInfo, Has360WithRatersForCoachAllocation);
              if ((selectedCoachInfo == null || !canBeAssignedAsParticipantCoach) && selectedCoachId != CoacheeInfo?.CoachUserId) {
                ajax.AddBadField(FormFields.CoachUserId, "Please select a valid Coach");
                return false;
              }
            }
            formValues.CoachUserId = selectedCoachId;

          } else {
            formValues.CoachUserId = CoachInfo.UserId;
          }
        }

        formValues.ApplyCoachingToProgram = !WebHelper.GetFormValue(FormFields.ApplyCoachingToProgram).IsNullOrEmpty();

        if (formValues.ApplyCoachingToProgram && !CanApplyCoachingToProgram) {
          ajax.AddBadField(FormFields.ApplyCoachingToProgram, "Set for Program is not permitted.");
          return false;
        }

        // Check for compatible settings.

        if (formValues.CoachingTypeId == DbHelper.AlbertCoachingTypes.ReservedType_NoCoaching.CoachingTypeId) { // No coaching.

          if (CoacheeInfo.UserActivity?.SessionsBooked > 0) {
            ajax.AddBadField(FormFields.CoachingType, "Can't set 'No Coaching' - first delete all sessions and set Sessions Allocated to zero.");
            return false;
          }
          if (formValues.SessionsAllocated > 0) {
            ajax.AddBadField(FormFields.CoachingType, "Can't set 'No Coaching' - first set Sessions Allocated to zero.");
            return false;
          }

        } else { // Coaching

          if (formValues.SessionsAllocated < 1) ajax.AddBadField(FormFields.SessionsAllocated, "Sessions Allocated is required.");
          else if (formValues.SessionsAllocated > ConfigHelper.Max_Coaching_Sessions) ajax.AddBadField(FormFields.SessionsAllocated, $"Can't allocate more than {ConfigHelper.Max_Coaching_Sessions} sessions.");
          else if (CoacheeInfo.UserActivity?.SessionsBooked > formValues.SessionsAllocated) ajax.AddBadField(FormFields.SessionsAllocated, "Sessions Allocated cannot be less than Sesions Booked.");
        }

        return !ajax.HasErrors;
      }

      bool GetSessionComponents(AjaxSubmitHelper ajax, FormValues formValues) {

        int lastSessionNumber = 0; // Highest component SessionNumber.
        int lastAssignedSessionNumber = 0; // Highest component SessionNumber assigned to existing sessions.
        formValues.SessionComponents.Clear();

        // Copy existing component values to formValues.Sessions list.
        // Note it's important that SessionNumbers are *consecutive* from 1 to AllocatedSession. Should be a db rule.
        foreach (var component in DbHelper.ProgramComponents.GetSessionComponentsForCoachee(CoacheeInfo.CoacheeId)) { // Ordered by SessionNumber
          lastSessionNumber = component.SessionNumber; // Starts at 1.
          if (component.SessionId != null) lastAssignedSessionNumber = component.SessionNumber;
          formValues.SessionComponents.Add(new FormValues.SessionComponent(component.SessionNumber, component.SessionId, component.ComponentPrice, component.QuoteItemId, component.IsLocked));
        }

        // Ensure formValues.Sessions list is same size as submitted SessionsAllocated.

        if (formValues.SessionsAllocated > formValues.SessionComponents.Count) {

          // Add session components.
          while (formValues.SessionComponents.Count < formValues.SessionsAllocated) {
            lastSessionNumber++;
            formValues.SessionComponents.Add(new FormValues.SessionComponent(lastSessionNumber, null, null, null, false));
          }

        } else if (formValues.SessionsAllocated < formValues.SessionComponents.Count) {

          // Remove session components.
          // Don't allow removing components that are assigned to existing sessions.
          if (formValues.SessionsAllocated < lastAssignedSessionNumber) {
            ajax.AddBadField(FormFields.SessionsAllocated, $"Can't currently allocate less than {lastAssignedSessionNumber} sessions.");
            return false;
          } else {
            formValues.SessionComponents.RemoveRange(formValues.SessionsAllocated, formValues.SessionComponents.Count - formValues.SessionsAllocated);
          }
        }

        // Get the submitted component values, in order of the component Session Number.
        for (int sessionNumber = 1; sessionNumber <= formValues.SessionsAllocated; sessionNumber++) {

          var component = formValues.SessionComponents[sessionNumber - 1];

          if (component.SessionNumber != sessionNumber) { // Sanity check: List should be in order of session number.
            ajax.AddDialogMessage("Problem assigning sessions, please refresh page and try again.");
            return false;
          }

          if (!GetComponentFormValues(ajax, component)) return false;
        }

        return !ajax.HasErrors;
      }

      bool GetComponentFormValues(AjaxSubmitHelper ajax, FormValues.SessionComponent component) {

        // Get price and quoteitemid values.
        if (!component.IsLocked) {
          component.Revenue = ajax.CheckFieldDecimal(FormFields.SessList_Price + SessionKeyDelimiter + component.SessionNumber, "", false, 0, null, false, "");
        }
        if (!component.IsLocked || CanChangeLockedSessionQuoteItem) {
          component.QuoteItemId = ajax.CheckFieldIntOrNull(FormFields.SessList_QuoteItemId + SessionKeyDelimiter + component.SessionNumber);
        }

        if (component.QuoteItemId != null) { // Quote item selected.

          if (QuoteItemsForList.Find(qi => qi.QuoteItemId == component.QuoteItemId) == null) { // Invalid quote item id in form.
            ajax.AddDialogMessage("Problem assigning Quote Item, please refresh page and try again.");
            return false;
          }
          if (component.Revenue == null) { // Price must also be supplied.
            ajax.AddDialogMessage("A price must be assigned for the selected Quote Item.");
            return false;
          }

        } else { // No quote item selected.

          if (component.Revenue != null) { // Price must also be blank.
            ajax.AddDialogMessage("A price cannot be set if no Quote Item selected.");
            return false;
          }
        }

        return true;
      }

      void UpdateCoachingForCoachee(
        SqlTransaction trans,
        FormValues formValues,
        DbHelper.AlbertCoachees.AlbertCoacheeInfo updateCoacheeInfo,
        out bool coacheeUpdated,
        out bool meetCoachEmailSent,
        out string errorMessage) {

        DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo = null;
        DbHelper.CoachingSessions.SessionStats sessionStats = null;
        coacheeUpdated = false;
        meetCoachEmailSent = false;
        errorMessage = "";

        bool coachHasChanged = false;

        if (!LimitedEdit) {

          updateCoacheeInfo.MeetCoachEmailUtc = formValues.MeetCoachEmailUtc;
          updateCoacheeInfo.CoachingTypeId = formValues.CoachingTypeId;
          updateCoacheeInfo.UserActivity.SessionsAllocated = formValues.SessionsAllocated;

          if (CanChangeCoach) {
            // If coach changed, email new coach about assignment.
            // Note that coach is only changed for CURRENT coachee, not all of them (this function is called multiple times if updating whole program).
            if (updateCoacheeInfo.CoacheeId == CoacheeInfo.CoacheeId && updateCoacheeInfo?.CoachUserId != formValues.CoachUserId) {
              updateCoacheeInfo.CoachUserId = formValues.CoachUserId;
              coachHasChanged = true;
            }
          }
        }

        try {
          coacheeUpdated = DbHelper.AlbertCoachees.UpdateCoachee(trans, updateCoacheeInfo);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .FromSession()
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(updateCoacheeInfo.UserGuid))
            .WithOperation(nameof(UpdateCoachingForCoachee))
            .WithOperationContext("UpdateCoacheeCoaching")
            .Track();

          throw new ApplicationException("Error updating participant. Please try again later.", ex);
        }
        if (!coacheeUpdated) {
          errorMessage = "Problem updating participant. Please try again later.";
          return;
        }

        // Update coaching sessions.
        try {
          var updateComponentsInfo = new DbHelper.ProgramComponents.UpdateSessionComponentsInfo(updateCoacheeInfo);
          foreach (var session in formValues.SessionComponents) updateComponentsInfo.AddSessionToUpdate(session.SessionNumber, session.Revenue, session.QuoteItemId);
          DbHelper.ProgramComponents.UpdateSessionComponents(trans, updateComponentsInfo);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .FromSession()
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(CoacheeInfo?.UserGuid))
            .WithOperation(nameof(UpdateCoachingForCoachee))
            .WithOperationContext("UpdateSessionComponents")
            .FromSession()
            .Track();

          coacheeUpdated = false;
          throw new ApplicationException("Error updating session components. Please try again later.", ex);
        }

        // Update session stats for this coachee.
        try {
          coachInfo = DbHelper.AlbertCoaches.GetCoachInfo(trans, updateCoacheeInfo.CoachUserId); // Note that any coach from any Org can be selected.
          sessionStats = DbHelper.CoachingSessions.GetSessionStats(trans, updateCoacheeInfo.CoacheeId);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .FromSession()
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(CoacheeInfo?.UserGuid))
            .WithOperation(nameof(UpdateCoachingForCoachee))
            .WithOperationContext("GetSessionStats")
            .FromSession()
            .Track();

          throw new ApplicationException("Error retrieving session stats. Please try again later.", ex);
        }
        if (sessionStats == null) {
          errorMessage = "Failed to retrieve session stats. Please try again later.";
          return;
        }

        try {
          DbHelper.AlbertCoachees.UpdateSessionStatsAndTargetDates(trans, updateCoacheeInfo, sessionStats);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .FromSession()
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(updateCoacheeInfo.UserGuid))
            .WithOperation(nameof(UpdateCoachingForCoachee))
            .WithOperationContext("UpdateSessionStats")
            .FromSession()
            .Track();

          throw new ApplicationException("Error updating session stats. Please try again later.", ex);
        }

        if (coachHasChanged && updateCoacheeInfo.CoachUserId != ConfigHelper.UserId.Unassigned) {
          try {
            EmailCoachAssignment(updateCoacheeInfo, coachInfo);
          } catch (Exception ex) {
            var user = Page.userInfo;
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(updateCoacheeInfo?.UserGuid))
              .WithOperation(nameof(UpdateCoachingForCoachee))
              .WithOperationContext("SendCoachAssignmentEmail")
              .FromSession()
              .Track();

            errorMessage += " Error sending assignment email to Coach.";
            // Not fatal, continue.
          }
        }

        if (formValues.SendMeetCoachNow) {
          if (!updateCoacheeInfo.CanReceiveMeetCoachEmail) {
            errorMessage +=
              " Meet Coach email not sent "
              + $"({(updateCoacheeInfo.CoachUserId == ConfigHelper.UserId.Unassigned ? "Coach unassigned" : "invalid pax status")}).";
          } else {
            try {
              meetCoachEmailSent = AlbertEmails.SendMeetCoachEmail(trans, updateCoacheeInfo, ProjectInfo, coachInfo);
            } catch (Exception ex) {
              var user = Page.userInfo;
              var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
              telemetry?.Exception(ex)
                .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(updateCoacheeInfo?.UserGuid))
                .WithOperation(nameof(UpdateCoachingForCoachee))
                .WithOperationContext("SendMeetCoachEmail")
                .FromSession()
                .Track();
            } // not fatal, ignore.
          }
        }
      }

      void EmailCoachAssignment(DbHelper.AlbertCoachees.AlbertCoacheeInfo CoacheeInfo, DbHelper.AlbertCoaches.AlbertCoachInfo coachInfo) {

        string message =
          "Hi " + coachInfo.FirstName + ",<br/>"
          + "<br/>"
          + "You have been assigned a Coachee:<br/>"
          + "<br/>"
          + "<b>" + CoacheeInfo.GetFullName() + "</b> (" + CoacheeInfo.EmailAddress + ")<br/>"
          + CoacheeInfo.CompanyName + "<br/>"
          + "<br/>"
          + "Program Notes and Coachee Notes can be found in Able.<br/>"
          + "<br/>"
          + "<a href=\"" + PathHelper.Pages.CoacheeEdit(CoacheeInfo.CoacheeId, PathHelper.CoacheeTabEnum.notes, true) + "\">Click here to go to the Participant page.</a><br/>"
          + "<br/>"
          + "Kind Regards,<br/>"
          + "<br/>"
          + "Coordination Team<br/>";

        AlbertEmails.SendGenericEmail(
          projectInfoBrief: ProjectInfo,
          subject: "You've been assigned a Coachee!",
          titleText: "Coachee Assignment",
          bodyHtml: message,
          allowProjectOverrideSender: false,
          preferredSenderEmailName: ProjectInfo.ComputedSenderEmailName.ValueIfNullOrEmpty(UserInfo.ComputedSenderEmailName),
          preferredSenderEmailAddress: ProjectInfo.ComputedSenderEmailAddress.ValueIfNullOrEmpty(UserInfo.ComputedSenderEmailAddress),
          filesToAttach: null,
          sendResult: out _,
          tos: new MailAddress(coachInfo.EmailAddress, coachInfo.GetFullName()));
      }
    }

    public class SurveyTabModel { // Vars and functions for Survey tab in here.

      public List<DbHelper.AlbertSurveys.SurveyInfo> SurveyList = null;
      public List<DbHelper.AlbertSurveys.SurveyInfo> FocusSurveyList = null;
      public List<DbHelper.AlbertSurveys.SurveyInfo> EvalsPulseSurveyList = null;

      private readonly Pages_Albert.CoacheeEdit Page;
      public string ActionButtonColumnClass = "w150";
      public enum SurveyFilterEnum { Focus, EvalsPulse };

      public class FormAttr {
        public const string SurveyUID = "svuid";
        public const string ParticipantUID = "partuid";
        public const string SurveyPath = "spath";
      }

      public SurveyTabModel(Pages_Albert.CoacheeEdit page) {

        Page = page;

        // Get coachee's linked user surveys from all programs including Jarvis'
        SurveyList = DbHelper.AlbertSurveys.GetSurveyInfoListForUser(Page.CoacheeInfo.UserId.Value, DbHelper.AlbertSurveys.OnlyViewerCompatible.No);
        GetSurveyListFiltered();

      }

      public void GetSurveyListFiltered() {

        if (SurveyList.IsNullOrEmpty()) return;

        EvalsPulseSurveyList = new List<DbHelper.AlbertSurveys.SurveyInfo>();
        FocusSurveyList = new List<DbHelper.AlbertSurveys.SurveyInfo>();

        foreach (var surveyInfo in SurveyList) {
          if (!SessionHelper.AppAccess.Surveys.CanListParticipantSurvey(surveyInfo)) continue;
          if (surveyInfo.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Eval || surveyInfo.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Pulse360) {
            EvalsPulseSurveyList.Add(surveyInfo);
          } else {
            FocusSurveyList.Add(surveyInfo);
          }
        }
      }
    }

    public class EmailsTabModel { // Vars and functions for Email tab in here.

      private readonly Pages_Albert.CoacheeEdit Page;

      public readonly List<DbHelper.EmailHistory.EmailHistoryInfo> EmailHistoryList;
      public readonly bool CanSendEmailToCoachee;
      public bool EmailHistoryNoResultsVisible = false;
      public bool EmailHistoryResultsVisible = false;

      public EmailsTabModel(Pages_Albert.CoacheeEdit page) {

        Page = page;
        CanSendEmailToCoachee = SessionHelper.AppAccess.Participants.CanSendEmailToCoachee(Page.CoacheeInfo);
        EmailHistoryList = DbHelper.EmailHistory.GetEmailHistoryForCoachee(Page.CoacheeInfo.CoacheeId).Items;

        if (EmailHistoryList == null || EmailHistoryList.Count == 0) {
          EmailHistoryNoResultsVisible = true;
          return;
        }
        EmailHistoryResultsVisible = true;
      }

      public void GetMessageContent(AjaxSubmitHelper ajax) {

        if (!DateTime.TryParse("" + WebHelper.GetFormValue("sentDate"), out DateTime sentDate)) {
          ajax.AddDialogMessage("Invalid Message Date.");
          return;
        }

        string subject = "" + WebHelper.GetFormValue("subject");

        var messageContent = Integrations.Mandrill.GetMessageContent(
          ConfigHelper.IsLiveServer ? Page.CoacheeInfo.EmailAddress : null,
          subject,
          sentDate.ToUniversalTime(null).AddMinutes(-1),
          sentDate.ToUniversalTime(null).AddMinutes(+1));

        if (messageContent == null) {
          ajax.AddDialogMessage("Message Not Found.");
          return;
        }

        ajax.AddSuccessDialog(messageContent.text);
      }
    }

    public class SettingsTabModel {
      private readonly Pages_Albert.CoacheeEdit Page;
      private readonly DbHelper.AlbertCoachees.AlbertCoacheeInfo CoacheeInfo;
      private readonly bool CanApplySettingsToProgram, CanChangeProgramStatus;
      private readonly List<DbHelper.Subscriptions.SubscriptionInfo> AllSubscriptionsList;
      private readonly List<DbHelper.AbleQuotes.QuoteItemForSubscription> QuoteItemsForSubscription;
      private readonly List<DbHelper.CoacheeProgramStatus.ProgramStatusInfo> ProgramStatusList = null;
      private readonly DbHelper.Projects.ProjectInfo ProjectInfo;
      public bool CanEditCoachAI, CanEditParticipantSettings;

      public class FormFields {
        public const string EnableNudges = "EnableNudges";
        public const string ApplySettingsToProgram = "ApplySettingsToProgram";
        public const string EnableAICoaching = "EnableAICoaching";
        public const string EnablePulse = "EnablePulse";
        public const string SubscriptionId = "SubscriptionId";
        public const string ProgramStatus = "ProgramStatus";
        public const string WelcomeEmailDate = "WelcomeEmailDate";
        public const string SendWelcomeNow = "SendWelcomeNow";
        public const string ApplyProfileToProgram = "ApplyProfileToProgram";
        public const string SubscriptionQuoteItemId = "SubscriptionQuoteItemId";
      }

      public class SubscriptionsDataAttr {
        public const string SubscriptionId = "subscriptionid";
        public const string QuoteItemId = "quoteitemid";
        public const string HasAICoaching = "hasaicoaching";
        public const string HasPulse = "haspulse";
        public const string HasMicrolearnings = "hasmicrolearnings";
        public const string HasNudges = "hasnudges";
      }

      private class FormValues {
        public bool EnableNudges;
        public bool EnableAICoaching;
        public bool EnablePulse;
        public int? SubscriptionId;
        public int ProgramStatusId;
        public bool SendWelcomeNow;
        public DateTime? WelcomeEmailUtc;
        public bool ApplyProfileToProgram;
        public DbHelper.AbleQuotes.QuoteItemForSubscription QuoteItemForSubscription;
        public bool DoSubscriptionUpdate;
      }

      public class AjaxReturnData {
        public const string CanUpdateSubscription = "canupdatesubscription";
      }

      public class CoacheeUpdateResult {
        public bool IsProfileUpdated { get; set; }
        public bool IsWelcomeEmailSent { get; set; }
        public bool IsSubscriptionUpdated { get; set; }
        public string WelcomeEmailResponseMessage { get; set; }
        public DbHelper.AbleQuotes.QuoteItemForSubscription UpdatedSubscription { get; set; }
      }

      public SettingsTabModel(Pages_Albert.CoacheeEdit page) {
        Page = page;
        CoacheeInfo = Page.CoacheeInfo;
        CanApplySettingsToProgram = Page.CanApplySettingsToProgram;
        CanEditParticipantSettings = Page.CanEditParticipantSettings;
        AllSubscriptionsList = Page.AllSubscriptionsList;
        QuoteItemsForSubscription = Page.QuoteItemsForSubscription;
        CanEditCoachAI = Page.CanEditCoachAI;
        CanChangeProgramStatus = Page.CanChangeProgramStatus;
        ProjectInfo = Page.ProjectInfo;
        ProgramStatusList = Page.ProgramStatusList;
      }

      public List<WebHelper.ButtonGroupButton> GetProgramStatusOptions() {
        var optionList = new List<WebHelper.ButtonGroupButton>();

        foreach (var ps in ProgramStatusList) {
          optionList.Add(new WebHelper.ButtonGroupButton(ps.DisplayTitle, ps.ProgramStatusId.ToString()));
        }
        return optionList;
      }

      private bool DoApplySettingsToProgram(AjaxSubmitHelper ajax) {

        if (!CanApplySettingsToProgram) {
          return false;
        }

        if (ajax.BadFieldCount > 0) return false;

        return !WebHelper.GetFormValue(FormFields.ApplySettingsToProgram).IsNullOrEmpty();
      }

      FormValues GetFormValues(AjaxSubmitHelper ajax, DbHelper.AlbertCoachees.AlbertCoacheeInfo thisCoacheeInfo, bool canUpdateSubscription) {

        var formValues = new FormValues();
        DbHelper.Subscriptions.SubscriptionInfo selectedSubscription = null;

        if (CanChangeProgramStatus) {
          int programStatusId = WebHelper.GetFormValueIntOrDefault(FormFields.ProgramStatus, 0);
          var dbProgramStatus = ProgramStatusList.Find(x => x.ProgramStatusId == programStatusId);
          if (dbProgramStatus == null) {
            ajax.AddBadField(FormFields.ProgramStatus, "Incorrect Program Status selected.");
            return null;
          }
          formValues.ProgramStatusId = dbProgramStatus.ProgramStatusId;
        } else {
          formValues.ProgramStatusId = thisCoacheeInfo.ProgramStatusId;
        }

        formValues.WelcomeEmailUtc = ajax.GetDatePickerToUtc(FormFields.WelcomeEmailDate, SessionHelper.GetSessionTimeZone(), "Welcome Email Date", false, "Please provide a date.");
        formValues.SendWelcomeNow = WebHelper.GetFormValue(FormFields.SendWelcomeNow) == "1";
        if (CanApplySettingsToProgram) formValues.ApplyProfileToProgram = !WebHelper.GetFormValue(FormFields.ApplyProfileToProgram).IsNullOrEmpty();

        if (canUpdateSubscription && Page.CanUpdateSubscription) {

          int quoteItemId = WebHelper.GetFormValueIntOrDefault(FormFields.SubscriptionQuoteItemId, 0);
          int subscriptionId = WebHelper.GetFormValueIntOrDefault(FormFields.SubscriptionId, 0);
          bool allowSubUpdate = false;

          if (subscriptionId == 0) {
            // If updating to no Subscription, keep element values for current coachee.
            formValues.SubscriptionId = null;
            formValues.EnableNudges = thisCoacheeInfo.DisableNudges;
            formValues.EnablePulse = thisCoacheeInfo.PulseSurveyEnabled;
            return formValues;
          }

          selectedSubscription = AllSubscriptionsList.Find(x => x.SubscriptionId == subscriptionId);
          if (selectedSubscription == null) {
            ajax.AddBadField(FormFields.SubscriptionId, "Invalid Subscription.");
            return null;
          }

          // If a QuoteItemId was selected, do validations
          if (quoteItemId != 0 || selectedSubscription.SubscriptionId != ConfigHelper.SubscriptionId.FoundationFree) {

            // If user selected a Subscription that isn't free but not a QuoteItemId, they must select one
            if (quoteItemId == 0) {
              ajax.AddBadField(FormFields.SubscriptionQuoteItemId, "You must select a Quote Item.");
              return null;
            }

            // Validate selected QuoteItemId is in the List
            var selectedQuoteItemInfo = QuoteItemsForSubscription.Find(x => x.QuoteItemId == quoteItemId);
            if (selectedQuoteItemInfo == null) {
              ajax.AddBadField(FormFields.SubscriptionQuoteItemId, "Invalid Quote Item.");
              return null;
            }

            // Validate that the selected SubscriptionId is the same in the selected QuoteItemId
            if (selectedQuoteItemInfo.SubscriptionId != selectedSubscription.SubscriptionId) {
              ajax.AddBadField(FormFields.SubscriptionId, "Selected Subscription doesn't match this Quote Item.");
              return null;
            }


            // Validate that there's available subscriptions to assign
            if (selectedQuoteItemInfo.AvailableSubscriptions <= 0) {
              if (thisCoacheeInfo.CoacheeId == CoacheeInfo.CoacheeId) {
                ajax.AddBadField(FormFields.SubscriptionQuoteItemId, "Not available Subscriptions in this Quote Item.");
                return null;
              }
            } else {
              allowSubUpdate = true;
            }

            formValues.QuoteItemForSubscription = selectedQuoteItemInfo;
          }

          if (allowSubUpdate) {
            formValues.SubscriptionId = subscriptionId;
            formValues.DoSubscriptionUpdate = true;
          }

        } else if (!thisCoacheeInfo.HasSubscription) {
          // If Coachee doesn't have an active subscription, do not allow editing factors.
          ajax.AddDialogMessage("This Coachee doesn't have an active subscription.");

        } else {

          selectedSubscription = AllSubscriptionsList.Find(x => x.SubscriptionId == thisCoacheeInfo.UserSubscription.SubscriptionId);
        }

        // Check what the subscription contains
        if (selectedSubscription.HasNudges) {
          formValues.EnableNudges = ajax.CheckFieldBool(FormFields.EnableNudges, WebHelper.YesNoButton_ValueNo);
        } else {
          formValues.EnableNudges = thisCoacheeInfo.DisableNudges;
        }

        if (selectedSubscription.HasPulse) {
          formValues.EnablePulse = ajax.CheckFieldBool(FormFields.EnablePulse, WebHelper.YesNoButton_ValueYes);
        } else {
          formValues.EnablePulse = thisCoacheeInfo.PulseSurveyEnabled;
        }

        if (selectedSubscription.HasAICoaching && CanEditCoachAI) {
          formValues.EnableAICoaching = ajax.CheckFieldBool(FormFields.EnableAICoaching, WebHelper.YesNoButton_ValueYes);
        } else if (thisCoacheeInfo.HasSubscription) {
          formValues.EnableAICoaching = thisCoacheeInfo.UserSubscription.UserHasAICoachEnabled;
        }

        return formValues;
      }

      public void UpdateSettings(AjaxSubmitHelper ajax) {

        string responseMessage = "Settings updated";

        if (!CanEditParticipantSettings) {
          ajax.AddDialogMessage("You do not have permission to edit Settings");
          return;
        }

        if (DoApplySettingsToProgram(ajax)) {

          // Set multiple coachees.
          int profilesUpdated = 0, welcomeEmailsSent = 0, subscriptionsUpdated = 0;

          foreach (int coacheeId in Page.CoacheesInProgram) {
            var thisCoacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(coacheeId);
            if (thisCoacheeInfo != null) {
              var updateResults = DoParticipantUpdate(ajax, thisCoacheeInfo);
              if (updateResults != null) {
                if (updateResults.IsProfileUpdated) profilesUpdated++;
                if (updateResults.IsWelcomeEmailSent) welcomeEmailsSent++;
                if (updateResults.IsSubscriptionUpdated) subscriptionsUpdated++;
              }
            }
          }

          responseMessage += " for " + profilesUpdated.ToString() + " participants.";
          if (welcomeEmailsSent > 0) {
            responseMessage += $@"<br/>Welcome/On-boarding email was sent to {welcomeEmailsSent} participants.";
          }
          if (subscriptionsUpdated > 0) {
            responseMessage += $@"<br/>Subscriptions updated for {subscriptionsUpdated} participants.";
          }

        } else {

          var updateResults = DoParticipantUpdate(ajax, CoacheeInfo);
          if (updateResults != null) {
            if (updateResults.IsProfileUpdated) {

            }
            if (updateResults.IsWelcomeEmailSent) {
              responseMessage += $@"<br/>Welcome/On-boarding email was sent.";
            }
            if (updateResults.IsSubscriptionUpdated) {
              responseMessage += $@"<br/>Subscription updated.";

              var user = SessionHelper.UserInfo;
              var participantExternalId = ConfigHelper.UserRole.Leader.ToExternalUserId(CoacheeInfo.UserGuid);

              if (participantExternalId.HasValue) {
                SendEvent(
                  intercom => {
                    var builder = intercom.SubscriptionUpdated()
                      .WithUser(SessionHelper.UserInfo.UserId, SessionHelper.AsExternalUserId())
                      .WithEmail(user.EmailAddress)
                      .WithOrganisation(user?.OrgId ?? 0, user.OrgName)
                      .WithParticipant(participantExternalId.Value, CoacheeInfo.EmailAddress);

                    if (CoacheeInfo.ProgramJobId.HasValue) {
                      builder.WithProject(CoacheeInfo.ProgramJobId.Value, ProjectInfo?.ProjectName);
                    }

                    if (updateResults.UpdatedSubscription != null) {
                      builder.WithSubscriptionDetails(
                        subscriptionType: updateResults.UpdatedSubscription.ProductTitle,
                        quantity: 1,
                        unitPrice: updateResults.UpdatedSubscription.UnitPrice
                      );
                    }

                    return builder;
                  },
                  operationName: "CoacheeEdit_SubscriptionUpdated",
                  requestRawUrl: SystemWeb.RequestRawUrl,
                  telemetryProperties: new Dictionary<string, object> {
                    ["ParticipantEmail"] = CoacheeInfo?.EmailAddress,
                    ["SubscriptionType"] = updateResults.UpdatedSubscription?.ProductTitle
                  }
                );
              }
            }
          }
        }

        if (!ajax.HasErrors) {
          ajax.AddSuccessToast(responseMessage);
          ajax.AddReturnValue(AjaxReturnData.CanUpdateSubscription, SessionHelper.AppAccess.Participants.CanUpdateSubscription(Page.ProgramInfo, CoacheeInfo).ToJSTrueFalse());
        }
      }

      public CoacheeUpdateResult DoParticipantUpdate(AjaxSubmitHelper ajax, DbHelper.AlbertCoachees.AlbertCoacheeInfo thisCoacheeInfo) {

        CoacheeUpdateResult updateResult = new CoacheeUpdateResult();

        bool canUpdateSubscription = SessionHelper.AppAccess.Participants.CanUpdateSubscription(Page.ProgramInfo, thisCoacheeInfo);
        var formValues = GetFormValues(ajax, thisCoacheeInfo, canUpdateSubscription);
        if (formValues == null) return updateResult;

        if (formValues.DoSubscriptionUpdate) {
          if (thisCoacheeInfo.UserSubscription == null) { // If SubscriptionInfo is null, create new object to insert into it.
            thisCoacheeInfo.UserSubscription = new DbHelper.Subscriptions.User.UserSubscriptionInfo();
          }
          thisCoacheeInfo.UserSubscription.SubscriptionId = formValues.SubscriptionId;
        }

        thisCoacheeInfo.DisableNudges = formValues.EnableNudges;
        thisCoacheeInfo.PulseSurveyEnabled = formValues.EnablePulse;
        thisCoacheeInfo.ProgramStatusId = formValues.ProgramStatusId;

        if (!Page.LimitedEdit) {
          thisCoacheeInfo.WelcomeEmailUtc = formValues.WelcomeEmailUtc;
        }

        if (CanEditCoachAI && thisCoacheeInfo.UserSubscription != null) {
          // This flag is dependent of a subscription, so only update if subscription is present.
          thisCoacheeInfo.UserSubscription.UserHasAICoachEnabled = formValues.EnableAICoaching;
        }

        DbHelper.Common.UsingTransaction(trans => {
          try {
            updateResult.IsProfileUpdated = DbHelper.AlbertCoachees.UpdateCoachee(trans, thisCoacheeInfo);

            if (updateResult.IsProfileUpdated && formValues.DoSubscriptionUpdate) {
              DbHelper.Subscriptions.User.UpdateCoacheeSubscription(trans, thisCoacheeInfo, formValues.QuoteItemForSubscription);
              updateResult.IsSubscriptionUpdated = true;
              updateResult.UpdatedSubscription = formValues.QuoteItemForSubscription;
            }

            if (CanEditCoachAI) {
              DbHelper.Subscriptions.User.UpdateHasAICoachEnabled(trans, thisCoacheeInfo.UserSubscription);
            }

            return true;

          } catch (Exception ex) {
            var user = SessionHelper.GetUserInfoOrNull();
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(thisCoacheeInfo?.UserGuid))
              .WithOperation(nameof(DoParticipantUpdate))
              .WithOperationContext("UpdateParticipantSettings")
              .FromSession()
              .Track();

            ajax.AddDialogMessage("Error updating settings for participant: " + thisCoacheeInfo.GetFullName() + "<br/>" + ex.Message);
            return false;
          }
        });

        // Send Welcome Email if selected
        if (formValues.SendWelcomeNow) {
          UpdateDbAndSendWelcomeEmail(ajax, formValues, thisCoacheeInfo, out bool emailSent, out string errorMessage, out Exception exception);
          updateResult.IsWelcomeEmailSent = emailSent;
          if (!errorMessage.IsNullOrEmpty()) {
            updateResult.WelcomeEmailResponseMessage += $"<br/><b>{thisCoacheeInfo.GetFullName().HTMLEncode()}</b>: {errorMessage.ValueIfNullOrEmpty("Unknown Error.")}";
          }
          if (emailSent) updateResult.WelcomeEmailResponseMessage += $".<br/>Welcome/On-boarding email was sent.";
        }

        return updateResult;
      }

      public string GetEnableNudgesOptions(string labelText) {
        return WebHelper.GetYesNoButtons(labelText, FormFields.EnableNudges, !CoacheeInfo.DisableNudges, !CanEditParticipantSettings);
      }

      public string GetEnableAICoachingOptions(string labelText) {
        return WebHelper.GetYesNoButtons(labelText, FormFields.EnableAICoaching, CoacheeInfo.UserHasAICoach, !CanEditCoachAI);
      }

      public string GetEnablePulseOptions(string labelText) {
        string lastSentOn = CoacheeInfo.PulseSurveyLastSentUtc.HasValue ? $"Sent On: {WebHelper.DisplayDate(CoacheeInfo.PulseSurveyLastSentUtc.UtcToTZOrNull())}." : "";
        return WebHelper.GetYesNoButtons(labelText, FormFields.EnablePulse, CoacheeInfo.PulseSurveyEnabled, !CanEditParticipantSettings, lastSentOn);
      }

      public string GetSubscriptionOptions(string label) {

        var optionsHtml = new StringBuilder();

        optionsHtml.AppendLine("<option value=\"\">[Select Subscription]</option>");

        foreach (var sub in AllSubscriptionsList) {
          optionsHtml.Append("<option");
          int subscriptionId = sub.SubscriptionId.Value;
          if (CoacheeInfo.HasSubscription && CoacheeInfo.UserSubscription.SubscriptionId.GetValueOrDefault(0) == subscriptionId) {
            optionsHtml.Append(" selected");
          }
          optionsHtml.Append(" value=\"" + subscriptionId + "\"");
          optionsHtml.Append(" data-" + SubscriptionsDataAttr.HasMicrolearnings + " =\"" + sub.HasMicrolearnings.ToJSTrueFalse() + "\"");
          optionsHtml.Append(" data-" + SubscriptionsDataAttr.HasNudges + " =\"" + sub.HasNudges.ToJSTrueFalse() + "\"");
          optionsHtml.Append(" data-" + SubscriptionsDataAttr.HasAICoaching + " =\"" + sub.HasAICoaching.ToJSTrueFalse() + "\"");
          optionsHtml.Append(" data-" + SubscriptionsDataAttr.HasPulse + " =\"" + sub.HasPulse.ToJSTrueFalse() + "\">");
          optionsHtml.Append(string.Join(" - ", sub.SubscriptionName, sub.SubscriptionDescription));
          optionsHtml.AppendLine("</option>");
        }

        return WebHelper.GetSelectRow(label, FormFields.SubscriptionId, 6, optionsHtml.ToString(), "", !Page.CanUpdateSubscription);
      }

      public string GetQuoteItemsForSubscriptionHtml(string label) {

        var optionsHtml = new StringBuilder();

        optionsHtml.AppendLine("<option value=\"\">[Select Quote Item]</option>");

        foreach (var qi in QuoteItemsForSubscription) {
          optionsHtml.Append("<option");
          int quoteItem = qi.QuoteItemId;
          if (CoacheeInfo.HasSubscription && CoacheeInfo.UserSubscription.QuoteItemId.GetValueOrDefault(0) == quoteItem) {
            optionsHtml.Append(" selected");
          }
          optionsHtml.Append(" value=\"" + quoteItem + "\"");
          optionsHtml.Append(" data-" + SubscriptionsDataAttr.SubscriptionId + " =\"" + qi.SubscriptionId + "\"");
          optionsHtml.Append(" data-" + SubscriptionsDataAttr.QuoteItemId + " =\"" + qi.QuoteItemId + "\">");
          optionsHtml.Append(string.Join(" - ", $"({qi.AvailableSubscriptions} available)", qi.ItemDescription));
          optionsHtml.AppendLine("</option>");
        }

        return WebHelper.GetSelectRow(label, FormFields.SubscriptionQuoteItemId, 6, optionsHtml.ToString(), "", !Page.CanUpdateSubscription);
      }


      void UpdateDbAndSendWelcomeEmail(AjaxSubmitHelper ajax, FormValues formValues, DbHelper.AlbertCoachees.AlbertCoacheeInfo sendToCoacheeInfo,
        out bool emailSent, out string errorMessage, out Exception exception) {

        emailSent = false;
        errorMessage = null;
        exception = null;

        if (!sendToCoacheeInfo.CanReceiveWelcomeEmail) {
          errorMessage = "Welcome Email cannot be sent, requires either coaching or workshops.";
          return;
        }

        // Update coachee status to onboarding if its status is prior to that.
        // Don't send email if db update fails.
        try {
          DbHelper.AlbertCoachees.UpdateStatusWaitingToOnboarding(CoacheeInfo);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(CoacheeInfo?.UserGuid))
            .WithOperation(nameof(UpdateDbAndSendWelcomeEmail))
            .WithOperationContext("SetParticipantToOnboarding")
            .FromSession()
            .Track();

          errorMessage = "Error setting Coachee to OnBoarding. Email(s) NOT sent.";
          exception = ex;
          return;
        }

        var coachInfo = DbHelper.AlbertCoaches.GetCoachInfo(sendToCoacheeInfo.CoachUserId);

        EmailHelper.MandrillSentResult sendResult;
        try {
          emailSent = AlbertEmails.ParticipantWelcome.Send(ProjectInfo, sendToCoacheeInfo, coachInfo, ProjectInfo, out sendResult, AlbertEmails.ParticipantWelcome.SetSendDates.Yes);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(CoacheeInfo?.UserGuid))
            .WithOperation(nameof(UpdateDbAndSendWelcomeEmail))
            .WithOperationContext("SendWelcomeEmail")
            .FromSession()
            .Track();

          errorMessage = "Error sending Welcome email.";
          exception = ex;
          return;
        }

        // Send Intercom event for coachee invitation
        if (emailSent) {
          var user = SessionHelper.UserInfo;
          SendEvent(
            intercom => intercom.CoacheeInvited()
              .WithUser(SessionHelper.UserInfo.UserId, SessionHelper.AsExternalUserId())
              .WithEmail(user.EmailAddress)
              .WithCoacheeEmailAddress(sendToCoacheeInfo.EmailAddress)
              .WithOrganisation(sendToCoacheeInfo.TenantOrgId, sendToCoacheeInfo.OrgName),
            operationName: "CoacheeEdit_CoacheeInvited",
            requestRawUrl: SystemWeb.RequestRawUrl,
            telemetryProperties: new Dictionary<string, object> {
              ["CoacheeEmail"] = sendToCoacheeInfo.EmailAddress
            }
          );
        }

        if (!emailSent) {
          errorMessage = $"Welcome email NOT sent.";
          if (!ConfigHelper.IsLiveServer) {
            errorMessage += $" From: {sendResult.Message.FromEmail}. Reason: {sendResult.StatusAndReason}";
          }
        }
      }
    }

    public class NotesTabModel {
      public class CoachingNoteInfo {
        public int CoachingSessionId { get; set; }
        public string CoachingNote { get; set; }
        public string SessionNotes { get; set; }
        public CoachingNoteInfo(int coachingSessionId, string coachingNote, string sessionNotes) {
          this.CoachingSessionId = coachingSessionId;
          this.CoachingNote = coachingNote;
          this.SessionNotes = sessionNotes;
        }
      }
      public class FormFields {
        public const string CoacheeNotes = "CoacheeNotes";
        public const string PrivateCoachNote = "PrivateCoachNote";
        public const string CoachingSessionNote = "CoachingSessionNote";
      }

      private class FormValues {
        public string CoacheeNotes;
        public string PrivateCoachNote;
        public List<CoachingNoteInfo> CoachingSessionNotesList;
      }

      // Vars for convenience assigned from the page object.
      private readonly Pages_Albert.CoacheeEdit Page;
      private readonly DbHelper.AlbertCoachees.AlbertCoacheeInfo CoacheeInfo;
      private readonly bool CanEditParticipantNotes, CanEditPrivateCoachNote, CanEditCoachingSessionNotes;
      private readonly List<DbHelper.CoachingSessions.AbleSessionInfo> CoachingSessionList;
      private bool CoachingSessionsNotesChanged = false;

      // Constructor - just pass "this" (current page object).
      public NotesTabModel(Pages_Albert.CoacheeEdit page) {

        Page = page;

        // Convenience vars
        CoacheeInfo = Page.CoacheeInfo;
        CanEditPrivateCoachNote = Page.CanEditPrivateCoachNote;
        CanEditParticipantNotes = Page.CanEditParticipantNotes;
        CanEditCoachingSessionNotes = Page.CanEditCoachingSessionNotes;
        CoachingSessionList = Page.CoachingSessionList;
      }

      public string GetSessionNotesHtml() {
        string html = "";
        if (CoachingSessionList.IsNullOrEmpty()) return html;

        html = $"<div class=\"mb10\"><h4>Coaching Sessions Notes {WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.info, "Private notes, not accessible for Participants or any other users.", "", "mt3")}</h4></div>";

        int sessionCount = 1;
        foreach (var cs in CoachingSessionList) {
          html += WebHelper.GetRichTextArea($"Session #{sessionCount}<br/>{WebHelper.DisplayDate(cs.ApptDateUTC)}", FormFields.CoachingSessionNote + sessionCount.ToString(), 2, 8, cs.CoachNotes, "", !CanEditCoachingSessionNotes);
          sessionCount++;
        }
        return html.EnsureStartsWith("<hr />");
      }



      FormValues GetFormValues(AjaxSubmitHelper ajax) {

        var formValues = new FormValues();

        if (CanEditParticipantNotes) {
          formValues.CoacheeNotes = ajax.CheckFieldRegex(FormFields.CoacheeNotes, "Coachee Notes", AppHelper.Regex.HTML, false, "Please remove unusual characterss.");
        } else {
          formValues.CoacheeNotes = CoacheeInfo.CoacheeNotes;
        }

        if (CanEditPrivateCoachNote) {
          formValues.PrivateCoachNote = ajax.CheckFieldRegex(FormFields.PrivateCoachNote, "Private Coach Notes", AppHelper.Regex.HTML, false, "Please remove unusual characters.");
        } else {
          formValues.PrivateCoachNote = CoacheeInfo.PrivateCoachNote;
        }

        CoachingSessionsNotesChanged = false; // Default value
        if (CanEditCoachingSessionNotes && !CoachingSessionList.IsNullOrEmpty()) {
          int sessionCount = 1;
          formValues.CoachingSessionNotesList = new List<CoachingNoteInfo>();
          foreach (var cs in CoachingSessionList) {
            var thisCoachingSessionNote = ajax.CheckFieldRegex(FormFields.CoachingSessionNote + sessionCount.ToString(), $"Session #{sessionCount} Note", AppHelper.Regex.HTML, false, "Please remove unusual characters.");
            if (cs.CoachNotes != thisCoachingSessionNote) {
              CoachingSessionsNotesChanged = true;
              formValues.CoachingSessionNotesList.Add(new CoachingNoteInfo(cs.SessionId, thisCoachingSessionNote, cs.SessionNotes)); // SessionNotes always takes existing value in db.
            }
            sessionCount++;
          }
        }

        if (ajax.BadFieldCount > 0) return null;

        return formValues;
      }

      public void UpdateNotes(AjaxSubmitHelper ajax) {
        var formValues = GetFormValues(ajax);
        if (formValues == null) return;

        bool ParticipantNotesChanged = CoacheeInfo.CoacheeNotes != formValues.CoacheeNotes || CoacheeInfo.PrivateCoachNote != formValues.PrivateCoachNote;
        if (!ParticipantNotesChanged && !CoachingSessionsNotesChanged) return; // Do not do update if upcoming text is the same as in db.

        // Update coaching notes
        if (CanEditCoachingSessionNotes && CoachingSessionsNotesChanged && !CoachingSessionList.IsNullOrEmpty() && !formValues.CoachingSessionNotesList.IsNullOrEmpty()) {
          foreach (var cs in formValues.CoachingSessionNotesList) {
            // Update sessions Coach Notes - In id_CoachingSession
            DbHelper.CoachingSessions.UpdateSessionLocked(null, cs.CoachingSessionId, cs.CoachingNote, cs.SessionNotes);
          }
        }

        if (CanEditParticipantNotes) CoacheeInfo.CoacheeNotes = formValues.CoacheeNotes;
        if (CanEditPrivateCoachNote) CoacheeInfo.PrivateCoachNote = formValues.PrivateCoachNote;

        if (ParticipantNotesChanged && (CanEditParticipantNotes || CanEditPrivateCoachNote)) {
          try {
            DbHelper.AlbertCoachees.UpdateCoacheeNotes(null, CoacheeInfo); // Notes in al_Coachees
          } catch (Exception ex) {
            var user = SessionHelper.GetUserInfoOrNull();
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .AddExternalUserId(ExternalUserKind.Leader, ConfigHelper.UserRole.Leader.ToExternalUserId(CoacheeInfo?.UserGuid))
              .WithOperation(nameof(UpdateNotes))
              .WithOperationContext("UpdateParticipantNotes")
              .FromSession()
              .Track();

            ajax.AddDialogMessage("Error updating participant: " + ex.Message);
            return;
          }
        }
      }
    }

    // Common permissions flags.
    public bool ShowCompanySelection, CanEditCoachee, CanAddCoachingSession, CanViewNonProfileTabs, CanUpdateProfile, CanUpdateSubscription, CanViewParticipantSummary, CanEditPrivateCoachNote;
    public bool CanApplySettingsToProgram, CanEditCoachAI, CanEditParticipantSettings, CanViewSurveyDetails, CanChangeUserIdIfEmailChanged;
    public bool CanEditParticipantNotes, CanViewParticipantNotes, CanEditCoachingSessionNotes, AllowNotesUpdate, CanViewCoachingNotes;

    public bool IsReadOnly = false;
    public PathHelper.CoacheeTabEnum SelectedCoacheeTab;
    public List<int> CoacheesInProgram = new List<int>();
    public List<DbHelper.Subscriptions.SubscriptionInfo> AllSubscriptionsList = null;
    public List<DbHelper.OrgRoles.OrgRolesInfo> OrgRolesInfo = null;
    public List<DbHelper.CoachingSessions.AbleSessionInfo> CoachingSessionList;
    public List<DbHelper.AbleQuotes.QuoteItemForSubscription> QuoteItemsForSubscription = null;
    List<DbHelper.CoacheeProgramStatus.ProgramStatusInfo> ProgramStatusList = null;

    public class AjaxAction {
      public const string UpdateProfile = "UpdateProfile";
      public const string UpdateCoaching = "UpdateCoaching";
      public const string DeleteParticipant = "DeleteParticipant";
      public const string EmailMessageContent = "EmailMessageContent";
      public const string UpdateSettings = "UpdateSettings";
      public const string CheckEmailChange = "CheckEmailChange";
      public const string UpdateNotes = "UpdateNotes";
    }

    public SummaryTabModel SummaryTab = null;
    public ProfileTabModel ProfileTab = null;
    public CoachingTabModel CoachingTab = null;
    public EmailsTabModel EmailsTab = null;
    public SettingsTabModel SettingsTab = null;
    public SurveyTabModel SurveyTab = null;
    public NotesTabModel NotesTab = null;

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Participant: " + CoacheeInfo.GetFullName();
      CoacheesInProgram = DbHelper.AlbertCoachees.GetCoacheesInProgram(CoacheeInfo.ProgramJobId ?? 0).Select(c => c.CoacheeId).ToList();
      ShowCompanySelection = SessionHelper.AppAccess.Participants.CanChangeCompany(CoacheeInfo);
      CanEditCoachee = SessionHelper.AppAccess.Programs.CanUpdateProgramParticipant(ProgramInfo);
      CanAddCoachingSession = SessionHelper.AppAccess.Sessions.CanAdd(CoacheeInfo);
      CanEditPrivateCoachNote = SessionHelper.AppAccess.Participants.CanEditPrivateCoachNote(CoacheeInfo);
      IsReadOnly = !CanEditCoachee;
      SelectedCoacheeTab = PathHelper.CoacheeTabEnum.settings; // Default to profile tab.
      CanUpdateProfile = CanEditCoachee || LimitedEdit;
      CanUpdateSubscription = SessionHelper.AppAccess.Participants.CanUpdateSubscription(ProgramInfo, CoacheeInfo);
      CanApplySettingsToProgram = SessionHelper.AppAccess.Participants.CanApplySettingsToProgram(CoacheeInfo);
      CanEditParticipantSettings = SessionHelper.AppAccess.Participants.CanEditParticipantSettings(CoacheeInfo) && CanUpdateProfile;
      CanEditCoachAI = SessionHelper.AppAccess.Participants.CanEditCoachAI(CoacheeInfo);
      CanViewSurveyDetails = SessionHelper.AppAccess.Participants.CanViewSurveyDetails(ProgramInfo, CoacheeInfo);
      CanChangeUserIdIfEmailChanged = SessionHelper.AppAccess.Participants.CanChangeUserIdIfEmailChanged();
      CanViewParticipantSummary = SessionHelper.AppAccess.Participants.CanViewParticipantSummary(CoacheeInfo);
      CanViewParticipantNotes = SessionHelper.AppAccess.Participants.CanViewParticipantNotes(CoacheeInfo);
      CanEditParticipantNotes = SessionHelper.AppAccess.Participants.CanEditParticipantNotes(CoacheeInfo);
      CanViewCoachingNotes = SessionHelper.AppAccess.Participants.CanViewCoachingNotes(CoacheeInfo);
      CanEditCoachingSessionNotes = SessionHelper.AppAccess.Sessions.CanEditNotes(CoacheeInfo);
      AllowNotesUpdate = CanViewParticipantNotes || CanEditParticipantNotes || CanEditCoachingSessionNotes || CanViewCoachingNotes;

      // Get Coachee's coaching sessions. Here so it's accessible for multiple tabs.
      CoachingSessionList = DbHelper.CoachingSessions.GetSessionsForCoacheeId(CoacheeInfo.CoacheeId, DbHelper.CoachingSessions.SessionSort.DateAscending);

      // Get subscription options
      AllSubscriptionsList = DbHelper.Subscriptions.GetAllSubscriptions();
      // Get Quote Items available to allocate a subscription for user
      QuoteItemsForSubscription = DbHelper.AbleQuotes.GetQuoteItemsForSubscriptions(ProgramInfo.ProgramJobNumber);
      // Get OrgRoles
      OrgRolesInfo = DbHelper.OrgRoles.GetOrgRolesList();
      // Get Coachee Program Status list
      ProgramStatusList = DbHelper.CoacheeProgramStatus.GetProgramStatusList();
      // Note that when page tabs are hidden, the "Profile" details are still showed, but on the main page not within a tab.
      CanViewNonProfileTabs = SessionHelper.AppAccess.Participants.CanViewNonProfileTabs();

      // Reads if URL contains a specific tab to redirect to, this variable is read in jQuery
      string pageTabQuery = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.PageTab, "");
      if (!Enum.TryParse(pageTabQuery, true, out SelectedCoacheeTab)) SelectedCoacheeTab = PathHelper.CoacheeTabEnum.settings;

      if (!SystemWeb.IsHttpPost) {

        // Instantiate all tab models.
        SummaryTab = new SummaryTabModel(this);
        ProfileTab = new ProfileTabModel(this);
        CoachingTab = new CoachingTabModel(this);
        EmailsTab = new EmailsTabModel(this);
        SettingsTab = new SettingsTabModel(this);
        SurveyTab = new SurveyTabModel(this);
        NotesTab = new NotesTabModel(this);

      } else {

        AjaxSubmitHelper.Process(ajax => {

          switch (PageAjaxAction) {

            case AjaxAction.UpdateProfile:
              if (!CanUpdateProfile) {
                ajax.AddDialogMessage("Update not allowed.");
                return;
              }
              new ProfileTabModel(this).UpdateProfile(ajax);
              break;

            case AjaxAction.UpdateCoaching:
              if (!CanUpdateCoaching) {
                ajax.AddDialogMessage("Update not allowed.");
                return;
              }
              new CoachingTabModel(this).UpdateCoaching(ajax);
              break;

            case AjaxAction.DeleteParticipant:
              if (!CanDeleteParticipant) {
                ajax.AddDialogMessage("Deletion not allowed.");
              } else {
                new CoachingTabModel(this).DeleteParticipant(ajax);
              }
              break;

            case AjaxAction.EmailMessageContent:
              EmailsTab = new EmailsTabModel(this);
              EmailsTab.GetMessageContent(ajax);
              break;

            case AjaxAction.UpdateSettings:
              if (!CanUpdateProfile) {
                ajax.AddDialogMessage("Update not allowed.");
                return;
              }
              new SettingsTabModel(this).UpdateSettings(ajax);
              break;

            case AjaxAction.CheckEmailChange:
              new ProfileTabModel(this).CheckEmailChange(ajax);
              break;

            case AjaxAction.UpdateNotes:
              if (!AllowNotesUpdate) {
                ajax.AddDialogMessage("Update not allowed.");
                return;
              }
              new NotesTabModel(this).UpdateNotes(ajax);
              break;
          }
        });

        return;
      }
    }

    public void UpdateUserLinked(DbHelper.AlbertCoachees.AlbertCoacheeInfo thisCoacheeInfo) {

      if (thisCoacheeInfo == null) return;

      // Get Coachee's user info by email
      var coacheeUserInfo = DbHelper.AbleUser.GetUserByEmailOrNull(thisCoacheeInfo.EmailAddress, DbHelper.AbleUser.RegisteredFilter.Any);
      int invitedByUserId = DbHelper.AbleUser.GetInvitedByUserId(coacheeUserInfo);

      if (coacheeUserInfo != null) {
        // If Coachee happens to not be linked to a user, update.
        if (thisCoacheeInfo.UserId == null) {
          DbHelper.AlbertCoachees.UpdateCoacheeUserId(null, thisCoacheeInfo);
        }

        // If user hasn't been flagged as Participant, update.
        if (!coacheeUserInfo.IsParticipant) {
          DbHelper.AbleUser.UpdateIsParticipant(null, coacheeUserInfo, true);
        }

        // If user is not registered and doesn't have an invite code, create one. Also update data if Coach was changed to update InvitedByUserId.
        if (!coacheeUserInfo.IsRegistered && (coacheeUserInfo.InviteCode.IsNullOrEmpty() || coacheeUserInfo.InvitedByUserId != invitedByUserId)) {
          DbHelper.AbleUser.UpdateInviteDetails(coacheeUserInfo, invitedByUserId);
        }

        // Make sure that by default we always set the free subscription.
        if (coacheeUserInfo.UserSubscription == null) {
          thisCoacheeInfo.UserSubscription = new DbHelper.Subscriptions.User.UserSubscriptionInfo();
          thisCoacheeInfo.UserSubscription.SubscriptionId = ConfigHelper.SubscriptionId.FoundationFree;
          DbHelper.Subscriptions.User.UpdateCoacheeSubscription(null, thisCoacheeInfo);
        }
      }
    }
  }
}
