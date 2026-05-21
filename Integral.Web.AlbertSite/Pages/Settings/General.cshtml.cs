using System;
using System.Text;
using Integral.Web;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert.Settings {

  public class General : AppCode.PageBaseClasses.SettingsPageBase {

    public class AjaxAction {
      public const string TenantOrgLogo = "TenantOrgLogo";
      public const string UpdateCompany = "updatecompany";
    }

    public class FormFields {
      public const string OrgName = "OrgName";
      public const string OrgFriendlyName = "OrgFriendlyName";
      public const string BusinessIdNumber = "BusinessIdNumber";
      public const string ContactPhoneNumber = "ContactPhoneNumber";
      public const string GeneralEmail = "GeneralEmail";
      public const string WebSiteURL = "WebSiteURL";
      public const string GenericSenderEmailName = "GenericSenderEmailName";
      public const string GenericSenderEmailAddress = "GenericSenderEmailAddress";
    }

    public class FormValues {
      public string OrgName;
      public string OrgFriendlyName;
      public string BusinessIdNumber;
      public string ContactPhoneNumber;
      public string GeneralEmail;
      public string WebSiteURL;
      public string GenericSenderEmailName;
      public string GenericSenderEmailAddress;
    }

    public WebHelper.Form.ImageWithUpload CompanyLogoControl;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Settings";

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          if (PageAjaxAction == AjaxAction.UpdateCompany) {

            UpdateCompany(ajax);

          } else if (PageAjaxAction == AjaxAction.TenantOrgLogo) {

            SaveUploadedCompanyLogo();
          }
        });
        return new EmptyResult();
      }

      CompanyLogoControl = new WebHelper.Form.ImageWithUpload(
        PathHelper.Images.TenantOrgLogo(TenantOrgInfo, true) + $"?t={DateTime.Now.Ticks}",
        WebHelper.Form.ImageType.CompanyLogo,
        AjaxAction.TenantOrgLogo,
        true);

      return Page();
    }

    public string GetCountryOptionsHtml() {

      var html = new StringBuilder();
      html.AppendLine($@"<option value="""">[Select Country]</option>");

      DbHelper.Common.Query(@"
        SELECT CountryId, CountryName
        FROM id_Country
        ORDER BY CountryName",
        dr => {
          html.AppendLine($@"<option value=""{dr.GetInt("CountryId")}"">{dr.GetString("CountryName").HTMLEncode()}</option>");
        }
      );

      return html.ToString();
    }

    void SaveUploadedCompanyLogo() {

      var uploadedFile = SystemWeb.GetRequestFile("image");
      if (uploadedFile == null) return;

      using (var fileStream = System.IO.File.Create(PathHelper.Images.TenantOrgLogoServerPath(TenantOrgInfo.OrgGuid))) {
        using (var inputStream = uploadedFile.OpenReadStream()) {
          inputStream.CopyTo(fileStream);
        }
      }
    }

    private void UpdateCompany(AjaxSubmitHelper ajax) {

      var formValues = new FormValues();

      formValues.OrgName = ajax.CheckFieldRegex(FormFields.OrgName, "Company Name", AppHelper.Regex.GeneralText, true, "Please enter Company Name");
      formValues.OrgFriendlyName = ajax.CheckFieldRegex(FormFields.OrgFriendlyName, "Company Friendly Name", AppHelper.Regex.GeneralText, true, "Please enter Friendly Company Name");
      formValues.BusinessIdNumber = ajax.CheckFieldRegex(FormFields.BusinessIdNumber, "Business ID Number", AppHelper.Regex.GeneralText, true, "Please enter Business ID Number");
      formValues.ContactPhoneNumber = ajax.CheckFieldRegex(FormFields.ContactPhoneNumber, "Contact Phone Number", AppHelper.Regex.Mobile, false, "Please enter Contact Phone Number");
      formValues.GeneralEmail = ajax.CheckFieldRegex(FormFields.GeneralEmail, "General Email", AppHelper.Regex.Email, true, "Please enter your General Email");
      formValues.WebSiteURL = ajax.CheckFieldRegex(FormFields.WebSiteURL, "Website URL", AppHelper.Regex.URL, false, "Please enter your Website URL");
      formValues.GenericSenderEmailName = ajax.CheckFieldRegex(FormFields.GenericSenderEmailName, "Sender Email Name", AppHelper.Regex.GeneralText, false, "Please use only text for sender name.");
      formValues.GenericSenderEmailAddress = ajax.CheckFieldRegex(FormFields.GenericSenderEmailAddress, "Sender Email Address", AppHelper.Regex.Email, false, "Please enter valid email address.");

      if (ajax.BadFieldCount > 0) return;

      // If either sender email name or address given, both are required.
      if (formValues.GenericSenderEmailName.IsNullOrEmpty() && !formValues.GenericSenderEmailAddress.IsNullOrEmpty()) {
        ajax.AddBadField(FormFields.GenericSenderEmailName, "Please enter both email name and email address.");
        return;
      } else if (formValues.GenericSenderEmailAddress.IsNullOrEmpty() && !formValues.GenericSenderEmailName.IsNullOrEmpty()) {
        ajax.AddBadField(FormFields.GenericSenderEmailAddress, "Please enter both email name and email address.");
        return;
      }

      // Check if sender email domain is verified.
      if (!formValues.GenericSenderEmailAddress.IsNullOrEmpty()) {
        string emailDomain = formValues.GenericSenderEmailAddress.Split('@')[1];
        var domainInfo = DbHelper.SendingDomain.GetDomainByName(emailDomain);
        if (domainInfo == null) {
          ajax.AddBadField(FormFields.GenericSenderEmailAddress, "The domain '" + emailDomain + "' has not yet been verfied.");
          return;
        }
      }

      TenantOrgInfo.OrgName = formValues.OrgName;
      TenantOrgInfo.OrgFriendlyName = formValues.OrgFriendlyName;
      TenantOrgInfo.BusinessIDNumber = formValues.BusinessIdNumber;
      TenantOrgInfo.OrgEmail = formValues.GeneralEmail;
      TenantOrgInfo.OrgPhone = formValues.ContactPhoneNumber;
      TenantOrgInfo.WebSiteURL = formValues.WebSiteURL;
      TenantOrgInfo.GenericSenderEmailName = formValues.GenericSenderEmailName;
      TenantOrgInfo.GenericSenderEmailAddress = formValues.GenericSenderEmailAddress;

      try {
        DbHelper.TenantOrg.UpdateCompany(null, TenantOrgInfo);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation(nameof(UpdateCompany))
          .FromSession()
          .WithProperty(ApplicationInsightsConstants.CompanyId, TenantOrgInfo.OrgId)
          .WithProperty(ApplicationInsightsConstants.CompanyName, formValues.OrgName)
          .Track();
        ajax.AddDialogMessage("There was a problem updating the company information, please try again later.", ex);
        return;
      }

      ajax.AddSuccessToast("General Settings Updated");
    }
  }
}
