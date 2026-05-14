using Integral.Web.Services;
using System;
using System.Collections.Generic;
using static Integral.Web.PortalSite.AppCode.IntercomHelpers;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class ProjectSettings : AppCode.PageBaseClasses.ProjectPageBase {

    public bool disableDelete = false;
    public const string disableDeleteMsg = "Can't delete Project if Coachees or Workshops exist. Please delete those first.";
    public bool isNewCompany = false, isNoCompany = false, isReadOnly = false;
    public bool CanUpdateDefaultCostItemMarkupPercent, CanAllowCostItemPriceOverwrite, CanChangeTenantOrg, CanChangeProjectCompany, CanEditXeroAccountCode, CanEditInvoiceTypeId;
    public List<DbHelper.ClientCompanies.BriefCompanyInfo> CompanyList;

    public class AjaxAction {
      public const string Update = "update";
      public const string Delete = "delete";
    }

    // Note that "PurchaseOrderNumber" in the UI is actually the "InvoiceNumber" column in the project table.
    public class FormFields {
      public const string TenantOrgId = "TenantOrgId";
      public const string InvoiceNumber = "InvoiceNumber";
      public const string CompanyId = "CompanyId";
      public const string NewCompanyName = "NewCompanyName";
      public const string JobNumber = "JobNumber";
      public const string ProjectName = "ProjectName";
      public const string XeroAccountCode = "XeroAccountCode";
      public const string XeroContactId = "XeroContactId";
      public const string InvoicingNotes = "InvoicingNotes";
      public const string InvoiceType = "InvoiceType";
      public const string ProjectIntent = "ProjectIntent";
      public const string ProgramContext = "ProgramContext";
      public const string PurchaseOrderRequired = "PurchaseOrderRequired";
      public const string DefaultCostItemMarkupPercent = "DefaultCostItemMarkupPercent";
      public const string AllowCostItemUnitPriceManualOverwrite = "AllowCostItemUnitPriceManualOverwrite";
      public const string CanSelfSelectCoach = "CanSelfSelectCoach";
    }

    class FormValues {
      public int TenantOrgId;
      public int CompanyId;
      public string NewCompanyName;
      public string InvoiceNumber;
      public string JobNumber;
      public string ProjectName;
      public string XeroAccountCode;
      public int? XeroContactId;
      public string InvoicingNotes;
      public int? InvoiceInstructionTypeId;
      public string ProjectIntent;
      public string ProgramContext;
      public bool PurchaseOrderRequired;
      public decimal? DefaultCostItemMarkupPercent;
      public bool AllowCostItemUnitPriceManualOverwrite;
      public bool CanSelfSelectCoach;
    }

    protected void Page_Load(object sender, EventArgs e) {

      this.PageTitle = "Project Settings";
      this.PageSubtitle = "";

      isReadOnly = !(SessionHelper.AppAccess.Projects.CanEditProject(ProjectInfo) || IsNewProject);
      CanUpdateDefaultCostItemMarkupPercent = SessionHelper.AppAccess.Projects.CanUpdateDefaultCostItemMarkupPercent(ProjectInfo);
      CanAllowCostItemPriceOverwrite = SessionHelper.AppAccess.Projects.CanAllowCostItemPriceOverwrite(ProjectInfo);
      CanChangeTenantOrg = SessionHelper.AppAccess.Projects.CanChangeTenantOrg();
      CanChangeProjectCompany = SessionHelper.AppAccess.Projects.CanChangeProjectCompany(ProjectInfo);
      CanEditXeroAccountCode = SessionHelper.AppAccess.Projects.CanEditXeroAccountCode(ProjectInfo);
      CanEditInvoiceTypeId = SessionHelper.AppAccess.Projects.CanEditInvoiceTypeId(ProjectInfo, IsNewProject);

      CompanyList = DbHelper.ClientCompanies.GetCompanyList(SessionHelper.GetUserInfoOrNull());

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          if (isReadOnly) {

            return;

          } else if (!IsNewProject && ProjectInfo == null) {

            ajax.AddDialogMessage("Error: Project not found.");

          } else if (ajax.Action == AjaxAction.Update) {

            UpdateProject(ajax, ProjectInfo);

          } else if (ajax.Action == AjaxAction.Delete) {

            ajax.AddDialogMessage("Delete not available yet.");
          }
        });
        return;
      }

      if (IsNewProject) {
        PageTitle = "New Project";
        PageSubtitle = "";
      } else {
        PageTitle = "Update Project";
        PageSubtitle = ProjectInfo.JobNumber + ": " + ProjectInfo.ProjectName;
      }
    }

    public string GetCompanyOptions() {
      string html = "";
      foreach (var cmp in CompanyList) {
        html += "<option";
        if (cmp.CompanyId == ProjectInfo.CompanyId) html += " selected";
        html += " value=\"" + cmp.CompanyId + "\">" + cmp.CompanyName.HTMLEncode() + "</option>";
      }
      return html;
    }

    public string GetTenantOrgOptions() {
      var orgList = DbHelper.TenantOrg.GetAllTenantOrgs();
      string html = "<option value=\"\">[select organisation]</option>";
      foreach (var org in orgList) {
        html += "<option";
        if (org.OrgId == ProjectInfo.TenantOrgId) html += " selected";
        html += " value=\"" + org.OrgId + "\">" + org.OrgName.HTMLEncode() + "</option>";
      }
      return html;
    }

    public string GetXeroContactOptions() {

      string html = "<option value=\"\">[select contact]</option>";

      DbHelper.Common.Query(@"
        SELECT xc.XeroContactId, xc.ContactName
        FROM al_XeroContacts xc
        ORDER BY xc.ContactName",
        dr => {
          html += "<option ";
          if (dr.GetInt("XeroContactId") == ProjectInfo.XeroContactId) html += "selected ";
          html += "value=\"" + dr.GetInt("XeroContactId") + "\">" + dr.GetString("ContactName").HTMLEncode() + "</option>";
        }
      );
      return html;
    }

    public string GetXeroAccountOptions() {

      string html = "<option value=\"\">[select account]</option>";

      DbHelper.Common.Query(@"
        SELECT xa.XeroAccountCode, xa.AccountName
        FROM al_XeroAccounts xa
        ORDER BY xa.AccountName",
        dr => {
          var accountCode = dr.GetString("XeroAccountCode");
          html += "<option ";
          if (accountCode == ProjectInfo.XeroAccountCode) html += "selected ";
          html += "value=\"" + accountCode + "\">" + dr.GetString("AccountName").HTMLEncode() + "</option>";
        }
      );
      return html;
    }

    public string GetInvoiceTypeOptions() {

      string html = "<option value=\"\">[select option]</option>";

      DbHelper.Common.Query(@"
        SELECT it.InvoiceInstructionTypeId, it.Title
        FROM al_InvoiceInstructionType it
        ORDER BY it.Title",
        dr => {
          var invoiceTypeId = dr.GetIntOrNull("InvoiceInstructionTypeId");
          html += "<option ";
          if (invoiceTypeId == ProjectInfo.InvoiceInstructionTypeId) html += "selected ";
          html += "value=\"" + invoiceTypeId + "\">" + dr.GetString("Title").HTMLEncode() + "</option>";
        }
      );
      return html;
    }

    void UpdateProject(AjaxSubmitHelper ajax, DbHelper.Projects.ProjectInfo projectInfo) {

      // Form validation.
      var formValues = new FormValues();

      formValues.ProjectName = ajax.CheckFieldRegex(FormFields.ProjectName, "Project Title", AppHelper.Regex.GeneralText, true, "Use only text characters in Project Title.");
      formValues.ProjectIntent = ajax.CheckFieldRegex(FormFields.ProjectIntent, "Project Intent", AppHelper.Regex.HTML, false, "Invalid Characters in Project Intent.");
      formValues.ProgramContext = ajax.CheckFieldRegex(FormFields.ProgramContext, "Program Context", AppHelper.Regex.HTML, false, "Invalid Characters in Program Context.");
      formValues.CanSelfSelectCoach = ajax.CheckFieldBool(FormFields.CanSelfSelectCoach, "1");

      if (CanEditInvoiceTypeId) {
        formValues.InvoiceInstructionTypeId = ajax.CheckFieldIDOrNull(FormFields.InvoiceType, "Invoice Type", true, "Select an Invoice Type.");

        // If InvoiceTypeId is 'No transaction', reset all the values regarding invoicing settings.
        if (formValues.InvoiceInstructionTypeId == ConfigHelper.InvoiceInstructionTypeId_NoTransaction) {

          formValues.InvoiceNumber = "";
          formValues.InvoicingNotes = "";
          formValues.XeroContactId = null;
          formValues.PurchaseOrderRequired = false;
          formValues.DefaultCostItemMarkupPercent = ConfigHelper.DefaultCostItemMarkupPercent;
          formValues.AllowCostItemUnitPriceManualOverwrite = false;

        } else {

          formValues.InvoiceNumber = ajax.CheckFieldRegex(FormFields.InvoiceNumber, "Purchase Order Number", AppHelper.Regex.GeneralText, false, "Use only text characters in Purchase Order Number.");
          formValues.InvoicingNotes = ajax.CheckFieldRegex(FormFields.InvoicingNotes, "Invoicing Notes", AppHelper.Regex.HTML, false, "Invalid Characters in Invoicing Notes.");
          formValues.XeroContactId = ajax.CheckFieldIDOrNull(FormFields.XeroContactId, "Client", false, "Select a Client.");
          formValues.PurchaseOrderRequired = ajax.CheckFieldBool(FormFields.PurchaseOrderRequired, "1");

          if (CanUpdateDefaultCostItemMarkupPercent) {
            formValues.DefaultCostItemMarkupPercent = ajax.CheckFieldPercent(FormFields.DefaultCostItemMarkupPercent, "Cost Item Markup Percent ", false, true, "Please enter a valid percentage.", 0, 100) ?? 0;
          } else {
            formValues.DefaultCostItemMarkupPercent = ProjectInfo.DefaultCostItemMarkupPercent;
          }

          if (CanAllowCostItemPriceOverwrite) {
            formValues.AllowCostItemUnitPriceManualOverwrite = ajax.CheckFieldBool(FormFields.AllowCostItemUnitPriceManualOverwrite, "1");
          } else {
            formValues.AllowCostItemUnitPriceManualOverwrite = ProjectInfo.AllowCostItemUnitPriceManualOverwrite;
          }

          if (CanEditXeroAccountCode) {
            formValues.XeroAccountCode = ajax.CheckFieldRegex(FormFields.XeroAccountCode, "Xero Account Code", AppHelper.Regex.GeneralText, true, "Use only text characters in Xero Account Code.");
          } else {
            formValues.XeroAccountCode = projectInfo.XeroAccountCode;
          }
        }

      } else {

        if (IsNewProject) {
          // When creating a new project, make InvoiceTypeId 'No transaction' by default. The other settings will be null/empty
          formValues.InvoiceInstructionTypeId = ConfigHelper.InvoiceInstructionTypeId_NoTransaction;
          formValues.DefaultCostItemMarkupPercent = ConfigHelper.DefaultCostItemMarkupPercent;

        } else {
          // If CanEditInvoiceTypeId is false and is not creating a new project, use the project info.
          formValues.InvoiceInstructionTypeId = projectInfo.InvoiceInstructionTypeId;
          formValues.InvoiceNumber = projectInfo.PurchaseOrderNumber;
          formValues.InvoicingNotes = projectInfo.InvoicingNotes;
          formValues.XeroContactId = projectInfo.XeroContactId;
          formValues.PurchaseOrderRequired = projectInfo.PurchaseOrderRequired;
          formValues.DefaultCostItemMarkupPercent = ProjectInfo.DefaultCostItemMarkupPercent;
          formValues.AllowCostItemUnitPriceManualOverwrite = ProjectInfo.AllowCostItemUnitPriceManualOverwrite;
          formValues.XeroAccountCode = projectInfo.XeroAccountCode;
        }

      }

      if (CanChangeTenantOrg) {
        formValues.TenantOrgId = ajax.CheckFieldID(FormFields.TenantOrgId, "Organisation", true, "Select an Organisation");
        var checkTenantOrgInfo = DbHelper.TenantOrg.GetTenantOrgById(formValues.TenantOrgId);
        if (checkTenantOrgInfo == null) ajax.AddBadField(FormFields.TenantOrgId, "Select an Organisation");
      } else if (!IsNewProject) {
        formValues.TenantOrgId = projectInfo.TenantOrgId;
      } else {
        formValues.TenantOrgId = userInfo.OrgId;
      }

      if (CanChangeProjectCompany) {

        if (WebHelper.GetFormValue(FormFields.CompanyId).IsNullOrEmpty()) ajax.AddBadField(FormFields.CompanyId, "Select or add a Company");

        if (WebHelper.GetFormValue(FormFields.CompanyId) == PathHelper.AbleUrlValues.IdNew) {
          isNewCompany = true;
          formValues.NewCompanyName = ajax.CheckFieldRegex(FormFields.NewCompanyName, "Company Name", AppHelper.Regex.GeneralText, true, "Please Provide a Company Name").TrimWhitespace();
        } else {
          isNewCompany = false;
          formValues.CompanyId = ajax.CheckFieldID(FormFields.CompanyId, "Company", true, "Please select a Company.");
        }

      } else {
        if (IsNewProject) {
          formValues.CompanyId = userInfo.ClientCompanyId.Value;
        } else {
          formValues.CompanyId = projectInfo.CompanyId.Value;
        }
      }


      if (ajax.BadFieldCount > 0) return;

      if (isNewCompany) {
        if (!CreateNewCompany(ajax, formValues.NewCompanyName, out formValues.CompanyId)) return;
      } else {
        var companyInfo = CompanyList.Find(x => x.CompanyId == formValues.CompanyId); // Check company id exists.
        if (companyInfo == null) {
          ajax.AddDialogMessage("Error: Company not found.");
          return;
        }
      }

      string redirectToJobNumber = null;

      if (IsNewProject) {

        int newProjectId = 0;
        try {
          DbHelper.Common.UsingTransaction(trans => {
            DbHelper.Projects.CreateProjectAndProgram(
              trans: trans,
              tenantOrgId: formValues.TenantOrgId,
              companyId: formValues.CompanyId,
              projectName: formValues.ProjectName,
              preferredProgramName: null,
              projectIntent: formValues.ProjectIntent,
              programContext: formValues.ProgramContext,
              invoiceInstructionTypeId: formValues.InvoiceInstructionTypeId,
              invoiceNumber: formValues.InvoiceNumber,
              purchaseOrderRequired: formValues.PurchaseOrderRequired,
              invoicingNotes: formValues.InvoicingNotes,
              xeroContactId: formValues.XeroContactId,
              xeroAccountCode: formValues.XeroAccountCode,
              defaultCostItemMarkupPercent: formValues.DefaultCostItemMarkupPercent,
              allowCostItemUnitPriceManualOverwrite: formValues.AllowCostItemUnitPriceManualOverwrite,
              canSelfSelectCoach: formValues.CanSelfSelectCoach,
              createdByUserId: userInfo.UserId,
              newJobNumber: out redirectToJobNumber,
              newProgramJobId: out newProjectId
            );
            return true;
          });
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("ProjectSettings_CreateProject")
            .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
            .WithPageUrl(Request.RawUrl)
            .WithProperty("ProjectName", formValues.ProjectName)
            .WithProperty("CompanyId", formValues.CompanyId)
            .WithProperty("TenantOrgId", formValues.TenantOrgId)
            .WithProperty("InvoiceInstructionTypeId", formValues.InvoiceInstructionTypeId)
            .Track();

          ajax.AddDialogMessage("Failed to add new Project", ex);
          return;
        }

        // Send Intercom event for project creation
        var companyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(formValues.CompanyId, userInfo);
        SendEvent(
          intercom => intercom.ProjectCreated()
            .FromSession()
            .WithProject(newProjectId, formValues.ProjectName)
            .WithProjectJobNumber(redirectToJobNumber)
            .WithCompany(formValues.CompanyId, companyInfo?.CompanyName),
          operationName: "ProjectSettings_ProjectCreated",
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["ProjectId"] = newProjectId,
            ["ProjectName"] = formValues.ProjectName,
            ["JobNumber"] = redirectToJobNumber,
            ["CompanyId"] = formValues.CompanyId
          }
        );

      } else {

        redirectToJobNumber = ProjectInfo.JobNumber;

        try {
          DbHelper.Projects.UpdateProject(
            trans: null,
            jobNumberToUpdate: ProjectInfo.JobNumber,
            companyId: formValues.CompanyId,
            projectName: formValues.ProjectName,
            tenantOrgId: CanChangeTenantOrg ? formValues.TenantOrgId : ProjectInfo.TenantOrgId,
            projectIntent: formValues.ProjectIntent,
            programContext: formValues.ProgramContext,
            invoiceInstructionTypeId: formValues.InvoiceInstructionTypeId,
            invoiceNumber: formValues.InvoiceNumber,
            purchaseOrderRequired: formValues.PurchaseOrderRequired,
            invoicingNotes: formValues.InvoicingNotes,
            xeroContactId: formValues.XeroContactId,
            xeroAccountCode: formValues.XeroAccountCode,
            defaultCostItemMarkupPercent: formValues.DefaultCostItemMarkupPercent,
            allowCostItemUnitPriceManualOverwrite: formValues.AllowCostItemUnitPriceManualOverwrite,
            canSelfSelectCoach: formValues.CanSelfSelectCoach
          );
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("ProjectSettings_UpdateProject")
            .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
            .WithPageUrl(Request.RawUrl)
            .WithProperty("JobNumber", ProjectInfo?.JobNumber)
            .WithProperty("ProjectName", formValues.ProjectName)
            .WithProperty("CompanyId", formValues.CompanyId)
            .Track();

          ajax.AddDialogMessage("Failed to update Project", ex);
          return;
        }
      }

      if (!ajax.HasErrors) {
        ajax.AddReturnValue("JobNumber", redirectToJobNumber); // If created, this is the newly-assigned job number.
      }

      ajax.SetRedirectUrl(PathHelper.Pages.ProjectSettings(redirectToJobNumber),
        IsNewProject ? "Project Created." : "Project Updated.",
        AjaxSubmitHelper.PageMessageType.SuccessToast);
    }

    bool CreateNewCompany(AjaxSubmitHelper ajax, string companyName, out int newCompanyId) {

      try {
        var companyInfo = DbHelper.ClientCompanies.CreateCompany(null, userInfo.OrgId, companyName);
        newCompanyId = companyInfo.CompanyId;
        return true;
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("ProjectSettings_CreateCompany")
          .AddExternalUserId(ExternalUserKind.CurrentUser, SessionHelper.AsExternalUserId())
          .WithPageUrl(Request.RawUrl)
          .WithProperty("CompanyName", companyName)
          .WithProperty("IsDuplicateKey", DbHelper.IsDuplicateKeyError(ex))
          .Track();

        if (DbHelper.IsDuplicateKeyError(ex)) {
          ajax.AddDialogMessage("That company name already exists.");
        } else {
          ajax.AddDialogMessage("Error creating company: " + ex.Message);
        }
      }
      newCompanyId = 0;
      return false;
    }

    public decimal? GetMarkupCostPercentage() {
      return IsNewProject || ProjectInfo.DefaultCostItemMarkupPercent == null ? ConfigHelper.DefaultCostItemMarkupPercent : ProjectInfo.DefaultCostItemMarkupPercent;
    }

  }
}
