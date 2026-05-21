using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class ModuleEdit : AppCode.PageBaseClasses.ModulePageBase {

    public List<DbHelper.Content.ContentInfo> AvailableContentInfoList = null;
    public List<DbHelper.Modules.ModuleContentDetailsInfo> ContentInModuleList = null;

    public List<DbHelper.AlbertCoaches.AlbertCoachInfo> PartnerList;
    public bool CanAddModule, CanEditModule, CanDeleteModule, CanChangeAuthor, CanEditIsPublished, CanAddContentItems;
    public bool CanSetPublicForParticipants, CanSetPublicForPartners, CanDeleteContentItems, CanUpdateContentDisplayOrder;
    public DbHelper.Content.ProgramContentInfo ProgramModuleInfo;
    public List<DbHelper.WorkshopEvents.WorkshopEventInfo> WorkshopListInfo;

    public class FormFields {
      public const string ModuleId = "ModuleId";
      public const string ModuleGuid = "ModuleGuid";
      public const string AuthorUserId = "AuthorUserId";
      public const string ModuleTitle = "ModuleTitle";
      public const string ModuleDescriptionHtml = "ModuleDescriptionHtml";
      public const string ModuleSummary = "ModuleSummary";
      public const string ShowToParticipants = "ShowToParticipants";
      public const string ShowToPartners = "ShowToPartners";
      public const string ScheduledSendDateUtc = "ScheduledSendDateUtc";
      public const string WorkshopEventId = "WorkshopEventId";
      public const string ContentId = "ContentId";
      public const string IsPublished = "IsPublished";
      public const string ContentList = "ContentList";
    }
    public class AjaxAction {
      public const string UpdateModule = "UpdateModule";
      public const string UploadCoverImage = "UploadCoverImage";
      public const string DeleteModule = "DeleteModule";
      public const string AddContentItemToModule = "AddContentItemToModule";
      public const string RemoveContentItemFromModule = "RemoveContentItemFromModule";
      public const string UpdateContentDisplayOrder = "UpdateContentDisplayOrder";
    }
    public class AjaxReturnData {
      public const string ModuleGuid = "ModuleGuid";
      public const string ContentItemHtml = "ContentItemHtml";
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      CanSetPublicForParticipants = SessionHelper.AppAccess.Modules.CanSetPublicForParticipants();
      CanSetPublicForPartners = SessionHelper.AppAccess.Modules.CanSetPublicForPartners();

      PageTitle = "Module Details";

      if (IsNewModule) {

        ModuleInfo = new DbHelper.Modules.ModuleInfo();

        if (IsProgramView) {
          CanAddModule = SessionHelper.AppAccess.Modules.CanAddToProgram(ProgramInfo);
        } else {
          CanAddModule = SessionHelper.AppAccess.Modules.CanAdd();
        }

        CanChangeAuthor = CanAddModule;
        CanEditModule = CanAddModule;
        CanEditIsPublished = CanEditModule;
        CanAddContentItems = false;

        if (!CanAddModule) {
          RedirectNoAccess("You do not have access to create microlearning.");
          return new EmptyResult();
        }

      } else {

        string moduleUrlValue = WebHelper.GetQueryStringValue(PathHelper.AbleUrlKeys.ModuleGuid);

        if (!Guid.TryParse("" + moduleUrlValue, out Guid moduleGuid)) {
          RedirectNoAccess("");
          return new EmptyResult();
        }

        ModuleInfo = DbHelper.Modules.GetModuleInfo(null, moduleGuid, SessionHelper.UserInfo, SessionHelper.GetUserRole());

        if (ModuleInfo == null) {
          RedirectNoAccess("Module not found.");
          return new EmptyResult();
        }

        ContentInModuleList = DbHelper.Modules.GetModuleContentList(ModuleInfo.ModuleId, SessionHelper.GetUserRole());

        CanChangeAuthor = SessionHelper.AppAccess.Modules.CanChangeAuthor(ModuleInfo);
        CanDeleteModule = SessionHelper.AppAccess.Modules.CanDeleteModule(ModuleInfo);
        CanEditModule = SessionHelper.AppAccess.Modules.CanEdit(ModuleInfo);
        CanEditIsPublished = SessionHelper.AppAccess.Modules.CanEditIsPublished(ModuleInfo);
        CanAddContentItems = SessionHelper.AppAccess.Modules.CanAddContentItems(ModuleInfo);
        CanDeleteContentItems = SessionHelper.AppAccess.Modules.CanDeleteContentItems(ModuleInfo);
        CanUpdateContentDisplayOrder = SessionHelper.AppAccess.Modules.CanUpdateContentDisplayOrder(ModuleInfo);
      }

      if (IsProgramView) {

        CanEditModule = SessionHelper.AppAccess.Modules.CanEditProgramContent(ProgramInfo, ModuleInfo, IsNewModule);
        WorkshopListInfo = DbHelper.WorkshopEvents.GetWorkshopsInProgram(ProgramInfo.ProgramJobId);
        ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(ProgramInfo.ProgramJobNumber);
        MenuThirdLayerActive_Programs = ProjectMenuIsActive = true;
      }

      if (CanChangeAuthor) {
        PartnerList = DbHelper.AlbertCoaches.GetCoachInfoList(false, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);
      }

      if (CanAddContentItems) {
        AvailableContentInfoList = DbHelper.Content.GetContentForUserAndSearchTerm(SessionHelper.UserInfo, SessionHelper.GetUserRole());
        if (!ContentInModuleList.IsNullOrEmpty() && !AvailableContentInfoList.IsNullOrEmpty()) {
          AvailableContentInfoList = AvailableContentInfoList.Where(a => !ContentInModuleList.Any(c => c.ContentId == a.ContentId)).ToList();
        }
      }

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          switch (PageAjaxAction) {

            case AjaxAction.UpdateModule:
              if ((!IsNewModule && !CanEditModule) || (IsNewModule && !CanAddModule)) {
                ajax.AddDialogMessage("Update not allowed.");
                return;
              }
              UpdateModule(ajax);
              break;

            case AjaxAction.UploadCoverImage:
              if (!CanEditModule) {
                ajax.AddDialogMessage("Update not allowed.");
                return;
              }
              SaveCoverImage(ajax);
              break;

            case AjaxAction.DeleteModule:
              if (!CanDeleteModule) {
                ajax.AddDialogMessage("Delete not allowed.");
                return;
              }
              DeleteModule(ajax);
              break;

            case AjaxAction.AddContentItemToModule:
              if (!CanAddContentItems) {
                ajax.AddDialogMessage("Update not allowed.");
                return;
              }
              AddContentItemToModule(ajax);
              break;

            case AjaxAction.RemoveContentItemFromModule:
              if (!CanDeleteContentItems) {
                ajax.AddDialogMessage("Update not allowed.");
                return;
              }
              RemoveContentItemFromModule(ajax);
              break;

            case AjaxAction.UpdateContentDisplayOrder:
              if (!CanUpdateContentDisplayOrder) {
                ajax.AddDialogMessage("Update not allowed.");
                return;
              }
              UpdateContentDisplayOrder();
              break;
          }
        });
        return new EmptyResult();
      }

      return Page();
    }

    public string GetRequiredFieldIconHtml() {
      return WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.requiredField, "Required field.", "This field must be filled for it to be public or published.");
    }

    private void RedirectNoAccess(string msg) {
      SetRedirect(PathHelper.Pages.Content(), msg);
      return;
    }

    void DeleteModule(AjaxSubmitHelper ajax) {
      bool deletedContent = DbHelper.Modules.DeleteModule(ModuleInfo);
      if (deletedContent) {
        ajax.SetRedirectUrl(PathHelper.Pages.Content(), "Module Deleted.");
        return;
      } else {
        ajax.AddErrorToast("Module couldn't be deleted");
      }
    }

    void SaveCoverImage(AjaxSubmitHelper ajax) {

      var uploadedFile = SystemWeb.GetRequestFile("image");
      if (uploadedFile == null) return;

      var moduleGuid = ajax.CheckGuid(FormFields.ModuleGuid, "Module Guid", true, "Something went wrong creating the Module item.");

      if (moduleGuid == null) {
        ajax.AddDialogMessage("Microlearning not found");
        return;
      }
      // TODO: MODULE PATH
      PathHelper.Content.RemoveContentFiles(moduleGuid, PathHelper.Content.ContentFileType.CoverImage); // Remove old files if existing
      using (var inputStream = uploadedFile.OpenReadStream()) {
        PathHelper.Images.SaveStreamToContentImage(inputStream, moduleGuid, PathHelper.Content.ContentFileType.CoverImage);
      }
    }

    public string GetAuthorHtml() {

      string authorsHtml = "";

      if (CanChangeAuthor) {

        var selectedAuthorUserId = IsNewModule ? userInfo.UserId : ModuleInfo.AuthorUserId;

        authorsHtml = WebHelper.Content.GetContentAuthorDropdownHtml(new WebHelper.PartnerDropdownInfo() {
          PartnerInfoList = PartnerList,
          FormName = FormFields.AuthorUserId,
          IsReadOnly = !CanChangeAuthor,
          InputCols = 11,
          SelectedPartnerUserId = selectedAuthorUserId,
          CanViewHiddenPartners = SessionHelper.AppAccess.Coaches.CanViewHiddenPartners(),
          CanViewInactivePartners = SessionHelper.AppAccess.Coaches.CanViewInactivePartners(),
          DropdownPurpose = WebHelper.PartnerDropdownPurpose.ContentAuthor
        });

      } else {

        PartnerList = null;
        var authorUserGuid = IsNewModule ? userInfo.UserGuid : ModuleInfo.AuthorUserGuid;
        var authorFullName = IsNewModule ? userInfo.GetFullName() : ModuleInfo.GetAuthorFullName();

        authorsHtml = WebHelper.GetAvatarForTable_User(PathHelper.Images.UserPhoto(authorUserGuid, true), authorFullName, null);
      }

      return WebHelper.GetTextDisplayRow("Author:", 11, authorsHtml);
    }

    public void UpdateModule(AjaxSubmitHelper ajax) {

      // Keep the flags on top always, so it can determine if certain fields are Required or Optional.
      // Flags section starts
      if (CanSetPublicForPartners) {
        ModuleInfo.ShowToPartners = ajax.CheckFieldBool(FormFields.ShowToPartners, "1");
      } else if (IsNewModule) {
        ModuleInfo.ShowToPartners = false;
      }

      if (CanSetPublicForParticipants) {
        ModuleInfo.ShowToParticipants = ajax.CheckFieldBool(FormFields.ShowToParticipants, "1");
      } else if (IsNewModule) {
        ModuleInfo.ShowToParticipants = false;
      }

      if (CanEditIsPublished) {
        var isPublished = ajax.CheckFieldBool(FormFields.IsPublished, "1");
        ModuleInfo.PublishedUtc = isPublished ? DateTime.UtcNow : (DateTime?)null;
      }
      // Flags section ends

      var allFieldsAreRequired = ModuleInfo.ShowToPartners || ModuleInfo.ShowToParticipants;

      // Can be optional fields
      ModuleInfo.ModuleDescriptionHtml = ajax.CheckFieldRegex(FormFields.ModuleDescriptionHtml, "Module Description", AppHelper.Regex.HTML.Replace("]", @"}{\|\*]"), allFieldsAreRequired, "Module Description");

      // Required fields always
      ModuleInfo.ModuleTitle = ajax.CheckFieldRegex(FormFields.ModuleTitle, "Module Title", AppHelper.Regex.GeneralText, true, "Please enter a Module Title.");
      ModuleInfo.ModuleSummary = ajax.CheckFieldRegex(FormFields.ModuleSummary, "Module Summary", AppHelper.Regex.GeneralText, true, "Module Summary");

      if (CanChangeAuthor && !PartnerList.IsNullOrEmpty()) {

        var selectedAuthorId = ajax.CheckFieldID(FormFields.AuthorUserId, "Author", true, "Please select an Author.");

        if (!PartnerList.Exists(x => x.UserId == selectedAuthorId)) {
          ajax.AddBadField(FormFields.AuthorUserId, "Please select a valid Author");
        }

        ModuleInfo.AuthorUserId = selectedAuthorId;

      } else if (IsNewModule) {
        ModuleInfo.AuthorUserId = userInfo.UserId;
      }

      if (ajax.BadFieldCount > 0) return;

      bool contentUpdated = false;

      if (IsNewModule) {
        ModuleInfo = DbHelper.Modules.AddModule(null, ModuleInfo);
        contentUpdated = ModuleInfo != null;
      } else {
        contentUpdated = DbHelper.Modules.UpdateModule(ModuleInfo);
      }

      if (contentUpdated) {

        if (IsProgramView && CanEditModule && ProgramInfo != null) {
          var scheduledSendDateUtc = ajax.GetDatePickerToUtc(FormFields.ScheduledSendDateUtc, SessionHelper.GetSessionTimeZone(), "Scheduled Send Date", false, "Please provide a date.");
          var workshopEventId = ajax.CheckFieldIDOrNull(FormFields.WorkshopEventId, "Microlearning", false, "Please select a Microlearning to attach to the email.");

          if (workshopEventId != null) {
            if (WorkshopListInfo.IsNullOrEmpty() || !WorkshopListInfo.Exists(x => x.WorkshopEventId == workshopEventId)) {
              ajax.AddErrorToast("Select a valid Workshop");
              return;
            }
          }

          // TODO: Add to corresponding ModuleContentProgram table
        }

        SessionHelper.SetNextPageMessageText($"Module {(IsNewModule ? "Created" : "Updated")}.");
        SessionHelper.SetNextPageMessageType(AjaxSubmitHelper.PageMessageType.SuccessToast);
        ajax.AddReturnValue(AjaxReturnData.ModuleGuid, ModuleInfo.ModuleGuid);
      }
    }

    private void AddContentItemToModule(AjaxSubmitHelper ajax) {
      int contentId = WebHelper.GetFormValueIntOrDefault(FormFields.ContentId, 0);

      if (ContentInModuleList.Exists(x => x.ContentId == contentId)) {
        ajax.AddInfoToast("The selected Microlearning is already part of the Module");
        return;
      }

      if (contentId > 0) {

        var contentInfo = AvailableContentInfoList.Find(x => x.ContentId == contentId);

        if (contentInfo != null) {

          var moduleContentInfo = new DbHelper.Modules.ModuleContentDetailsInfo();
          moduleContentInfo.ModuleId = ModuleInfo.ModuleId;
          moduleContentInfo.ContentId = contentInfo.ContentId;
          moduleContentInfo.AddedByUserId = userInfo.UserId;

          bool addedToModule = DbHelper.Modules.AddContentToModule(moduleContentInfo, contentInfo);

          if (addedToModule) {

            var contentItemHtml = WebHelper.Modules.GetTableRowHtmlForPage_ModuleEdit(ModuleInfo, moduleContentInfo, CanDeleteContentItems, WebHelper.Content.ViewFromPageEnum.ModuleEdit);
            ajax.AddReturnValue(AjaxReturnData.ContentItemHtml, contentItemHtml);
            ajax.AddSuccessToast("Microlearning Added to Module.");

          } else {

            ajax.AddDialogMessage("Microlearning could not be added to module.");
          }

        } else {

          ajax.AddDialogMessage("Microlearning not found.");
        }

      } else {

        ajax.AddDialogMessage("Microlearning Id is required.");
      }
    }

    public string GetPublicTooltipInfo(string formName, bool canEdit) {

      if (!canEdit) return "";

      string tooltipTitle = "", tooltipText = "";

      if (formName == FormFields.ShowToParticipants) {
        tooltipTitle = "Visible to all participants on Able";

      } else if (formName == FormFields.ShowToPartners) {
        tooltipTitle = "Visible to all partners on Able.";

      } else if (formName == FormFields.IsPublished) {

        if (IsNewModule) {

          tooltipTitle = "Publishing this Module will make it available to all users in the roles you selected.";
          tooltipText = "Please note that quizzes will not be editable once a user has submitted responses, but any other microlearning can be edited after publishing.";

        } else {

          tooltipTitle = "This Module will be unavailable to users if unplublished.";
        }

        if (IsProgramView) {
          tooltipTitle += " Partners in the program will still be able to see it.";
        }
      }

      return WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.info, tooltipTitle, tooltipText, "ml5 pb5");
    }

    public string GetContentLibraryModalHtml() {
      if (!CanAddContentItems || AvailableContentInfoList.IsNullOrEmpty()) return "";

      return $@"
        <div id=""dlg{WebHelper.Content.CSS.ContentContainer}"" class=""displaynone {WebHelper.Content.CSS.ContentContainer}"">
          {WebHelper.Content.GetContentCardsForPartnerHtml(AvailableContentInfoList, WebHelper.Content.ViewFromPageEnum.ModuleEdit)}
        </div>";
    }

    public void RemoveContentItemFromModule(AjaxSubmitHelper ajax) {
      int contentId = WebHelper.GetFormValueIntOrDefault(FormFields.ContentId, 0);

      if (contentId > 0) {

        var contentInfo = ContentInModuleList.Find(x => x.ContentId == contentId);

        if (contentInfo != null) {

          bool removedFromModule = DbHelper.Modules.RemoveContentFromModule(contentInfo);

          if (removedFromModule) {

            ajax.AddSuccessToast("Microlearning removed from Module.");

          } else {

            ajax.AddDialogMessage("Microlearning could not be added to Module.");
          }

        } else {

          ajax.AddDialogMessage("Microlearning not found in Module.");
        }

      } else {

        ajax.AddDialogMessage("Microlearning Id is required.");
      }
    }

    public void UpdateContentDisplayOrder() {

      var contentList = WebHelper.GetFormIntListOrDefault(FormFields.ContentList);

      if (contentList.Count == 0 || ContentInModuleList.IsNullOrEmpty()) return; // Nothing to update

      // When ContentInModuleList is consulted, it's order by Display order
      // If the received contentList in this operation, doesn't march with the same ContentId as the other list, the DisplayOrder must be updated.
      for (int i = 0; i < contentList.Count; i++) {
        // Skip items where the ContentId is the same at i, for both lists (always the same length)
        if (contentList[i] != ContentInModuleList[i].ContentId) {
          // Get the corresponding ContentInfo based on the ContentId from ContentList
          var contentInfo = ContentInModuleList.Find(x => x.ContentId == contentList[i]);
          DbHelper.Modules.UpdateContentDisplayOrderInModule(null, contentInfo, i + 1);
        }
      }
    }
  }
}
