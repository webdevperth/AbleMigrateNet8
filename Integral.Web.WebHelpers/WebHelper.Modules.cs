using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Integral.Web.WebHelper.Content;

namespace Integral.Web {
  public partial class WebHelper {
    public class Modules {

      public const string BtnShowModuleLibrary_ExploreLibrary = "Explore Library";
      public const string BtnShowModuleLibrary_HideLibrary = "Hide Library";

      public enum ModuleCardButtonTypeEnum { None, Add, Remove }
      public enum ModuleCardBadgeTypeEnum { None, Draft, Custom, Enrolled, EnrolmentPending, Completed }

      internal static readonly Dictionary<Enum, string> BadgeTextForType = new Dictionary<Enum, string> {
        { ModuleCardBadgeTypeEnum.None, "" },
        { ModuleCardBadgeTypeEnum.Draft, "Draft" },
        { ModuleCardBadgeTypeEnum.Enrolled, "Enrolled" },
        { ModuleCardBadgeTypeEnum.EnrolmentPending, "Enrolment Pending" },
        { ModuleCardBadgeTypeEnum.Completed, "Completed" }
      };

      public class ModuleBadgeInfo {
        public ModuleCardBadgeTypeEnum ModuleCardBadgeType { get; private set; }
        public string CustomBadgeText { get; private set; }
        public string CustomClass { get; private set; }
        public ModuleBadgeInfo(ModuleCardBadgeTypeEnum moduleCardBadgeType, string customBadgeText = "", string customClass = "") {
          this.ModuleCardBadgeType = moduleCardBadgeType;
          this.CustomBadgeText = customBadgeText;
          this.CustomClass = customClass;
        }
      }

      public class CSS {
        public const string ModuleContentTable = "ModuleContentTable";
        public const string ProgramModuleContainer = "program-module-container";
        public const string ModuleLibraryContainer = "module-library-container";
        public const string ModuleLibraryShowPanel = "module-library-show"; // Used to show or hide from Participant in Content page, Module Panel
        public const string BtnShowModuleLibrary = "btn-show-module-library";
      }

      public class DataAttrs {
        public const string ModuleId = "moduleid";
        public const string DisplayOrder = "displayorder";
      }

      public class ModuleCardInfo {
        public DbHelper.Modules.ModuleInfo ModuleInfo = null;
        public DbHelper.Modules.UserModuleContentInfo UserModuleContentInfo = null;
        public string ModuleUrlPath = "";
        public string CoverImagePath = "";
        public string AuthorImagePath = "";
        public ModuleCardButtonTypeEnum ModuleCardButtonType = ModuleCardButtonTypeEnum.None;

        // Participant Info
        public bool IsUserEnrolled = false;
        public bool IsEnrolmentPending = false;
        public bool IsCompleted = false;
        public DateTime? StartUtc = null;
        public DateTime? DueUtc = null;
        public int CompletedPercentage = 0;
      }

      public static ModuleCardInfo GetModuleCardInfo(DbHelper.Modules.ModuleInfo moduleInfo, ModuleCardButtonTypeEnum moduleCardButtonType = ModuleCardButtonTypeEnum.None) {

        if (moduleInfo == null) return null;

        return new ModuleCardInfo {
          ModuleInfo = moduleInfo,
          ModuleUrlPath = SessionHelper.IsUserRoleLeader ? PathHelper.Pages.Module(moduleInfo.ModuleGuid) : PathHelper.Pages.ModuleEdit(moduleInfo.ModuleGuid),
          CoverImagePath = PathHelper.Images.ContentCoverImage(moduleInfo.ModuleGuid, PathHelper.Images.ContentCoverImageSize.Card),
          AuthorImagePath = PathHelper.Images.UserPhoto(moduleInfo.AuthorFirstName, moduleInfo.AuthorLastName, PathHelper.Images.UserPhotoSize.Thumbnail, true),
          ModuleCardButtonType = moduleCardButtonType
        };
      }

      public static ModuleCardInfo GetModuleCardInfo(DbHelper.Modules.ParticipantModuleInfo participantModuleInfo) {

        if (participantModuleInfo == null) return null;

        var info = GetModuleCardInfo(participantModuleInfo.ModuleInfo);

        // Extend it with participant-specific data
        info.IsCompleted = participantModuleInfo.IsCompleted;
        info.IsEnrolmentPending = participantModuleInfo.IsEnrolmentPending;
        info.IsUserEnrolled = participantModuleInfo.IsUserEnrolled;
        info.StartUtc = participantModuleInfo.ModuleStartUtc;
        info.DueUtc = participantModuleInfo.ModuleDueUtc;
        info.CompletedPercentage = participantModuleInfo.CompletedPercentage;

        return info;
      }

