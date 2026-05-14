<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ProgramInsights.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ProgramInsights"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <%= WebHelper.GetPartialLoaderHtml(new WebHelper.PartialLoaderOptions() {
    Url = PathHelper.Partials.ActivityChartForProgram(ProgramInfo.ProgramJobId),
    InitialWidth = "100%",
    InitialHeight = "320px",
    DeferInitialLoad = false,
    InitialStyle = WebHelper.PartialLoaderStyle.Blank,
    LoaderStyle = WebHelper.PartialLoaderStyle.Chart
  }) %>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      $(document).ready(function() {

      }); // ready.

    })(jQuery);
  </script>

</asp:Content>
