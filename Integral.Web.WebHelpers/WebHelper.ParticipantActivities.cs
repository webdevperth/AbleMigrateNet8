using System;
using System.Collections.Generic;
using System.Linq;

namespace Integral.Web {

  public partial class WebHelper {

    public class ParticipantActivities {

      private class HtmlElements {
        public const string GreenCircleEmoji = "&#128994;";
        public const string RedCircleEmoji = "&#128308;";
        public const string OrangeCircleEmoji = "&#128992;";
        public const string CheckMarkEmoji = "&#10004;&#65039;";
      }

      public enum ParticipantUserActivityBadgeTextEnum { ParticipantStatus, SubscriptionInfo }
      private enum UserActivityInfoHtmlType { Badge, Header }
      private static UserActivityInfoHtmlType CurrentActivityInfoHtmlType;

      public static string GetBadgeParticipantUserActivityInfo(
        DbHelper.AbleUser.UserActivityInfo userActivityInfo,
        DbHelper.Subscriptions.User.UserSubscriptionInfo userSubscriptionInfo,
        ParticipantUserActivityBadgeTextEnum participantUserActivityBadgeText = ParticipantUserActivityBadgeTextEnum.ParticipantStatus) {
        List<string> badgeInfoListToStyle = new List<string>();
        CurrentActivityInfoHtmlType = UserActivityInfoHtmlType.Badge;

        // Get registration status
        badgeInfoListToStyle.Add(GetParticipantRegistrationStatus(userActivityInfo));

        // Append Alert if needed
        string coacheeAlertHtml = GetParticipantAlert(userActivityInfo);
        bool hasCoacheeAlert = !coacheeAlertHtml.IsNullOrEmpty();
        if (hasCoacheeAlert) badgeInfoListToStyle.Add(HtmlElements.RedCircleEmoji + " " + coacheeAlertHtml);

        // Get last seen info -> Only get and display "Last Seen" info when registered
        string lastSeenHtml = GetParticipantLastSeenHtml(userActivityInfo);
        badgeInfoListToStyle.Add(lastSeenHtml);

        // Getting user sub type info
        string lineHtml = @"<hr class=""badge-useractivity-hr"" />";
        badgeInfoListToStyle.Add(lineHtml);
        badgeInfoListToStyle.Add(GetSubscriptionDisplayHtml(userSubscriptionInfo, true));
        badgeInfoListToStyle.Add(lineHtml);

        // Getting user activity info
        badgeInfoListToStyle.Add(GetParticipantSurveyInfoHtml(userActivityInfo, true, UserActivityInfoHtmlType.Badge));

        // Getting AI Coach info
        badgeInfoListToStyle.Add(GetAIInfoHtml(userActivityInfo, userSubscriptionInfo, UserActivityInfoHtmlType.Badge));

        string badgeClass = GetUserActivityBadgeClass(userActivityInfo, hasCoacheeAlert);

        string badgeHtml = string.Empty;

        if (participantUserActivityBadgeText == ParticipantUserActivityBadgeTextEnum.ParticipantStatus) {

          string programStatus = userActivityInfo.CoacheeStatusId == null
            ? "N/A"
            : DbHelper.CoacheeProgramStatus.GetDisplayTitleOrNull(userActivityInfo.CoacheeStatusId).EmptyIfNull();
          badgeHtml = GetStatusBadge(programStatus, badgeClass);

        } else {

          badgeHtml = GetStatusBadge(GetSubscriptionDisplayName(userSubscriptionInfo), badgeClass);
          if (!lastSeenHtml.IsNullOrEmpty()) {
            badgeHtml
              = "<div class=\"w100p\">"
              + badgeHtml
              + "<br/><div><p class=\"mt5 align-center\">" + lastSeenHtml + "</p></div>"
              + "</div>";
          }
        }

        return
          GetFormWithTooltip(
            badgeHtml,
            "Activity Info",
            ToolTipContentType.Text,
            "<p>" + badgeInfoListToStyle.Join("</p><p>") + "</p>"
          );
      }