      private static string GetRowDataAttrs(Guid? moduleGuid, bool isRowLink, Guid contentGuid, int contentId, int displayOrder) {
        if (isRowLink) {
          return $@"data-rowlink-url=""{(PathHelper.Pages.ContentDetails(contentGuid, moduleGuid))}""";
        } else {
          return $@"data-{DataAttrs.DisplayOrder}=""{displayOrder}"" data-{Content.DataAttrs.ContentId}=""{contentId}""";
        }
      }

      public static string GetTableRowHtmlForPage_ModuleEdit(DbHelper.Modules.ModuleInfo moduleInfo, DbHelper.Modules.ModuleContentDetailsInfo moduleItem, bool canDeleteContentItems, Content.ViewFromPageEnum viewFromPage) {

        if (moduleInfo == null) return "";

        return GetTableRowHtmlForPage(moduleInfo, moduleItem, canDeleteContentItems, viewFromPage);
      }

      private static string GetTableRowHtmlForPage(DbHelper.Modules.ModuleInfo moduleInfo, DbHelper.Modules.ModuleContentDetailsInfo moduleItem, bool canDeleteContentItems, Content.ViewFromPageEnum viewFromPage) {
        string columnsBasedOnPageHtml = string.Empty;

        if (moduleInfo == null) return "";

        if (viewFromPage == ViewFromPageEnum.ModuleEdit) {

          string actionBtnsHtml = GetActionButtonsHtml(moduleItem, canDeleteContentItems);

          if (!actionBtnsHtml.IsNullOrEmpty()) {
            columnsBasedOnPageHtml = $@"<td class=""w75"">{actionBtnsHtml}</td>";
          }

          return GetModuleContentRowHtml(moduleInfo, moduleItem.ContentGuid, moduleItem.ContentId, moduleItem.ContentSummary, moduleItem.ContentTitle, moduleItem.DisplayOrder,
            moduleItem.ContentType, moduleItem.AuthorFirstName, moduleItem.AuthorLastName, moduleItem.AuthorUserId, viewFromPage, columnsBasedOnPageHtml);
        }

        return "";
      }

      private static string GetTableRowHtmlForPage(ModuleCardInfo moduleCardInfo, Content.ViewFromPageEnum viewFromPage) {
        string columnsBasedOnPageHtml = string.Empty;

        if (moduleCardInfo == null) return "";

        if (viewFromPage == ViewFromPageEnum.Module) {

          if (moduleCardInfo.UserModuleContentInfo == null) {
            return "<td col-span=\"4\">No Microlearning has been added to Module yet.</td>";
          }

          columnsBasedOnPageHtml = $@"
            <td class=""type-status"">
              {GetStatusBadge($"{(moduleCardInfo.UserModuleContentInfo.IsCompleted && moduleCardInfo.IsUserEnrolled ? "Complete" : "Pending")}")}
            </td>";

          return GetModuleContentRowHtml(moduleCardInfo.ModuleInfo, moduleCardInfo.UserModuleContentInfo.ContentGuid, moduleCardInfo.UserModuleContentInfo.ContentId, moduleCardInfo.UserModuleContentInfo.ModuleContentDetailsInfo.ContentSummary,
            moduleCardInfo.UserModuleContentInfo.ModuleContentDetailsInfo.ContentTitle, moduleCardInfo.UserModuleContentInfo.ModuleContentDetailsInfo.DisplayOrder, moduleCardInfo.UserModuleContentInfo.ModuleContentDetailsInfo.ContentType,
            moduleCardInfo.UserModuleContentInfo.ModuleContentDetailsInfo.AuthorFirstName, moduleCardInfo.UserModuleContentInfo.ModuleContentDetailsInfo.AuthorLastName, moduleCardInfo.UserModuleContentInfo.ModuleContentDetailsInfo.AuthorUserId,
            viewFromPage, columnsBasedOnPageHtml);
        }

        return "";
      }

