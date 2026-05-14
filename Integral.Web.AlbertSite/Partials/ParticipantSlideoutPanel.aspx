<%@ Page Language="C#" AutoEventWireup="true" CodeFile="ParticipantSlideoutPanel.aspx.cs" Inherits="Integral.Web.PortalSite.Page_Partials.ParticipantSlideoutPanel" %>

<%@ Import Namespace="Integral.Web" %>

<div class="flex1 overflow-y-auto">

  <div class="flex-wrap flex-justify-center">
    <img class="profile-image" src="<%= UserPhotoUrl %>" alt="Profile Image" />
    <div class="align-self-end">
      <h4 class="mt5 mb5"><%= CoacheeInfo.GetFullName().HTMLEncode() %></h4>
      <div><%= CoacheeInfo.EmailAddress.HTMLEncode() %></div>
    </div>
  </div>

  <hr/>

  <div class="details-list"><%= ActivityInfoHtml %></div>

  <div>
    <div class="table-title">Monthly Activity Minutes</div>
    <%= WebHelper.Charts.Coachee.GetMonthlyActivityChart(CoacheeInfo.CoacheeId, DateTime.UtcNow.AddMonths(-5), DateTime.UtcNow, 250, "mt15") %>
    <div class="table-title">Monthly Progress</div>
    <%= WebHelper.Charts.Coachee.GetMonthlyProgressChart(CoacheeInfo.CoacheeId, DateTime.UtcNow.AddMonths(-5), DateTime.UtcNow, 250, "mt15") %>
  </div>

</div>
<% if (CanViewParticipantProfile) { %>
  <div class="flex0 mt20"><a class="btn btn-secondary" href="<%= CoacheeEditUrl %>">Full Profile</a></div>
<% } %>