      public static string GetPeopleDetailsHeaderHtml(
        DbHelper.OrganisationUsers.ProfileInfo profileInfo) {

        if (profileInfo == null || profileInfo.UserActivity == null) return "";

        CurrentActivityInfoHtmlType = UserActivityInfoHtmlType.Header;

        return $@"
          <div class=""user-activity-header flex flex-fill-sm"">
            <div class=""user-info-col user-info-photo"">
              <div class=""team-photo-sm mb5"">
                <img class=""mb5"" height=""120"" src=""{PathHelper.Images.UserPhoto(profileInfo.FirstName, profileInfo.LastName, PathHelper.Images.UserPhotoSize.Thumbnail, true)}"" />
              </div>
              <p class=""photo-sm-user-name""><b>{profileInfo.FirstName.HTMLEncode()} {profileInfo.LastName.HTMLEncode()}</b></p>
              <p>{profileInfo.Email.HTMLEncode()}</p>
              <p class=""overflow-ellipsis"">{profileInfo.LastestCompanyName.HTMLEncode()}<p>
            </div>
            {GetUserActivityBodyHtml(profileInfo.UserId, profileInfo.UserActivity, profileInfo.UserSubscription)}
          </div>";
      }

      public static string GetHeaderParticipantUserActivityInfo(
        DbHelper.AlbertCoachees.AlbertCoacheeInfo coacheeInfo,
        DbHelper.AbleUser.UserActivityInfo userActivityInfo,
        DbHelper.Subscriptions.User.UserSubscriptionInfo userSubscriptionInfo,
        bool canUpdateProfile) {

        if (coacheeInfo == null || userActivityInfo == null) return "";

        CurrentActivityInfoHtmlType = UserActivityInfoHtmlType.Header;

        string updateButtonHtml = canUpdateProfile ? $@"<button type=""button"" class=""btn btn-primary btnUpdateProfile"" title=""Update Profile"">{Icon.Edit}</button>" : "";

        return $@"
          <div class=""user-activity-header"">
            <div class=""mw125"">
              <div class=""team-photo-sm"">
                <img class=""mb15"" height=""120"" src=""{PathHelper.Images.UserPhoto(coacheeInfo.FirstName, coacheeInfo.LastName, PathHelper.Images.UserPhotoSize.Large, true)}"" />
                {updateButtonHtml}
              </div>
            </div>
            {GetUserActivityBodyHtml(coacheeInfo.UserId.Value, userActivityInfo, userSubscriptionInfo)}
          </div>";
      }

      public static string GetSlideoutParticipantUserActivityInfo(
        int userId,
        DbHelper.AbleUser.UserActivityInfo userActivityInfo,
        DbHelper.Subscriptions.User.UserSubscriptionInfo userSubscriptionInfo) {

        if (userActivityInfo == null) return "";

        CurrentActivityInfoHtmlType = UserActivityInfoHtmlType.Header;
        string customWidthClass = "w275";

        return $@"
          <div class=""user-activity-header"">
            <div class=""flex flex-fill gap15"">
              {GetParticipantAbleInfoHtml(userActivityInfo, userSubscriptionInfo, customWidthClass)}
              {GetParticipantCoachingHtml(userActivityInfo, customWidthClass)}
            </div>
            <div class=""flex flex-fill gap15"">
              {GetParticipantWorkshopHtml(userId, customWidthClass)}
              {GetParticipantSurveyHtml(userActivityInfo, customWidthClass)}
            </div>
          </div>";
      }

      private static string GetUserActivityBodyHtml(
        int userId,
        DbHelper.AbleUser.UserActivityInfo userActivityInfo,
        DbHelper.Subscriptions.User.UserSubscriptionInfo userSubscriptionInfo) {

        return $@"
          {GetParticipantAbleInfoHtml(userActivityInfo, userSubscriptionInfo, "minw225")}
          {GetParticipantCoachingHtml(userActivityInfo, "minw250")}
          {GetParticipantWorkshopHtml(userId, "minw250")}
          {GetParticipantSurveyHtml(userActivityInfo, "minw225")}";
      }

      private static string GetParticipantSurveyHtml(
        DbHelper.AbleUser.UserActivityInfo userActivityInfo, string customClass = "") {

        return $@"
          <div class=""user-info-col {customClass}"">
            {GetColumnHeader("Survey Info")}
            {GetParticipantSurveyInfoHtml(userActivityInfo, false, UserActivityInfoHtmlType.Header)}
          </div>";
      }

