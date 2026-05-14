using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;

namespace Integral.Web.PortalSite.Pages_Albert {

  public partial class ProgramContent : AppCode.PageBaseClasses.ProgramPageBase {

    public enum TypeTabsEnum { Module, Content }

    public bool CanViewContent, CanSearchContent, CanAddContentToProgram, CanRemoveContentFromProgram, CanAddModuleToProgram, CanRemoveModuleFromProgram;
    public List<DbHelper.Content.ProgramContentInfo> ProgramContentInfoList;
    public List<DbHelper.WorkshopEvents.WorkshopEventInfo> WorkshopListInfo;
    public List<DbHelper.Modules.ContentModulesInProgramInfo> ContentModulesInProgram;

    public class FormFields {
      public const string SearchTerm = "SearchTerm";
      public const string ContentId = "ContentId";
      public const string ModuleId = "ModuleId";
      public const string ScheduledSendDateUtc = "ScheduledSendDateUtc";
      public const string IsContentListed = "IsContentListed";
      public const string WorkshopEventId = "WorkshopEventId";
      public const string ModuleStartDateUtc = "ModuleStartDateUtc";
      public const string ModuleDueUtc = "ModuleDueUtc";
      public const string EnrolParticipantsInProgram = "EnrolParticipantsInProgram";
    }
    public class AjaxAction {
      public const string SearchContentLibrary = "SearchContentLibrary";
      public const string AddContentToProgram = "AddContentToProgram";
      public const string AddModuleToProgram = "AddModuleToProgram";
      public const string RemoveContentFromProgram = "RemoveContentFromProgram";
      public const string RemoveModuleFromProgram = "RemoveModuleFromProgram";
    }
    public class AjaxReturnData {
      public const string ContentLibraryHtml = "ContentLibraryHtml";
      public const string CardItemHtml = "CardItemHtml";
      public const string ModuleLibraryHtml = "ModuleLibraryHtml";
    }

