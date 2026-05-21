using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class ParticipantUpcoming : AppCode.PageBaseClasses.LoggedInPageModel {

    public enum EventPeriodEnum { Complete, Overview, Upcoming }
    public enum RowClickAction { None, Redirect, TriggerSlideoutPanel, ShowModal }

    public List<DbHelper.AlbertCoachees.AlbertCoacheeInfo> userCoacheeInfoList;

    public int MaxActions = ConfigHelper.MaxParticipantActions_Upcoming;
    const int AICoachActionCutoffDays = 14; // Add a reminder action if last AI interaction was more than x days ago.
    public const int Module_UpcomingDeadlineDays = 7; // Due date of Module
    public bool ShowBlockingModal;
    public List<ParticipantEventInfo> UserEventList = new List<ParticipantEventInfo>();
    public List<WebHelper.ParticipantActionCard> ActionCardList = new List<WebHelper.ParticipantActionCard>();
    List<DbHelper.AlbertSurveys.SurveyInfo> UserSurveys = null;

    public List<(string, string)> ProjectsUserIsIn = new List<(string, string)>(); // JobNumber / Project title
    public string CurrentJobNumber;
    public class FormFields {
      public const string ProjectJobNumber = "ProjectJobNumber";
    }
    public class AjaxAction {
      public const string ChangeProject = "ChangeProject";
    }
    public class AjaxReturnData {
      public const string UpcomingHtml = "UpcomingHtml";
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "Upcoming";

      userCoacheeInfoList = userInfo.LatestCoacheeInfo == null ? null : DbHelper.AlbertCoachees.GetCoacheesByUserId(userInfo.UserId);
      ShowBlockingModal = !SessionHelper.AppAccess.Users.CanNavigatePlatform();

      if (!userCoacheeInfoList.IsNullOrEmpty()) {
        // Get each Project
        foreach (var pj in userCoacheeInfoList) {
          if (!ProjectsUserIsIn.Exists(x => x.Item1 == pj.ProgramJobNumber)) {
            ProjectsUserIsIn.Add((pj.ProgramJobNumber, pj.FriendlyProjectTitle));
          }
        }
      }

      GetSurveyInfo();

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {

          switch (PageAjaxAction) {

            case AjaxAction.ChangeProject:
              if (ProjectsUserIsIn.IsNullOrEmpty() || ProjectsUserIsIn.Count == 1) {
                return;
              }
              ChangeProject(ajax);
              break;
          }
        });
        return new EmptyResult();

      } else {

        CurrentJobNumber = "";
        if (!ProjectsUserIsIn.IsNullOrEmpty()) {
          CurrentJobNumber = ProjectsUserIsIn[0].Item1;
        }
      }

      return Page();
    }

    public string GetUserActionCards() {

      AddActionForModules();

      if (!userCoacheeInfoList.IsNullOrEmpty()) {
        // Actions based in CoacheeId
        foreach (var userCoacheeInfo in userCoacheeInfoList) {

          AddActionForBooking(userCoacheeInfo);
        }
      }
      // Actions based in UserId
      AddActionForDevPlan();
      AddActionForAICoach();
      AddActionToCompleteProfile();
      AddActionsForSurveys();

      var html = new StringBuilder();
      if (!ActionCardList.IsNullOrEmpty()) {
        html.Append(@"<div class=""table-title"">Actions</div>");

        string actionCardHtml = "";
        foreach (var actionCard in ActionCardList) {
          actionCardHtml += WebHelper.GetParticipantActionCard(actionCard);
        }

        html.Append($@"
          <div class=""mb10 action-cards-container action-cards-container-row"">
            {actionCardHtml}
          </div>");

        if (ActionCardList.Count > MaxActions) {
          html.Append($@"
          <div class=""{(!ProjectsUserIsIn.IsNullOrEmpty() && ProjectsUserIsIn.Count > 1 ? "mb10 " : "")}h30 w100p pr5"">
            <span class=""float-right updateShowActions"" style=""cursor:pointer;"">Show All</span>
          </div>");
        }
      }

      return html.ToString();
    }

    public string GetProjectUserHtml() {
      var html = new StringBuilder();

      GetEventList();

      html.Append(WebHelper.GetPageTabs(
      new WebHelper.PageTabsInfo() { PageTabsStyle = WebHelper.PageTabsStyle.Links },
      new WebHelper.PageTabItem(EventPeriodEnum.Overview.ToString(), "Overview", true),
      new WebHelper.PageTabItem(EventPeriodEnum.Upcoming.ToString(), "Upcoming"),
      new WebHelper.PageTabItem(EventPeriodEnum.Complete.ToString(), "Historic")));

      html.Append(GetTabPanel(EventPeriodEnum.Overview));
      html.Append(GetTabPanel(EventPeriodEnum.Upcoming));
      html.Append(GetTabPanel(EventPeriodEnum.Complete));

      return html.ToString();
    }

    private string GetTabPanel(EventPeriodEnum eventPeriod) {

      string rowsHtml = "";

      var eventList = GetEventsForPeriod(eventPeriod);

      if (eventList.IsNullOrEmpty()) {
        rowsHtml = @"<tr><td colspan=""5""> No records found.</td></tr>";
      } else {

        foreach (var eventInfo in eventList) {
          string rowAttr = "";
          if (eventInfo.RowClickAction == RowClickAction.TriggerSlideoutPanel) {
            rowAttr = WebHelper.GetSlideoutTriggerDataAttributes("Event Information", eventInfo.EventUrl);

          } else if (eventInfo.RowClickAction == RowClickAction.Redirect) {
            rowAttr = $@"data-rowlink-url=""{eventInfo.EventUrl}""";

          } else {
            rowAttr = eventInfo.EventUrl;
          }

          rowsHtml += $@"
            <tr tabindex=""0"" {rowAttr}>
              <td class=""type-eventtypeicon"">{WebHelper.GetUpcomingEventTypeTooltip(eventInfo.EventTypeIconPath, eventInfo.EventIconTooltipText, "")}</td>
              <td class=""type-description"">{GetEventTitleHtml(eventInfo)}</td>
              <td class=""type-user-nameWithAvatar"">{GetProfileImageHtml(eventInfo)}</td>
              <td class=""type-delivery"">{WebHelper.GetDeliveryBadge(eventInfo.IsDeliveryInPerson)}</td>
              {(eventPeriod == EventPeriodEnum.Overview ? $@"<td class=""type-delivery"">{WebHelper.GetStatusBadge(eventInfo.EventPeriod.ToString(), "period-" + eventInfo.EventPeriod.ToString())}</td>" : "")}
              <td class=""type-datetime"">{eventInfo.StartDateHtml}</td>
            </tr>";
        }
      }

      return $@"
        <div class=""tab-panel"" data-appendTo=""panel-{eventPeriod}"">
          <div class=""table-responsive"">
            <table class=""table table-bordered table-hover table-rowlink"" data-rowlink-url="""">
              <thead>
                <tr>
                  <th class=""type-eventtypeicon""></th>
                  <th class=""type-description"">Title</th>
                  <th class=""type-user-nameWithAvatar"">Practitioner</th>
                  <th class=""type-delivery"">Delivery</th>
                  {(eventPeriod == EventPeriodEnum.Overview ? $@"<th class=""type-delivery"">Status</th>" : "")}
                  <th class=""type-datetime"">Date &amp; Time</th>
                </tr>
              </thead>
              <tbody>
                {rowsHtml}
              </tbody>
            </table>
          </div>
        </div>";
    }

    public void ChangeProject(AjaxSubmitHelper ajax) {
      CurrentJobNumber = WebHelper.GetFormValue(FormFields.ProjectJobNumber, "");
      ajax.AddReturnValue(AjaxReturnData.UpcomingHtml, GetProjectUserHtml());
    }

    public string GetUserProjectOptions() {

      if (ProjectsUserIsIn.IsNullOrEmpty()) return "";
      if (ProjectsUserIsIn.Count == 1) return $@"<div class=""table-title project-title"">{ProjectsUserIsIn[0].Item2.HTMLEncode()}</div>";

      string projectOptions = "";
      foreach (var project in ProjectsUserIsIn) {
        projectOptions += $"<option {(CurrentJobNumber == project.Item1 ? "selected" : "")} value=\"{project.Item1}\">{project.Item2}</option>";
      }
      return WebHelper.GetSelect(FormFields.ProjectJobNumber, projectOptions);
    }

    void GetSurveyInfo() {

      if (!UserSurveys.IsNullOrEmpty()) return;

      UserSurveys = DbHelper.AlbertSurveys.GetSurveyInfoListForUser(userInfo.UserId, DbHelper.AlbertSurveys.OnlyViewerCompatible.No);
    }

    void AddActionsForSurveys() {

      if (UserSurveys == null) return;

      foreach (var survey in UserSurveys) {

        if (survey.FoundParticipantBrief == null || survey.IsClosed || (survey.FoundParticipantBrief?.IsSurveyAvailable ?? false) == false) continue;

        if (survey.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Pulse360) {

          ActionCardList.Add(new WebHelper.ParticipantActionCard(
            headerText: "Update your monthly Goals",
            descriptionText: "Complete your monthly Pulse: set your top three goals for the month and reflect on your growth.",
            actionText: "Update pulse",
            iconPath: PathHelper.Images.PulseIcon(),
            iconClass: WebHelper.Icon.ActionCardIconClass.Pulse,
            linkUrl: PathHelper.Pages.Survey(survey, survey.FoundParticipantBrief.PartUniqueId),
            targetNewTab: WebHelper.TargetNewTab.No
          ));

        } else if (survey.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Intake) {

          ActionCardList.Add(new WebHelper.ParticipantActionCard(
            headerText: "Get started on your Learning Journey",
            descriptionText: "Complete your intake form to helps us tailor the program experience to your personal goals.",
            actionText: "Complete form",
            iconPath: PathHelper.Images.DocumentTextIcon(),
            iconClass: WebHelper.Icon.ActionCardIconClass.Intake,
            linkUrl: PathHelper.Pages.Survey(survey, survey.FoundParticipantBrief.PartUniqueId),
            targetNewTab: WebHelper.TargetNewTab.No
          ));

        } else if (survey.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Eval && survey.EvalType == DbHelper.AlbertSurveys.EvalTypeEnum.Coaching) {

          ActionCardList.Add(new WebHelper.ParticipantActionCard(
            headerText: "Let us know your feedback",
            descriptionText: "Help us improve your program by completing your post-session feedback and evaluation.",
            actionText: "Give feedback",
            iconPath: PathHelper.Images.PeopleCircleIcon(),
            iconClass: WebHelper.Icon.ActionCardIconClass.CoachingEval,
            linkUrl: PathHelper.Pages.Survey(survey, survey.FoundParticipantBrief.PartUniqueId),
            targetNewTab: WebHelper.TargetNewTab.No
          ));

        } else if (survey.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Eval && survey.EvalType == DbHelper.AlbertSurveys.EvalTypeEnum.Workshop) {

          ActionCardList.Add(new WebHelper.ParticipantActionCard(
            headerText: "Let us know your feedback",
            descriptionText: "Help us improve your program by completing your post-workshop feedback and evaluation.",
            actionText: "Give feedback",
            iconPath: PathHelper.Images.SchoolIcon(),
            iconClass: WebHelper.Icon.ActionCardIconClass.WorkshopEval,
            linkUrl: PathHelper.Pages.Survey(survey, survey.FoundParticipantBrief.PartUniqueId),
            targetNewTab: WebHelper.TargetNewTab.No
          ));

        } else if (survey.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Standard360) {

          if (survey.IsSelfOnly) {

            ActionCardList.Add(new WebHelper.ParticipantActionCard(
              headerText: "Assess Your Own Leadership Skills",
              descriptionText: "Complete your leadership development profile and accelerate your professional growth.",
              actionText: "Complete survey",
              iconPath: PathHelper.Images.CompassIcon(),
              iconClass: WebHelper.Icon.ActionCardIconClass.Profile,
              linkUrl: PathHelper.Pages.Survey(survey, survey.FoundParticipantBrief.PartUniqueId),
              targetNewTab: WebHelper.TargetNewTab.No
            ));

          } else {

            ActionCardList.Add(new WebHelper.ParticipantActionCard(
              headerText: "Grow with Feedback",
              descriptionText: "Complete your capability profile and accelerate your professional growth.",
              actionText: "Complete survey",
              iconPath: PathHelper.Images.CompassIcon(),
              iconClass: WebHelper.Icon.ActionCardIconClass.Profile,
              linkUrl: PathHelper.Pages.Survey(survey, survey.FoundParticipantBrief.PartUniqueId),
              targetNewTab: WebHelper.TargetNewTab.No
            ));

          }

        } else {

          ActionCardList.Add(new WebHelper.ParticipantActionCard(
            headerText: "Grow with Feedback",
            descriptionText: "Complete your leadership development profile and accelerate your professional growth.",
            actionText: "Complete survey",
            iconPath: PathHelper.Images.CompassIcon(),
            iconClass: WebHelper.Icon.ActionCardIconClass.Profile,
            linkUrl: PathHelper.Pages.Survey(survey, survey.FoundParticipantBrief.PartUniqueId),
            targetNewTab: WebHelper.TargetNewTab.No
          ));
        }
      }
    }

    void AddActionForBooking(DbHelper.AlbertCoachees.AlbertCoacheeInfo userCoacheeInfo) {

      if (userCoacheeInfo == null) return;

      bool remindBookNextSession =
        userCoacheeInfo != null &&
        userCoacheeInfo.UserActivity?.SessionsBooked < userCoacheeInfo.UserActivity?.SessionsAllocated &&
        userCoacheeInfo.UserActivity?.SessionsUpcoming == 0 &&
        userCoacheeInfo.CoachUserId != ConfigHelper.UserId.Unassigned;

      if (!remindBookNextSession) return;

      string descriptionText;
      if (userCoacheeInfo.UserActivity?.SessionsBooked == 0) {
        descriptionText = $"Meet your coach and book your first 1-on-1 coaching session with {userCoacheeInfo.UserActivity.CoachFirstName} {userCoacheeInfo.UserActivity.CoachLastName}.";
      } else {
        descriptionText = $"Book your next 1-on-1 coaching session with your coach {userCoacheeInfo.UserActivity.CoachFirstName} {userCoacheeInfo.UserActivity.CoachLastName}.";
      }

      string cardLinkUrl = PathHelper.Pages.CoacheeSessionBooking(userCoacheeInfo.CoacheeUID);

      if (!ActionCardList.Exists(x => x.LinkUrl == cardLinkUrl && x.DescriptionText == descriptionText)) {
        ActionCardList.Add(new WebHelper.ParticipantActionCard(
          headerText: "Develop with Leadership Coaching",
          descriptionText: descriptionText,
          actionText: "Book session",
          iconPath: PathHelper.Images.UserPhoto(userCoacheeInfo, PathHelper.Images.UserPhotoSize.Thumbnail, true),
          iconClass: WebHelper.Icon.ActionCardIconClass.None,
          linkUrl: cardLinkUrl,
          targetNewTab: WebHelper.TargetNewTab.No
        ));
      }
    }

    void AddActionForModules() {

      if (!SessionHelper.AppAccess.PageAccess.CanAccessModule()) return;

      DbHelper.Modules.ParticipantModuleInfo participantModuleInfo = null;

      DbHelper.Common.UsingTransaction(trans => {

        try {

          participantModuleInfo = DbHelper.Modules.GetParticipantModuleForActionCard(trans, userInfo.UserId);
          return true;

        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("AddActionForModules_GetParticipantModuleForActionCard")
            .FromSession()
            .Track();

          return false;
        }
      });

      string headerText = "", descriptionText = "", actionText = "", cardLinkUrl = "";

      if (participantModuleInfo == null) {
        // User is not enrolled to any
        headerText = "Find Your Next Learning Adventure";
        descriptionText = "Explore our library and something that inspires you. Take the first step today.";
        actionText = "Browse modules";
        cardLinkUrl = PathHelper.Pages.Content();

      } else if (participantModuleInfo.IsLinkedToProgram && !participantModuleInfo.IsUserEnrolled) {
        // Is pending to enrol
        headerText = "Start Your Learning Journey";
        descriptionText = "A new module is waiting for you! Join now to unlock content designed specifically for your growth path.";
        actionText = "Join module";
        cardLinkUrl = PathHelper.Pages.Module(participantModuleInfo.ModuleInfo.ModuleGuid);

      } else {
        // User is enroled and has pending lessons
        headerText = "Keep Up the Momentum";
        descriptionText = $"You have completed {participantModuleInfo.CompletedPercentage}% of your module. Pick up where you left off and finish strong!";
        actionText = "Continue learning";
        cardLinkUrl = PathHelper.Pages.Module(participantModuleInfo.ModuleInfo.ModuleGuid);
      }

      ActionCardList.Add(new WebHelper.ParticipantActionCard(
        headerText: headerText,
        descriptionText: descriptionText,
        actionText: actionText,
        iconPath: PathHelper.Images.ModuleIcon(),
        iconClass: WebHelper.Icon.ActionCardIconClass.Module,
        linkUrl: cardLinkUrl,
        targetNewTab: WebHelper.TargetNewTab.No
      ));
    }

    void AddActionForDevPlan() {

      if (!SessionHelper.AppAccess.PageAccess.CanAccessDevelopmentPlan()) return;

      var plansForUser = DbHelper.DevelopmentPlans.GetPlansForUser(SessionHelper.GetUserIdOrNull().Value, DbHelper.DevelopmentPlans.GetPlansStatus.OnlyOpen);

      if (!plansForUser.IsNullOrEmpty()) return;

      string cardLinkUrl = PathHelper.Pages.DevelopmentPlan();

      if (!ActionCardList.Exists(x => x.LinkUrl == cardLinkUrl)) {
        ActionCardList.Add(new WebHelper.ParticipantActionCard(
        headerText: "Define your Personal Development Goals",
        descriptionText: "Complete your leadership development plan and pick actions to help you grow.",
        actionText: "Complete plan",
        iconPath: PathHelper.Images.GoalIcon(),
        iconClass: WebHelper.Icon.ActionCardIconClass.DevPlan,
        linkUrl: cardLinkUrl,
        targetNewTab: WebHelper.TargetNewTab.No
      ));
      }
    }

    void AddActionForAICoach() {

      if (!SessionHelper.AppAccess.PageAccess.CanAccessParticipantAICoach()) return;

      var participantLatestAIMessages = DbHelper.AIMessage.GetUserLatestAIMessages(userInfo.UserId);

      if (!participantLatestAIMessages.IsNullOrEmpty()
        && (DateTime.UtcNow - participantLatestAIMessages[0].SentUtc).TotalDays < AICoachActionCutoffDays) return; // No need for a reminder.

      string cardLinkUrl = PathHelper.Pages.ParticipantAICoach();

      if (!ActionCardList.Exists(x => x.LinkUrl == cardLinkUrl)) {
        ActionCardList.Add(new WebHelper.ParticipantActionCard(
          headerText: "Say hi to your AI-Coach",
          descriptionText: "Get personalised nudges, advice, support and coaching from your AI-Coach.",
          actionText: "Message AI-Coach",
          iconPath: PathHelper.Images.AIIcon(),
          iconClass: WebHelper.Icon.ActionCardIconClass.AICoach,
          linkUrl: cardLinkUrl,
          targetNewTab: WebHelper.TargetNewTab.No
        ));
      }
    }

    void AddActionToCompleteProfile() {

      bool isProfileComplete = !userInfo.FirstName.IsNullOrEmpty()
        && !userInfo.LastName.IsNullOrEmpty()
        && !userInfo.EmailAddress.IsNullOrEmpty()
        && !userInfo.MobileNumber.IsNullOrEmpty()
        && !PathHelper.Images.UserPhoto(userInfo, PathHelper.Images.UserPhotoSize.Thumbnail, false).IsNullOrEmpty();

      if (isProfileComplete) return;

      string cardLinkUrl = PathHelper.Pages.CoachEdit(userInfo.UserId);

      if (!ActionCardList.Exists(x => x.LinkUrl == cardLinkUrl)) {
        ActionCardList.Add(new WebHelper.ParticipantActionCard(
          headerText: "Complete your Profile",
          descriptionText: "Complete your profile so we can tailor your experience at able platform.",
          actionText: "Complete profile",
          iconPath: PathHelper.Images.PuzzleIcon(),
          iconClass: WebHelper.Icon.ActionCardIconClass.Profile,
          linkUrl: PathHelper.Pages.CoachEdit(userInfo.UserId),
          targetNewTab: WebHelper.TargetNewTab.No
        ));
      }
    }

    private void GetEventList() {

      GetEventListCoachingSessions();
      GetEventListWorkshopSessions();
      GetEventListScheduledContent();
      GetEventListModules();
      GetEventListUserSurveys();

      // Events dependent on Coachee
      if (!userCoacheeInfoList.IsNullOrEmpty()) {
        foreach (var coacheeInfo in userCoacheeInfoList) {

          if (coacheeInfo.ProgramJobNumber != CurrentJobNumber) continue;

          GetEventListUnbookedSessions(coacheeInfo);
        }
      }
    }

    private void GetEventListCoachingSessions() {
      // Get all coaching and workshop sessions belonging to this participant (user).
      var userCoachingSessions = DbHelper.CoachingSessions.GetCoachingSessionsForUser(userInfo.UserId, null, DbHelper.CoachingSessions.CoachingSessionsForUserPeriod.All);

      // Combine both types of sessions into one common list of existing events.

      if (!userCoachingSessions.IsNullOrEmpty()) {
        foreach (var s in userCoachingSessions) {

          if (!CurrentJobNumber.IsNullOrEmpty() && s.ProgramJobNumber != CurrentJobNumber) continue; // Only include those that belong to this project

          UserEventList.Add(new ParticipantEventInfo(
            eventTitle: s.EventSessionTypeDisplayName,
            eventTitleTooltipHtml: "",
            startDateUtc: s.ApptDateUTC,
            startDateHtml: WebHelper.DisplayDateTime(SessionHelper.UtcToUserTime(s.ApptDateUTC), WebHelper.TimeDisplayMinutes.Yes),
            eventType: WebHelper.ParticipantEventType.CoachingSession,
            eventIconTooltipText: "Coaching Session",
            eventTypeIconPath: PathHelper.Images.PeopleCircleIcon(),
            coacheeId: s.CoacheeId,
            coacheeProgramStatusId: s.CoacheeProgramStatusId,
            venueName: s.ApptVenue,
            venueAddress: s.ApptVenueAddress,
            practitionerUserId: s.CoachUserId,
            practitionerFirstName: s.CoachFirstName,
            practitionerLastName: s.CoachLastName,
            isDeliveryInPerson: s.SessionTypeInPerson,
            eventURL: PathHelper.Partials.EventSlideoutPanel_CoachingSession(s.SessionId),
            rowClickAction: RowClickAction.TriggerSlideoutPanel,
            eventPeriod: GetEventPeriodEnum(s.ApptDateUTC)
          ));
        }
      }
    }

    private void GetEventListWorkshopSessions() {
      var userWorkshopSessions = DbHelper.WorkshopEvents.GetWorkshopsForUser(userInfo.UserId, null, DbHelper.WorkshopEvents.WorkshopsForUserPeriod.All);


      if (!userWorkshopSessions.IsNullOrEmpty()) {
        foreach (var w in userWorkshopSessions) {

          if (!CurrentJobNumber.IsNullOrEmpty() && w.ProgramJobNumber != CurrentJobNumber) continue; // Only include those that belong to this project

          UserEventList.Add(new ParticipantEventInfo(
            eventTitle: w.WorkshopTitle,
            eventTitleTooltipHtml: "",
            startDateUtc: w.WhenStartUtc,
            startDateHtml: GetWorkshopDisplayDate(w.WhenStartLocal, w.WhenEndLocal),
            eventType: WebHelper.ParticipantEventType.Workshop,
            eventIconTooltipText: "Workshop",
            eventTypeIconPath: PathHelper.Images.SchoolIcon(),
            coacheeId: w.CoacheeId,
            coacheeProgramStatusId: w.CoacheeProgramStatusId,
            venueName: w.Location,
            venueAddress: "",
            practitionerUserId: w.KeyFacilitatorUserId,
            practitionerFirstName: w.KeyFacilitatorFirstName,
            practitionerLastName: w.KeyFacilitatorLastName,
            isDeliveryInPerson: !w.IsVirtual,
            eventURL: PathHelper.Partials.EventSlideoutPanel_Workshop(w.WorkshopEventId),
            rowClickAction: RowClickAction.TriggerSlideoutPanel,
            eventPeriod: GetEventPeriodEnum(w.WhenStartUtc)
          ));
        }
      }
    }

    private void GetEventListUnbookedSessions(DbHelper.AlbertCoachees.AlbertCoacheeInfo userCoacheeInfo) {

      if (userCoacheeInfo == null) return; // No coachee to work on.
      if (userCoacheeInfo.CoachUserId == ConfigHelper.UserId.Unassigned) return; // No coach assigned, don't add any booking actions.

      int unbookedSessions = userCoacheeInfo.UserActivity.SessionsAllocated - userCoacheeInfo.UserActivity.SessionsBooked;
      if (unbookedSessions <= 0) return; // No unbooked sessions, no actions needed.

      // Add unbooked coaching "events" with null date.
      for (int i = 0; i < unbookedSessions; i++) {
        UserEventList.Add(new ParticipantEventInfo(
          eventTitle: "Book Your Next Coaching Session",
          eventTitleTooltipHtml: "",
          startDateUtc: null,
          startDateHtml: WebHelper.GetActionLink(WebHelper.ActionButtonTypeEnum.book, "book-link button btn btn-primary", " Book", "", PathHelper.Pages.CoacheeSessionBooking(userCoacheeInfo.CoacheeUID)),
          eventType: WebHelper.ParticipantEventType.CoachingSession,
          eventIconTooltipText: "Coaching Session",
          eventTypeIconPath: PathHelper.Images.PeopleCircleIcon(),
          coacheeId: 0,
          coacheeProgramStatusId: 0,
          venueName: "",
          venueAddress: "",
          practitionerUserId: userCoacheeInfo.CoachUserId,
          practitionerFirstName: userCoacheeInfo.UserActivity.CoachFirstName,
          practitionerLastName: userCoacheeInfo.UserActivity.CoachLastName,
          isDeliveryInPerson: false,
          eventURL: PathHelper.Pages.CoacheeSessionBooking(userCoacheeInfo.CoacheeUID),
          rowClickAction: RowClickAction.Redirect,
          eventPeriod: GetEventPeriodEnum(null)
        ));
      }
    }

    private void GetEventListScheduledContent() {

      if (CurrentJobNumber == null) return;
      if (!SessionHelper.AppAccess.PageAccess.CanAccessContentPage()) return;

      var schedulesContent = DbHelper.Content.GetProgramContentByJobNumber(CurrentJobNumber, true);

      if (!schedulesContent.IsNullOrEmpty()) {
        foreach (var sc in schedulesContent) {
          // Only show content that's been flagged to be listed when added to program
          if (!sc.IsContentListed) continue;
          // Only show those from Programs where the user is a Coachee
          if (!userCoacheeInfoList.IsNullOrEmpty() && !userCoacheeInfoList.Exists(x => x.ProgramJobId == sc.ProgramJobId)) return;

          UserEventList.Add(new ParticipantEventInfo(
            eventTitle: sc.ContentInfo.ContentTitle,
            eventTitleTooltipHtml: "",
            startDateUtc: sc.ScheduledSendDateUtc,
            startDateHtml: WebHelper.DisplayDate(sc.ScheduledSendDateUtc),
            eventType: WebHelper.ParticipantEventType.Microlearning,
            eventIconTooltipText: "Microlearning",
            eventTypeIconPath: PathHelper.Images.MicrolearningIcon(),
            coacheeId: null,
            coacheeProgramStatusId: null,
            venueName: sc.ContentInfo.ContentSummary,
            venueAddress: "",
            practitionerUserId: sc.AddedByUserId,
            practitionerFirstName: sc.AddedByFirstName,
            practitionerLastName: sc.AddedByLastName,
            isDeliveryInPerson: true,
            eventURL: PathHelper.Pages.ProgramContentDetails(sc.ProgramJobId, sc.ContentInfo.ContentGuid),
            rowClickAction: RowClickAction.Redirect,
            eventPeriod: GetEventPeriodEnum(sc.ScheduledSendDateUtc)
          ));
        }
      }

    }

    private void GetEventListModules() {

      if (CurrentJobNumber == null) return;
      if (!SessionHelper.AppAccess.PageAccess.CanAccessModule()) return;

      List<DbHelper.Modules.ParticipantModuleInfo> projectModuleInfoList = null;

      DbHelper.Common.UsingTransaction(trans => {

        try {

          projectModuleInfoList = DbHelper.Modules.GetParticipantModulesInProject(trans, CurrentJobNumber, userInfo.UserId);
          return true;

        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("GetEventListModules_GetParticipantModulesInProject")
            .FromSession()
            .WithProperty("JobNumber", CurrentJobNumber)
            .Track();

          return false;
        }
      });

      if (!projectModuleInfoList.IsNullOrEmpty()) {
        foreach (var mod in projectModuleInfoList) {

          string dateHtml = "";

          if (mod.ModuleDueUtc == null) {
            dateHtml = WebHelper.DisplayDate(mod.ModuleStartUtc, "-");
          } else {

            dateHtml = $"<p>{WebHelper.DisplayDate(mod.ModuleStartUtc, "-")}</p><p>{WebHelper.DisplayDate(mod.ModuleDueUtc, "-")}</p>";
          }

          bool isDueSoon = mod.ModuleDueUtc != null && mod.ModuleDueUtc > DateTime.UtcNow && mod.ModuleDueUtc <= DateTime.UtcNow.AddDays(Module_UpcomingDeadlineDays);
          string completedText = $"You have completed {mod.CompletedPercentage}% of this Module";
          string notEnrolledText = "You haven't enrolled to this Module and it's included in your program";

          string titleTooltip = WebHelper.GetIconTooltip(
            iconType: WebHelper.ActionButtonTypeEnum.info,
            tooltipTitle: mod.IsUserEnrolled ? completedText : notEnrolledText,
            tooltipText: isDueSoon ? "This module is due soon!" : "",
            customClass: !mod.IsUserEnrolled || isDueSoon ? "important" : "");

          UserEventList.Add(new ParticipantEventInfo(
            eventTitle: $"{mod.ModuleInfo.ModuleTitle}",
            eventTitleTooltipHtml: titleTooltip,
            startDateUtc: mod.ModuleStartUtc,
            startDateHtml: dateHtml,
            eventType: WebHelper.ParticipantEventType.Module,
            eventIconTooltipText: "Module",
            eventTypeIconPath: PathHelper.Images.ModuleIcon(),
            coacheeId: null,
            coacheeProgramStatusId: null,
            venueName: mod.ModuleInfo.ModuleSummary,
            venueAddress: "",
            practitionerUserId: mod.ModuleInfo.AuthorUserId,
            practitionerFirstName: mod.ModuleInfo.AuthorFirstName,
            practitionerLastName: mod.ModuleInfo.AuthorLastName,
            isDeliveryInPerson: false,
            eventURL: PathHelper.Pages.Module(mod.ModuleInfo.ModuleGuid, mod.ProgramJobId.Value),
            rowClickAction: RowClickAction.Redirect,
            eventPeriod: GetEventPeriodEnum(mod.ModuleDueUtc)
          ));
        }
      }

    }

    private void GetEventListUserSurveys() {

      if (UserSurveys.IsNullOrEmpty()) return;

      // Remove surveys that don't correspond.
      UserSurveys.RemoveAll(x => x.FoundParticipantBrief == null || x.CreatedByUserId == userInfo.UserId
      || (!x.ProgramJobNumber.IsNullOrEmpty() && x.ProgramJobNumber != CurrentJobNumber) || x.SurveyType == DbHelper.AlbertSurveys.SurveyTypeEnum.Eval);
      if (UserSurveys.IsNullOrEmpty()) return;

      foreach (var ss in UserSurveys) {

        UserEventList.Add(new ParticipantEventInfo(
          eventTitle: ss.SurveyName,
          eventTitleTooltipHtml: "",
          startDateUtc: ss.CloseDateSelfLocal,
          startDateHtml: WebHelper.DisplayDate(ss.CloseDateSelfLocal),
          eventType: WebHelper.ParticipantEventType.Survey,
          eventIconTooltipText: "Scheduled Survey",
          eventTypeIconPath: PathHelper.Images.SurveyIcon(),
          coacheeId: null,
          coacheeProgramStatusId: null,
          venueName: "",
          venueAddress: "",
          practitionerUserId: null,
          practitionerFirstName: ConfigHelper.AbleCoachInfo.FirstName,
          practitionerLastName: ConfigHelper.AbleCoachInfo.LastName,
          isDeliveryInPerson: false,
          eventURL: WebHelper.GetSurveyListRowDataAttrs(ss),
          rowClickAction: RowClickAction.ShowModal,
          eventPeriod: GetEventPeriodEnum(ss.CloseDateSelfLocal)
        ));
      }
    }

    public EventPeriodEnum GetEventPeriodEnum(DateTime? eventDate) {
      if (eventDate == null || eventDate > DateTime.UtcNow) {

        return EventPeriodEnum.Upcoming;

      } else if (eventDate < DateTime.UtcNow) {

        return EventPeriodEnum.Complete;
      }

      return EventPeriodEnum.Overview;
    }

    public string GetProfileImageHtml(ParticipantEventInfo eventInfo) {
      return WebHelper.GetAvatarForTable_User(
        PathHelper.Images.UserPhoto(eventInfo.PractitionerFirstName, eventInfo.PractitionerLastName, PathHelper.Images.UserPhotoSize.Thumbnail, true),
        eventInfo.PractitionerFirstName + " " + eventInfo.PractitionerLastName, eventInfo.PractitionerUserId
      );
    }

    public string GetEventTitleHtml(ParticipantEventInfo eventInfo) {

      if (eventInfo.IsUnbookedCoachingSession) return eventInfo.EventTitle.HTMLEncode();

      // If the Venue Address has text, make it a double line.
      string venueHtml = "";
      if (eventInfo.EventType == WebHelper.ParticipantEventType.Microlearning || eventInfo.EventType == WebHelper.ParticipantEventType.Module) {

        venueHtml = eventInfo.VenueName;

      } else {

        venueHtml = WebHelper.GetEventVenueHtml(eventInfo.VenueName, eventInfo.StartDateUtc);
      }

      if (!eventInfo.VenueAddress.IsNullOrEmpty()) venueHtml += "<br/>" + eventInfo.VenueAddress.HTMLEncode();
      return $"<b>{eventInfo.EventTitle.HTMLEncode()}</b>{eventInfo.EventTitleTooltipHtml}<br/>{venueHtml}";
    }

    public List<ParticipantEventInfo> GetEventsForPeriod(EventPeriodEnum eventPeriod) {

      var eventForPeriod = new List<ParticipantEventInfo>();

      if (eventPeriod == EventPeriodEnum.Overview) {

        eventForPeriod = UserEventList; // Overview contains all

      } else {

        eventForPeriod = UserEventList.FindAll(x => x.EventPeriod == eventPeriod); // Specific period
      }

      if (!eventForPeriod.IsNullOrEmpty()) {
        // Sort list

        // Group events into future, past, and null
        var allEventsWithDates = eventForPeriod.Where(e => e.StartDateUtc.HasValue).OrderBy(e => e.StartDateUtc.Value);
        var nullEvents = eventForPeriod.Where(e => !e.StartDateUtc.HasValue);

        eventForPeriod = nullEvents.Concat(allEventsWithDates).ToList();
      }

      return eventForPeriod;
    }

    private string GetWorkshopDisplayDate(DateTime startDateLocal, DateTime endDateLocal) {

      if (startDateLocal.Date == endDateLocal.Date) {
        // Single day event - show date on one line, times on 2nd line.
        return $"{WebHelper.DisplayDate(startDateLocal)}<br/>"
             + $"{WebHelper.DisplayTime(startDateLocal, WebHelper.TimeDisplayMinutes.Yes)} - {WebHelper.DisplayTime(endDateLocal, WebHelper.TimeDisplayMinutes.Yes)}";
      } else {
        // Multiple day event - show dates & times on 2 lines.
        return $"{WebHelper.DisplayDateTime(startDateLocal, WebHelper.TimeDisplayMinutes.Yes)}<br/>"
             + $"{WebHelper.DisplayDateTime(endDateLocal, WebHelper.TimeDisplayMinutes.Yes)}";
      }
    }

    public class ParticipantEventInfo {

      public string EventTitle { get; private set; }
      public string EventTitleTooltipHtml { get; private set; }
      public DateTime? StartDateUtc { get; private set; }
      public string StartDateHtml { get; private set; }
      public WebHelper.ParticipantEventType EventType { get; private set; }
      public string EventIconTooltipText { get; private set; }
      public string EventTypeIconPath { get; private set; }
      public int? CoacheeId { get; private set; }
      public int? CoacheeProgramStatusId { get; private set; }
      public string VenueName { get; private set; }
      public string VenueAddress { get; private set; }
      public int? PractitionerUserId { get; private set; }
      public string PractitionerFirstName { get; private set; }
      public string PractitionerLastName { get; private set; }
      public bool IsDeliveryInPerson { get; private set; }
      public string EventUrl { get; private set; }
      public RowClickAction RowClickAction { get; private set; }
      public EventPeriodEnum EventPeriod { get; private set; }

      public ParticipantEventInfo(
        string eventTitle,
        string eventTitleTooltipHtml,
        DateTime? startDateUtc, string startDateHtml,
        WebHelper.ParticipantEventType eventType,
        string eventIconTooltipText,
        string eventTypeIconPath,
        int? coacheeId,
        int? coacheeProgramStatusId,
        string venueName, string venueAddress,
        int? practitionerUserId,
        string practitionerFirstName,
        string practitionerLastName,
        bool isDeliveryInPerson,
        string eventURL,
        RowClickAction rowClickAction,
        EventPeriodEnum eventPeriod
      ) {
        EventTitle = eventTitle;
        EventTitleTooltipHtml = eventTitleTooltipHtml;
        StartDateUtc = startDateUtc;
        StartDateHtml = startDateHtml;
        EventType = eventType;
        EventIconTooltipText = eventIconTooltipText;
        EventTypeIconPath = eventTypeIconPath;
        CoacheeId = coacheeId;
        CoacheeProgramStatusId = coacheeProgramStatusId;
        VenueName = venueName;
        VenueAddress = venueAddress;
        PractitionerUserId = practitionerUserId;
        PractitionerFirstName = practitionerFirstName;
        PractitionerLastName = practitionerLastName;
        IsDeliveryInPerson = isDeliveryInPerson;
        EventUrl = eventURL;
        RowClickAction = rowClickAction;
        EventPeriod = eventPeriod;
      }

      public bool IsUnbookedCoachingSession => StartDateUtc == null && EventType == WebHelper.ParticipantEventType.CoachingSession;
    }

  }
}
