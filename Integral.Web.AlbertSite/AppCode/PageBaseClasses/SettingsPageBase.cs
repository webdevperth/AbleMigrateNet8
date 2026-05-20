namespace Integral.Web.PortalSite.AppCode.PageBaseClasses {

  public class SettingsPageBase : LoggedInPageModel {

    // Note we get the current OrgId from the current user's OrgId,

    public DbHelper.TenantOrg.TenantOrgInfo TenantOrgInfo { get; private set; }

    protected override void InitializePage() {

      base.InitializePage();

      if (WebHelper.IsRequestExiting()) return;

      WebHelper.AddBodyClass("BasePage-Org");

      FallbackUrl = PathHelper.Pages.Home();

      if (!SessionHelper.AppAccess.Settings.CanUpdateSettings()) {
        SetFallbackRedirect();
        return;
      }

      // Every user has a valid OrgId, never null.
      TenantOrgInfo = DbHelper.TenantOrg.GetTenantOrgById(SessionHelper.UserInfo.OrgId);

      // Mirror onto LayoutModel for future ViewComponent consumers. See LayoutModel.cs.
      LayoutModel.GetCurrent().TenantOrgInfo = TenantOrgInfo;

      // Certain pages restricted to just the owner.
      if (PathHelper.IsCurrentPage(PathHelper.Pages.Settings.Billings())
        || PathHelper.IsCurrentPage(PathHelper.Pages.Settings.Invoices())
        || PathHelper.IsCurrentPage(PathHelper.Pages.Settings.Plans())) {

        if (!SessionHelper.AppAccess.Settings.CanUpdateBilling(TenantOrgInfo)) {
          SetFallbackRedirect();
          return;
        }
      }

    }
  }
}
