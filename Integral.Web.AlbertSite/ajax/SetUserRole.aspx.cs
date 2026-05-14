using System;

namespace Integral.Web.PortalSite.ajax {

  public partial class SetUserRole : AppCode.PageBaseClasses.LoggedInPageBase {

    protected void Page_Load(object sender, EventArgs e) {

      if (!SystemWeb.IsHttpPost) return;

      AjaxSubmitHelper.Process(ajax => {
        if (Enum.TryParse(WebHelper.GetFormValue(PathHelper.FormKeys.UserRole) ?? "", out ConfigHelper.UserRole userRoleRequest)) {
          SessionHelper.SetUserRole(userRoleRequest);
          SessionHelper.SetLoginRequiredRedirectedFrom(SessionHelper.LoginRequiredRedirectedFrom.Login);
          ajax.SetRedirectUrl(PathHelper.Pages.Home());
        }
      });

    }
  }
}