      private static string GetParticipantAbleInfoHtml(
        DbHelper.AbleUser.UserActivityInfo userActivityInfo,
        DbHelper.Subscriptions.User.UserSubscriptionInfo userSubscriptionInfo,
        string customClass = "") {

        return $@"
          <div class=""user-info-col {customClass}"">
            {GetColumnHeader("Able Info")}
            {GetParticipantRegistrationStatus(userActivityInfo)}
            {FormatDisplayLineHtml(GetParticipantAlert(userActivityInfo))}
            {GetParticipantLastSeenHtml(userActivityInfo)}
            {GetUserSubscriptionHtml(userSubscriptionInfo)}
            {GetAIInfoHtml(userActivityInfo, userSubscriptionInfo, UserActivityInfoHtmlType.Header)}
          </div>";
      }

      private static string GetParticipantCoachingHtml(
        DbHelper.AbleUser.UserActivityInfo userActivityInfo,
        string customClass = "") {
        if (userActivityInfo == null || userActivityInfo?.SessionsAllocated == 0) return "";

        return $@"
          <div class=""user-info-col {customClass}"">
            {GetColumnHeader("Coaching Info")}
            {(userActivityInfo == null || userActivityInfo.LastestCoachingSessionUtc == null ? "" : $"{FormatDisplayLineHtml($"Latest session:", DisplayDate(userActivityInfo.LastestCoachingSessionUtc))}")}
            {GetParticipantSessionsProgress(userActivityInfo)}
            {GetLatestCoachDisplayHtml(userActivityInfo)}
          </div>";
      }

      private static string GetUserSubscriptionHtml(DbHelper.Subscriptions.User.UserSubscriptionInfo userSubscriptionInfo) {
        return $@"
          <div class=""flex mt10"">
            <span style=""margin-top:0px;""><b>Subscription:</b></span>
            <span class=""ml5"">{GetSubscriptionDisplayName(userSubscriptionInfo)}</span>
            {(userSubscriptionInfo != null ? GetPartnerStatusIcon(userSubscriptionInfo != null, "ml5 mt1") : "")}
          </div>";
      }

      private static string GetColumnHeader(string columnHeaderText) {
        return $"<h6>{columnHeaderText}</h6>";
      }

      private static string GetParticipantSessionsProgress(DbHelper.AbleUser.UserActivityInfo userActivityInfo) {
        if (userActivityInfo == null || userActivityInfo.SessionsAllocated == 0 || !userActivityInfo.HasCoaching) return "";

        return FormatDisplayLineHtml("Sessions:", GetProgressBarHtml(userActivityInfo.SessionsCompleted, userActivityInfo.SessionsAllocated, "mt5 ml5 w100"));
      }

      private static string GetParticipantWorkshopHtml(int participantUserId, string customClass = "") {

        var WorkshopList = DbHelper.WorkshopEvents.GetWorkshopsForUser(participantUserId, null, DbHelper.WorkshopEvents.WorkshopsForUserPeriod.All);
        if (WorkshopList.IsNullOrEmpty()) return "";

        var workshopUpcoming = WorkshopList.FindAll(x => x?.WhenStartUtc > DateTime.UtcNow).OrderBy(x => x?.WhenStartUtc).ToList();
        var workshopPassed = WorkshopList.FindAll(x => x?.WhenStartUtc < DateTime.UtcNow).OrderByDescending(x => x?.WhenStartUtc).ToList();
        int maxWorkshopsToDisplay = 4, idealItemsOfEach = 2;
        int upcomingCount = Math.Min(workshopUpcoming.Count, idealItemsOfEach);
        int passedCount = Math.Min(maxWorkshopsToDisplay - upcomingCount, workshopPassed.Count);

        string workshopHtml = "";
        // Display upcoming workshops
        for (int i = 0; i < upcomingCount; i++) {
          workshopHtml += GetWorkshopItemInfo(workshopUpcoming[i]);
        }

        // Display passed workshops
        for (int i = 0; i < passedCount; i++) {
          workshopHtml += GetWorkshopItemInfo(workshopPassed[i]);
        }

        return $@"
          <div class=""user-info-col {customClass}"">
            {GetColumnHeader("Group Sessions")}
            {workshopHtml}
          </div>";
      }

