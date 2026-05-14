using System;

namespace Integral.Web.PortalSite.AppCode.PageBaseClasses {

  public class CompanyInfoBase : LoggedInPageBase {

    protected string newCompanyIdValue = "new";
    protected int urlCompanyId = 0;
    public bool IsNewCompany;
    public DbHelper.ClientCompanies.AlbertCompanyInfo CompanyInfo;
    public bool CanUpdateCompany;

    protected override void Page_Init(object sender, EventArgs e) {

      if (WebHelper.IsRequestExiting()) return;

      base.Page_Init(sender, e);

      if (SessionHelper.IsUserRoleAdmin || SessionHelper.IsUserRoleCoach) FallbackUrl = PathHelper.Pages.Organisations();

      // Check url param wants to add new company or view existing one.
      if (WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.CompanyId) == newCompanyIdValue) {
        // Creating new company.
        IsNewCompany = true;
        urlCompanyId = 0;
      } else {
        IsNewCompany = false;
        urlCompanyId = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.CompanyId) ?? 0;
      }

      if (IsNewCompany) {
        // If adding new company, must be allowed to update, and only on Settings page.

        CompanyInfo = new DbHelper.ClientCompanies.AlbertCompanyInfo();
        CanUpdateCompany = SessionHelper.AppAccess.Companies.CanCreate();

        if (!CanUpdateCompany) {
          SetFallbackRedirect();
          return;
        }

      } else {

        CompanyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(urlCompanyId, SessionHelper.GetUserInfoOrNull());

        if (CompanyInfo == null) {
          SetFallbackRedirect();
          return;
        }

        CanUpdateCompany = SessionHelper.AppAccess.Companies.CanEdit(CompanyInfo);
      }

      if (!CheckUserPageAccess()) {
        SetFallbackRedirect();
        return;
      }
    }

    internal bool CheckUserPageAccess() {

      // Creating company can only occur on settings page.
      if (IsNewCompany) {
        if (PathHelper.IsCurrentPage(PathHelper.Pages.OrganisationSettings())
          && SessionHelper.AppAccess.Companies.CanViewOrganisationSettings(CompanyInfo, isNewCompany: true)
          && IsNewCompany
          && SessionHelper.AppAccess.Companies.CanCreate()) {
          return true;
        }
        return false;
      }

      if (PathHelper.IsCurrentPage(PathHelper.Pages.OrganisationOverview()))
        return SessionHelper.AppAccess.Companies.CanViewOrganisationOverview(CompanyInfo);

      if (PathHelper.IsCurrentPage(PathHelper.Pages.OrganisationSettings()))
        return SessionHelper.AppAccess.Companies.CanViewOrganisationSettings(CompanyInfo);

      if (PathHelper.IsCurrentPage(PathHelper.Pages.OrganisationPeople()))
        return SessionHelper.AppAccess.Companies.CanViewOrganisationPeople(CompanyInfo);

      if (PathHelper.IsCurrentPage(PathHelper.Pages.PeopleDetails()))
        return SessionHelper.AppAccess.Companies.CanViewOrganisationPeople(CompanyInfo);

      if (PathHelper.IsCurrentPage(PathHelper.Pages.OrganisationDepartments()))
        return SessionHelper.AppAccess.Companies.CanViewOrganisationDepartments(CompanyInfo);

      if (PathHelper.IsCurrentPage(PathHelper.Pages.OrganisationProjects()))
        return SessionHelper.AppAccess.Companies.CanViewOrganisationProjects(CompanyInfo);

      if (PathHelper.IsCurrentPage(PathHelper.Pages.OrganisationCapabilities()))
        return SessionHelper.AppAccess.Companies.CanViewOrganisationCapabilities(CompanyInfo);

      if (PathHelper.IsCurrentPage(PathHelper.Reports.OrganisationIOSReports()))
        return SessionHelper.AppAccess.Companies.CanViewOrganisationIOSReports(CompanyInfo);

      return false;

    }
  }
}
