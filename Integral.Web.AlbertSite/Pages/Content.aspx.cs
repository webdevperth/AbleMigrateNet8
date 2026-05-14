using System;
using System.Collections.Generic;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class Content : AppCode.PageBaseClasses.LoggedInPageBase {

    public bool CanAddContent, CanViewContent, CanSearchContent, IsParticipantView, CanAddModule;
    public enum ParticipantTabsEnum { Featured, Quizes, Program, Favourites, Completed, Modules }
    public enum PartnerTabsEnum { Module, Content }

    private const int MaxContentItemsFromProgramInFeatured = 4;

    public class FormFields {
      public const string SearchTerm = "SearchTerm";
      public const string ContentId = "ContentId";
      public const string IsFavourite = "IsFavourite";
    }
    public class AjaxAction {
      public const string SearchContent = "SearchContent";
      public const string UpdateFavourite = "UpdateFavourite";
    }
    public class AjaxReturnData {
      public const string ContentHtml = "ContentHtml";
    }

    public Dictionary<PartnerTabsEnum, string> PartnerTabsTitle = new Dictionary<PartnerTabsEnum, string>() {
      { PartnerTabsEnum.Module, "Modules" },
      { PartnerTabsEnum.Content, "Microlearnings" }
    };

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Microlearning";

      CanAddContent = SessionHelper.AppAccess.Content.CanAddContent();
      CanSearchContent = SessionHelper.AppAccess.Content.CanSearchContent();
      CanViewContent = SessionHelper.AppAccess.Content.CanViewContentList();
      CanAddModule = SessionHelper.AppAccess.Modules.CanAdd();
      IsParticipantView = SessionHelper.IsUserRoleLeader;

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          switch (PageAjaxAction) {

            case AjaxAction.SearchContent:
              SearchContent(ajax);
              break;

            case AjaxAction.UpdateFavourite:
              UpdateFavourite(ajax);
              break;

          }
        });
        return;
      }
    }

    private void UpdateFavourite(AjaxSubmitHelper ajax) {

      int contentId = WebHelper.GetFormValueIntOrDefault(FormFields.ContentId, 0);
      bool isFavourite = WebHelper.GetFormValueIntOrDefault(FormFields.IsFavourite, 0) == 1;

      var content = DbHelper.Content.GetContentInfo(contentId, SessionHelper.UserInfo, SessionHelper.UserRole);
      if (content != null) {
        DbHelper.Content.UpsertContentFavorite(null, content, SessionHelper.UserInfo, isFavourite);
      }
    }

    private string GetParticipantContentHtml(string searchTerm) {

      var participantContentInfo = DbHelper.Content.GetContentListForUser(SessionHelper.UserInfo, searchTerm);

      var contentHtml = WebHelper.GetPageTabs(
      new WebHelper.PageTabsInfo() { PageTabsStyle = WebHelper.PageTabsStyle.Links },
      new WebHelper.PageTabItem(ParticipantTabsEnum.Modules.ToString(), ParticipantTabsEnum.Modules.ToString(), true),
      new WebHelper.PageTabItem(ParticipantTabsEnum.Featured.ToString(), ParticipantTabsEnum.Featured.ToString()),
      new WebHelper.PageTabItem(ParticipantTabsEnum.Program.ToString(), ParticipantTabsEnum.Program.ToString()),
      new WebHelper.PageTabItem(ParticipantTabsEnum.Favourites.ToString(), ParticipantTabsEnum.Favourites.ToString()),
      new WebHelper.PageTabItem(ParticipantTabsEnum.Completed.ToString(), ParticipantTabsEnum.Completed.ToString()),
      new WebHelper.PageTabItem(ParticipantTabsEnum.Quizes.ToString(), ParticipantTabsEnum.Quizes.ToString()));

      var featuredContentInfo = new List<DbHelper.Content.ParticipantContentInfo>();
      var programContentInfo = new List<DbHelper.Content.ParticipantContentInfo>();
      var favouritesContentInfo = new List<DbHelper.Content.ParticipantContentInfo>();
      var completedContentInfo = new List<DbHelper.Content.ParticipantContentInfo>();
      var quizesContentInfo = new List<DbHelper.Content.ParticipantContentInfo>();

      if (!participantContentInfo.IsNullOrEmpty()) {
        foreach (var pci in participantContentInfo) {

          if (pci.ProgramContentId == null) {
            featuredContentInfo.Add(pci);
          } else if (pci.ProgramContentId != null && !programContentInfo.Exists(x => x.ContentInfo.ContentId == pci.ContentInfo.ContentId)) {
            programContentInfo.Add(pci);
          }

          if (pci.IsFavourite) {
            favouritesContentInfo.Add(pci);
          }

          if (pci.IsCompleted) {
            if ((pci.ContentInfo.ContentType == DbHelper.Content.ContentTypeEnum.LearningActions)) {
              continue;
            }
            completedContentInfo.Add(pci);
          }

          if (pci.ContentInfo.ContentType == DbHelper.Content.ContentTypeEnum.Quiz) {
            quizesContentInfo.Add(pci);
          }
        }
      }

      if (!programContentInfo.IsNullOrEmpty()) {
        // Sort content in the program by display order.
        programContentInfo.Sort((x, y) => x.DisplayOrder.GetValueOrDefault().CompareTo(y.DisplayOrder.GetValueOrDefault()));

        // Take items from Program to include in Featured tab, sorted by last added to Program.
        programContentInfo.Sort((x, y) => y.AddedToProgramUtc.GetValueOrDefault().CompareTo(x.AddedToProgramUtc.GetValueOrDefault()));

        var contentByLastAdded = new List<DbHelper.Content.ParticipantContentInfo>();
        int count = 0;

        foreach (var item in programContentInfo) {
          if (count >= MaxContentItemsFromProgramInFeatured) break;
          contentByLastAdded.Add(item);
          count++;
        }

        featuredContentInfo.AddRange(contentByLastAdded); // Add the items to the featuredContentInfo.
      }

      if (!featuredContentInfo.IsNullOrEmpty()) {
        // Remove duplicate items.
        var uniqueFeaturedContent = new HashSet<int>();
        var filteredFeaturedContent = new List<DbHelper.Content.ParticipantContentInfo>();

        foreach (var item in featuredContentInfo) {
          if (uniqueFeaturedContent.Add(item.ContentInfo.ContentId)) {
            filteredFeaturedContent.Add(item);
          }
        }

        // Sort Featured content by Program (if applies) and then by Content Title alphabetically.
        filteredFeaturedContent.Sort((x, y) => {
          int programComparison = (x.ProgramJobId == null).CompareTo(y.ProgramJobId == null);
          if (programComparison != 0) return programComparison;

          int relevanceComparison = (!x.RelevanceScore.HasValue).CompareTo(!y.RelevanceScore.HasValue);
          if (relevanceComparison != 0) return relevanceComparison;

          return string.Compare(x.ContentInfo.ContentTitle, y.ContentInfo.ContentTitle, StringComparison.Ordinal);
        });

        featuredContentInfo = filteredFeaturedContent;
      }


      if (!completedContentInfo.IsNullOrEmpty()) {
        completedContentInfo.Sort((x, y) => y.ViewedUtc.GetValueOrDefault().CompareTo(x.ViewedUtc.GetValueOrDefault())); // Sort completed by last viewed
      }

      if (!quizesContentInfo.IsNullOrEmpty()) {
        quizesContentInfo.Sort((x, y) => x.CompletedUtc.GetValueOrDefault().CompareTo(y.CompletedUtc.GetValueOrDefault())); // Sort by Responded
      }

      contentHtml += GetPanelHtml(ParticipantTabsEnum.Featured, featuredContentInfo);
      contentHtml += AttachToPanelHtml(ParticipantTabsEnum.Modules.ToString(), GetModulesHtml(searchTerm));
      contentHtml += GetPanelHtml(ParticipantTabsEnum.Quizes, quizesContentInfo);
      contentHtml += GetPanelHtml(ParticipantTabsEnum.Program, programContentInfo);
      contentHtml += GetPanelHtml(ParticipantTabsEnum.Favourites, favouritesContentInfo);
      contentHtml += GetPanelHtml(ParticipantTabsEnum.Completed, completedContentInfo);

      return contentHtml;
    }

    private string GetPanelHtml(ParticipantTabsEnum participantTabs, List<DbHelper.Content.ParticipantContentInfo> contentInfoList) {

      string html = "";

      if (!contentInfoList.IsNullOrEmpty()) {

        foreach (var contentInfo in contentInfoList) {

          var contentBadges = new List<WebHelper.Content.ContentBadgeInfo>();

          if (participantTabs == ParticipantTabsEnum.Featured && contentInfo.RelevanceScore.HasValue) {
            contentBadges.Add(new WebHelper.Content.ContentBadgeInfo(WebHelper.Content.ContentCardBadgeTypeEnum.AI));
          }

          if (contentInfo.IsCompleted && contentInfo.ContentInfo.ContentType != DbHelper.Content.ContentTypeEnum.Quiz && contentInfo.ContentInfo.ContentType != DbHelper.Content.ContentTypeEnum.LearningActions) {
            if (participantTabs == ParticipantTabsEnum.Featured) {
              continue;
            } else {
              contentBadges.Add(new WebHelper.Content.ContentBadgeInfo(WebHelper.Content.ContentCardBadgeTypeEnum.Read));
            }
          }

          if (contentInfo.IsRespondedQuiz) {
            string customClass = contentInfo.QuizScore > 60 ? "quiz-passed" : "quiz-notpassed";
            string customBadgeText = $"Score: {contentInfo.QuizScore?.ToString("0")}%";
            contentBadges.Add(new WebHelper.Content.ContentBadgeInfo(WebHelper.Content.ContentCardBadgeTypeEnum.Custom, customBadgeText, customClass));
          }

          if (contentInfo.ContentInfo.ContentType == DbHelper.Content.ContentTypeEnum.LearningActions && contentInfo.IsCompleted) {
            contentBadges.Add(new WebHelper.Content.ContentBadgeInfo(WebHelper.Content.ContentCardBadgeTypeEnum.Custom, "Completed", "complete"));
          }

          var buttonType = WebHelper.Content.ContentCardButtonTypeEnum.None;
          if (contentInfo.IsFavourite) {
            buttonType = WebHelper.Content.ContentCardButtonTypeEnum.Favourited;
          } else {
            buttonType = WebHelper.Content.ContentCardButtonTypeEnum.ToBeFavourited;
          }

          html += WebHelper.Content.GetContentCardHtml(contentInfo, buttonType, contentBadges);
        }

      } else {

        html = WebHelper.GetNoRecordsBadge("No microlearning found");
      }

      return $@"
        <div class=""tab-panel"" data-appendTo=""panel-{participantTabs}"">
          <div class=""content-card-container"">
            {html}
          </div>
        </div>";
    }

    public void SearchContent(AjaxSubmitHelper ajax) {

      string searchContent = "", contentHtml = "";

      if (CanSearchContent) {
        searchContent = ajax.CheckFieldRegex(FormFields.SearchTerm, "Search Term", AppHelper.Regex.GeneralText, false, "").EmptyIfNull();
      }

      if (IsParticipantView) {
        contentHtml = GetParticipantContentHtml(searchContent);
      } else {
        contentHtml = GetPartnerViewHtml(searchContent);
      }

      ajax.AddReturnValue(AjaxReturnData.ContentHtml, contentHtml);
    }

    private string GetPartnerViewHtml(string searchTerm) {

      var contentHtml = WebHelper.GetPageTabs(
      new WebHelper.PageTabsInfo() { PageTabsStyle = WebHelper.PageTabsStyle.Links },
      new WebHelper.PageTabItem(PartnerTabsEnum.Module.ToString(), PartnerTabsTitle[PartnerTabsEnum.Module], true),
      new WebHelper.PageTabItem(PartnerTabsEnum.Content.ToString(), PartnerTabsTitle[PartnerTabsEnum.Content]));

      contentHtml += AttachToPanelHtml(PartnerTabsEnum.Module.ToString(), GetModulesHtml(searchTerm));
      contentHtml += GetContentHtml(searchTerm);

      return contentHtml;
    }

    private string GetContentHtml(string searchTerm) {
      var contentInfoList = DbHelper.Content.GetContentForUserAndSearchTerm(SessionHelper.UserInfo, SessionHelper.GetUserRole(), searchTerm);
      return AttachToPanelHtml(PartnerTabsEnum.Content.ToString(), WebHelper.Content.GetContentCardsForPartnerHtml(contentInfoList, WebHelper.Content.ViewFromPageEnum.Content));
    }

    private string GetModulesHtml(string searchTerm) {

      List<DbHelper.Modules.ModuleInfo> moduleInfoList = null;
      List<DbHelper.Modules.ParticipantModuleInfo> participantModuleInfo = null;

      moduleInfoList = DbHelper.Modules.GetModuleForUserAndSearchTerm(null, SessionHelper.UserInfo, SessionHelper.GetUserRole(), searchTerm);
      if (IsParticipantView) {
        participantModuleInfo = DbHelper.Modules.GetParticipantModuleInfo(null, SessionHelper.UserInfo, SessionHelper.GetUserRole());
      }

      if (IsParticipantView) {

        return WebHelper.Modules.GetModuleCardsHtml_Participant(participantModuleInfo, moduleInfoList);

      } else if (moduleInfoList != null) {

        return WebHelper.Modules.GetModuleCardsHtml(moduleInfoList);
      }

      return WebHelper.GetNoRecordsBadge("No modules found");
    }

    private string AttachToPanelHtml(string htmlTabName, string panelHtml) {
      return $@"
        <div class=""tab-panel"" data-appendTo=""panel-{htmlTabName}"">
          <div class="""">
            {panelHtml}
          </div>
        </div>";
    }
  }
}