      private static string GetWorkshopItemInfo(DbHelper.WorkshopEvents.WorkshopListInfo workshop) {
        return FormatDisplayLineHtml(
            $"<a class=\"groupsession\" title=\"{workshop.WorkshopTitle}\" target=\"_blank\" href=\"{PathHelper.Pages.Workshops_Edit(workshop.ProgramJobId, workshop.WorkshopEventId)}\"><b>{workshop.WorkshopTitle}</b></a>" +
            $"<span class=\"ml5\">{DisplayDate(workshop.WhenStartUtc)}</span>");
      }

      private static string GetLatestCoachDisplayHtml(DbHelper.AbleUser.UserActivityInfo userActivityInfo) {

        if (userActivityInfo == null) return "";

        if (userActivityInfo.CoachUserId != ConfigHelper.UserId.Unassigned) {
          string coachHtml = GetAvatarForTable_User(PathHelper.Images.UserPhoto(userActivityInfo.CoachFirstName, userActivityInfo.CoachLastName, PathHelper.Images.UserPhotoSize.Thumbnail, true), userActivityInfo.CoachFullName, userActivityInfo.CoachUserId).SurroundWith("<div class=\"user-avatar-horizontal\">", "</div>");
          return $@"
            <div class=""flex-inline""><span class=""mt7""><b>Coach:</b></span> {coachHtml}</div>";
        }

        return FormatDisplayLineHtml("Coach:", userActivityInfo.CoachFullName.Replace("[", "").Replace("]", ""));
      }

      private static string GetParticipantAlert(DbHelper.AbleUser.UserActivityInfo userActivityInfo) {

        if (userActivityInfo == null) return "";

        string message = "";

        if (userActivityInfo.SessionsAllocated > userActivityInfo.SessionsBooked
          && (userActivityInfo.CoacheeStatusId != null && (userActivityInfo.CoacheeStatusId == DbHelper.CoacheeProgramStatus.Ids.Onboarding || userActivityInfo.CoacheeStatusId == DbHelper.CoacheeProgramStatus.Ids.Active))
          && userActivityInfo.DaysSinceBooking > ConfigHelper.ProgramOverviewCoacheeAlert_DaysSinceLastCoachingSession) {

          message = $"{userActivityInfo.DaysSinceBooking} Days since last booking";

        } else if (userActivityInfo.MeetCoachEmailSentUtc != null && userActivityInfo.SessionsBooked == 0 && userActivityInfo.HasCoaching) {

          int daysSinceMeetCoachSent = (DateTime.UtcNow - userActivityInfo.MeetCoachEmailSentUtc.Value).Days;
          if (userActivityInfo.CoacheeStatusId != null && userActivityInfo.CoacheeStatusId == DbHelper.CoacheeProgramStatus.Ids.Active
            && daysSinceMeetCoachSent > ConfigHelper.ProgramOverviewCoacheeAlert_DaysSinceMeetCoachEmail) {
            message = $"Never booked session, {daysSinceMeetCoachSent} days lapsed";
          }
        }

        if (message.IsNullOrEmpty()) return "";

        return message.SurroundWith("<b>", "</b>").EnsureEndsWith(" &#9888;&#65039; ", StringExt.Ensure.IfNotBlank);
      }

      private static bool ParticipantHasAlert(DbHelper.AbleUser.UserActivityInfo userActivityInfo) {
        return !GetParticipantAlert(userActivityInfo).IsNullOrEmpty();
      }

      private static string GetUserActivityBadgeClass(DbHelper.AbleUser.UserActivityInfo userActivityInfo, bool hasCoacheeAlert) {

        if (userActivityInfo == null) return string.Empty;

        if (userActivityInfo.IsRegistered && userActivityInfo.LastLoginUtc != null) {
          if (userActivityInfo.CoacheeStatusId != null && userActivityInfo.CoacheeStatusId == DbHelper.CoacheeProgramStatus.Ids.Paused) {
            return "reg-inactive";
          } else if (hasCoacheeAlert) {
            return "coacheealert";
          } else {
            return UserHasBeenSeenThisMonth(userActivityInfo.LastLoginUtc) ? "registered" : "reg-inactive";
          }
        }
        return "unregistered";
      }

