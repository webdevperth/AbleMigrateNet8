<%@ Page Language="C#" AutoEventWireup="true" CodeFile="SurveyCompleted.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.SurveyCompleted" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <% if (!ShowError) { %>

    <h3>Thank you for your participation!</h3>
    <p class="mb20">
      In the next section, you will be able to invite your raters,<br/
      >who will complete their version of your questionnaire.</p>
    <a class="btn btn-primary" href="<%= PathHelper.Pages.SurveyRaters(urlSurveyUID, urlPartUID) %>">Invite Your Raters</a>

  <% } else { %>

    <div class="container">
      <div class="row">
        <div class="col-md-12">
          <h3>Survey Not Found</h3>
          <p>Unfortunately the link you followed is not valid.</p>
          <p>Please consult your survey contact. Perhaps your survey hasn't been launched yet.</p>
        </div>
      </div>
    </div>

  <% } %>

</asp:Content>
