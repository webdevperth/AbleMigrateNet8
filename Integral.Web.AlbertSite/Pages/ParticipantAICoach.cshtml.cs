using Integral.Integrations.Amplitude;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using static Integral.Web.AmplitudeHelper;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class ParticipantAICoach : AppCode.PageBaseClasses.LoggedInPageModel {

    public bool CanViewAIChat, CanViewChatNudges, CanInteractWithChat;

    public const string ChatErrorMessageText = "Apologies, I can't respond at this time.";
    public const string AICoach_ActionCardClass = "AI-CoachCard";

    public List<NudgeItem> NudgeList;

    public class AjaxAction {
      public const string UserMesage = "UserMesage";
    }

    public class FormFields {
      public const string UserMessageText = "UserMessageText";
      public const string UserMessageGuid = "UserMessageGuid";
    }

    public class AjaxReturnData {
      public const string UserMessageGuid = "UserMessageGuid";
    }

    public class NudgeKeys {
      public const string BuildTeamTrust = "buildTeamTrust";
      public const string Prioritization = "prioritization";
      public const string DifficultConversations = "difficultConversations";
      public const string IncreaseProductivity = "increaseProductivity";
    }

    public int MaxEvents = ConfigHelper.MaxParticipantEvents;
    public int TotalUpcomingEvents;

    private DbHelper.AlbertCoachees.AlbertCoacheeInfo latestCoachee;
    private List<DbHelper.AlbertSurveys.SurveyInfo> OpenSurveys;
    private List<DbHelper.AIMessage.AIMessageInfo> ParticipantLatestAIMessages;

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = "AI Coach";

      NudgeList = GetDefaultNudgeList();

      CanViewAIChat = SessionHelper.AppAccess.Coaches.CanViewAIChat();

      if (SessionHelper.GetUserInfoOrNull().LatestCoachingInfo != null) {
        latestCoachee = DbHelper.AlbertCoachees.GetCoacheeInfo(SessionHelper.GetUserInfoOrNull().LatestCoachingInfo.CoacheeId);
      }

      OpenSurveys = DbHelper.AlbertSurveys.GetOpenSurveysForUserId(userInfo.UserId, DbHelper.AlbertSurveys.OnlyViewerCompatible.No);

      CanInteractWithChat = SessionHelper.AppAccess.Participants.CanInteractWithAIChat(OpenSurveys);

      if (!CanInteractWithChat) {
        if (SystemWeb.IsHttpPost) {
          AjaxSubmitHelper.Process(ajax => {
            ajax.RespondNoAccessToFunction();
            return;
          });
          return new EmptyResult();
        }
        return Page();
      }

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {
          if (ajax.Action == AjaxAction.UserMesage) {
            var userMessageGuid = SaveUserMessage(ajax);
            ajax.AddReturnValue(AjaxReturnData.UserMessageGuid, userMessageGuid);
          }
        });
        return new EmptyResult();
      }

      ParticipantLatestAIMessages = DbHelper.AIMessage.GetUserLatestAIMessages(userInfo.UserId);

      return Page();
    }

    private Guid SaveUserMessage(AjaxSubmitHelper ajax) {

      var messageText = ajax.FormValue(FormFields.UserMessageText);
      var messageGuid = DbHelper.AIMessage.InsertMessage(userInfo.UserId, messageText, false);

      // Send Amplitude event for AI chatbot interaction
      var userRole = SessionHelper.GetUserRole();
      var externalUserId = userRole.ToExternalUserId(userInfo.UserGuid);
      var deviceId = SessionHelper.GetSessionGuid().ToAmplitudeDeviceId();

      if (externalUserId.HasValue) {
        var eventProperties = new Dictionary<string, object> {
          { AmplitudePropertyConstants.MessageId, messageGuid.ToString() },
          { AmplitudePropertyConstants.MessageLength, messageText?.Length ?? 0 },
          { AmplitudePropertyConstants.InteractionType, "message_sent" }
        };

        SendEvent(
          amplitude => amplitude.TrackEvent(
            externalUserId.Value,
            deviceId,
            AmplitudeEventConstants.AICoachInteraction,
            eventProperties,
            null  // userPropertyOperations
          ),
          operationName: "ParticipantAICoach_AICoachInteraction",
          eventName: AmplitudeEventConstants.AICoachInteraction,
          requestRawUrl: SystemWeb.RequestRawUrl,
          telemetryProperties: new Dictionary<string, object> {
            ["MessageId"] = messageGuid.ToString(),
            ["MessageLength"] = messageText?.Length ?? 0,
            ["InteractionType"] = "message_sent"
          }
        );
      }

      return messageGuid;
    }

    public string GetLatestMessagessHtml() {

      string latestMsgsHtml = "";

      if (!CanViewAIChat) return latestMsgsHtml;

      if (ParticipantLatestAIMessages != null && ParticipantLatestAIMessages.Count > 0) {

        bool lastMsgWasError = !ParticipantLatestAIMessages[0].IsFromAI;

        // Check if last interaction was 24hrs ago or more
        if (ParticipantLatestAIMessages[0].SentUtc < DateTime.UtcNow.AddHours(-24)) {
          CanViewChatNudges = true;
        }

        ParticipantLatestAIMessages.Reverse();

        if (ParticipantLatestAIMessages.Count < 30) {
          latestMsgsHtml += GetWelcomeMessage();
        }

        bool lastMsgIsFromUser = false;
        foreach (var message in ParticipantLatestAIMessages) {

          if (lastMsgIsFromUser && !message.IsFromAI) {
            // The only reason to have two consecutive messages from user means there was an error in between.
            // As this isn't registered, display it to keep fidelity of the chat for the user.
            latestMsgsHtml += GetErrorMessage();
          }

          latestMsgsHtml += GetSingleMessageHtml(message.IsFromAI, message.MessageBodyText);

          lastMsgIsFromUser = !message.IsFromAI;
        }

        if (lastMsgWasError) {
          latestMsgsHtml += GetErrorMessage();
        }

      } else {
        latestMsgsHtml = GetWelcomeMessage();
        CanViewChatNudges = true;
      }

      return latestMsgsHtml;
    }

    public string GetSingleMessageHtml(bool isFromAI, string messageText, bool isTemplate = false) {
      var avatarUrl = isFromAI ? PathHelper.Images.AIIcon() : PathHelper.Images.UserPhoto(userInfo, PathHelper.Images.UserPhotoSize.Thumbnail, true);
      var senderName = isFromAI ? ConfigHelper.AbleAIChatName : userInfo.GetFullName();
      return WebHelper.GetAIChatMessageListItem(isFromAI, avatarUrl, senderName, messageText, isTemplate);
    }

    private string GetWelcomeMessage() {
      return GetSingleMessageHtml(true, $"Hi {userInfo.FirstName}, ask me anything.");
    }

    private string GetErrorMessage() {
      return GetSingleMessageHtml(true, ChatErrorMessageText);
    }

    private class EventInfo {

      public DateTime EventDateUtc { get; private set; }
      public string EventName { get; private set; }
      public WebHelper.ParticipantEventType EventType { get; private set; }
      public string Venue { get; private set; }
      public string VenueAddress { get; private set; }
      public string SlideoutPanelURL { get; private set; }

      public EventInfo(DateTime eventDateUtc, string eventName, WebHelper.ParticipantEventType eventType, string venue, string venueAddress, string slideoutPanelURL) {
        EventDateUtc = eventDateUtc;
        EventName = eventName;
        EventType = eventType;
        Venue = venue;
        VenueAddress = venueAddress;
        SlideoutPanelURL = slideoutPanelURL;
      }
    }

    public string GetParticipantUpcomingEvents() {

      List<EventInfo> eventList = new List<EventInfo>();

      var upcomingSessions = DbHelper.CoachingSessions.GetCoachingSessionsForUser(userInfo.UserId, MaxEvents, DbHelper.CoachingSessions.CoachingSessionsForUserPeriod.Upcoming);
      var upcomingWorkshops = DbHelper.WorkshopEvents.GetWorkshopsForUser(userInfo.UserId, MaxEvents, DbHelper.WorkshopEvents.WorkshopsForUserPeriod.Upcoming);

      // If there's no upcoming events, display default event.
      if (upcomingSessions.Count == 0 && upcomingWorkshops.Count == 0) {
        return WebHelper.GetDefaultEventItem();
      }

      // Iterate through each list and add it to the event list to combine them and process.
      foreach (var s in upcomingSessions) {
        eventList.Add(new EventInfo(
          eventDateUtc: s.ApptDateUTC,
          eventName: s.EventSessionTypeDisplayName,
          venue: s.ApptVenue,
          venueAddress: s.ApptVenueAddress,
          eventType: WebHelper.ParticipantEventType.CoachingSession,
          slideoutPanelURL: PathHelper.Partials.EventSlideoutPanel_CoachingSession(s.SessionId)
        ));
      }

      foreach (var w in upcomingWorkshops) {
        eventList.Add(new EventInfo(
          eventDateUtc: w.WhenStartUtc,
          eventName: w.WorkshopTitle,
          venue: w.Location,
          venueAddress: "",
          eventType: WebHelper.ParticipantEventType.Workshop,
          slideoutPanelURL: PathHelper.Partials.EventSlideoutPanel_Workshop(w.WorkshopEventId)
        ));
      }

      TotalUpcomingEvents = eventList.Count;

      // Sort the list by Date in asc order
      eventList.Sort((x, y) => DateTime.Compare(x.EventDateUtc, y.EventDateUtc));

      // Ensure complete list within max length.
      if (eventList.Count > MaxEvents) eventList.RemoveRange(MaxEvents, eventList.Count - MaxEvents);

      // Create the HTML to display with the event list.
      string eventsHtml = "";
      foreach (var evt in eventList) {
        eventsHtml += WebHelper.GetParticipantEventItem(evt.EventDateUtc, evt.EventName, evt.Venue, evt.VenueAddress, evt.EventType, evt.SlideoutPanelURL, userInfo.TimeZoneIdIana, true);
      }

      return eventsHtml;
    }

    public string GetParticipantActions() {

      bool remindBookNextSession = latestCoachee != null
        && latestCoachee.UserActivity?.SessionsBooked < latestCoachee.UserActivity?.SessionsAllocated
        && latestCoachee.UserActivity?.SessionsUpcoming == 0
        && latestCoachee.CoachUserId != ConfigHelper.UserId.Unassigned;

      var moduleAction = AddActionForModules();

      if (OpenSurveys == null && !remindBookNextSession && moduleAction.IsNullOrEmptyOrWhitespace()) return "";

      int maxActionItems = ConfigHelper.MaxParticipantActions_AICoach;
      int actionsCount = 0;
      string html = "";

      if (!moduleAction.IsNullOrEmptyOrWhitespace()) {
        html += moduleAction;
        actionsCount++;
      }

      if (remindBookNextSession) {
        html += WebHelper.GetParticipantActionItem("Book a Coaching Session", "You haven't booked your next session yet.", PathHelper.Pages.ParticipantCoaching());
        actionsCount++;
      }

      foreach (var sp in OpenSurveys) {

        if (sp.FoundParticipantBrief == null) continue;

        html += WebHelper.GetParticipantActionItem("Complete Survey", sp.SurveyName, PathHelper.Pages.Survey(sp, sp.FoundParticipantBrief.PartUniqueId));

        actionsCount++;
        if (actionsCount == maxActionItems) break;
      }

      if (actionsCount < maxActionItems) {
        if (ParticipantLatestAIMessages.IsNullOrEmpty() || ParticipantLatestAIMessages[0].SentUtc <= DateTime.UtcNow.AddDays(-14)) {
          html += WebHelper.GetParticipantActionItem("Say hi to your AI-Coach", "Interact with your AI-Coach.", PathHelper.Pages.ParticipantAICoach(), WebHelper.TargetNewTab.No, AICoach_ActionCardClass);
          actionsCount++;
        }
      }

      if (actionsCount < maxActionItems) {

        bool isProfileComplete = !userInfo.FirstName.IsNullOrEmpty() && !userInfo.LastName.IsNullOrEmpty() && !userInfo.EmailAddress.IsNullOrEmpty() &&
          !userInfo.MobileNumber.IsNullOrEmpty() && !PathHelper.Images.UserPhoto(userInfo, PathHelper.Images.UserPhotoSize.Thumbnail, false).IsNullOrEmpty();

        if (!isProfileComplete) {
          html += WebHelper.GetParticipantActionItem("Complete Profile", "Please complete your profile.", PathHelper.Pages.CoachEdit(userInfo.UserId));
          actionsCount++;
        }
      }

      return actionsCount == 0 ? string.Empty : html;
    }

    private string AddActionForModules() {

      if (!SessionHelper.AppAccess.PageAccess.CanAccessModule()) return "";

      DbHelper.Modules.ParticipantModuleInfo participantModuleInfo = null;

      DbHelper.Common.UsingTransaction(trans => {

        try {

          participantModuleInfo = DbHelper.Modules.GetParticipantModuleForActionCard(trans, userInfo.UserId);
          return true;

        } catch (Exception) {

          return false;
        }
      });

      string actionTitle = "", actionDescription = "", actionUrl = "";

      if (participantModuleInfo == null) {
        // User is not enrolled to any
        actionTitle = "Explore Modules";
        actionDescription = "Find a topic you love and start learning.";
        actionUrl = PathHelper.Pages.Content();

      } else if (participantModuleInfo.IsLinkedToProgram && !participantModuleInfo.IsUserEnrolled) {
        // Is pending to enrol
        actionTitle = "Start Your Learning";
        actionDescription = "A module is ready for you. Enrol now!";
        actionUrl = PathHelper.Pages.Module(participantModuleInfo.ModuleInfo.ModuleGuid);

      } else {
        // User is enroled and has pending lessons
        actionTitle = participantModuleInfo.CompletedPercentage < 70 ? "Keep going" : "You're Almost There";
        actionDescription = $"You've completed {participantModuleInfo.CompletedPercentage}% of your module.";
        actionUrl = PathHelper.Pages.Module(participantModuleInfo.ModuleInfo.ModuleGuid);
      }

      return WebHelper.GetParticipantActionItem(actionTitle, actionDescription, actionUrl, WebHelper.TargetNewTab.Yes);
    }

    private List<NudgeItem> GetDefaultNudgeList() {

      return new List<NudgeItem>() {

        new NudgeItem(
          NudgeKeys.BuildTeamTrust,
          "Build Team Trust",
          "Learn how to foster trust among your team members for a more cohesive work environment.",
          "What are actionable ways to build trust within my team?"),

        new NudgeItem(
          NudgeKeys.Prioritization,
          "Master Prioritization",
          "Gain insights into how to effectively prioritize tasks and projects for better team output.",
          "What are proven methods for prioritizing tasks and projects in a fast-paced environment?"),

        new NudgeItem(
          NudgeKeys.DifficultConversations,
          "Difficult Conversations",
          "Develop the skills to handle difficult conversations with team members or stakeholders effectively.",
          "Let's simulate a difficult conversation. I have to tell an employee they're not meeting performance expectations. Play the role of the employee for this practice. Afterwards, give me feedback on how to improve handling difficult conversations."),

        new NudgeItem(
          NudgeKeys.IncreaseProductivity,
          "Increase Productivity",
          "Discover strategies to boost team productivity without sacrificing well-being.",
          "How can I improve team productivity while maintaining a work-life balance?")
      };
    }

    public class NudgeItem {

      public string NudgeKey, HeadingText, BodyText, ChatPrompt;

      public NudgeItem(string nudgeKey, string headingText, string bodyText, string chatPrompt) {
        NudgeKey = nudgeKey;
        HeadingText = headingText;
        BodyText = bodyText;
        ChatPrompt = chatPrompt;
      }
    }

  }
}