    protected void Page_Load(object sender, EventArgs e) {

      PageTitle = "Microlearning";

      CanSearchContent = SessionHelper.AppAccess.Content.CanSearchProgramContent(ProgramInfo);
      CanViewContent = SessionHelper.AppAccess.Content.CanViewContentList();
      CanAddContentToProgram = SessionHelper.AppAccess.Content.CanAddContentToProgram(ProgramInfo);
      CanAddModuleToProgram = SessionHelper.AppAccess.Modules.CanAddToProgram(ProgramInfo);
      CanRemoveContentFromProgram = SessionHelper.AppAccess.Content.CanRemoveContentFromProgram(ProgramInfo);
      CanRemoveModuleFromProgram = SessionHelper.AppAccess.Modules.CanRemoveFromProgram(ProgramInfo);

      ProgramContentInfoList = DbHelper.Content.GetProgramContentList(ProgramInfo);
      ContentModulesInProgram = DbHelper.Modules.GetModulesInProgram(ProgramInfo);
      WorkshopListInfo = DbHelper.WorkshopEvents.GetWorkshopsInProgram(ProgramInfo.ProgramJobId);

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          switch (PageAjaxAction) {

            case AjaxAction.SearchContentLibrary:
              if (!CanSearchContent && !CanAddContentToProgram) {
                ajax.AddErrorToast("You do not have permission to search content.");
                return;
              }
              SearchContent(ajax);
              break;

            case AjaxAction.AddContentToProgram:
              if (!CanAddContentToProgram) {
                ajax.AddErrorToast("You do not have permission to add content to program.");
                return;
              }
              AddContentToProgram(ajax);
              break;

            case AjaxAction.RemoveContentFromProgram:
              if (!CanRemoveContentFromProgram) {
                ajax.AddErrorToast("You do not have permission to remove content from program.");
                return;
              }
              RemoveContentFromProgram(ajax);
              break;

            case AjaxAction.AddModuleToProgram:
              if (!CanAddModuleToProgram) {
                ajax.AddErrorToast("You do not have permission to add content to program.");
                return;
              }
              AddModuleToProgram(ajax);
              break;

            case AjaxAction.RemoveModuleFromProgram:
              if (!CanRemoveModuleFromProgram) {
                ajax.AddErrorToast("You do not have permission to remove content from program.");
                return;
              }
              RemoveModuleFromProgram(ajax);
              break;
          }
        });
        return;
      }
    }

    public string GetPanelHtml() {
      var tabsHtml = WebHelper.GetPageTabs(
       new WebHelper.PageTabsInfo() { PageTabsStyle = WebHelper.PageTabsStyle.Links },
       new WebHelper.PageTabItem(TypeTabsEnum.Module.ToString(), TypeTabsEnum.Module.ToString(), true),
       new WebHelper.PageTabItem(TypeTabsEnum.Content.ToString(), "Microlearning"));

      StringBuilder html = new StringBuilder();
      html.Append(tabsHtml);
      html.Append(GetContentTabHtml());
      html.Append(GetModuleTabHtml());

      return html.ToString();
    }

    public void SearchContent(AjaxSubmitHelper ajax) {
      string searchTerm = "";

      if (CanSearchContent) {
        searchTerm = ajax.CheckFieldRegex(FormFields.SearchTerm, "Search Term", AppHelper.Regex.GeneralText, false, "").EmptyIfNull();
      }

      ajax.AddReturnValue(AjaxReturnData.ContentLibraryHtml, GetSearchContentHtml(searchTerm));
      ajax.AddReturnValue(AjaxReturnData.ModuleLibraryHtml, GetSearchModulesHtml(searchTerm));
    }

    public string SearchContent(TypeTabsEnum typeTabsEnum, string searchTerm = "") {

      if (!CanSearchContent) searchTerm = "";

      if (typeTabsEnum == TypeTabsEnum.Content) {
        return GetSearchContentHtml(searchTerm);

      } else if (typeTabsEnum == TypeTabsEnum.Module) {
        return GetSearchModulesHtml(searchTerm);
      }

      return "";
    }

    private string GetSearchContentHtml(string searchTerm) {
      var contentInfoList = DbHelper.Content.GetContentForUserAndSearchTerm(SessionHelper.UserInfo, SessionHelper.GetUserRole(), searchTerm);
      return GetContentLibraryHtml(contentInfoList);
    }

    private string GetSearchModulesHtml(string searchTerm) {
      var moduleInfoList = DbHelper.Modules.GetModuleForUserAndSearchTerm(null, SessionHelper.UserInfo, SessionHelper.GetUserRole(), searchTerm);
      return GetModuleLibraryHtml(moduleInfoList);
    }

    private string GetContentTabHtml() {

      StringBuilder html = new StringBuilder();

      html.Append(GetContentInProgram());

      if (CanAddContentToProgram) {

        html.Append("<hr/>");
        html.Append($"<div class=\"content-library-container {WebHelper.Content.CSS.ContentLibraryContainer}\">");
        html.Append(SearchContent(TypeTabsEnum.Content));
        html.Append("</div>");
        html.Append(GetAddContentModalHtml());
      }

      return AttachToPanelHtml(TypeTabsEnum.Content, html.ToString());
    }

    private string GetModuleTabHtml() {
      StringBuilder html = new StringBuilder();

      html.Append(GetModulesInProgram());

      if (CanAddContentToProgram) {

        html.Append("<hr/>");
        html.Append($"<div class=\"module-library-container {WebHelper.Modules.CSS.ModuleLibraryContainer}\">");
        html.Append(SearchContent(TypeTabsEnum.Module));
        html.Append("</div>");
        html.Append(GetAddModuleModalHtml());
      }

      return AttachToPanelHtml(TypeTabsEnum.Module, html.ToString());
    }

    private string GetAddContentModalHtml() {
      return $@"
        <div id=""AddContentModal"" class=""displaynone content-image-fullView"">
          <form id=""formAddContent"">
            {WebHelper.GetInputDateRow("Scheduled Send Date: ", FormFields.ScheduledSendDateUtc, null, 4, 4)}
            {WebHelper.CustomCheckBoxRow("List Content:", FormFields.IsContentListed, "1", false, !CanAddContentToProgram, "",
                WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.info, "This will show on participants program overview.", "", "ml5"), "pt15", 4)}
            {WebHelper.GetWorkshopDropdownHtml(WorkshopListInfo, FormFields.WorkshopEventId, 4, 5, false)}
          </form>
        </div>";
    }

    private string GetAddModuleModalHtml() {
      return $@"
        <div id=""AddModuleModal"" class=""displaynone content-image-fullView"">
          <form id=""formAddModule"">
            {WebHelper.GetInputDateRow("Start Date: ", FormFields.ModuleStartDateUtc, null, 4, 4)}
            {WebHelper.GetInputDateRow("Due Date: ", FormFields.ModuleDueUtc, null, 4, 4)}
            {WebHelper.CustomCheckBoxRow("Enroll participants now:", FormFields.EnrolParticipantsInProgram, "1", false, false, "",
                WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.info, "This will automatically enrol all participants in program. Otherwise the participants will be asked to enrol.", "", "ml5"), "pt15", 4)}
          </form>
        </div>";
    }

    private string GetContentInProgram() {

      string html = "";

      if (!ProgramContentInfoList.IsNullOrEmpty()) {

        ProgramContentInfoList.Sort((x, y) => (x.DisplayOrder ?? 0).CompareTo(y.DisplayOrder ?? 0));

        foreach (var programContentInfo in ProgramContentInfoList) {
          bool isLinkedToWorkshop = programContentInfo.WorkshopEventId != null;
          html += GetContentCardHtml(programContentInfo.ContentInfo, true, isLinkedToWorkshop);
        }

      } else {

        html = WebHelper.GetNoRecordsBadge("No microlearning has been added to program yet.".HTMLEncode());
      }

      return $@"
        <div class=""{WebHelper.Content.CSS.ContentCardContainer} {WebHelper.Content.CSS.ProgramContentContainer}"">
          {html}
        </div>";
    }

    private string GetModulesInProgram() {

      if (!ContentModulesInProgram.IsNullOrEmpty()) {

        ContentModulesInProgram.Sort((x, y) => (x.ModuleDisplayOrder).CompareTo(y.ModuleDisplayOrder));

        return WebHelper.Modules.GetModuleCardsInProgramHtml(ContentModulesInProgram);

      } else {

        return $@"
        <div class=""{WebHelper.Content.CSS.ContentCardContainer} {WebHelper.Modules.CSS.ProgramModuleContainer}"">
          {WebHelper.GetNoRecordsBadge("No module has been added to program yet.".HTMLEncode())}
        </div>";
      }
    }

    private string GetModuleLibraryHtml(List<DbHelper.Modules.ModuleInfo> moduleInfoList) {

      if (!ContentModulesInProgram.IsNullOrEmpty() && !moduleInfoList.IsNullOrEmpty()) {
        moduleInfoList.RemoveAll(x => ContentModulesInProgram.Any(mp => mp?.ModuleInfo?.ModuleId == x.ModuleId));
      }

      return
        "<h3 class=\"mt0\">Module Library</h3>" +
        WebHelper.Modules.GetModuleLibraryCardsForProgramHtml(moduleInfoList);
    }

    private string GetContentLibraryHtml(List<DbHelper.Content.ContentInfo> contentInfoList) {

      string titleHtml = "<h3 class=\"mt0\">Microlearning Library</h3>";

      if (!ProgramContentInfoList.IsNullOrEmpty() && !contentInfoList.IsNullOrEmpty()) {
        // Remove any content that is already in the program
        contentInfoList.RemoveAll(x => ProgramContentInfoList.Any(pc => pc.ContentInfo.ContentId == x.ContentId));
      }

      if (contentInfoList.IsNullOrEmpty()) {
        return titleHtml + WebHelper.GetNoRecordsBadge();
      }

      string html = "";

      foreach (var contentInfo in contentInfoList) {

        html += GetContentCardHtml(contentInfo, false);
      }

      return $@"
          {titleHtml}
          <div class=""{WebHelper.Content.CSS.ContentCardContainer}"">
            {html}
          </div>";
    }

    private string GetContentCardHtml(DbHelper.Content.ContentInfo contentInfo, bool isInProgram, bool isLinkedToWorkshop = false) {

      string html = "";
      var contentBadgeList = new List<WebHelper.Content.ContentBadgeInfo>();
      string contentPath = PathHelper.Pages.ContentDetails(contentInfo.ContentGuid);
      var buttonType = WebHelper.Content.ContentCardButtonTypeEnum.None;

      if (isInProgram) {
        contentPath = PathHelper.Pages.ProgramContentDetails(ProgramInfo.ProgramJobId, contentInfo.ContentGuid);
        if (CanRemoveContentFromProgram) buttonType = WebHelper.Content.ContentCardButtonTypeEnum.Remove;
        if (!contentInfo.IsPublished) contentBadgeList.Add(new WebHelper.Content.ContentBadgeInfo(WebHelper.Content.ContentCardBadgeTypeEnum.Draft));
      } else {
        if (CanAddContentToProgram) buttonType = WebHelper.Content.ContentCardButtonTypeEnum.Add;
      }

      string authorImagePath = PathHelper.Images.UserPhoto(contentInfo.AuthorFirstName, contentInfo.AuthorLastName, PathHelper.Images.UserPhotoSize.Thumbnail, true);

      if (isLinkedToWorkshop) {
        contentBadgeList.Add(new WebHelper.Content.ContentBadgeInfo(WebHelper.Content.ContentCardBadgeTypeEnum.In_Workshop));
      }

      html += WebHelper.Content.GetContentCardItemHtml(contentInfo, contentPath, authorImagePath, buttonType, contentBadgeList);

      return html;
    }

    private string AttachToPanelHtml(TypeTabsEnum typeTabsEnum, string panelHtml) {
      return $@"
        <div class=""tab-panel"" data-appendTo=""panel-{typeTabsEnum}"">
          {panelHtml}
        </div>";
    }

    public void RemoveModuleFromProgram(AjaxSubmitHelper ajax) {

      int moduleId = WebHelper.GetFormValueIntOrDefault(FormFields.ModuleId, 0);

      if (moduleId == 0) {
        ajax.AddDialogMessage("Module Id is required.");
        return;
      }

      var programModuleInfo = ContentModulesInProgram.FirstOrDefault(x => x.ModuleInfo.ModuleId == moduleId);
      if (programModuleInfo == null) {
        ajax.AddDialogMessage("Module not found in program.");
        return;
      }

      DbHelper.Common.UsingTransaction(trans => {

        bool removed = false;
        try {
          removed = DbHelper.Modules.RemoveModuleFromProgram(trans, programModuleInfo);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation(nameof(RemoveModuleFromProgram))
            .FromSession()
            .WithProperty(ApplicationInsightsConstants.ModuleId, programModuleInfo?.ModuleInfo?.ModuleId)
            .WithProperty(ApplicationInsightsConstants.ProgramJobId, ProgramInfo.ProgramJobId)
            .WithProperty(ApplicationInsightsConstants.JobNumber, ProgramInfo?.ProgramJobNumber)
            .Track();
          ajax.AddDialogMessage("Module couldn't be removed. ", ex);
          return false;
        }
        if (!removed) {
          ajax.AddDialogMessage("Module could not be removed from program.");
          return false;
        }

        return true;
      });

      ajax.AddReturnValue(AjaxReturnData.CardItemHtml, WebHelper.Modules.GetModuleCardHtml(programModuleInfo.ModuleInfo, WebHelper.Modules.ModuleCardButtonTypeEnum.Add));
    }


    public void RemoveContentFromProgram(AjaxSubmitHelper ajax) {

      int contentId = WebHelper.GetFormValueIntOrDefault(FormFields.ContentId, 0);

      if (contentId == 0) {
        ajax.AddDialogMessage("Microlearning Id is required.");
        return;
      }

      var programContentInfo = ProgramContentInfoList.FirstOrDefault(x => x.ContentInfo.ContentId == contentId);
      if (programContentInfo == null) {
        ajax.AddDialogMessage("Microlearning not found in program.");
        return;
      }

      DbHelper.Common.UsingTransaction(trans => {

        bool removed = false;
        try {
          removed = DbHelper.Content.RemoveContentFromProgram(trans, programContentInfo);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation(nameof(RemoveContentFromProgram))
            .FromSession()
            .WithProperty(ApplicationInsightsConstants.ContentId, contentId)
            .WithProperty(ApplicationInsightsConstants.ProgramJobId, ProgramInfo.ProgramJobId)
            .WithProperty(ApplicationInsightsConstants.JobNumber, ProgramInfo?.ProgramJobNumber)
            .Track();
          ajax.AddDialogMessage("Microlearning couldn't be removed. ", ex);
          return false;
        }
        if (!removed) {
          ajax.AddDialogMessage("Microlearning could not be removed from program.");
          return false;
        }

        return true;
      });

      ajax.AddReturnValue(AjaxReturnData.CardItemHtml, GetContentCardHtml(programContentInfo.ContentInfo, false));
    }

    public void AddContentToProgram(AjaxSubmitHelper ajax) {

      int contentId = WebHelper.GetFormValueIntOrDefault(FormFields.ContentId, 0);
      var scheduledSendDateUtc = ajax.GetDatePickerToUtc(FormFields.ScheduledSendDateUtc, SessionHelper.GetSessionTimeZone(), "Scheduled Send Date", false, "Please provide a date.");
      var isContentListed = ajax.CheckFieldBool(FormFields.IsContentListed, "1");
      var workshopEventId = ajax.CheckFieldIDOrNull(FormFields.WorkshopEventId, "Microlearning", false, "Please select a Microlearning to attach to the email.");

      if (contentId == 0) {
        ajax.AddDialogMessage("Microlearning Id is required.");
        return;
      }

      if (!ProgramContentInfoList.IsNullOrEmpty()) {
        if (ProgramContentInfoList.Exists(x => x.ContentInfo.ContentId == contentId)) {
          ajax.AddErrorToast("This microlearning already exists in Program");
          return;
        }
      }

      if (workshopEventId != null) {
        if (WorkshopListInfo.IsNullOrEmpty() || !WorkshopListInfo.Exists(x => x.WorkshopEventId == workshopEventId)) {
          ajax.AddErrorToast("Select a valid Workshop");
          return;
        }
      }

      var contentInfo = DbHelper.Content.GetContentInfo(contentId, SessionHelper.UserInfo, SessionHelper.UserRole);
      if (contentInfo == null) {
        ajax.AddDialogMessage("Microlearning not found.");
        return;
      }

      int displayOrder = ProgramContentInfoList.Count + 1;
      var newProgramContent = new DbHelper.Content.ProgramContentInfo(ProgramInfo.ProgramJobId, scheduledSendDateUtc, displayOrder, isContentListed, userInfo.UserId, DateTime.UtcNow, contentInfo, workshopEventId);

      DbHelper.Common.UsingTransaction(trans => {

        bool addedToProgram = false;
        try {
          addedToProgram = DbHelper.Content.AddContentToProgram(trans, newProgramContent);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation(nameof(AddContentToProgram))
            .FromSession()
            .WithProperty(ApplicationInsightsConstants.ContentId, contentId)
            .WithProperty(ApplicationInsightsConstants.ProgramJobId, ProgramInfo.ProgramJobId)
            .WithProperty(ApplicationInsightsConstants.JobNumber, ProgramInfo?.ProgramJobNumber)
            .Track();
          ajax.AddDialogMessage("Microlearning couldn't be added.", ex);
          return false;
        }
        if (!addedToProgram) {
          ajax.AddDialogMessage("Microlearning could not be added to program.");
          return false;
        }

        return true;
      });

      ajax.AddReturnValue(AjaxReturnData.CardItemHtml, GetContentCardHtml(contentInfo, true, workshopEventId != null));
    }

    public void AddModuleToProgram(AjaxSubmitHelper ajax) {

      int moduleId = WebHelper.GetFormValueIntOrDefault(FormFields.ModuleId, 0);
      var moduleStartDateUtc = ajax.GetDatePickerToUtc(FormFields.ModuleStartDateUtc, SessionHelper.GetSessionTimeZone(), "Start Date", false, "Please provide a date.");
      var moduleDueDateUtc = ajax.GetDatePickerToUtc(FormFields.ModuleDueUtc, SessionHelper.GetSessionTimeZone(), "Due Date", false, "Please provide a date.");
      var enrolParticipantsInProgram = ajax.CheckFieldBool(FormFields.EnrolParticipantsInProgram, "1");

      if (moduleId == 0) {
        ajax.AddDialogMessage("Module Id is required.");
        return;
      }

      if (!ContentModulesInProgram.IsNullOrEmpty()) {
        if (ContentModulesInProgram.Exists(x => x.ModuleInfo.ModuleId == moduleId)) {
          ajax.AddErrorToast("This module already exists in Program");
          return;
        }
      }

      var moduleInfo = DbHelper.Modules.GetModuleInfo(null, moduleId, SessionHelper.UserInfo, SessionHelper.GetUserRole());
      if (moduleInfo == null) {
        ajax.AddDialogMessage("Module not found.");
        return;
      }

      int displayOrder = ContentModulesInProgram.Count + 1;
      var newProgramModule = new DbHelper.Modules.ContentModulesInProgramInfo(moduleInfo, ProgramInfo.ProgramJobId, DateTime.UtcNow, userInfo.UserId, displayOrder, moduleStartDateUtc, moduleDueDateUtc);

      DbHelper.Common.UsingTransaction(trans => {

        bool addedToProgram = false;
        try {
          addedToProgram = DbHelper.Modules.AddModuleToProgram(trans, newProgramModule);
        } catch (Exception ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation(nameof(AddModuleToProgram))
            .FromSession()
            .WithProperty(ApplicationInsightsConstants.ModuleId, moduleId)
            .WithProperty(ApplicationInsightsConstants.ProgramJobId, ProgramInfo.ProgramJobId)
            .WithProperty(ApplicationInsightsConstants.JobNumber, ProgramInfo?.ProgramJobNumber)
            .Track();
          ajax.AddDialogMessage("Module couldn't be added.", ex);
          return false;
        }
        if (!addedToProgram) {
          ajax.AddDialogMessage("Module could not be added to program.");
          return false;
        }

        if (enrolParticipantsInProgram && ProgramInfo.ParticipantCount > 0) {
          try {
            DbHelper.Modules.EnrolParticipantsInModule(trans, moduleId, ProgramInfo.ProgramJobId);
          } catch (Exception ex) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation(nameof(AddModuleToProgram))
              .WithOperationContext("EnrolParticipants")
              .FromSession()
              .WithProperty(ApplicationInsightsConstants.ModuleId, moduleId)
              .WithProperty(ApplicationInsightsConstants.ProgramJobId, ProgramInfo.ProgramJobId)
              .WithProperty("ParticipantCount", ProgramInfo.ParticipantCount)
              .WithProperty(ApplicationInsightsConstants.JobNumber, ProgramInfo?.ProgramJobNumber)
              .Track();
            ajax.AddDialogMessage("Participants couldn't be enrolled.", ex);
            return false;
          }
        }

        return true;
      });

      ajax.AddReturnValue(AjaxReturnData.CardItemHtml, WebHelper.Modules.GetModuleCardHtml(moduleInfo, WebHelper.Modules.ModuleCardButtonTypeEnum.Remove));
    }
  }
}
