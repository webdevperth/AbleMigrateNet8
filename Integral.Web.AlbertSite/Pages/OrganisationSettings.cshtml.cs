using System;
using System.Collections.Generic;
using System.Text;
using Integral.Web;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class OrganisationSettings : AppCode.PageBaseClasses.CompanyInfoBase {

    public List<DbHelper.AlbertCoaches.AlbertCoachInfo> PartnerList;

    public bool CanUpdateClientLead, CanEditAIContext, CanUpdateDisplayLogoInNavBar;
    public WebHelper.Form.ImageWithUpload CompanyLogoControl;

    public class FormFields {
      public const string CompanyName = "CompanyName";
      public const string WebSiteUrl = "WebSiteUrl";
      public const string City = "City";
      public const string CountryId = "CountryId";
      public const string NumberOfStaff = "NumberOfStaff";
      public const string SectorId = "SectorId";
      public const string ClientLeadUserId = "ClientLeadUserId";
      public const string AI_Context = "AI_Context";
      public const string DisplayLogoInNavBar = "DisplayLogoInNavBar";
    }

    public class AjaxAction {
      public const string Update = "update";
      public const string CompanyLogo = "companylogo";
    }

    public class FormValues {
      public string CompanyName;
      public string WebSiteUrl;
      public string City;
      public int? CountryId;
      public int? NumberOfStaff;
      public int? SectorId;
      public int? ClientLeadUserId;
      public string AI_Context;
      public bool DisplayLogoInNavBar;
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = IsNewCompany ? "New Organisation" : "Organisation Settings";

      PartnerList = DbHelper.AlbertCoaches.GetCoachInfoList(true, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);

      CanUpdateClientLead = IsNewCompany || SessionHelper.AppAccess.Companies.CanUpdateClientLead(CompanyInfo);
      CanEditAIContext = IsNewCompany || SessionHelper.AppAccess.Companies.CanEditAIContext(CompanyInfo);
      CanUpdateDisplayLogoInNavBar = IsNewCompany || SessionHelper.AppAccess.Companies.CanUpdateDisplayLogoInNavBar(CompanyInfo);

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          if (PageAjaxAction == AjaxAction.Update) {

            if (!CanUpdateCompany) {
              ajax.RespondNoAccessToFunction();
              return;
            }

            if (AssignFormValues(ajax, out var formValues)) {
              UpdateCompany(ajax, formValues);
            }

          } else if (PageAjaxAction == AjaxAction.CompanyLogo) {

            if (!CanUpdateCompany) {
              ajax.RespondNoAccessToFunction();
              return;
            } else {
              SaveUploadedCompanyLogo();
              return;
            }
          }
        });
      }

      CompanyLogoControl = new WebHelper.Form.ImageWithUpload(
        PathHelper.Images.TenantOrgLogo(CompanyInfo, true) + $"?t={DateTime.Now.Ticks}",
        WebHelper.Form.ImageType.CompanyLogo,
        AjaxAction.CompanyLogo,
        CanUpdateCompany);

      if (SystemWeb.IsHttpPost) return new EmptyResult();

      return Page();
    }

    public string GetClientLeadDropdownOptionsHtml() {

      var selectedClientLeadUserId = IsNewCompany ? userInfo.UserId : CompanyInfo.ClientLeadUserId;

      return WebHelper.GetPartnerDropdownOptionsHtml(new WebHelper.PartnerDropdownInfo() {
        PartnerInfoList = PartnerList,
        FormName = FormFields.ClientLeadUserId,
        IsReadOnly = !CanUpdateClientLead,
        LabelText = "Client Lead:",
        InputCols = 8,
        SelectedPartnerUserId = selectedClientLeadUserId,
        CanViewHiddenPartners = SessionHelper.AppAccess.Coaches.CanViewHiddenPartners(),
        CanViewInactivePartners = SessionHelper.AppAccess.Coaches.CanViewInactivePartners(),
        IncludeUnassignedUser = true
      });

    }

    bool AssignFormValues(AjaxSubmitHelper ajax, out FormValues formValues) {

      formValues = new FormValues();

      formValues.CompanyName = ajax.CheckFieldRegex(FormFields.CompanyName, "Company Name", AppHelper.Regex.GeneralText, true, "Please enter a Company Name.");
      formValues.WebSiteUrl = ajax.CheckFieldRegex(FormFields.WebSiteUrl, "Web Site", AppHelper.Regex.GeneralText, false, "Please enter a Web Site URL.");
      formValues.City = ajax.CheckFieldRegex(FormFields.City, "City", AppHelper.Regex.GeneralText, false, "Please enter a valid City.");
      formValues.CountryId = ajax.CheckFieldIntOrNull(FormFields.CountryId, "Country", null, null, false, "Please enter a valid Country.");
      formValues.NumberOfStaff = ajax.CheckFieldIntOrNull(FormFields.NumberOfStaff, "Number of Staff", null, null, false, "Please enter a valid Number of Staff.");
      formValues.SectorId = ajax.CheckFieldIntOrNull(FormFields.SectorId, "Sector", null, null, false, "Please enter a valid Sector.");

      if (CanUpdateDisplayLogoInNavBar) {
        formValues.DisplayLogoInNavBar = ajax.CheckFieldBool(FormFields.DisplayLogoInNavBar, WebHelper.YesNoButton_ValueYes);
      } else {
        formValues.DisplayLogoInNavBar = CompanyInfo.DisplayLogoInNavBar;
      }

      if (CanUpdateClientLead) {
        int? clientLeadUserId = ajax.CheckFieldIntOrNull(FormFields.ClientLeadUserId, "Client Lead", null, null, false, "Please enter a valid Client Lead.");

        if (clientLeadUserId != null && !PartnerList.Exists(x => x.UserId == clientLeadUserId)) {

          ajax.AddBadField(FormFields.ClientLeadUserId, "Please select a valid Client Lead.");
        }

        formValues.ClientLeadUserId = clientLeadUserId;

      } else {
        formValues.ClientLeadUserId = CompanyInfo.ClientLeadUserId;
      }

      if (CanEditAIContext) {
        formValues.AI_Context = ajax.CheckFieldRegex(FormFields.AI_Context, "AI Context", AppHelper.Regex.HTML, false, "Please remove unusual characterss.");
      } else {
        formValues.AI_Context = CompanyInfo.AI_Context;
      }

      return !ajax.HasErrors;
    }

    void UpdateCompany(AjaxSubmitHelper ajax, FormValues formValues) {

      if (formValues == null) return;

      var companyInfo = new DbHelper.ClientCompanies.AlbertCompanyInfo(
        companyId: this.CompanyInfo.CompanyId,
        companyGUID: this.CompanyInfo.CompanyGUID,
        orgId: this.CompanyInfo.OrgId,
        companyName: formValues.CompanyName,
        webSiteUrl: formValues.WebSiteUrl,
        city: formValues.City,
        countryId: formValues.CountryId,
        numberOfStaff: formValues.NumberOfStaff,
        sectorId: formValues.SectorId,
        clientLeadUserId: formValues.ClientLeadUserId,
        aI_Context: formValues.AI_Context,
        displayLogoInNavBar: formValues.DisplayLogoInNavBar,
        createdByUserId: userInfo.UserId);

      if (IsNewCompany) {

        bool companyNameExists = DbHelper.ClientCompanies.CompanyNameExists(companyInfo.CompanyName);
        if (companyNameExists) {
          ajax.AddErrorToast("The organisation name already exists");
          return;
        }

        // Add new company.
        try {
          DbHelper.ClientCompanies.CreateCompany(null, userInfo.OrgId, companyInfo);
          ajax.SetRedirectUrl(PathHelper.Pages.OrganisationSettings(companyInfo.CompanyId), "Organisation successfully created", AjaxSubmitHelper.PageMessageType.SuccessToast);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("UpdateCompany_CreateCompany")
            .FromSession()
            .WithProperty(ApplicationInsightsConstants.CompanyName, companyInfo.CompanyName)
            .WithProperty(ApplicationInsightsConstants.OrgId, companyInfo.OrgId)
            .Track();
          ajax.AddDialogMessage("Error creating new company.");
          return;
        }
      } else {
        try {
          DbHelper.ClientCompanies.UpdateCompany(null, companyInfo);
          ajax.AddSuccessToast("Organisation settings have been updated.");
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("UpdateCompany_UpdateCompany")
            .FromSession()
            .WithProperty(ApplicationInsightsConstants.CompanyId, companyInfo.CompanyId)
            .WithProperty(ApplicationInsightsConstants.CompanyName, companyInfo.CompanyName)
            .Track();
          ajax.AddDialogMessage("Error updating company.");
          return;
        }
      }
    }

    void SaveUploadedCompanyLogo() {
      var uploadedFile = SystemWeb.GetRequestFile("image");
      if (uploadedFile == null) return;
      // Note ClientCompany (aka SurveyCompany) logos should really be in a different folder to TenantOrg logos.
      using (var fileStream = System.IO.File.Create(PathHelper.Images.TenantOrgLogoServerPath(CompanyInfo.CompanyGUID))) {
        using (var inputStream = uploadedFile.OpenReadStream()) {
          inputStream.CopyTo(fileStream);
        }
      }
    }

    public string GetCountryOptionsHtml() {

      var html = new StringBuilder();
      html.AppendLine($@"<option value="""">[Select Country]</option>");

      DbHelper.Common.Query(@"
        SELECT CountryId, CountryName
        FROM id_Country
        ORDER BY CountryName",
        dr => {
          html.AppendLine($@"<option {(CompanyInfo?.CountryId == dr.GetInt("CountryId") ? "selected" : "")} value=""{dr.GetInt("CountryId")}"">{dr.GetString("CountryName").HTMLEncode()}</option>");
        }
      );

      return html.ToString();
    }

    public string GetSectorOptionsHtml() {

      var html = new StringBuilder();
      html.AppendLine($@"<option value="""">[Select Sector]</option>");

      DbHelper.Common.Query(@"
        SELECT SectorId, SectorName
        FROM sv_Sector
        ORDER BY SectorName",
        dr => {
          html.AppendLine($@"<option {(CompanyInfo?.SectorId == dr.GetInt("SectorId") ? "selected" : "")} value=""{dr.GetInt("SectorId")}"">{dr.GetString("SectorName").HTMLEncode()}</option>");
        }
      );

      return html.ToString();
    }
  }
}
