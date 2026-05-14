using System;
using Integral.Web.Services;

namespace Integral.Web.PortalSite.api {

  public partial class companies : AppCode.PageBaseClasses.LoggedInPageBase {

    public bool isNewCompany = false, isNewProgram = false;

    protected void Page_Load(object sender, EventArgs e) {

      if (!SystemWeb.IsHttpPost) return;

      AjaxSubmitHelper.Process(ajax => {
        SubmittedForm(ajax);
      });

    }

    void SubmittedForm(AjaxSubmitHelper ajax) {

      DbHelper.ClientCompanies.AlbertCompanyInfo companyInfo = null;

      if (!SessionHelper.IsUserRoleAdmin) {
        ajax.AddDialogMessage("Access denied.");
        return;
      }

      if (WebHelper.GetFormValue("CompanyId") == "new") isNewCompany = true;

      if (!isNewCompany) {
        ajax.AddDialogMessage("Can only create new companies for now.");
        return;
      }

      int newCompanyId = 0;
      string companyName = ajax.CheckFieldRegex("CompanyName", "Company Name", AppHelper.Regex.GeneralText, isNewCompany, "Please provide a Company Name.").TrimWhitespace();

      if (ajax.BadFieldCount > 0) return;

      // Add new company.
      try {
        companyInfo = DbHelper.ClientCompanies.CreateCompany(null, userInfo.OrgId, companyName);
        newCompanyId = companyInfo.CompanyId;
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation("API_Companies_CreateCompany")
          .WithProperty(ApplicationInsightsConstants.CompanyName, companyName)
          .WithProperty(ApplicationInsightsConstants.OrgId, userInfo.OrgId)
          .Track();
        if (ex.Message.Contains("duplicate key")) {
          ajax.AddDialogMessage("That company name already exists.");
        } else {
          ajax.AddDialogMessage("Error creating company: " + ex.Message);
        }
        return;
      }

      if (isNewCompany) ajax.AddReturnValue("NewCompanyId", newCompanyId.ToString());

    }

  }
}