      private static string GetModuleContentRowHtml(DbHelper.Modules.ModuleInfo moduleInfo, Guid contentGuid, int contentId, string contentSummary, string contentTitle, int displayOrder,
        DbHelper.Content.ContentTypeEnum contentType, string authorFirstName, string authorLastName, int authorUserId,
        Content.ViewFromPageEnum viewFromPage, string columnsBasedOnPageHtml) {

        if (moduleInfo == null) return "";

        string profileAvatar = PathHelper.Images.UserPhoto(authorFirstName, authorLastName, PathHelper.Images.UserPhotoSize.Thumbnail, true);
        bool isRowLink = viewFromPage == ViewFromPageEnum.Module;

        return $@"
            <tr tabindex=""0"" class=""content-item"" {(GetRowDataAttrs(moduleInfo.ModuleGuid, isRowLink, contentGuid, contentId, displayOrder))}>
              <td class=""type-description""><b>{contentTitle}</b><br/>{contentSummary.LimitLengthTo(100, "...")}</td>
              <td class=""type-user-nameWithAvatar"">{GetAvatarForTable_User(profileAvatar, authorFirstName + " " + authorLastName, authorUserId)}</td>
              <td class=""type-delivery"">{Content.GetContentTypeBadgeHtml(contentType)}</td>
              {columnsBasedOnPageHtml}
            </tr>";
      }

      public static string GetModuleContentTableHtml(DbHelper.Modules.ModuleInfo moduleInfo, List<DbHelper.Modules.ModuleContentDetailsInfo> modulesContentList, bool canDeleteContentItems, Content.ViewFromPageEnum viewFromPage) {
        string rowsHtml = "";

        if (!modulesContentList.IsNullOrEmpty()) {
          foreach (var content in modulesContentList) {
            rowsHtml += GetTableRowHtmlForPage(moduleInfo, content, canDeleteContentItems, viewFromPage);
          }
        }

        return GetModuleContentTableHtml(rowsHtml, viewFromPage);
      }

      public static string GetModuleContentTableHtml(DbHelper.Modules.ModuleInfo moduleInfo, List<DbHelper.Modules.UserModuleContentInfo> modulesContentList, DbHelper.Modules.ParticipantModuleInfo participantModuleInfo, Content.ViewFromPageEnum viewFromPage) {
        string rowsHtml = "";

        if (moduleInfo == null) return "";

        if (!modulesContentList.IsNullOrEmpty()) {
          foreach (var userContent in modulesContentList) {
            var moduleCardData = participantModuleInfo != null ? GetModuleCardInfo(participantModuleInfo) : GetModuleCardInfo(moduleInfo);
            moduleCardData.UserModuleContentInfo = userContent; // Content items in Module
            rowsHtml += GetTableRowHtmlForPage(moduleCardData, viewFromPage);
          }
        }

        return GetModuleContentTableHtml(rowsHtml, viewFromPage);
      }

      private static string GetModuleContentTableHtml(string tableRowsHtml, Content.ViewFromPageEnum viewFromPage) {

        bool isRowLink = viewFromPage == ViewFromPageEnum.Module; // Row re-directs to a url
        string customClass = "w75", columnText = "";

        if (viewFromPage == ViewFromPageEnum.ModuleEdit) {
          customClass = "w75";
        } else if (viewFromPage == ViewFromPageEnum.Module) {
          customClass = "type-status";
          columnText = "Status";
        }

        return $@"
          <div class=""table-responsive"">
            <table class=""table table-bordered table-hover {(isRowLink ? "table-rowlink" : "")}"" {(isRowLink ? "data-rowlink-url=\"\"" : "")}>
              <thead>
                <tr>
                  <th class=""type-description"">Content</th>
                  <th class=""type-user-nameWithAvatar"">Author</th>
                  <th class=""type-delivery"">Mode</th>
                  <th class=""{customClass}"">{columnText}</th>
                </tr>
              </thead>
              <tbody class=""{CSS.ModuleContentTable}"">
                {tableRowsHtml}
              </tbody>
            </table>
          </div>";
      }

      public static string GetActionButtonsHtml(DbHelper.Modules.ModuleContentDetailsInfo moduleContent, bool canDeleteContentItems) {

        if (!SessionHelper.AppAccess.Modules.CanViewActionButtons()) return "";

        string html = "";

        html += GetActionLink(
          action: ActionButtonTypeEnum.view,
          customClasses: "",
          linkText: "",
          toolTipText: "View Content Details",
          url: PathHelper.Pages.ContentDetails(moduleContent.ContentGuid),
          targetNewTab: TargetNewTab.Yes);

        if (canDeleteContentItems) {
          html += GetActionButton(
          action: ActionButtonTypeEnum.remove,
          customClasses: $"btn btn-danger {Content.CSS.RemoveOptionButtonInTable}",
          isDisabled: false,
          toolTipText: "Remove Content Items",
          dataAttributes: new DataAttributes((Content.DataAttrs.ContentId, moduleContent.ContentId.ToString())));
        }

        return $@"
          <div class=""action-button-list"">
            {html}
          </div>";
      }