      private static bool UserHasBeenSeenThisMonth(DateTime? lastLoginUtc) {
        if (lastLoginUtc == null) return false; // (i.e. never seen).
        var daysLastSeen = (int)(DateTime.UtcNow - lastLoginUtc.Value).TotalDays;
        return daysLastSeen <= 30;
      }

      private static string GetSubscriptionDisplayName(DbHelper.Subscriptions.User.UserSubscriptionInfo userSubscriptionInfo) {
        return userSubscriptionInfo != null ? userSubscriptionInfo.SubscriptionName : "No Subscription";
      }
      private static string GetSubscriptionDisplayHtml(DbHelper.Subscriptions.User.UserSubscriptionInfo userSubscriptionInfo, bool isSeparateLine) {
        return FormatDisplayLineHtml($"Subscription:", GetSubscriptionDisplayName(userSubscriptionInfo), "", isSeparateLine);
      }

      private static string GetParticipantLastSeenHtml(DbHelper.AbleUser.UserActivityInfo userActivityInfo) {
        string lastSeenInfo = "";
        if (userActivityInfo != null && userActivityInfo.IsRegistered && userActivityInfo.LastLoginUtc != null) {
          lastSeenInfo = DisplayDate(userActivityInfo?.LastLoginUtc.Value);
          string badgeColorIndicator = ""; // User not being active for the month will turn the badge color organge, we are indicating int he tooltip with an orange circle
          if (!UserHasBeenSeenThisMonth(userActivityInfo.LastLoginUtc) && !ParticipantHasAlert(userActivityInfo)) badgeColorIndicator = HtmlElements.OrangeCircleEmoji;
          return FormatDisplayLineHtml($"Last seen on Able:", $"{lastSeenInfo} {badgeColorIndicator}");
        }
        return lastSeenInfo;
      }

      private static string GetParticipantRegistrationStatus(DbHelper.AbleUser.UserActivityInfo userActivityInfo) {

        if (userActivityInfo == null) return "";

        string statusLabel = "Status:";

        if (userActivityInfo.IsSoftDeleted) {

          return FormatDisplayLineHtml(statusLabel, $"Login blocked {HtmlElements.RedCircleEmoji}");

        } else if (userActivityInfo.IsRegistered) {

          return FormatDisplayLineHtml(statusLabel, $"Registered {HtmlElements.CheckMarkEmoji}");

        } else {
          if (userActivityInfo.InvitedUtc != null) {
            string invitedHtml = "";
            invitedHtml += FormatDisplayLineHtml(statusLabel, $"Invited to the platform {HtmlElements.OrangeCircleEmoji}");
            invitedHtml += FormatDisplayLineHtml("First invited on:", DisplayDate(userActivityInfo.InvitedUtc.Value));

            if (userActivityInfo.LastestInviteReminderSentUtc != null) {
              invitedHtml += FormatDisplayLineHtml("Last invite reminder:", DisplayDate(userActivityInfo.LastestInviteReminderSentUtc.Value));
            }
            return invitedHtml;

          } else {
            return FormatDisplayLineHtml(statusLabel, $"Hasn't been invited to the platform {HtmlElements.RedCircleEmoji}");
          }
        }
      }

