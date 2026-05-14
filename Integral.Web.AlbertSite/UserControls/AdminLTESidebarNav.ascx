<%@ Control Language="C#" AutoEventWireup="true" CodeFile="AdminLTESidebarNav.ascx.cs" Inherits="Integral.Web.PortalSite.UserControls.AdminLTESidebarNav" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.AppCode.PageBaseClasses" %>

<!-- Left side column. contains the logo and sidebar -->
<aside class="main-sidebar">
  <!-- sidebar: style can be found in sidebar.less -->
  <section class="sidebar">

    <ul class="sidebar-menu">
    <%
      if (SessionHelper.AppAccess.PageAccess.CanAccessParticipantMenu()) {

        if (SessionHelper.AppAccess.PageAccess.CanAccessParticipantAICoach()) {
          MenuItem(() => {
            SystemWeb.ResponseWriteLine(TopMenuLink(PathHelper.Pages.ParticipantAICoach(), "AI Coach", "sparkles-outline"));
          });
        }

        MenuItem(() => {
          SystemWeb.ResponseWriteLine(TopMenuLink(GetUpcomingUrl(), "Upcoming", "calendar-outline"));
        });

        if (SessionHelper.AppAccess.PageAccess.CanAccessParticipantDevelopment()) {
          SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.ParticipantDevelopment(), "Development", "analytics-outline"));
        }

        MenuItem(() => {
          SystemWeb.ResponseWriteLine(TopMenuLink(PathHelper.Pages.ParticipantSurveys(), MenuItems.Surveys, "bar-chart-outline",
            PathHelper.Pages.SurveyQuestions(),
            PathHelper.Pages.SurveyCompleted(null, null),
            PathHelper.Pages.SurveyRaters(null, null)));
        });

        if (SessionHelper.AppAccess.PageAccess.CanAccessParticipantCoaching()) {
          MenuItem(() => {
            SystemWeb.ResponseWriteLine(TopMenuLink(PathHelper.Pages.ParticipantCoaching(), "Coaching", "rocket-outline"));
          });
        }

        if (SessionHelper.AppAccess.PageAccess.CanAccessDevelopmentPlan()) {
          MenuItem(() => {
            SystemWeb.ResponseWriteLine(TopMenuLink(PathHelper.Pages.DevelopmentPlan(), MenuItems.DevelopmentPlan, "navigate-circle-outline",
              PathHelper.Pages.DevelopmentPlanForm()));
          });
        }

        if (SessionHelper.AppAccess.Content.CanViewContentTopLevelMenuItem()) {
          SystemWeb.ResponseWriteLine(TopMenuLink(PathHelper.Pages.Content(), MenuItems.Microlearning, "archive-outline"));
        }

        if (SessionHelper.AppAccess.PageAccess.CanAccessPartnerProfile()) {
          SystemWeb.ResponseWriteLine(TopMenuLink(PathHelper.Pages.CoachEdit(UserInfo.UserId), MenuItems.MyProfile, "person-circle-outline", true));
        }

        if (SessionHelper.AppAccess.PageAccess.CanAccessAdminTools()) {
          MenuItem(() => {
            SystemWeb.ResponseWriteLine(TopMenuLink(PathHelper.Pages.AdminTools(), "Admin Tools", "hammer-outline"));
          });
        }

      } else {

        MenuItemWithSubMenu(PathHelper.GetDefaultPostLoginURL(UserInfo), MenuItems.Dashboard, "home-outline", () => {

          if (CanExpandDashboardMenu()) {
            SubMenu(() => {

              SystemWeb.ResponseWriteLine(SubmenuItem(GetUpcomingUrl(), "Upcoming", "calendar-outline"));

              if (SessionHelper.AppAccess.PageAccess.CanAccessPayruns()) {
                SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.OverviewPayruns(), "Pay Runs", "wallet-outline"));
              }
              if (SessionHelper.AppAccess.PageAccess.CanAccessInvitePartner()) {
                SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.CoachEdit(UserInfo.UserId, PathHelper.CoachTabEnum.partners), "Invite", "arrow-redo-outline"));
              }
              if (SessionHelper.AppAccess.PageAccess.CanAccessPartnerProfile()) {
                SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.CoachEdit(UserInfo.UserId), MenuItems.MyProfile, "person-circle-outline"));
              }
            });
          }
        });

        if (SessionHelper.IsUserIOSClientHR) {
          MenuItem(() => {
            SystemWeb.ResponseWriteLine(TopMenuLink(PathHelper.Pages.IOSParticipants(), "Participants", "people-outline"));
          });
        } else if (SessionHelper.AppAccess.PageAccess.CanAccessTopLevelParticipantsPageMenu()) {
          MenuItem(() => {
            SystemWeb.ResponseWriteLine(TopMenuLink(PathHelper.Pages.Coachees(), MenuItems.Participants, "people-outline"));
          });
        }

        if (SessionHelper.AppAccess.PageAccess.CanAccessProjectLevel()) { %>

          <li class="<%= MenuStyles.SubmenuClass + (MenuThirdLayerActive_Program ? " menuThirdLayerActive" : "") %>">

            <%= MenuLink(PathHelper.Pages.Projects_List(), "Projects", "folder-open-outline", false) %>

            <% if (CanExpandProjectMenu()) { %>
              <ul class="<%= MenuStyles.SubmenuClass + (MenuThirdLayerActive_Program ? " menuThirdLayer" : "") %>">

                <% if (ProjectPage != null && ProjectPage.IsNewProject) { %>

                  <% if (SessionHelper.AppAccess.PageAccess.CanAccessProjectSettings(ProjectInfo)) { %>
                    <%= SubmenuItem(PathHelper.Pages.ProjectSettings(ProjectInfo.JobNumber), "New Project", "settings-outline") %>
                  <% } %>

                <% } else { %>

                  <% if (SessionHelper.AppAccess.PageAccess.CanAccessProgramLevel()) { %>
                    <li class="<%= MenuStyles.SubmenuClass + (MenuThirdLayerActive_Program ? " menuThirdLayerHolder" : "") %>">
                      <%= MenuLink(PathHelper.Pages.ProjectPrograms(ProjectInfo.JobNumber), "Programs", "list-outline", MenuThirdLayerActive_Program) %>

                      <% if (CanExpandProgramMenu()) { %>
                        <ul class="<%= MenuStyles.SubmenuClass + (MenuThirdLayerActive_Program ? " menuThirdLayer" : "") %>">

                          <% if (ProgramPage == null ? false : ProgramPage.IsNewProgram) { %>

                            <%= SubmenuItem(PathHelper.Pages.ProgramSettings(), "New Program", "add-outline") %>

                          <% } else { %>

                            <% if (SessionHelper.AppAccess.Programs.CanViewProgramSettings(ProgramInfo)) { %>
                              <%= SubmenuItem(PathHelper.Pages.ProgramSettings(ProgramInfo.ProgramJobId), "Program Settings", "cog-outline") %>
                            <% } %>

                            <% if (SessionHelper.AppAccess.Programs.CanViewProgramOverview(ProgramInfo)) { %>
                              <%= SubmenuItem(PathHelper.Pages.ProgramOverview(ProgramInfo.ProgramJobId), "Overview", "bookmark-outline") %>
                            <% } %>

                            <% if (SessionHelper.AppAccess.Programs.CanViewProgramInsights(ProgramInfo)) { %>
                              <%= SubmenuItem(PathHelper.Pages.ProgramInsights(ProgramInfo.ProgramJobId), "Insights", "bar-chart-outline") %>
                            <% } %>

                            <% if (SessionHelper.AppAccess.PageAccess.CanAccessProgramInformation()) { %>
                              <%= SubmenuItem(PathHelper.Pages.ProgramInformation(ProgramInfo.ProgramJobId), "Information", "rocket-outline") %>
                            <% } %>

                            <% if (SessionHelper.AppAccess.PageAccess.CanAccessProgramContent()) { %>
                              <%= SubmenuItem(PathHelper.Pages.ProgramContent(ProgramInfo.ProgramJobId), MenuItems.Microlearning, "archive-outline") %>
                            <% } %>

                            <% if (SessionHelper.AppAccess.PageAccess.CanAccessProgramParticipants()) { %>
                              <%= SubmenuItem(PathHelper.Pages.ProgramParticipants(ProgramInfo.ProgramJobId), MenuItems.Participants, "people-outline") %>
                            <% } %>

                            <% if (SessionHelper.AppAccess.Programs.Workshops.CanViewWorkshops(ProgramInfo)) { %>
                              <%= SubmenuItem(PathHelper.Pages.Workshops_List(ProgramInfo.ProgramJobId), "Workshops", "documents-outline") %>
                            <% } %>

                            <% if (SessionHelper.AppAccess.PageAccess.CanAccessProgramConsultingItems() && SessionHelper.AppAccess.Programs.Consulting.CanListConsultingItems(ProgramInfo)) { %>
                              <%= SubmenuItem(PathHelper.Pages.Consulting_List(ProgramInfo.ProgramJobId), "Consulting", "hourglass-outline") %>
                            <% } %>

                            <% if (SessionHelper.AppAccess.PageAccess.CanAccessProgramCostItems() && SessionHelper.AppAccess.Programs.CanListCostItems(ProgramInfo)) { %>
                              <%= SubmenuItem(PathHelper.Pages.ProgramCostItems_List(ProgramInfo.ProgramJobId), "Cost Items", "cash-outline") %>
                            <% } %>

                            <% if (SessionHelper.AppAccess.PageAccess.CanAccessProgramSurveyStatus(ProgramInfo)) { %>
                              <%= SubmenuItem(PathHelper.Pages.ProgramSurveyStatus(ProgramInfo.ProgramJobId), MenuItems.Surveys, "podium-outline",
                                    PathHelper.Reports.EvalViewer(),
                                    PathHelper.Pages.ProgramSkillsViewer(ProgramInfo)) %>
                            <% } %>

                            <% if (SessionHelper.AppAccess.PageAccess.CanAccessProgramSendEmail() && SessionHelper.AppAccess.Programs.CanSendProgramEmail(ProgramInfo)) { %>
                              <%= SubmenuItem(PathHelper.Pages.ProgramSendEmail(ProgramInfo.ProgramJobId), "Email", "mail-outline") %>
                            <% } %>

                          <% } %>
                        </ul>
                      <% } %>
                    </li>
                  <% } %>

                  <% if (SessionHelper.AppAccess.PageAccess.CanAccessProjectSettings(ProjectInfo) && SessionHelper.AppAccess.Projects.CanEditProject(ProjectInfo)) { %>
                    <%= SubmenuItem(PathHelper.Pages.ProjectSettings(ProjectInfo.JobNumber), "Project Settings", "settings-outline") %>
                  <% } %>

                  <% if (SessionHelper.AppAccess.Projects.CanViewProject(ProjectInfo)) { %>

                    <% if (SessionHelper.AppAccess.PageAccess.CanAccessProjectQuotes()) { %>
                      <li class="<%= MenuStyles.SubmenuItemClass %>">
                        <%= SubmenuItem(PathHelper.Pages.ProjectQuotes(ProjectInfo.JobNumber), MenuItems.Quotes, "cash-outline") %>
                      </li>
                    <% } %>

                    <% if (SessionHelper.AppAccess.PageAccess.CanAccessProjectComponents()) { %>
                      <%= SubmenuItem(PathHelper.Pages.ProjectComponents(ProjectInfo.JobNumber), "Components", "grid-outline") %>
                    <% } %>

                    <% if (SessionHelper.AppAccess.Projects.CanViewProjectInvoicing(ProjectInfo)) { %>
                      <%= SubmenuItem(PathHelper.Pages.ProjectInvoicing(ProjectInfo.JobNumber), "Invoicing", "documents-outline") %>
                    <% } %>

                    <% if (SessionHelper.AppAccess.PageAccess.CanAccessProjectAccess(ProjectInfo)) { %>
                      <%= SubmenuItem(PathHelper.Pages.ProjectAccess_List(ProjectInfo.JobNumber), "Access", "lock-open-outline") %>
                    <% } %>

                    <% if (SessionHelper.AppAccess.Projects.CanViewProjectCustomise(ProjectInfo)) { %>
                      <%= SubmenuItem(PathHelper.Pages.ProjectCustomise(ProjectInfo.JobNumber), "Customise", "create-outline") %>
                    <% } %>
                  <% } %>

                <% } %>
              </ul>
            <% } %>
          </li>
        <% }

            GetOrganisationMenu();

            if (SessionHelper.AppAccess.PageAccess.CanAccessQuoteLevel()) {
              MenuItem(() => {
                SystemWeb.ResponseWriteLine(TopMenuLink(PathHelper.Pages.QuoteList(), MenuItems.Quotes, "cash-outline"));
              });
            }

            string insightsDefaultUrl = "";
            if (SessionHelper.AppAccess.Insights.CanViewQuality()) insightsDefaultUrl = PathHelper.Reports.Quality(); // Least preferred.
            if (SessionHelper.AppAccess.Insights.CanCurrentRoleViewIOSReports()) insightsDefaultUrl = PathHelper.Reports.InsightsIOSReports();
            if (SessionHelper.AppAccess.Insights.CanViewSurveyViewer()) insightsDefaultUrl = PathHelper.Reports.SurveyViewer();
            if (SessionHelper.AppAccess.Insights.CanViewSkillsViewer()) insightsDefaultUrl = PathHelper.Reports.SkillsViewer(); // Most preferred.

            if (!insightsDefaultUrl.IsNullOrEmpty()) {

              MenuItemWithSubMenu(insightsDefaultUrl, MenuItems.Insights, "bar-chart-outline", () => {
                if (PathHelper.IsCurrentPage(PathHelper.Reports.Quality())
                  || PathHelper.IsCurrentPage(PathHelper.Reports.SurveyViewer())
                  || PathHelper.IsCurrentPage(PathHelper.Reports.SkillsViewer())
                  || PathHelper.IsCurrentPage(PathHelper.Reports.InsightsIOSReports())) {
                  SubMenu(() => {

                    if (SessionHelper.AppAccess.Insights.CanViewQuality()) {
                      SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Reports.Quality(), "Quality reports", "cellular-outline"));
                    }

                    if (SessionHelper.AppAccess.Insights.CanViewSurveyViewer()) {
                      SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Reports.SurveyViewer(), "Survey Viewer", "bar-chart-outline"));
                    }

                    if (SessionHelper.AppAccess.Insights.CanViewSkillsViewer()) {
                      SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Reports.SkillsViewer(), "Capabilities", "bar-chart-outline"));
                    }

                    if (SessionHelper.AppAccess.Insights.CanCurrentRoleViewIOSReports()) {
                      SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Reports.InsightsIOSReports(), "IOS Reports", "bar-chart-outline"));
                    }
                    if (SessionHelper.AppAccess.Insights.CanViewEvalViewer()) {
                      // Not adding this in the menu for now.
                      // SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Reports.EvalViewer(), "Eval Viewer", "bar-chart-outline"));
                    }
                  });
                }
              });
            }

            if (SessionHelper.AppAccess.Content.CanViewContentTopLevelMenuItem()) {
              SystemWeb.ResponseWriteLine(TopMenuLink(PathHelper.Pages.Content(), MenuItems.Microlearning, "archive-outline"));
            }

            if (SessionHelper.AppAccess.PageAccess.CanAccessPartnersLevel()) {
              MenuItemWithSubMenu(PathHelper.Pages.Coaches(), "Partners", "people-outline", () => {
                if (CoachInfo != null && !PathHelper.IsCurrentPage(PathHelper.Pages.CoachReferrals()) && !IsDisplayingProfileInDashboard()) {
                  SubMenu(() => {
                    if (SessionHelper.AppAccess.PageAccess.CanAccessPartnerProfile()) {
                      SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.CoachEdit(true), "Profile", "person-circle-outline"));
                    }
                    if (SessionHelper.AppAccess.PageAccess.CanAccessPayruns() && SessionHelper.AppAccess.Coaches.CanViewPayRuns(UserInfo, ViewingEntityId)) {
                      SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.CoachPayRuns(true), "Pay Runs", "receipt-outline"));
                    }
                    if (SessionHelper.AppAccess.PageAccess.CanAccessPartnerUpcoming()) {
                      SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.CoachUpcoming(true), "Upcoming", "calendar-outline"));
                    }
                    if (SessionHelper.AppAccess.PageAccess.CanAccessInvitePartner()) {
                      SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.CoachEdit(UserInfo.UserId, PathHelper.CoachTabEnum.partners), "Invite", "arrow-redo-outline"));
                    }
                  });
                }
              });
            }

            if (SessionHelper.AppAccess.Settings.CanUpdateSettings()) {

              MenuItemWithSubMenu(PathHelper.Pages.Settings.General(), "Settings", "settings-outline", () => {

                if (Page is SettingsPageBase) {

                  var tenantOrgInfo = (Page as SettingsPageBase).TenantOrgInfo;

                  SubMenu(() => {

                    // Pages available to org admin users.
                    SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.Settings.General(), "General", "business-outline"));
                    //SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.Settings.Team(), "Team", "people-outline"));

                    // Pages only available to org owner.
                    if (SessionHelper.AppAccess.Settings.CanUpdateBilling(tenantOrgInfo)) {
                      SystemWeb.ResponseWriteLine(SubmenuItem(PathHelper.Pages.Settings.Billings(), "Subscriptions", "calendar-outline"));
                    }
                  });
                }
              });
            }

            if (SessionHelper.AppAccess.PageAccess.CanAccessAdminTools()) {
              MenuItem(() => {
                SystemWeb.ResponseWriteLine(TopMenuLink(PathHelper.Pages.AdminTools(), "Admin Tools", "hammer-outline"));
              });
            }

            if (SessionHelper.AppAccess.PageAccess.CanAccessContactLevel()) {
              MenuItem(() => {
                SystemWeb.ResponseWriteLine(TopMenuLink(ConfigHelper.HelpUrls.ContactUs, "Contact", WebHelper.MenuIconName[WebHelper.MenuIconTypeEnum.Contact], true));
              });
            }

            if (SessionHelper.AppAccess.PageAccess.CanAccessHelpLevel()) {
              MenuItem(() => {
                SystemWeb.ResponseWriteLine(TopMenuLink(ConfigHelper.HelpUrls.Help, "Help", WebHelper.MenuIconName[WebHelper.MenuIconTypeEnum.Help], true));
              });
            }

            SystemWeb.ResponseWriteLine(GetUserRoleSubmenuHtml());

            MenuItem(() => {
              SystemWeb.ResponseWriteLine(TopMenuLink(PathHelper.Pages.Logout(), "Log Out", WebHelper.MenuIconName[WebHelper.MenuIconTypeEnum.Logout], true));
            });

          }
    %>

    </ul>

  </section>

</aside>
