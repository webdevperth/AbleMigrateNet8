using System;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class Logout : System.Web.UI.Page {

    protected void Page_Load(object sender, EventArgs e) {

      SessionHelper.LogOut();
      WebHelper.Redirect(PathHelper.WebRoot);
    }
  }
}
