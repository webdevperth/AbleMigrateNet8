<%@ Page Language="C#" AutoEventWireup="true"
    CodeFile="calendly-submit.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.CalendlySubmit" %>

<% if (showForm) { %>

  <form action="#" method="post">
    <p><textarea name="json" cols="150" rows="30"></textarea></p>
    <p><input type="submit"/></p>
  </form>

<% } %>