      private static string GetParticipantSurveyInfoHtml(
        DbHelper.AbleUser.UserActivityInfo userActivityInfo, bool preventAddingTopMargin, UserActivityInfoHtmlType userActivityInfoHtmlType) {
        string developmentInfoHtml = "";

        if (userActivityInfo != null) {
          if (userActivityInfo.LatestCompletedSurveyUtc != null) {
            developmentInfoHtml += FormatDisplayLineHtml("Last Survey:", DisplayDate(userActivityInfo.LatestCompletedSurveyUtc.Value), "", preventAddingTopMargin);
          }

          if (userActivityInfoHtmlType == UserActivityInfoHtmlType.Badge) {
            if (userActivityInfo.LastestCoachingSessionUtc != null) {
              developmentInfoHtml += FormatDisplayLineHtml("Last Coaching Session:", DisplayDate(userActivityInfo.LastestCoachingSessionUtc.Value));
            }
            if (userActivityInfo.LatestWorkshop != null) {
              developmentInfoHtml += FormatDisplayLineHtml("Last Workshop:", DisplayDate(userActivityInfo.LatestWorkshop.Value));
            }
          } else if (userActivityInfo.Latest360CompletedUtc != null) {
            developmentInfoHtml += FormatDisplayLineHtml("Last 360:", DisplayDate(userActivityInfo.Latest360CompletedUtc.Value));
          }

          // Intake info
          string intakeText = "Intake Form:";
          if (userActivityInfo.LatestIntakeCompletedUtc != null) {
            developmentInfoHtml += FormatDisplayLineHtml(intakeText, $"Completed: {DisplayDate(userActivityInfo.LatestIntakeCompletedUtc.Value)}");

          } else if (userActivityInfo.LatestIntakeClosedUtc != null) {
            developmentInfoHtml += FormatDisplayLineHtml(intakeText, "Closed");

          } else if (userActivityInfo.LatestIntakeCreatedOpenUtc != null) {
            developmentInfoHtml += FormatDisplayLineHtml(intakeText, "Open");
          } else {
            developmentInfoHtml += FormatDisplayLineHtml(intakeText, "No intakes.");
          }

          // Always display Last Dev Plan
          developmentInfoHtml += FormatDisplayLineHtml("Last Development Plan:", DisplayDate(userActivityInfo.LatestDevPlanUtc, "None"));
        }

        return developmentInfoHtml;
      }

      private static string GetAIInfoHtml(DbHelper.AbleUser.UserActivityInfo userActivityInfo, DbHelper.Subscriptions.User.UserSubscriptionInfo userSubscriptionInfo, UserActivityInfoHtmlType userActivityInfoHtmlType) {
        string aiInfoHtml = "";
        if (userSubscriptionInfo != null && userSubscriptionInfo.HasAICoaching) {
          aiInfoHtml += FormatDisplayLineHtml($@"Last AI Coach Message:", DisplayDate(userActivityInfo.LastestAIMessageSentUtc, "Never"));

          string fontSizeStyle = userActivityInfoHtmlType == UserActivityInfoHtmlType.Badge ? "9" : "11";
          if (userActivityInfo != null && userActivityInfo.LastestAIMessageSentUtc != null) {
            aiInfoHtml += FormatDisplayLineHtml("AI Coach Msgs:", $@"{userActivityInfo.AIMsgsLast30Days} <span style=""font-size:{fontSizeStyle}px;"">(Last 30 Days)</span>  /  {userActivityInfo.TotalAIMsgs} <span style=""font-size:{fontSizeStyle}px;"">(Total)</span>");
          }
        }
        return aiInfoHtml;
      }
      private static string FormatDisplayLineHtml(string labelTextHtml, string contentColHtml, string stylingClass = "", bool preventAddingTopMargin = false) {

        if (labelTextHtml.IsNullOrEmpty() && contentColHtml.IsNullOrEmpty()) return "";

        var lineHtml = $@"
          <div class=""flex {(preventAddingTopMargin ? "" : "mt10")} {stylingClass}"">
            <span><b>{labelTextHtml}</b></span>
            <div class=""ml5"">
              {contentColHtml}
            </div>
          </div>";

        if (CurrentActivityInfoHtmlType == UserActivityInfoHtmlType.Header) {
          return lineHtml;
        } else {
          return lineHtml.SurroundWith("<p>", "</p>");
        }
      }

      private static string FormatDisplayLineHtml(string contentHtml, bool preventAddingTopMargin = false, string stylingClass = "") {
        if (contentHtml.IsNullOrEmpty()) return "";

        var lineHtml = $@"
          <div class=""flex {(preventAddingTopMargin ? "" : "mt10")} {stylingClass}"">
            {contentHtml}
          </div>";

        if (CurrentActivityInfoHtmlType == UserActivityInfoHtmlType.Header) {
          return lineHtml;
        } else {
          return lineHtml.SurroundWith("<p>", "</p>");
        }
      }
    }
  }
}
