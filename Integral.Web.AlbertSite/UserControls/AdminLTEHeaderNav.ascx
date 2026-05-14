<%@ Control Language="C#" AutoEventWireup="true"
    CodeFile="AdminLTEHeaderNav.ascx.cs" Inherits="Integral.Web.PortalSite.UserControls.AdminLTEHeaderNav" %>

<%@ Import Namespace="Integral.Web" %>

  <header class="main-header">

    <!-- Logo -->
    <a href="<%= PathHelper.Pages.Home() %>" class="logo">
      <!-- mini logo for sidebar mini 50x50 pixels -->
      <div class="logo-mini"><img src="<%= PathHelper.Images.AbleFavicon() %>" /></div>
      <!-- logo for regular state and mobile devices -->
      <%= GetNavBarLogoHtml() %>
    </a>

    <nav class="navbar navbar-static-top">

      <div class="navbar-custom-menu">
        <ul class="nav navbar-nav nav-main">

          <li class="visible-xs">
            <a href="#" class="sidebar-toggle" data-toggle="offcanvas" role="button"><%= WebHelper.GetMenuIcon(WebHelper.MenuIconTypeEnum.Hamburger) %></a>
          </li>

          <% if (SessionHelper.AppAccess.PageAccess.CanAccessContactLevel()) { %>
            <li class="hidden-xs">
              <a class="nav-icons" href="<%= ConfigHelper.HelpUrls.ContactUs %>" target="_blank" tabindex="-1">
                <%= WebHelper.GetMenuIcon(WebHelper.MenuIconTypeEnum.Contact) %><span class="hidden-xs"> Contact</span>
              </a>
            </li>
          <% } else if (SessionHelper.AppAccess.PageAccess.CanViewClientLeadContact()) { %>
            <% if (UserInfo.ClientLeadUserId != null) { %>
              <li class="hidden-xs">
                <a class="nav-icons partner-contact-slideout-trigger" href="#" tabindex="-1" <%= WebHelper.GetSlideoutTriggerDataAttributes("Partner Details", PathHelper.Partials.PartnerSlideoutPanel(UserInfo.ClientLeadUserId)) %>>
                  <%= WebHelper.GetMenuIcon(WebHelper.MenuIconTypeEnum.Contact) %><span class="hidden-xs"> Contact</span>
                </a>
              </li>
            <% } %>
          <% } %>

          <% if (SessionHelper.AppAccess.PageAccess.CanAccessHelpLevel()) { %>
            <li class="hidden-xs">
              <a class="nav-icons" href="<%= ConfigHelper.HelpUrls.Help %>" target="_blank" tabindex="0" aria-hidden="true" >
                <%= WebHelper.GetMenuIcon(WebHelper.MenuIconTypeEnum.Help) %><span class="hidden-xs"> Help</span>
              </a>
            </li>
          <% } %>

          <li class="dropdown user user-menu hidden-xs">
            <a href="#" class="dropdown-toggle" data-toggle="dropdown" tabindex="0">
              <img src="<%= PathHelper.Images.UserPhoto(UserInfo, PathHelper.Images.UserPhotoSize.Thumbnail, true) %>" class="user-image" alt="User Image" />
              <span class="user-name hidden-sm"><%= UserInfo.GetFullName().HTMLEncode() %> <%= GetUserRoleName().HTMLEncode() %></span>
            </a>
            <div class="dropdown-menu dropdown-menu-right shadow animated--grow-in" aria-labelledby="userDropdown">
              <% if (SessionHelper.AppAccess.PageAccess.CanAccessPartnerProfile()) { %>
                <a class="dropdown-item" href="<%= PathHelper.Pages.CoachEdit(UserInfo.UserId) %>"><i class="fas fa-user fa-sm fa-fw"></i>Profile</a>
              <% } %>
              <a class="dropdown-item" href="<%= GetUpcomingPath() %>"><i class="fas fa-cogs fa-sm fa-fw"></i>Upcoming</a>
              <%= GetUserRoleSubmenuHtml() %>
              <div class="dropdown-divider"></div>
              <a class="dropdown-item" href="<%= PathHelper.Pages.Logout() %>"><i class="fas fa-sign-out-alt fa-sm fa-fw"></i>Logout</a>
            </div>
          </li>

        </ul>
      </div>
    </nav>

  </header>
