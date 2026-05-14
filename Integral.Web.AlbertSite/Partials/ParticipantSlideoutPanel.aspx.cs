using System;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class ParticipantSlideoutPanel : AppCode.PageBaseClasses.CoacheeInfoBase {

    public string ActivityInfoHtml, UserPhotoUrl, CoacheeEditUrl;

    protected void Page_Load(object sender, EventArgs e) {

      if (CoacheeInfo == null || CoacheeInfo.UserActivity == null) return;

      PageTitle = "Participant Details";

      ActivityInfoHtml = WebHelper.ParticipantActivities.GetSlideoutParticipantUserActivityInfo(CoacheeInfo.UserId.Value, CoacheeInfo.UserActivity, CoacheeInfo.UserSubscription);
      UserPhotoUrl = PathHelper.Images.UserPhoto(CoacheeInfo.FirstName, CoacheeInfo.LastName, PathHelper.Images.UserPhotoSize.Large, true);
      CoacheeEditUrl = PathHelper.Pages.CoacheeEdit(CoacheeInfo.CoacheeId, PathHelper.CoacheeTabEnum.summary);
    }

  }
}
