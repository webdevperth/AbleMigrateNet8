namespace Integral.Web.WebHelpers {

  public class SurveyHelper {

    public enum NavigatePage { Previous = -1, Next = 1 }

    public class InstructionsTags {
      public const string SurveyName = "SurveyName";
      public const string CloseDate = "CloseDate|ClosingDate";
      public const string FirstName = "FirstName";
      public const string LastName = "LastName";
      public const string SelfName = "ParticipantName|SelfName|SelfFullName";
    }

    public static bool ShowSurveyLoggedIn(DbHelper.AlbertSurveys.SurveyInfo surveyInfo, DbHelper.Participants.ParticipantInfo partInfo) {
      return surveyInfo.IsAbleSurvey && partInfo.IsSelf && (surveyInfo.ShowSurveyLoggedIn || SessionHelper.IsUserRoleLeader);
    }

    // Whether the question is visible in this survey for this participant.
    public static bool IsQuestionVisible(
      DbHelper.Questions.SurveyQuestionInfo questionInfo,
      DbHelper.Participants.ParticipantInfo partInfo) {

      if (questionInfo.Hidden) return false;
      if (questionInfo.HideForLevel.Length >= partInfo.QuestionSet && questionInfo.HideForLevel[partInfo.QuestionSet] == true) return false;
      if (partInfo.IsSelf && questionInfo.QuestionTextSelf.ToLower() == "na") return false;
      if (!partInfo.IsSelf && questionInfo.QuestionTextRater.ToLower() == "na") return false;
      return true;
    }

    public static void GetSharedSurveyInfo(out DbHelper.SurveyShare.SharedSurveysInfo SharedSurveyInfo, out bool IsViewingSharedSurvey) {

      SharedSurveyInfo = null;
      IsViewingSharedSurvey = false;

      var surveyShareIdUrl = WebHelper.GetQueryStringInt(PathHelper.AbleUrlKeys.SurveyShareId, null);

      if (surveyShareIdUrl != null) {

        SharedSurveyInfo = DbHelper.SurveyShare.GetSharedSurveyInfo(surveyShareIdUrl.Value);

        if (SharedSurveyInfo != null && SharedSurveyInfo.UserIdSharedWith == SessionHelper.GetUserIdOrNull()) {
          IsViewingSharedSurvey = true;
        }
      }
    }
  }
}
