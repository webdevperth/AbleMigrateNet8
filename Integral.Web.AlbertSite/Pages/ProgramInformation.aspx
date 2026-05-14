<%@ Page Language="C#" AutoEventWireup="true"
  CodeFile="ProgramInformation.aspx.cs"
  Inherits="Integral.Web.PortalSite.Pages_Albert.ProgramInformation"
  MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <h3 class="mt0">Your Team</h3>

  <div class="flex-wrap gap25">
    <%= GetTeamCardsHtml() %>
  </div>

  <hr />

  <div class="programNotes">
    <%= GetNotesHtml() %>
  </div>

</asp:Content>

<asp:Content ContentPlaceHolderID="PostScriptContent" runat="server">

  <script type="text/javascript">
    (function($) {

      })(jQuery);
  </script>

</asp:Content>
