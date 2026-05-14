using System.Collections.Generic;
using System;

namespace Integral.Web {

  public partial class WebHelper {

    public class Content {

      public enum ContentCardButtonTypeEnum { None, Add, Remove, ToBeFavourited, Favourited }
      public enum ContentCardBadgeTypeEnum { None, Read, AI, In_Workshop, Draft, Custom }
      public enum ViewFromPageEnum { Content, ProgramContent, ModuleEdit, Module }

      internal static readonly Dictionary<Enum, string> BadgeTextForType = new Dictionary<Enum, string> {
        { ContentCardBadgeTypeEnum.None, "" },
        { ContentCardBadgeTypeEnum.Draft, "Draft" },
        { ContentCardBadgeTypeEnum.In_Workshop, "In Workshop" },
        { ContentCardBadgeTypeEnum.Read, "Read" }
      };

      public class CSS {
        public const string ContentContainer = "content-container";
        public const string ContentCardContainer = "content-card-container";
        public const string ProgramContentContainer = "program-content-container";
        public const string AddContentButton = "btnAdd";
        public const string RemoveContentButton = "btnRemove";
        public const string FavouriteContentButton = "btnFavourite";
        public const string ContentCard = "content-card";
        public const string ContentCardTopContainer = "top-container";
        public const string RemoveOptionButtonInTable = "btnRemoveOption";
        public const string ContentLibraryContainer = "content-library-container";
      }

      public class DataAttrs {
        public const string ContentId = "contentid";
      }

      public class ContentBadgeInfo {
        public ContentCardBadgeTypeEnum ContentCardBadgeType { get; private set; }
        public string CustomBadgeText { get; private set; }
        public string CustomClass { get; private set; }
        public ContentBadgeInfo(ContentCardBadgeTypeEnum contentCardBadgeType, string customBadgeText = "", string customClass = "") {
          this.ContentCardBadgeType = contentCardBadgeType;
          this.CustomBadgeText = customBadgeText;
          this.CustomClass = customClass;
        }
      }

      public static ActionButtonTypeEnum GetContentTypeIconEnum(DbHelper.Content.ContentTypeEnum contentType) {
        switch (contentType) {
          case DbHelper.Content.ContentTypeEnum.Text:
            return ActionButtonTypeEnum.text;
          case DbHelper.Content.ContentTypeEnum.Document:
            return ActionButtonTypeEnum.document;
          case DbHelper.Content.ContentTypeEnum.Image:
            return ActionButtonTypeEnum.image;
          case DbHelper.Content.ContentTypeEnum.Url:
            return ActionButtonTypeEnum.url;
          case DbHelper.Content.ContentTypeEnum.Video:
            return ActionButtonTypeEnum.video;
          case DbHelper.Content.ContentTypeEnum.Quiz:
            return ActionButtonTypeEnum.apps;
          case DbHelper.Content.ContentTypeEnum.LearningActions:
            return ActionButtonTypeEnum.learning;
          default:
            return ActionButtonTypeEnum.text;
        }
      }

      public static string GetContentTypeTextDisplay(DbHelper.Content.ContentTypeEnum contentType) {
        switch (contentType) {
          case DbHelper.Content.ContentTypeEnum.Text:
            return "Text";
          case DbHelper.Content.ContentTypeEnum.Document:
            return "Document";
          case DbHelper.Content.ContentTypeEnum.Image:
            return "Image";
          case DbHelper.Content.ContentTypeEnum.Url:
            return "Url";
          case DbHelper.Content.ContentTypeEnum.Video:
            return "Video";
          case DbHelper.Content.ContentTypeEnum.Quiz:
            return "Quiz";
          case DbHelper.Content.ContentTypeEnum.LearningActions:
            return "Learning Actions";
          default:
            return "";
        }
      }

      public static string GetContentCardHtml(DbHelper.Content.ParticipantContentInfo participantContentInfo,
      ContentCardButtonTypeEnum contentButtonType = ContentCardButtonTypeEnum.None,
      List<ContentBadgeInfo> contentBadgeType = null) {

        var contentInfo = participantContentInfo.ContentInfo;
        string contentPath = participantContentInfo.AddedToProgramUtc != null ? PathHelper.Pages.ProgramContentDetails(participantContentInfo.ProgramJobId, contentInfo.ContentGuid) : PathHelper.Pages.ContentDetails(contentInfo.ContentGuid);
        string authorImagePath = PathHelper.Images.UserPhoto(contentInfo.AuthorFirstName, contentInfo.AuthorLastName, PathHelper.Images.UserPhotoSize.Thumbnail, true);

        return GetContentCardItemHtml(contentInfo, contentPath, authorImagePath, contentButtonType, contentBadgeType);
      }

