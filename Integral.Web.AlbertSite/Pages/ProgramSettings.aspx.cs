using Integral.Web.Services;
using System;
using System.Collections.Generic;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class ProgramSettings : AppCode.PageBaseClasses.ProgramPageBase {

    private List<DbHelper.AlbertCoaches.AlbertCoachInfo> PartnerList;
    public bool DisableDelete, CanEditSettings, CanEditPercentages, CanViewPercentages, CanViewSettingsDates;
    public const string DisableDeleteMsg = "Can't delete Program if Coachees or Workshops exist. Please delete those first.";
    public bool CanUpdateJobNumber = false;

    string returnUrl = "";

    public class FormFields {
      public const string ProgramJobId = "ProgramJobId";
      public const string ProgramJobGUID = "ProgramJobGUID";
      public const string AddToProjectId = "AddToProjectId";

      public const string ProgramJobNumber = "JobNumber";
      public const string ProgramJobName = "JobName";
      public const string ProgramStatus = "ProgramStatus";

      public const string ProjectCoordinatorUserId = "ProjectCoordinatorUserId";
      public const string LeadConsultantUserId = "LeadConsultantUserId";

      public const string SalesPartnerUserId = "SalesPartnerUserId";
      public const string Partner_DeliveryPercentage = "Partner_DeliveryPercentage";
      public const string Partner_SalesDeliveryPercentage = "Partner_SalesDeliveryPercentage";
      public const string Partner_PLCPercentage = "Partner_PLCPercentage";

      public const string ProgramNotes = "ProgramNotes";
      public const string BookingPageInstructions = "BookingPageInstructions";
    }

    class FormValues {

      public Guid? ProgramJobGUID;

      public string ProgramJobNumber;
      public string ProgramJobName;
      public int? ProgramStatusId;

      public int? ProjectCoordinatorUserId;
      public int? SalesPartnerUserId;
      public int? LeadConsultantUserId;

      public decimal? Partner_DeliveryPercentage;       // Note all percentages are stored as 0.00 (0%) to 1.00 (100%).
      public decimal? Partner_SalesDeliveryPercentage;
      public decimal? Partner_PLCPercentage;

      public string ProgramNotes;
      public string BookingPageInstructions;

      public TimeZoneInfo timeZoneInfo = ConfigHelper.DefaultTimeZoneInfo;
    }

    protected void Page_Load(object sender, EventArgs e) {

      returnUrl = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ReturnUrl);

      CanEditSettings = SessionHelper.AppAccess.Programs.CanEditProgramSettings(ProgramInfo);
      CanEditPercentages = SessionHelper.AppAccess.Programs.CanEditSettingsPercentages(ProgramInfo);
      CanViewPercentages = SessionHelper.AppAccess.Programs.CanViewSettingsPercentages(ProjectInfo, IsNewProgram);
      CanViewSettingsDates = SessionHelper.AppAccess.Programs.CanViewSettingsDates(ProjectInfo, IsNewProgram);
      CanUpdateJobNumber = SessionHelper.IsUserRoleAdmin;
      PartnerList = DbHelper.AlbertCoaches.GetCoachInfoList(true, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);

      this.PageTitle = "Settings";
      this.PageSubtitle = "";

      if (IsNewProgram) {
        string addToProjectJobNumber = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ProjectJobNumber);
        if (!addToProjectJobNumber.IsNullOrEmpty()) {
          AddToProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(addToProjectJobNumber);
          CanEditSettings = SessionHelper.AppAccess.Programs.CanCreateProgram(AddToProjectInfo);
          this.PageSubtitle = "Adding to Project: <b>" + AddToProjectInfo.JobNumber.HTMLEncode() + " - " + AddToProjectInfo.ProjectName.HTMLEncode() + "</b>";
          this.PageSubtitleIsHtml = true;
        }
      }

      if (!SystemWeb.IsHttpPost) {
        ShowForm(ProgramInfo);
        return;
      }

      if (!CanEditSettings) {
        WebHelper.EndRequest(WebHelper.HttpStatusEnum.Forbidden);
        return;
      }

      // Form submission.
      AjaxSubmitHelper.Process(ajax => {
        if (!IsNewProgram && ProgramInfo == null) {
          ajax.AddDialogMessage("Error: Program not found.");
        } else if (PageAjaxAction == "update") {
          UpdateProgram(ajax, ProgramInfo);
        } else if (PageAjaxAction == "delete") {
          DeleteProgram(ajax, ProgramInfo);
        }
      });
      return;
    }

    void ShowForm(DbHelper.AblePrograms.AbleProgramInfo programInfo) {

      if (IsNewProgram) {
        PageTitle = "New Program";
      } else {
        PageTitle = "Program Settings";
        PageSubtitle = programInfo.ProgramJobNumber + ": " + programInfo.ProgramJobName;
      }

    }

    public string GetProgramStatusOptions(string labelText) {

      var psChosen = DbHelper.AlbertProgramStatus.GetProgramStatusByIdOrNull(ProgramInfo.ProgramStatusId);

      int? selectedValue = psChosen == null ? DbHelper.AlbertProgramStatus.Statuses.Setup.ProgramStatusId : (int?)psChosen.ProgramStatusId;

      var optionList = new List<WebHelper.ButtonGroupButton>();

      foreach (var ps in DbHelper.AlbertProgramStatus.GetProgramStatusList()) {
        optionList.Add(new WebHelper.ButtonGroupButton(ps.DisplayTitle, ps.ProgramStatusId.ToString()));
      }

      return WebHelper.GetButtonGroup(labelText, FormFields.ProgramStatus, optionList, selectedValue.ToStringOrNull(), !CanEditSettings);
    }

    public string GetPartnerDropdownHtml(string labelText, string formName, int inputCol, int? selectedPartnerId, bool isReadOnly) {

      return WebHelper.GetPartnerDropdown(new WebHelper.PartnerDropdownInfo() {
        PartnerInfoList = PartnerList,
        FormName = formName,
        IsReadOnly = isReadOnly,
        LabelText = labelText,
        InputCols = inputCol,
        SelectedPartnerUserId = selectedPartnerId,
        CanViewHiddenPartners = CanViewHiddenPartners,
        CanViewInactivePartners = CanViewInactivePartners,
        IncludeUnassignedUser = true
      });
    }

    public string GetParticipantRequestStatus() {
      if (ProgramInfo.ParticipantFormEmailSentUtc == null) {
        return "Request has not yet been sent.";
      } else {
        return "Request Last sent: " + WebHelper.DisplayDate(ProgramInfo.ParticipantFormEmailSentUtc.UtcToTZOrNull());
      }
    }

    void UpdateProgram(AjaxSubmitHelper ajax, DbHelper.AblePrograms.AbleProgramInfo programInfo) {

      // Form validation.
      var formValues = new FormValues();

      formValues.ProgramJobGUID = ajax.CheckGuid(FormFields.ProgramJobGUID, "Workshop UID", false, "Invalid Program GUID");

      if (AddToProjectInfo != null) {
        // We know the Project Job Number that this Program is being added to.
        formValues.ProgramJobNumber = AddToProjectInfo.JobNumber;
      } else {
        if (CanUpdateJobNumber) {
          formValues.ProgramJobNumber = ajax.CheckFieldRegex(FormFields.ProgramJobNumber, "Job Number", AppHelper.Regex.GeneralText, true, "Use only text characters in Program Job Number.");
        } else {
          // Keep existing job number.
          formValues.ProgramJobNumber = ProgramInfo.ProgramJobNumber;
        }
      }

      formValues.ProgramJobName = ajax.CheckFieldRegex(FormFields.ProgramJobName, "Program Name", AppHelper.Regex.GeneralText, true, "Use only text characters in Program Name.");

      formValues.ProgramStatusId = WebHelper.GetFormValue(FormFields.ProgramStatus).ToIntOrNull();
      if (formValues.ProgramStatusId != null) {
        var programStatus = DbHelper.AlbertProgramStatus.GetProgramStatusByIdOrNull(formValues.ProgramStatusId);
        if (programStatus == null) {
          ajax.AddBadField(FormFields.ProgramStatus, "Invalid Program Status.");
          return;
        }
      }
      formValues.ProjectCoordinatorUserId = ajax.CheckFieldIDOrNull(FormFields.ProjectCoordinatorUserId, "Project Coordinator", false, "Please choose a Project Coordinator.");
      formValues.LeadConsultantUserId = ajax.CheckFieldIDOrNull(FormFields.LeadConsultantUserId, "Lead Consultant", false, "Please choose a Lead Consultant.");

      formValues.SalesPartnerUserId = ajax.CheckFieldIDOrNull(FormFields.SalesPartnerUserId, "Sales Partner", false, "Please choose a Sales Partner.");

      if (CanEditPercentages && CanViewPercentages) {
        formValues.Partner_DeliveryPercentage = ajax.CheckFieldPercent(FormFields.Partner_DeliveryPercentage, "Delivery Percentage", false, false, "A number between 0 and 100, or blank.");
        formValues.Partner_SalesDeliveryPercentage = ajax.CheckFieldPercent(FormFields.Partner_SalesDeliveryPercentage, "Sales Delivery Percentage", false, false, "A number between 0 and 100, or blank.");
        formValues.Partner_PLCPercentage = ajax.CheckFieldPercent(FormFields.Partner_PLCPercentage, "PLC Percentage", false, false, "A number between 0 and 100, or blank.");
      }

      formValues.ProgramNotes = ajax.CheckFieldRegex(FormFields.ProgramNotes, "Program Notes", AppHelper.Regex.HTML, false, "Invalid Characters in Program Notes.");
      formValues.BookingPageInstructions = ajax.CheckFieldRegex(FormFields.BookingPageInstructions, "Booking Page Instructions", AppHelper.Regex.HTML, false, "Invalid Characters in Booking Page Instructions.");

      if (ajax.BadFieldCount > 0) return;

      // Form validation: The submitted GUID must also match the requested Program's GUID.
      if (!IsNewProgram && formValues.ProgramJobGUID != programInfo.ProgramJobGUID) {
        ajax.AddDialogMessage("Error validating Program to update.");
        return;
      }

      CopyFormValues(formValues, programInfo);

      var project = DbHelper.Projects.GetProjectInfoOrNull(formValues.ProgramJobNumber);
      if (project != null) {
        programInfo.CompanyId = project.CompanyId;
      }

      bool updateSuccess;
      if (IsNewProgram)
        updateSuccess = UpdateDb_AddNew(ajax, formValues, programInfo);
      else
        updateSuccess = UpdateDb_Existing(ajax, formValues, programInfo);

      if (!updateSuccess) {
        if (!ajax.MessagesExist()) ajax.AddDialogMessage("Error - Program was not updated."); // Just in case.
        return;
      }

      // Regardless of below, we are definitely redirecting after form submission.
      ajax.SetRedirectUrl(
        !returnUrl.IsNullOrEmpty() ? returnUrl : PathHelper.Pages.ProgramSettings(programInfo.ProgramJobId),
        "Program " + (IsNewProgram ? "Created" : "Updated") + ".", AjaxSubmitHelper.PageMessageType.SuccessToast);
    }

    bool UpdateDb_AddNew(AjaxSubmitHelper ajax, FormValues formValues, DbHelper.AblePrograms.AbleProgramInfo programInfo) {

      try {
        DbHelper.AblePrograms.CreateProgram(null, programInfo);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("ProgramSettings_CreateProgram")
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .WithPageUrl(Request.RawUrl)
          .WithProperty("ProgramJobNumber", programInfo?.ProgramJobNumber)
          .WithProperty("ProgramJobName", programInfo?.ProgramJobName)
          .WithProperty("CompanyId", programInfo?.CompanyId)
          .WithProperty("ProgramStatusId", programInfo?.ProgramStatusId)
          .Track();

        ajax.AddDialogMessage("Error adding new Program: " + ex.Message);
        return false;
      }

      // Send Intercom event for new program creation with initial status
      var newStatus = DbHelper.AlbertProgramStatus.GetProgramStatusByIdOrNull(programInfo.ProgramStatusId);
      if (newStatus != null) {
        var companyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(programInfo.CompanyId.Value, userInfo);
        SendEvent(
          intercom => intercom.ProgramStatusChanged()
            .FromSession()
            .WithProgram(programInfo.ProgramJobId, programInfo.ProgramJobName)
            .WithCompany(programInfo.CompanyId, companyInfo?.CompanyName)
            .WithOldStatus("new")
            .WithNewStatus(newStatus.DisplayTitle)
            .WithParticipantCount(0),
          operationName: "ProgramSettings_ProgramCreated",
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["ProgramJobId"] = programInfo.ProgramJobId,
            ["ProgramJobName"] = programInfo.ProgramJobName,
            ["CompanyId"] = programInfo.CompanyId,
            ["NewStatus"] = newStatus.DisplayTitle
          }
        );
      }

      return true;
    }

    bool UpdateDb_Existing(AjaxSubmitHelper ajax, FormValues formValues, DbHelper.AblePrograms.AbleProgramInfo programInfo) {

      // Capture old status before update for Intercom event
      var oldProgramInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(programInfo.ProgramJobId);
      var oldStatus = oldProgramInfo != null ? DbHelper.AlbertProgramStatus.GetProgramStatusByIdOrNull(oldProgramInfo.ProgramStatusId) : null;
      var newStatus = DbHelper.AlbertProgramStatus.GetProgramStatusByIdOrNull(programInfo.ProgramStatusId);
      bool statusChanged = oldStatus != null && newStatus != null && oldStatus.ProgramStatusId != newStatus.ProgramStatusId;

      try {
        DbHelper.AblePrograms.UpdateProgram(programInfo);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("ProgramSettings_UpdateProgram")
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .WithPageUrl(Request.RawUrl)
          .WithProperty("ProgramJobId", programInfo?.ProgramJobId)
          .WithProperty("ProgramJobNumber", programInfo?.ProgramJobNumber)
          .WithProperty("ProgramJobName", programInfo?.ProgramJobName)
          .WithProperty("ProgramStatusId", programInfo?.ProgramStatusId)
          .Track();

        ajax.AddDialogMessage("Error updating Program: " + ex.Message);
        return false;
      }

      try {
        DbHelper.AblePrograms.MaintainCompanyIds();
      } catch { }

      // Send Intercom event for program status change
      if (statusChanged) {
        var companyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(programInfo.CompanyId.Value, userInfo);
        SendEvent(
          intercom => intercom.ProgramStatusChanged()
            .FromSession()
            .WithProgram(programInfo.ProgramJobId, programInfo.ProgramJobName)
            .WithCompany(programInfo.CompanyId, companyInfo?.CompanyName)
            .WithOldStatus(oldStatus.DisplayTitle)
            .WithNewStatus(newStatus.DisplayTitle)
            .WithParticipantCount(programInfo.ParticipantCount),
          operationName: "ProgramSettings_ProgramStatusChanged",
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["ProgramJobId"] = programInfo.ProgramJobId,
            ["ProgramJobName"] = programInfo.ProgramJobName,
            ["CompanyId"] = programInfo.CompanyId,
            ["OldStatus"] = oldStatus.DisplayTitle,
            ["NewStatus"] = newStatus.DisplayTitle,
            ["ParticipantCount"] = programInfo.ParticipantCount
          }
        );
      }

      return true;
    }

    void CopyFormValues(FormValues formValues, DbHelper.AblePrograms.AbleProgramInfo programInfo) {

      programInfo.ProgramJobNumber = formValues.ProgramJobNumber;
      programInfo.ProgramJobName = formValues.ProgramJobName;

      programInfo.ProgramStatusId = formValues.ProgramStatusId ?? DbHelper.AlbertProgramStatus.Ids.Active;

      programInfo.ProjectCoordinatorUserId = formValues.ProjectCoordinatorUserId;
      programInfo.LeadConsultantUserId = formValues.LeadConsultantUserId;

      programInfo.SalesPartnerUserId = formValues.SalesPartnerUserId;

      if (CanEditPercentages) {
        programInfo.Partner_DeliveryPercentage = formValues.Partner_DeliveryPercentage;
        programInfo.Partner_SalesDeliveryPercentage = formValues.Partner_SalesDeliveryPercentage;
        programInfo.Partner_PLCPercentage = formValues.Partner_PLCPercentage;
      }

      programInfo.ProgramNotes = formValues.ProgramNotes;
      programInfo.BookingPageInstructions = formValues.BookingPageInstructions;
    }

    void DeleteProgram(AjaxSubmitHelper ajax, DbHelper.AblePrograms.AbleProgramInfo programInfo) {

      // Form validation: The submitted GUID must also match the requested Program's GUID.
      var programJobGUID = ajax.CheckGuid(FormFields.ProgramJobGUID, "Workshop GUID", true, "Invalid Program GUID");
      if (programInfo == null || ajax.BadFieldCount > 0 || programJobGUID != programInfo.ProgramJobGUID) {
        ajax.AddDialogMessage("Error validating Program to delete.");
        return;
      }

      var workshops = DbHelper.WorkshopEvents.GetWorkshopsInProgram(programInfo.ProgramJobId);
      if (workshops.Count > 0) {
        ajax.AddDialogMessage("Can't delete Program with Workshops.<br/>Delete Workshops first.");
        return;
      }

      var coachees = DbHelper.AlbertCoachees.GetCoacheesInProgram(programInfo.ProgramJobId);
      if (coachees.Count > 0) {
        ajax.AddDialogMessage("Can't delete Program with Coachees.<br/>Delete or Reassign Coachees first.");
        return;
      }

      var components = DbHelper.ProgramComponents.GetForProgram(programInfo.ProgramJobId);
      if (components.Count > 0) {
        ajax.AddDialogMessage("Can't delete Program - components for Quotes exist.");
        return;
      }

      // FIX: Don't use exceptions for program flow. First check if it can be deleted.
      try {
        DbHelper.AblePrograms.DeleteProgram(programInfo.ProgramJobId);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("ProgramSettings_DeleteProgram")
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .WithPageUrl(Request.RawUrl)
          .WithProperty("ProgramJobId", programInfo?.ProgramJobId)
          .WithProperty("ProgramJobNumber", programInfo?.ProgramJobNumber)
          .Track();

        ajax.AddDialogMessage("Error deleting Program: " + ex.Message);
        return;
      }

      ajax.SetRedirectUrl(PathHelper.Pages.ProjectPrograms(programInfo.ProgramJobNumber), "Program Deleted.");
    }

  }
}
