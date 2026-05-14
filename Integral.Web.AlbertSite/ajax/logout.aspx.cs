using System;

namespace Integral.Web.PortalSite.ajax {

  public partial class logout : System.Web.UI.Page {

    protected void Page_Load(object sender, EventArgs e) {

      SessionHelper.LogOut();

    }
  }
}