      public static string GetContentCardHtml(DbHelper.Content.ContentInfo contentInfo,
      ContentCardButtonTypeEnum contentButtonType = ContentCardButtonTypeEnum.None,
      List<ContentBadgeInfo> contentBadgeType = null) {

        string contentPath = PathHelper.Pages.ContentDetails(contentInfo.ContentGuid);
        string authorImagePath = PathHelper.Images.UserPhoto(contentInfo.AuthorFirstName, contentInfo.AuthorLastName, PathHelper.Images.UserPhotoSize.Thumbnail, true);

        return GetContentCardItemHtml(contentInfo, contentPath, authorImagePath, contentButtonType, contentBadgeType);
      }

      public static string GetContentCardItemHtml(DbHelper.Content.ContentInfo contentInfo, string contentPath, string authorImagePath,
        ContentCardButtonTypeEnum contentCardButtonType = ContentCardButtonTypeEnum.None,
        List<ContentBadgeInfo> cardBadgeList = null) {

        return $@"
        <div class=""{CSS.ContentCard}"">
          {GetContentCardTopHtml(contentInfo, contentCardButtonType, cardBadgeList)}
          <a href=""{contentPath}"">
            {GetCoverImageHtml(contentInfo)}
            <div class=""content-card-items"">
              <h3>{contentInfo.ContentTitle.HTMLEncode()}</h3>
              <p>{contentInfo.ContentSummary.HTMLEncode()}</p>
              <hr />
              <div class=""details"">
                {GetContentTypeBadgeHtml(contentInfo.ContentType)}
                <div class=""creator ml10"">
                  <img src=""{authorImagePath.HTMLEncode()}"" />
                  <span class=""ml5"">{contentInfo.GetAuthorFullName().HTMLEncode()}</span>
                </div>
              </div>
            </div>
          </a>
        </div>";
      }

      public static string GetContentTypeBadgeHtml(DbHelper.Content.ContentTypeEnum contentType) {
        return $@"
          <span class=""badge"">
            {GetIconHtml(GetContentTypeIconEnum(contentType), "mr5")}
            {GetContentTypeTextDisplay(contentType)}
          </span>";
      }

