<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Logout.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.Logout" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <script>
    Intercom('shutdown')
  </script>

</asp:Content>
