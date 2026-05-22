using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Page_Partials {

  public class ParticipantSlideoutPanel : AppCode.PageBaseClasses.CoacheeInfoBase {

    public string ActivityInfoHtml, UserPhotoUrl, CoacheeEditUrl;

    public IActionResult OnGet() {

      if (CoacheeInfo == null || CoacheeInfo.UserActivity == null) return Page();

      PageTitle = "Participant Details";

      ActivityInfoHtml = WebHelper.ParticipantActivities.GetSlideoutParticipantUserActivityInfo(CoacheeInfo.UserId.Value, CoacheeInfo.UserActivity, CoacheeInfo.UserSubscription);
      UserPhotoUrl = PathHelper.Images.UserPhoto(CoacheeInfo.FirstName, CoacheeInfo.LastName, PathHelper.Images.UserPhotoSize.Large, true);
      CoacheeEditUrl = PathHelper.Pages.CoacheeEdit(CoacheeInfo.CoacheeId, PathHelper.CoacheeTabEnum.summary);

      return Page();
    }

  }
}
