using System.Text;
using System.Web.UI;
using System.IO;

namespace Integral.Web {
  public static partial class StringExt {

    public static string RenderToString(this Control control) {
      //render control to string
      StringBuilder sb = new StringBuilder();
      using (HtmlTextWriter w = new HtmlTextWriter(new StringWriter(sb))) {
        control.RenderControl(w);
      }
      return sb.ToString();
    }

  }
}