      public static string GetModuleLibraryCardsForProgramHtml(List<DbHelper.Modules.ModuleInfo> moduleInfoList) {
        ModuleCardButtonTypeEnum moduleButtonType = ModuleCardButtonTypeEnum.Add;
        return GetModuleCardsHtml(moduleInfoList, moduleButtonType);
      }

      public static string GetModuleCardsInProgramHtml(List<DbHelper.Modules.ContentModulesInProgramInfo> contentModulesInProgram) {
        ModuleCardButtonTypeEnum moduleButtonType = ModuleCardButtonTypeEnum.Remove;
        var moduleInfoList = contentModulesInProgram.Select(x => x.ModuleInfo).ToList();

        return GetModuleCardsHtml(moduleInfoList, moduleButtonType, CSS.ProgramModuleContainer);
      }

      public static string GetModuleCardsHtml(List<DbHelper.Modules.ModuleInfo> moduleInfoList) {
        return GetModuleCardsHtml(moduleInfoList, ModuleCardButtonTypeEnum.None, "");
      }

      public static string GetModuleCardsHtml_Participant(List<DbHelper.Modules.ParticipantModuleInfo> participantModuleInfo, List<DbHelper.Modules.ModuleInfo> moduleInfoList) {

        StringBuilder activeCards = new StringBuilder();
        StringBuilder inactiveCards = new StringBuilder();

        if (!participantModuleInfo.IsNullOrEmpty()) {
          foreach (var pmi in participantModuleInfo) {

            var moduleCardInfo = GetModuleCardInfo(pmi);
            activeCards.Append(GetModuleCardItemHtml(moduleCardInfo));
          }
        }

        if (!moduleInfoList.IsNullOrEmpty()) {
          foreach (var mi in moduleInfoList) {

            var moduleCardInfo = GetModuleCardInfo(mi);
            inactiveCards.Append(GetModuleCardItemHtml(moduleCardInfo));
          }
        }

        StringBuilder html = new StringBuilder();
        bool hasActiveCards = activeCards.Length > 0;

        if (hasActiveCards) {
          html.Append(GetCardsInHtmlContainer(activeCards.ToString(), ""));
        }

        if (inactiveCards.Length > 0) {

          if (hasActiveCards) {
            html.Append($@"<div class=""{CSS.ModuleLibraryShowPanel} {(hasActiveCards ? "displaynone" : "")}"">");
            html.Append("<hr/>");
            html.Append("<h3 class=\"mt0\">Module Library</h3>");
            html.Append(GetCardsInHtmlContainer(inactiveCards.ToString(), ""));
            html.Append("</div>");
            html.Append("<hr/>");
            html.Append($@"<button class=""{CSS.BtnShowModuleLibrary} btn btn-m mb20 float-right btn-primary"">{BtnShowModuleLibrary_ExploreLibrary}</button>");
          } else {

            html.Append(GetCardsInHtmlContainer(inactiveCards.ToString(), ""));
          }

        }

        return html.ToString();
      }

      public static string GetModuleCardHtml(DbHelper.Modules.ModuleInfo moduleInfo, ModuleCardButtonTypeEnum moduleButtonType = ModuleCardButtonTypeEnum.None, string customClass = "") {
        var moduleCardInfo = GetModuleCardInfo(moduleInfo, moduleButtonType);
        return GetModuleCardItemHtml(moduleCardInfo);
      }

      private static string GetModuleCardsHtml(List<DbHelper.Modules.ModuleInfo> moduleInfoList, ModuleCardButtonTypeEnum moduleButtonType = ModuleCardButtonTypeEnum.None, string customClass = "") {

        if (moduleInfoList.IsNullOrEmpty()) return GetNoRecordsBadge("No modules found".HTMLEncode());

        string html = "";

        foreach (var moduleInfo in moduleInfoList) {

          var moduleCardInfo = GetModuleCardInfo(moduleInfo, moduleButtonType);
          html += GetModuleCardItemHtml(moduleCardInfo);
        }

        return GetCardsInHtmlContainer(html, customClass);
      }

