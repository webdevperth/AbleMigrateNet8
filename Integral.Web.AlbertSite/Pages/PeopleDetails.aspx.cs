using System;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class PeopleDetails : AppCode.PageBaseClasses.CompanyInfoBase {

    public DbHelper.OrganisationUsers.ProfileInfo ProfileInfo;
    public DbHelper.AlbertSurveys.SurveyInfo Latest360Survey;
    public DbHelper.AlbertCoachees.AlbertCoacheeInfo Latest360CoacheeInfo;
    public DbHelper.AblePrograms.AbleProgramInfo Latest360ProgramInfo;
    public bool HasPreSurvey, HasLatest360;
    public int PreSurveyIntakeId, PreSurveyPartId;
    public string PreSurveyUId, PreSurveyPartUId;
    public DateTime PreSurveyCompletedUtc;

    protected void Page_Load(object sender, EventArgs e) {

      // If Participant user is viewing, they're viewing their own, set title to Development.
      PageTitle = SessionHelper.IsUserRoleLeader ? "Development" : "Participant Details";

      if (!WebHelper.TryGetQueryStringGuid(PathHelper.AbleUrlKeys.UserGuid, out var userGuid)) {
        SetRedirectToReferrer();
        return;
      }

      ProfileInfo = DbHelper.OrganisationUsers.GetProfileInfo(userGuid, SessionHelper.IsUserRoleLeader);
      if (ProfileInfo == null) {
        SetRedirectToReferrer();
        return;
      }

      if (!SessionHelper.AppAccess.Reports.CanViewPeopleDetails(ProfileInfo)) {
        SetRedirectToReferrer();
        return;
      }

      if (ProfileInfo.UserActivity?.Latest360CoacheeId != null) {

        _ = DbHelper.AlbertSurveys.GetSurveyInfoListForCoachee(
          ProfileInfo.UserActivity.Latest360CoacheeId.Value,
          ProfileInfo.UserActivity.Latest360SvUID,
          ProfileInfo.UserActivity.Latest360PartUID,
          DbHelper.AlbertSurveys.OnlyViewerCompatible.Yes,
          SessionHelper.IsUserRoleLeader ? DbHelper.AlbertSurveys.OnlyWhereLeaderCanView360Report.Yes : DbHelper.AlbertSurveys.OnlyWhereLeaderCanView360Report.No,
          out Latest360Survey);

        if (Latest360Survey?.FoundParticipantBrief?.CoacheeId != null) {

          Latest360CoacheeInfo = DbHelper.AlbertCoachees.GetCoacheeInfo(Latest360Survey.FoundParticipantBrief.CoacheeId.Value);

          if (Latest360CoacheeInfo != null) {

            HasLatest360 = true;

            Latest360ProgramInfo = DbHelper.AblePrograms.GetProgramInfoOrNull(Latest360CoacheeInfo.ProgramJobId.Value);

            DbHelper.AlbertSurveys.GetFirstCompletedPreSurvey(
              DbHelper.AlbertSurveys.GetPreSurveyBy.CoacheeId,
              Latest360Survey, Latest360CoacheeInfo,
              out HasPreSurvey, out PreSurveyUId, out PreSurveyIntakeId,
              out PreSurveyPartUId, out PreSurveyPartId, out PreSurveyCompletedUtc);
          }
        }
      }

    }
  }
}