      private static string GetContentCardTopHtml(DbHelper.Content.ContentInfo contentInfo,
        ContentCardButtonTypeEnum contentCardButtonType = ContentCardButtonTypeEnum.None,
        List<ContentBadgeInfo> cardBadgeList = null) {

        string cardTopHtml = "";

        if (!cardBadgeList.IsNullOrEmpty()) {
          string badgeHtml = "";

          foreach (var badge in cardBadgeList) {
            var badgeText = "";

            if (badge.ContentCardBadgeType == ContentCardBadgeTypeEnum.Custom && !badge.CustomBadgeText.IsNullOrEmpty()) {
              badgeText = badge.CustomBadgeText;
            } else {
              BadgeTextForType.TryGetValue(badge.ContentCardBadgeType, out badgeText);
            }

            if (badge.ContentCardBadgeType == ContentCardBadgeTypeEnum.AI) {
              badgeHtml += $"<img src=\"{PathHelper.Images.AIIcon()}\" alt=\"AI\">";
            } else if (!badgeText.IsNullOrEmpty()) {
              badgeHtml += $"<span class=\"badge {(badge.CustomClass.EnsureStartsWith("badge-", true))}\">{badgeText.HTMLEncode()}</span>";
            }
          }

          if (!badgeHtml.IsNullOrEmpty()) {
            cardTopHtml += $@"
            <div class=""badge-container"">
              {badgeHtml}
            </div>";
          }
        }

        if (contentCardButtonType != ContentCardButtonTypeEnum.None) {

          string btnClasses = "";
          if (contentCardButtonType == ContentCardButtonTypeEnum.Add) {
            btnClasses = $"{CSS.AddContentButton} btn-primary";

          } else if (contentCardButtonType == ContentCardButtonTypeEnum.Remove) {
            btnClasses = $"{CSS.RemoveContentButton} btn-danger";

          } else if (contentCardButtonType == ContentCardButtonTypeEnum.Favourited || contentCardButtonType == ContentCardButtonTypeEnum.ToBeFavourited) {
            btnClasses = CSS.FavouriteContentButton;
          }

          if (contentCardButtonType == ContentCardButtonTypeEnum.Favourited || contentCardButtonType == ContentCardButtonTypeEnum.ToBeFavourited) {

            string iconName = "heart" + (contentCardButtonType == ContentCardButtonTypeEnum.Favourited ? "" : "-outline");
            string isFavourited = contentCardButtonType == ContentCardButtonTypeEnum.Favourited ? "1" : "0";
            string iconTitle = contentCardButtonType == ContentCardButtonTypeEnum.Favourited ? "Favourited" : "Favourite it";

            cardTopHtml += $@"
            <span class=""{btnClasses}"" data-ContentId=""{contentInfo.ContentId}"" data-isfavourited=""{isFavourited}"">
              <ion-icon name=""{iconName}"" title=""{iconTitle}""></ion-icon>
            </span>";

          } else if (!btnClasses.IsNullOrEmpty()) {

            cardTopHtml += $@"
            <button class=""{btnClasses} btn btn-xsm"" data-{DataAttrs.ContentId}=""{contentInfo.ContentId}"">
              {contentCardButtonType}
            </button>";
          }
        }

        return cardTopHtml.SurroundWith($"<div class=\"{CSS.ContentCardTopContainer}\">", "</div>", false);
      }

      public static string GetContentCardsForPartnerHtml(List<DbHelper.Content.ContentInfo> contentInfoList, ViewFromPageEnum viewFromPage) {

        if (contentInfoList.IsNullOrEmpty()) return GetNoRecordsBadge("No microlearning found".HTMLEncode());

        string html = "";

        var cardButton = ContentCardButtonTypeEnum.None;

        if (viewFromPage == ViewFromPageEnum.ModuleEdit) {
          cardButton = ContentCardButtonTypeEnum.Add;
        }

        foreach (var contentInfo in contentInfoList) {

          var contentBadges = new List<ContentBadgeInfo>();
          if (!contentInfo.IsPublished) {
            contentBadges.Add(new ContentBadgeInfo(ContentCardBadgeTypeEnum.Draft));
          }

          html += GetContentCardHtml(contentInfo, cardButton, contentBadges);
        }

        return $@"
        <div class=""{CSS.ContentCardContainer}"">
          {html}
        </div>"; ;
      }

      public static string GetContentAuthorDropdownHtml(PartnerDropdownInfo dropdownInfo) {

        string dropdownOptions = GetPartnerDropdownOptionsHtml(dropdownInfo);

        var selectOptions = new SelectInfo() {
          IsReadOnly = dropdownInfo.IsReadOnly,
          InputName = dropdownInfo.FormName,
          Class = CSSClasses.PartnerDropdownClass,
          TopOptionsHtml = dropdownOptions,
          Size = dropdownInfo.InputCols.GetValueOrDefault(BootstrapCols.FormContent_Legacy)
        };

        return GetSelect(selectOptions);
      }

      public static string GetDeleteButtonForTable(string customClass) {
        return $@"<button type=""button"" class=""btn btn-danger btn-sm {customClass}""><ion-icon name=""trash-outline"" title=""Remove Option""></ion-icon></button>";
      }

      public static string GetCoverImageHtml(DbHelper.Content.ContentInfo contentInfo) {

        string imageSrc = PathHelper.Images.ContentCoverImage(contentInfo.ContentGuid, PathHelper.Images.ContentCoverImageSize.DetailPage, false);

        if (imageSrc.IsNullOrEmpty()) {

          return $@"
            <div class=""defaultImage"">
              <div class=""flex details"">
                {GetIconHtml(GetContentTypeIconEnum(contentInfo.ContentType))}
              </div>
            </div>";

        } else {

          return $@"<img src=""{imageSrc.HTMLEncode()}"" alt=""{contentInfo.ContentTitle.HTMLEncode()}"">";
        }
      }
    }
  }
}
