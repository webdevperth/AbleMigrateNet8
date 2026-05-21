using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class OverviewUpcoming : AppCode.PageBaseClasses.OverviewPageBase {

    public DbHelper.ClientCompanies.AlbertCompanyInfo CompanyInfo { get; private set; }

    public List<DbHelper.AblePrograms.ClientProgramSummary> ClientProgramSummary { get; private set; }
    public const int ClientProgramSummaryMaxRows = 5;
    public int MaxActionsForCards = ConfigHelper.MaxParticipantActions_Upcoming;
    public int ClientProgramSummaryTotalRows;

    public decimal WorkshopTotalRevenue = 0;
    public decimal ConsultingTotalRevenue = 0;
    public decimal PayRunTotalRevenue = 0;
    public decimal SalesTotalRevenue = 0;
    public decimal PlcTotalRevenue = 0;

    public List<DbHelper.CoachingSessions.AbleSessionInfo> SessionList;
    public List<DbHelper.WorkshopEvents.WorkshopEventInfo> WorkshopItemsList;
    public List<DbHelper.ConsultingItems.ConsultingItemInfo> ConsultingItemsList;
    public List<DbHelper.PayRuns.UpcomingPLCSalesInfo> SalesItemsList;
    public List<DbHelper.PayRuns.UpcomingPLCSalesInfo> PLCItemsList;

    public string PendingActionsRows = "";
    public string DefaultBackupActionRows = "";
    public bool CanViewInternalActions, CanViewPartnerMetrics, CanViewOrgMetrics, CanViewClientProgramSummary, ShowBlockingModal;

    // todo: move this to webhelper (all of the actions functionality)
    // Type of actions that partner could need to do
    public enum PartnerActionType {
      CompleteProfile, SetupCompany, CreateProgram, CreateCoachingProgram, UploadProfilePicture, InviteTeam, CreateMicroleaerning, CreateQuote,
      QuotesNeedAttention, CompleteCompanyProfile, ConfirmWorkshops, AssignCoachToParticipants, UnstuckParticipants, SetupProgramQuotes, CompleteContract, DoTechnicalOnboardingTag, DoOrientationTag
    };

    public Dictionary<PartnerActionType, PartnerAction> PartnerActions;

    // These specific action types require to look for it's information in the database
    public List<PartnerActionType> ActionsTypesRequireSearchDatabase = new List<PartnerActionType>() { PartnerActionType.QuotesNeedAttention, PartnerActionType.ConfirmWorkshops,
      PartnerActionType.AssignCoachToParticipants, PartnerActionType.UnstuckParticipants, PartnerActionType.SetupProgramQuotes };

    // This contains all of the PartnerActionInfoForHtml objects, for each of the Actions the Partner needs to complete. And will be used to integrate to cards and table rows.
    public List<PartnerAction> ActionsRequiredByPartnerForHtml = new List<PartnerAction>();

    public IActionResult OnGet() {

      if (!SessionHelper.AppAccess.Coaches.CanViewPayRuns(userInfo, OverviewCoachInfo.UserId)) {
        WebHelper.Redirect(FallbackUrl);
        return new EmptyResult();
      }

      CanViewInternalActions = SessionHelper.AppAccess.Coaches.CanViewInternalActions();
      CanViewPartnerMetrics = SessionHelper.AppAccess.Metrics.CanViewPartnerMetrics();
      CanViewOrgMetrics = SessionHelper.AppAccess.Metrics.CanViewOrgMetrics();
      CanViewClientProgramSummary = SessionHelper.AppAccess.Programs.CanViewClientProgramSummary();
      ShowBlockingModal = !SessionHelper.AppAccess.Users.CanNavigatePlatform();

      PartnerActions = InitPartnerActions();

      PageTitle = "Upcoming";

      if (CanViewInternalActions) {
        DefaultBackupActionRows = FallbackInternalActions();
        GetPartnerActions();
        PendingActionsRows = CheckPendingInternalActions();
        GetInternalActions();
      } else {
        PendingActionsRows = CheckPendingExternalActions();
        DefaultBackupActionRows = FallbackExternalActions();
      }

      if (CanViewOrgMetrics && userInfo.ClientCompanyId.HasValue) {
        CompanyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(userInfo.ClientCompanyId.Value, SessionHelper.GetUserInfoOrNull());
      }

      if (CanViewClientProgramSummary) {
        ClientProgramSummary = DbHelper.AblePrograms.GetClientProgramSummary(userInfo.UserId, ClientProgramSummaryMaxRows, out ClientProgramSummaryTotalRows);
      }

      return Page();
    }

    void GetInternalActions() {

      var latestPayRunDateUtc = DbHelper.PayRuns.GetLatestProcessedDateUtc(OverviewCoachInfo.UserId).GetValueOrDefault(DateTime.MinValue);
      var earliestItemDateUtc = DateTime.UtcNow.AddDays(-ConfigHelper.OverviewPage_HistoricItemCutoffDays);

      SessionList = DbHelper.CoachingSessions.GetUpcomingSessionsForCoach(OverviewCoachInfo.UserId, earliestItemDateUtc);

      WorkshopItemsList = DbHelper.WorkshopEvents.GetUpcomingWorkshopsForCoach(OverviewCoachInfo.UserId, earliestItemDateUtc);
      WorkshopTotalRevenue = 0;
      if (WorkshopItemsList != null) foreach (var item in WorkshopItemsList) WorkshopTotalRevenue += item.WorkshopRevenue.GetValueOrDefault(0);

      ConsultingItemsList = DbHelper.ConsultingItems.GetUpcomingConsultingForCoach(OverviewCoachInfo.UserId, earliestItemDateUtc);
      ConsultingTotalRevenue = 0;
      if (ConsultingItemsList != null) foreach (var item in ConsultingItemsList) ConsultingTotalRevenue += item.ItemAmount;

      var salesAndPLC = DbHelper.PayRuns.GetUpcomingSalesAndPLC(OverviewCoachInfo.UserId, latestPayRunDateUtc);

      // Tweak descriptions a bit.
      salesAndPLC.ForEach(i => {
        if (i.CoachingSessionId != null) i.ItemName = "Coaching: " + i.ItemName;
        else if (i.WorkshopEventId != null) i.ItemName = "Workshop: " + i.ItemName;
        else if (i.ConsultingItemId != null) i.ItemName = "Consulting: " + i.ItemName;
      });

      // Separate sales and plc items to their own separate lists.
      SalesItemsList = (from i in salesAndPLC where i.SalesUserId == OverviewCoachInfo.UserId && i.SalesRevenue > 0 select i).ToList();
      PLCItemsList = (from i in salesAndPLC where i.PLCUserId == OverviewCoachInfo.UserId && i.PLCRevenue > 0 select i).ToList();

      PayRunTotalRevenue = WorkshopTotalRevenue + ConsultingTotalRevenue;
    }

    public string GetStartTime(DbHelper.WorkshopEvents.WorkshopEventInfo workshopEvent) {

      if (workshopEvent.WhenStartLocal == null || workshopEvent.WorkshopStatusId == DbHelper.WorkshopStatus.WorkshopStatus_NotPlanned.WorkshopStatusId) {
        return "-";
      }

      string startLocal = workshopEvent.WhenStartLocal.ToString("d MMM yyyy, h:mm tt");

      if (workshopEvent.TimeZoneIdIana == userInfo.TimeZoneIdIana) return startLocal;

      // Time zone differs from viewing user, so show user time and both in a tooltip.
      DateTime timeInUserTZ = TimeHelper.UtcToTimeZoneId(workshopEvent.WhenStartUtc, userInfo.TimeZoneIdIana).Value.DateTime;
      string startForUser = timeInUserTZ.ToString("d MMM yyyy, h:mm tt");

      return startForUser
        + " <i class=\"far fa-info-circle\" title=\""
        + startLocal + " " + workshopEvent.TimeZoneIdIana.HTMLEncode() + " Time<br>"
        + startForUser + " " + userInfo.TimeZoneIdIana.HTMLEncode() + " Time"
        + "\"></i>";
    }

    public decimal GetCoacheeDeliveryAmount(DbHelper.CoachingSessions.AbleSessionInfo coachingSession) {
      if (coachingSession.CoacheeRevenue == null || coachingSession.ProgramDeliveryPercentage == null) return 0;
      return coachingSession.ComponentPrice.GetValueOrDefault(0) * coachingSession.ProgramDeliveryPercentage.GetValueOrDefault(0);
    }

    public decimal GetWorkshopDeliveryAmount(DbHelper.WorkshopEvents.WorkshopEventInfo workshopEvent) {
      if (workshopEvent.WorkshopRevenue == null || workshopEvent.ProgramDeliveryPercentage == null) return 0;
      return workshopEvent.WorkshopRevenue.GetValueOrDefault(0) * workshopEvent.ProgramDeliveryPercentage.GetValueOrDefault(0);
    }

    public decimal GetConsultingDeliveryAmount(DbHelper.ConsultingItems.ConsultingItemInfo consultingItem) {
      if (consultingItem.ProgramDeliveryPercentage == null) return 0;
      return consultingItem.ItemAmount * consultingItem.ProgramDeliveryPercentage.GetValueOrDefault(0);
    }

    public string FallbackInternalActions() {
      // Actions that display only for internal users (Admins and Coaches) where there are no pending actions.
      string actionsTableRows = "";
      actionsTableRows += GetActionTableRow("Add More Quotes", "Create some more quotes", PathHelper.Pages.QuoteCreate());
      return actionsTableRows;
    }

    public string FallbackExternalActions() {
      // Actions that display only for external users (Clients) where there are no pending actions.
      string actionsTableRows = "";
      actionsTableRows += GetActionTableRow("View your programs", "Explore your programs in detail", PathHelper.Pages.Projects());
      return actionsTableRows;
    }

    public string CheckPendingExternalActions() {
      // Actions that display ONLY for External users (Clients).

      string actionsTableRows = "";

      // Check if user's profile is complete
      if (userInfo.FirstName.IsNullOrEmptyOrWhitespace()
        || userInfo.LastName.IsNullOrEmptyOrWhitespace()
        || userInfo.EmailAddress.IsNullOrEmptyOrWhitespace()) {

        actionsTableRows += GetActionTableRow("Complete Profile", "Complete your profile", PathHelper.Pages.CoachEdit(userInfo.UserId, PathHelper.CoachTabEnum.profile));
      }

      // Check if Client has non-signed quotes
      var quotesToSign = DbHelper.AbleQuotes.GetQuotesToSignForClient(OverviewCoachInfo);
      if (quotesToSign != null) {
        foreach (var q in quotesToSign.InfoList) {
          actionsTableRows += GetActionTableRow(
            "Quote needs attention",
            "You have an outstanding quote that is not signed off <br />"
            + $"<b>{(q.QuoteTitle.IsNullOrEmpty() ? q.ProjectName : q.QuoteTitle)}</b>",
            PathHelper.Pages.QuotePublicView(q.PublicGuid));
        }
      }

      if (userInfo.ClientCompanyId != null) {
        var companyInfo = DbHelper.ClientCompanies.GetCompanyInfoOrNull(userInfo.ClientCompanyId.Value, SessionHelper.GetUserInfoOrNull());
        if (companyInfo != null && (companyInfo.NumberOfStaff == null || companyInfo.NumberOfStaff == 0 || companyInfo.SectorId == null)) {
          actionsTableRows += GetActionTableRow(
            "Complete Company Profile",
            "Complete your company profile",
            PathHelper.Pages.OrganisationSettings(userInfo.ClientCompanyId));
        }
      }

      return actionsTableRows;
    }

    public void GetPartnerActions() {

      var partnerActionsInfo = DbHelper.AbleUser.GetPartnerActionsInfo(userInfo.UserId);

      if (partnerActionsInfo == null) return;

      // These are the actions found that Partner must get done
      var actionsRequiredByPartner = new List<PartnerActionType>();

      if (!OverviewCoachInfo.HasContract && OverviewCoachInfo.OrgId == ConfigHelper.IntegralTenantOrgId) {
        actionsRequiredByPartner.Add(PartnerActionType.CompleteContract);
      }

      if (!OverviewCoachInfo.HasTechnicalOnboardingTag) {
        actionsRequiredByPartner.Add(PartnerActionType.DoTechnicalOnboardingTag);
      }

      if (!OverviewCoachInfo.HasOrientationTag && OverviewCoachInfo.OrgId == ConfigHelper.IntegralTenantOrgId) {
        actionsRequiredByPartner.Add(PartnerActionType.DoOrientationTag);
      }

      // Check if user's profile is complete, this contemplates fields like: City, County, Timezone
      if (!partnerActionsInfo.HasCompletedProfile) {
        actionsRequiredByPartner.Add(PartnerActionType.CompleteProfile);
      }

      // If user has not been invited to join the platform, check if the user is the OrgOwnerUserId of a company, if so, check that the fields CompanyName, WebsideUrl are filled and that there's a CompanyLogo
      if (!partnerActionsInfo.HasBeenInvitedToThePlatform) {
        // to do
        //ActionsRequiredByPartner.Add(PartnerActionTypeEnum.SetupCompany);
      }

      if (partnerActionsInfo.HasParticipantsWithoutCoach) {
        actionsRequiredByPartner.Add(PartnerActionType.AssignCoachToParticipants);
      }

      if (partnerActionsInfo.HasStuckParticipants) {
        actionsRequiredByPartner.Add(PartnerActionType.UnstuckParticipants);
      }

      // Has quotes with more money than the components in the project/programs
      if (partnerActionsInfo.HasQuotesProgramSetup) {
        actionsRequiredByPartner.Add(PartnerActionType.SetupProgramQuotes);
      }

      if (partnerActionsInfo.HasWorkshopsToConfirm) {
        actionsRequiredByPartner.Add(PartnerActionType.ConfirmWorkshops);
      }

      // If user doesn't have participants, instruct them to create a coaching program.
      if (!partnerActionsInfo.HasParticipants) {
        actionsRequiredByPartner.Add(PartnerActionType.CreateCoachingProgram);
      }

      // Check if user has Profile Picture
      if (PathHelper.Images.UserPhoto(SessionHelper.UserInfo, PathHelper.Images.UserPhotoSize.Thumbnail, false).IsNullOrEmpty()) {
        actionsRequiredByPartner.Add(PartnerActionType.UploadProfilePicture);
      }

      // If user wasn't invited to the platform themselves. Check if they have invited anyone.
      if (!partnerActionsInfo.HasInvitedUsersToCompany) {
        actionsRequiredByPartner.Add(PartnerActionType.InviteTeam);
      }

      // If user hasn't created microlearning.
      if (!partnerActionsInfo.HasCreatedMicrolearnings) {
        actionsRequiredByPartner.Add(PartnerActionType.CreateMicroleaerning);
      }

      // If user hasn't created project/programs.
      if (!partnerActionsInfo.HasCreatedProjects) {
        actionsRequiredByPartner.Add(PartnerActionType.CreateProgram);
      }

      // Check if user owns quotes
      if (!partnerActionsInfo.HasQuotes) {
        actionsRequiredByPartner.Add(PartnerActionType.CreateQuote);
      }

      // Check if user has quotes that require attention
      if (partnerActionsInfo.HasQuotesNeedingAttention) {
        actionsRequiredByPartner.Add(PartnerActionType.QuotesNeedAttention);
      }

      GetActionsRequiredByPartnerForHtml(actionsRequiredByPartner);
    }

    public void GetActionsRequiredByPartnerForHtml(List<PartnerActionType> actionsRequiredByPartner) {

      // Return if no actions were found
      if (actionsRequiredByPartner.IsNullOrEmpty()) return;

      ActionsRequiredByPartnerForHtml = new List<PartnerAction>();

      string caretHtml = "<i class=\"fas fa-caret-right breadcrumb-separator\"></i>";

      foreach (var actionType in actionsRequiredByPartner) {

        if (ActionsTypesRequireSearchDatabase.Contains(actionType)) {

          if (actionType == PartnerActionType.AssignCoachToParticipants) {

            var coacheesWithoutCoach = DbHelper.AlbertCoachees.GetActions_MissingCoach(OverviewCoachInfo.UserId);

            foreach (var coachee in coacheesWithoutCoach) {
              ActionsRequiredByPartnerForHtml.Add(
                new PartnerAction() {
                  Title = "Assign Coach to Participant",
                  Description = $"<b>{coachee.FullName.HTMLEncode()}</b><br/>{coachee.CompanyName.HTMLEncode()}"
                     + $" {caretHtml} {coachee.ProjectName.HTMLEncode()} ({coachee.JobNumber})"
                     + $" {caretHtml} {coachee.ProgramName.HTMLEncode()}",
                  DescriptionIsHtml = true,
                  Url = PathHelper.Pages.CoacheeEdit(coachee.CoacheeId, PathHelper.CoacheeTabEnum.coaching),
                  IconPath = PathHelper.Images.TeamIconAction(),
                  ActionText_Card = "See Participant Profile",
                  ActionText_Table = ""
                }
              );
            }

          } else if (actionType == PartnerActionType.UnstuckParticipants) {

            var stuckCoachees = DbHelper.AlbertCoachees.GetActions_StuckCoachees(OverviewCoachInfo.UserId);

            foreach (var coachee in stuckCoachees) {
              ActionsRequiredByPartnerForHtml.Add(
                new PartnerAction() {
                  Title = "Book Session",
                  Description = "<b>" + coachee.FullName + "</b> has not booked for " + coachee.DaysSinceBooking + " days. Please follow up manually.",
                  DescriptionIsHtml = true,
                  Url = PathHelper.Pages.CoacheeEdit(coachee.CoacheeId, PathHelper.CoacheeTabEnum.coaching),
                  IconPath = PathHelper.Images.SupportIconAction(),
                  ActionText_Card = "Follow up",
                  ActionText_Table = ""
                }
              );
            }

          } else if (actionType == PartnerActionType.QuotesNeedAttention) {

            var quotesByUser = DbHelper.AbleQuotes.GetQuotesByOwner(OverviewCoachInfo.UserId);

            if (!quotesByUser.IsNullOrEmpty()) {

              foreach (var quote in quotesByUser) {

                if ((quote.ClientAcceptedAmount == 0 || quote.ClientAcceptedAmount == null)
                  && quote.QuoteStatusId != DbHelper.AbleQuoteStatus.GetStatus(DbHelper.AbleQuoteStatus.AppTagEnum.lost).QuoteStatusId
                  && quote.QuoteStatusId != DbHelper.AbleQuoteStatus.GetStatus(DbHelper.AbleQuoteStatus.AppTagEnum.accepted).QuoteStatusId) {
                  string actionDescription = "";

                  if ((DateTime.UtcNow - quote.CreatedUtc).Days <= 90) {

                    actionDescription = "You have an outstanding quote that is not signed off <br />" +
                      $"<b>{(quote.QuoteTitle.IsNullOrEmpty() ? quote.ProjectName.HTMLEncode() : quote.QuoteTitle.HTMLEncode())}</b>";

                  } else {

                    actionDescription = "You have an outstanding quote that is > 90 days, follow up recommended or set quote to 'Lost'<br />" +
                      $"<b>{(quote.QuoteTitle.IsNullOrEmpty() ? quote.ProjectName.HTMLEncode() : quote.QuoteTitle.HTMLEncode())}</b>";
                  }

                  ActionsRequiredByPartnerForHtml.Add(
                    new PartnerAction() {
                      Title = "Quote needs attention",
                      Description = actionDescription,
                      DescriptionIsHtml = true,
                      Url = PathHelper.Pages.QuoteDetails(quote.PublicGuid),
                      IconPath = PathHelper.Images.QuoteIconAction(),
                      ActionText_Card = "Go to quote",
                      ActionText_Table = ""
                    }
                  );
                }
              }
            }

          } else if (actionType == PartnerActionType.SetupProgramQuotes) {

            var QuotesProjectSetup = DbHelper.AbleQuotes.GetActions_QuotesProgramSetup(OverviewCoachInfo.UserId);
            if (QuotesProjectSetup != null) {

              foreach (var q in QuotesProjectSetup) {
                ActionsRequiredByPartnerForHtml.Add(
                  new PartnerAction() {
                    Title = "Set up Project after signing",
                    Description = $"Quote items total is different than Components <br><b>{q.ProjectName.HTMLEncode()} {caretHtml} {q.ProgramName.HTMLEncode()}</b>",
                    DescriptionIsHtml = true,
                    Url = PathHelper.Pages.QuoteDetails(q.QuotePublicGuid),
                    IconPath = PathHelper.Images.QuoteIconAction(),
                    ActionText_Card = "Go to quote",
                    ActionText_Table = ""
                  }
                );
              }
            }

          } else if (actionType == PartnerActionType.ConfirmWorkshops) {

            var workshopsToConfirm = DbHelper.WorkshopEvents.GetActions_WorkshopsToConfirm(OverviewCoachInfo.UserId);
            if (workshopsToConfirm != null) {

              foreach (var w in workshopsToConfirm) {
                ActionsRequiredByPartnerForHtml.Add(
                  new PartnerAction() {
                    Title = "Confirm Workshop",
                    Description = $"<b>{w.WorkshopStatusName.HTMLEncode()} {caretHtml}"
                       + $" {(w.StartDate.HasValue ? (w.StartDate.ToString("d MMM yyyy") + caretHtml) : "")} {w.WorkshopTitle.HTMLEncode()}</b><br>"
                       + $" {w.CompanyName.HTMLEncode()} {caretHtml} {w.ProjectName.HTMLEncode()} ({w.JobNumber}) {caretHtml} {w.ProgramName.HTMLEncode()}",
                    DescriptionIsHtml = true,
                    Url = PathHelper.Pages.Workshops_Edit(w.ProgramJobId, w.WorkshopEventId),
                    IconPath = PathHelper.Images.WorkshopIconAction(),
                    ActionText_Card = "Go to workshop",
                    ActionText_Table = ""
                  }
                );
              }
            }
          }

        } else {

          ActionsRequiredByPartnerForHtml.Add(GetPartnerAction(actionType));
        }
      }
    }

    public string GetPartnerActionCards() {

      if (ActionsRequiredByPartnerForHtml.IsNullOrEmpty()) return "";

      var actionCardList = new List<WebHelper.ParticipantActionCard>();
      int actionsAdded = 0;

      foreach (var actionInfo in ActionsRequiredByPartnerForHtml) {

        actionCardList.Add(new WebHelper.ParticipantActionCard(
          headerText: actionInfo.Title,
          descriptionText: actionInfo.Description,
          actionText: actionInfo.ActionText_Card,
          iconPath: actionInfo.IconPath,
          iconClass: "",
          linkUrl: actionInfo.Url,
          targetNewTab: WebHelper.TargetNewTab.No
        ));

        actionsAdded++;
        if (actionsAdded == MaxActionsForCards) break;
      }

      var html = new StringBuilder();

      if (!actionCardList.IsNullOrEmpty()) {

        string actionCardHtml = "";
        foreach (var actionCard in actionCardList) {
          actionCardHtml += WebHelper.GetParticipantActionCard(actionCard);
        }

        html.Append($@"
          <div class=""mb10 action-cards-container action-cards-container-row"">
            {actionCardHtml}
          </div>");
      }

      return html.ToString();
    }

    public string CheckPendingInternalActions() {
      // Actions that display ONLY for Internal users (Admins and Coaches).
      if (ActionsRequiredByPartnerForHtml.IsNullOrEmpty()) return "";

      // If actions are less than MaxActionsForCards, then all have been displayed in cards and there's nothing for to display on the table
      if (ActionsRequiredByPartnerForHtml.Count < MaxActionsForCards - 1) return "";

      string actionsTableRows = string.Empty;

      // Start counting from the action where the Card actions finished
      for (int i = MaxActionsForCards; i < ActionsRequiredByPartnerForHtml.Count; i++) {

        actionsTableRows += GetActionTableRow(ActionsRequiredByPartnerForHtml[i]);
      }

      return actionsTableRows;
    }

    public string GetActionTableRow(string Action, string Detail, string url) {
      // User for Client Actions
      string newRow = "";
      newRow += "<tr tabindex=\"0\" class=\"rowData gotodetailpage\" data-href=\"" + url + "\">";
      newRow += "<td class=\"type-fullname\">" + Action + "</td>";
      newRow += "<td class=\"type-action-description\">" + Detail + "</td>";
      newRow += "</tr>";
      return newRow;
    }

    public string GetActionTableRow(PartnerAction partnerAction) {

      bool isExternalUrl = PathHelper.IsExternalUrl(partnerAction.ActionUrl_Table);

      // Use for Partners Actions
      string rowHtml = "";

      rowHtml += $"<tr tabindex=\"0\" class=\"rowData gotodetailpage\" data-href=\"{partnerAction.Url.HTMLEncode()}\">";
      rowHtml += $"<td class=\"type-eventtypeicon\"><img class=\"action-card-icon float-right \" src=\"{partnerAction.IconPath}\"></td>";
      rowHtml += $"<td class=\"type-fullname\"><b>{partnerAction.Title.HTMLEncode()}</b></td>";
      rowHtml += $"<td class=\"type-action-description\"> {(partnerAction.DescriptionIsHtml ? partnerAction.Description : partnerAction.Description.HTMLEncode())}";
      if (!partnerAction.ActionText_Table.IsNullOrEmpty()) {
        rowHtml += "<div class=\"font-size-07rem mt5\">";
        if (!partnerAction.ActionUrl_Table.IsNullOrEmpty()) {
          rowHtml += $"<a class=\"flex flex-align-center gap5\" href=\"{partnerAction.ActionUrl_Table.HTMLEncode()}\" {(isExternalUrl ? $"target=\"_blank\"" : "")}>";
        }
        rowHtml += $"{partnerAction.ActionText_Table.HTMLEncode()}";
        if (!partnerAction.ActionUrl_Table.IsNullOrEmpty()) {
          if (isExternalUrl) rowHtml += WebHelper.Icon.ExternalLinkRegular.HTML;
          rowHtml += $"</a>";
        }
        rowHtml += "</div>";
      }
      rowHtml += "</td>";
      rowHtml += $"<td class=\"type-eventtypeicon\"><img class=\"action-card-icon float-right \" src=\"{PathHelper.Images.ActionsArrowForwardIcon()}\"></td>";
      rowHtml += "</tr>";

      return rowHtml;
    }

    public string GetPartnerMetricsHtml() {

      var metricsInfo = DbHelper.AlbertCoaches.GetPartnerMetricsInfo(userInfo.UserId);
      if (metricsInfo == null) return string.Empty;

      var html = new StringBuilder();

      // Earnings
      html.Append(
        WebHelper.GetOverviewScoreBox(
          "Earnings",
          "Year Earnings (From July) / Lifetime Earnings.",
          new WebHelper.OverviewBoxScore(
            WebHelper.GetAmountCurrencyFormat(metricsInfo.YearTotalEarnings, true),
            " / " + WebHelper.GetAmountCurrencyFormat(metricsInfo.LifetimeTotalEarnings, true))));

      // To be delivered
      html.Append(
        WebHelper.GetOverviewScoreBox(
          "Future Delivery",
          IncomeToolbox(
            $"Total future delivery is {WebHelper.GetAmountCurrencyFormat(metricsInfo.TotalRevenueToDeliver, true)}",
            metricsInfo.TotalRevToDeliverAsPartner.GetValueOrDefault(0),
            metricsInfo.TotalRevToDeliverAsPLC.GetValueOrDefault(0),
            metricsInfo.TotalRevToDeliverAsSales.GetValueOrDefault(0)),
          new WebHelper.OverviewBoxScore(WebHelper.GetAmountCurrencyFormat(metricsInfo.TotalRevenueToDeliver, true))));

      // Payruns
      html.Append(
        WebHelper.GetOverviewScoreBox(
          "Upcoming Payrun",
          IncomeToolbox($"Total Upcoming Payrun is {WebHelper.GetAmountCurrencyFormat(metricsInfo.TotalUpcomingPayrun, true)}",
            metricsInfo.TotalUpcomingPayrunAsPartner.GetValueOrDefault(0),
            metricsInfo.TotalUpcomingPayrunAsPLC.GetValueOrDefault(0),
            metricsInfo.TotalUpcomingPayrunAsSales.GetValueOrDefault(0)),
          null, PathHelper.Pages.OverviewPayruns(),
          new WebHelper.OverviewBoxScore(WebHelper.GetAmountCurrencyFormat(metricsInfo.TotalUpcomingPayrun, true))));

      // Deals Won
      html.Append(
        WebHelper.GetOverviewScoreBox(
          "Deals Won",
          "Deals Won Lifetime",
          new WebHelper.OverviewBoxScore(WebHelper.GetAmountCurrencyFormat(metricsInfo.TotalDealsWon, true))));

      // Sessions
      html.Append(
        WebHelper.GetOverviewScoreBox(
          "Coaching Sessions",
          "Sessions to Deliver / Total Lifetime Sessions.",
          new WebHelper.OverviewBoxScore(
            metricsInfo.TotalSessionsToBeDelivered.ToString(),
            " / " + metricsInfo.TotalSessions.ToString())));

      // Workshops
      html.Append(
        WebHelper.GetOverviewScoreBox(
        "Workshops",
        "Workshops to Deliver / Total Lifetime Workshops.",
        new WebHelper.OverviewBoxScore(
          metricsInfo.TotalWorkshopsToBeDelivered.ToString(),
          " / " + metricsInfo.TotalWorkshops.ToString())));

      return html.ToString();
    }

    private string IncomeToolbox(string firstLine, decimal totalPartner, decimal totalPLC, decimal totalSales) {

      string tooltipTextHtml = firstLine + "<br/>";
      string taxInfoHtml = "";

      if (totalPartner > 0 || totalPLC > 0 || totalSales > 0) {
        tooltipTextHtml += $"<br/>Which splits as follows per role:";
        taxInfoHtml = "<br/><br/>Please note that the amounts shown are before taxes and fees.";
      }
      if (totalPartner > 0) {
        tooltipTextHtml += $"<br/><b>Delivery:<b/> {WebHelper.GetAmountCurrencyFormat(totalPartner, true)}.";
      }
      if (totalPLC > 0) {
        tooltipTextHtml += $"<br/><b>PLC:<b/> {WebHelper.GetAmountCurrencyFormat(totalPLC, true)}.";
      }
      if (totalSales > 0) {
        tooltipTextHtml += $"<br/><b>Sales:<b/> {WebHelper.GetAmountCurrencyFormat(totalSales, true)}.";
      }

      return tooltipTextHtml + taxInfoHtml;
    }

    public string GetPersistentModalHtml() {

      if (!ShowBlockingModal) return "";

      string html = "";

      if (SessionHelper.IsUserRoleClient) {
        html = $@"
          <div class=""action-info""> Hi {userInfo.FirstName}, <br/>
          Please set up a meeting with a sales representative:</div>

          <iframe
          src=""https://calendly.com/integral-client-team/30mins""
          width=""100%""
          height=""70vh""
          frameborder=""0""
          scrolling=""no""
          style=""border: none;""></iframe>";
      }

      return html;
    }

    private Dictionary<PartnerActionType, PartnerAction> InitPartnerActions() =>
      new Dictionary<PartnerActionType, PartnerAction>() {
        {
          PartnerActionType.CompleteProfile, new PartnerAction() {
            Title = "Complete Your Profile",
            Description = "Complete your profile and bio so your clients know who your are.",
            Url = PathHelper.Pages.CoachEdit(SessionHelper.GetUserIdOrNull(), PathHelper.CoachTabEnum.profile),
            IconPath = PathHelper.Images.ProfileIconAction(),
            ActionText_Card = "Complete Profile",
            ActionText_Table = "Learn more about your profile",
            ActionUrl_Table = "https://help.helloable.co/en/articles/11960946-create-a-practitioner-account"
          }
        },
        {
          PartnerActionType.SetupCompany, new PartnerAction() {
            Title = "Setup Your Company",
            Description = "Setup your Company details and add your company logo.",
            Url = PathHelper.Pages.CoachEdit(SessionHelper.GetUserIdOrNull(), PathHelper.CoachTabEnum.company),
            IconPath = PathHelper.Images.CompanyIconAction(),
            ActionText_Card = "Setup Company",
            ActionText_Table = "Learn more about your company",
            ActionUrl_Table = "https://help.helloable.co/en/articles/11960965-setup-your-company"
          }
        },
        {
          PartnerActionType.CreateCoachingProgram, new PartnerAction {
            Title = "Create a Coaching Program",
            Description = "Create a coaching program for one of your clients and enhance their experience with AI generated coaching, nudges and pulse surveys.",
            Url = PathHelper.Pages.CoacheesQuickAdd(),
            IconPath = PathHelper.Images.ProjectIconAction(),
            ActionText_Card = "Create Program",
            ActionText_Table = "Learn more about Coaching Programs",
            ActionUrl_Table = "https://help.helloable.co/en/articles/11967879-create-a-simple-coaching-program"
          }
        },
        {
          PartnerActionType.UploadProfilePicture, new PartnerAction {
            Title = "Upload a Profile Photo",
            Description = "Upload a profile pic so your clients can see your great hair.",
            Url = PathHelper.Pages.CoachEdit(SessionHelper.GetUserIdOrNull(), PathHelper.CoachTabEnum.profile),
            IconPath = PathHelper.Images.ProfileIconAction(),
            ActionText_Card = "Upload Photo",
            ActionText_Table = "Learn more about your profile",
            ActionUrl_Table = "https://help.helloable.co/en/articles/11960946-create-a-practitioner-account"
          }
        },
        {
          PartnerActionType.InviteTeam, new PartnerAction {
            Title = "Invite your Team",
            Description = "Bring your team together in Able. Collaborate with your team and other Partners to manage and deliver client projects together.",
            Url = PathHelper.Pages.CoachEdit(SessionHelper.GetUserIdOrNull(), PathHelper.CoachTabEnum.partners),
            IconPath = PathHelper.Images.TeamIconAction(),
            ActionText_Card = "Invite Team",
            ActionText_Table = "Learn more about Partners",
            ActionUrl_Table = "https://help.helloable.co/en/articles/11967887-invite-your-team"
          }
        },
        {
          PartnerActionType.CreateMicroleaerning, new PartnerAction {
            Title = "Create Microlearning",
            Description = "Create, publish and share learning content for a specific program or with all of your programs and participants.",
            Url = PathHelper.Pages.ContentDetails_AddContent(),
            IconPath = PathHelper.Images.MicrolearningIconAction(),
            ActionText_Card = "Create Microlearning",
            ActionText_Table = "Learn more about Microlearning",
            ActionUrl_Table = "https://help.helloable.co/en/articles/11967906-create-microlearning"
          }
        },
        {
          PartnerActionType.CreateProgram, new PartnerAction {
            Title = "Create a Program",
            Description = "Create a blended program for your clients. Add group workshops, 1:on:1 coaching, AI-nudges and 360º surveys for one seamless high impact learning experience.",
            Url = PathHelper.Pages.Projects_Add(),
            IconPath = PathHelper.Images.ProjectIconAction(),
            ActionText_Card = "Create Program",
            ActionText_Table = "Learn more about Programs",
            ActionUrl_Table = "https://help.helloable.co/en/articles/11967919-create-a-program"
          }
        },
        {
          PartnerActionType.CreateQuote, new PartnerAction {
            Title = "Create a Quote",
            Description = "Cost your program, create a quote for your client and manage your project income and expenses.",
            Url = PathHelper.Pages.QuoteCreate(),
            IconPath = PathHelper.Images.QuoteIconAction(),
            ActionText_Card = "Create Quote",
            ActionText_Table = "Learn more about quotes",
            ActionUrl_Table = "https://help.helloable.co/en/articles/12007475-create-a-quote"
          }
        },
        {
          PartnerActionType.CompleteCompanyProfile, new PartnerAction {
            Title = "Complete Company Profile",
            Description = "Complete your company profile.",
            Url = PathHelper.Pages.OrganisationSettings(SessionHelper.GetUserInfoOrNull().ClientCompanyId),
            IconPath = PathHelper.Images.CompanyIconAction(),
            ActionText_Card = "Complete profile",
            ActionText_Table = "",
            ActionUrl_Table = ""
          }
        },
        {
          PartnerActionType.CompleteContract, new PartnerAction {
            Title = "Complete your Contract",
            Description = "You don't seem to have a contract. To be paid out, please complete the contract.",
            Url = PathHelper.Pages.CoachEdit(SessionHelper.GetUserIdOrNull(), PathHelper.CoachTabEnum.contract),
            IconPath = PathHelper.Images.ContractIconAction(),
            ActionText_Card = "Complete Contract",
            ActionText_Table = "",
            ActionUrl_Table = ""
          }
        },
        {
          PartnerActionType.DoTechnicalOnboardingTag, new PartnerAction {
            Title = "Let us help you get started",
            Description = "Arrange a call with our friendly support team and we’ll get you set up in under an hour.",
            Url = ConfigHelper.ExternalUrls.CalendlySupportBooking,
            IconPath = PathHelper.Images.SupportIconAction(),
            ActionText_Card = "Complete Onboarding",
            ActionText_Table = "Lean more about getting started",
            ActionUrl_Table = "https://help.helloable.co/en/articles/12095276-arrange-a-support-call-to-get-started"
          }
        },
        {
          PartnerActionType.DoOrientationTag, new PartnerAction {
            Title = "Complete Orientation",
            Description = "You seem to have not completed orientation. Click here to schedule your orientation.",
            Url = ConfigHelper.ExternalUrls.PartnerAction_OrientationNotionUrl,
            IconPath = PathHelper.Images.SupportIconAction(),
            ActionText_Card = "Schedule Orientation",
            ActionText_Table = "",
            ActionUrl_Table = ""
          }
        }
      };

    public PartnerAction GetPartnerAction(PartnerActionType type) {
      if (!PartnerActions.ContainsKey(type)) return null;
      return PartnerActions[type];
    }

    public class PartnerAction {
      public string Title { get; set; }
      public string Description { get; set; }
      public bool DescriptionIsHtml { get; set; }
      public string Url { get; set; }
      public string IconPath { get; set; }
      public string ActionText_Card { get; set; }
      public string ActionText_Table { get; set; }
      public string ActionUrl_Table { get; set; }
    }
  }
}