      private static string GetCardsInHtmlContainer(string moduleCardsHtml, string customClass) {
        return $@"
        <div class=""{Content.CSS.ContentCardContainer} {customClass}"">
          {moduleCardsHtml}
        </div>";
      }

      private static string GetModuleCardItemHtml(ModuleCardInfo moduleCardInfo) {

        if (moduleCardInfo == null) return "";

        return $@"
        <div class=""{Content.CSS.ContentCard} module-card"">
          {GetModuleCardTopHtml(moduleCardInfo)}
          <a href=""{moduleCardInfo.ModuleUrlPath}"">
            <img src=""{moduleCardInfo.CoverImagePath}"" alt=""{moduleCardInfo.ModuleInfo.ModuleTitle}"">
            <div class=""content-card-items"">
              <h3>{moduleCardInfo.ModuleInfo.ModuleTitle}</h3>
              <p>{moduleCardInfo.ModuleInfo.ModuleSummary}</p>
              <div class=""creator"">
                <img src=""{moduleCardInfo.AuthorImagePath}"" />
                <span class=""ml5"">Created by {moduleCardInfo.ModuleInfo.GetAuthorFullName()}</span>
              </div>
              <hr />
              <div class="""">
                {GetTotalLessonsFooterHtml(moduleCardInfo.ModuleInfo.TotalLessons, false)}
                {GetModuleProgressBar(moduleCardInfo.IsUserEnrolled, moduleCardInfo.IsCompleted, moduleCardInfo.CompletedPercentage)}
              </div>
            </div>
          </a>
        </div>";
      }

      private static string GetModuleCardTopHtml(ModuleCardInfo moduleCardInfo) {

        string cardTopHtml = "";
        string badgeHtml = "";

        if (SessionHelper.IsUserRoleLeader) {
          badgeHtml = GetParticipantModuleBadge(moduleCardInfo);

          if (moduleCardInfo.IsUserEnrolled || moduleCardInfo.IsEnrolmentPending) {
            badgeHtml += GetProgramDatesBadge(moduleCardInfo.IsUserEnrolled, moduleCardInfo.StartUtc, moduleCardInfo.DueUtc);
          }

        } else if (!moduleCardInfo.ModuleInfo.IsPublished) {
          badgeHtml = GetModuleBadge(new ModuleBadgeInfo(ModuleCardBadgeTypeEnum.Draft));
        }

        if (!badgeHtml.IsNullOrEmpty()) {
          cardTopHtml += $@"
          <div class=""badge-container"">
            {badgeHtml}
          </div>";
        }

        if (moduleCardInfo.ModuleCardButtonType != ModuleCardButtonTypeEnum.None) {

          string btnClasses = "";
          if (moduleCardInfo.ModuleCardButtonType == ModuleCardButtonTypeEnum.Add) {
            btnClasses = $"{Content.CSS.AddContentButton} btn-primary";

          } else if (moduleCardInfo.ModuleCardButtonType == ModuleCardButtonTypeEnum.Remove) {
            btnClasses = $"{Content.CSS.RemoveContentButton} btn-danger";

          }

          if (!btnClasses.IsNullOrEmpty()) {

            cardTopHtml += $@"
            <button class=""{btnClasses} btn btn-xsm"" data-{DataAttrs.ModuleId}=""{moduleCardInfo.ModuleInfo.ModuleId}"">
              {GetCardButtonTypeText(moduleCardInfo.ModuleCardButtonType)}
            </button>";
          }
        }

        return cardTopHtml.SurroundWith($"<div class=\"{Content.CSS.ContentCardTopContainer}\">", "</div>", false);
      }

      private static string GetCardButtonTypeText(ModuleCardButtonTypeEnum moduleCardButtonType) {
        if (moduleCardButtonType == ModuleCardButtonTypeEnum.Add) {
          return "Add";
        } else if (moduleCardButtonType == ModuleCardButtonTypeEnum.Remove) {
          return "Remove";
        }

        return "";
      }

      public static string GetParticipantModuleBadge(DbHelper.Modules.ParticipantModuleInfo participantModuleInfo) {
        if (participantModuleInfo == null) return "";

        return GetParticipantModuleBadge(GetModuleCardInfo(participantModuleInfo));
      }

      private static string GetParticipantModuleBadge(ModuleCardInfo moduleCardInfo) {

        if (moduleCardInfo == null || !SessionHelper.IsUserRoleLeader) return "";

        if (moduleCardInfo.IsCompleted) {
          return GetModuleBadge(new ModuleBadgeInfo(ModuleCardBadgeTypeEnum.Completed, "", "badge-completed-module"));
        } else if (moduleCardInfo.IsUserEnrolled) {
          return GetModuleBadge(new ModuleBadgeInfo(ModuleCardBadgeTypeEnum.Enrolled));
        } else if (moduleCardInfo.IsEnrolmentPending) {
          return GetModuleBadge(new ModuleBadgeInfo(ModuleCardBadgeTypeEnum.EnrolmentPending));
        }

        return "";
      }

      public static string GetProgramDatesBadge(DbHelper.Modules.ParticipantModuleInfo participantModuleInfo) {
        if (participantModuleInfo == null) return "";
        return GetProgramDatesBadge(participantModuleInfo.IsUserEnrolled, participantModuleInfo.ModuleStartUtc, participantModuleInfo.ModuleDueUtc, true);
      }

      private static string GetProgramDatesBadge(bool isUserEnrolled, DateTime? moduleStartUtc, DateTime? moduleDueUtc, bool addBothDates = false) {
        string badgeText = "", customClass = "";

        if (addBothDates) {

          if (moduleDueUtc.HasValue && moduleStartUtc.HasValue) {
            badgeText = $"Start date: {DisplayDate(moduleStartUtc)} - Due date: {DisplayDate(moduleDueUtc)}";
            customClass = "ml10";
          }
        } else {
          if (isUserEnrolled) {
            if (moduleDueUtc.HasValue) {
              badgeText = $"Due date: {DisplayDate(moduleDueUtc)}";
            }
          } else {
            if (moduleStartUtc.HasValue) {
              badgeText = $"Start date: {DisplayDate(moduleStartUtc)}";
            }
          }
        }

        if (!badgeText.IsNullOrEmpty()) {
          return GetModuleBadge(new ModuleBadgeInfo(ModuleCardBadgeTypeEnum.Custom, badgeText, $"badge-module-dates {customClass}"));
        }

        return "";
      }

      public static string GetModuleBadge(ModuleBadgeInfo badge) {
        string badgeText = "", badgeClass = "";

        if (badge == null || badge.ModuleCardBadgeType == ModuleCardBadgeTypeEnum.None) return "";

        if (badge.ModuleCardBadgeType == ModuleCardBadgeTypeEnum.Custom && !badge.CustomBadgeText.IsNullOrEmpty()) {
          badgeText = badge.CustomBadgeText;
          badgeClass = badge.CustomClass;

        } else {
          BadgeTextForType.TryGetValue(badge.ModuleCardBadgeType, out badgeText);
          badgeClass = $"badge-{badgeText.ToLower().Replace(" ", "-")} {badge.CustomClass}";
        }


        return GetModuleBadge(badgeText, badgeClass);
      }

      private static string GetModuleBadge(string badgeText, string customClass) {
        if (badgeText.IsNullOrEmpty()) return "";
        return $"<span class=\"badge {(customClass.EnsureStartsWith("badge-", true))}\">{badgeText.HTMLEncode()}</span>";
      }

      public static string GetModuleProgressBar(bool isUserEnrolled, bool isCompleted, int completedPercentage) {
        if (!isUserEnrolled) return "";

        return $@"
          <div class=""progress-container"">
            <div class=""progress-bar"">
              <div class=""progress-fill {(isCompleted ? "completed" : "")}"" id=""progress-fill"" style=""width: {completedPercentage}%;""></div>
            </div>
            <div class=""progress-text"" id=""progress-text"">{completedPercentage}% completed</div>
          </div>";
      }

      public static string GetTotalLessonsFooterHtml(int totalLessons, bool showLessonsIfEmpty) {

        string lessonsHtml = string.Empty;
        if (totalLessons > 0 || (totalLessons == 0 && showLessonsIfEmpty)) {
          lessonsHtml = $"<div class=\"module-lessons\">{GetModuleTotalLessonsText(totalLessons)}</div>";
        }

        return $@"
          <div class=""module-lessons-info"">
            <div class=""module-span mr10"">{GetIconHtml(ActionButtonTypeEnum.module, "mr5")} Module</div>
            {lessonsHtml}
          </div>";
      }

      private static string GetModuleTotalLessonsText(int totalLessons) {

        if (totalLessons == 0) {
          return "No lessons available yet";
        } else {
          return totalLessons.ToString() + " lesson".ToPlural(totalLessons);
        }
      }

    }
  }
}
