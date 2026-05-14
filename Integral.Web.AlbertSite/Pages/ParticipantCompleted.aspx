<%@ Page Language="C#" AutoEventWireup="true" CodeFile="ParticipantCompleted.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.ParticipantCompleted" MasterPageFile="~/MasterPages/Public.Master" %>

<%@ Import Namespace="Integral.Web" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <% if (ContentVisible) { %>
    <div class="container">
      <div class="row">
        <div class="col-md-12">
          <h3>Thank you for your participation!</h3>
          <% if (CanInviteRaters) { %>
            <h4>In the next section, you will be able to invite your raters,<br/>who will complete their version of your questionnaire.</h4>
            <a class="btn btn-primary" href="<%= PathHelper.Pages.ParticipantRaters(urlSurveyUID, urlPartUID) %>">Invite Your Raters</a>
          <% } else { %>
            <h4>You may now close this browser window.</h4>
          <% } %>
        </div>
      </div>
    </div>
  <% } %>

  <% if (ErrorVisible) { %>
    <div class="container">
      <div class="row">
        <div class="col-md-12">
          <h2>Survey Not Found</h2>
          <p>Unfortunately the link you followed is not valid.</p>
          <p>Please consult your survey contact. Perhaps your survey hasn't been launched yet.</p>
        </div>
      </div>
    </div>
  <% } %>

</asp:Content>
