using System;
using System.Collections.Generic;
using System.Linq;
using Integral.Web.PortalSite.AppCode;
using Integral.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Integral.Web.PortalSite.Pages_Albert {

  public class ContentDetails : AppCode.PageBaseClasses.ModulePageBase {

    public List<DbHelper.AlbertCoaches.AlbertCoachInfo> PartnerList;
    public DbHelper.Content.ContentTypeEnum CurrentContentType;
    public PathHelper.Content.ContentFileType CurrentContentFileType;
    public bool CanAddContent, CanEditContent, CanDeleteContentItem, CanChangeAuthor, CanEditIsPublished, CanEditQuiz, CanSubmitQuizResponses, CanSubmitLearningAction, CanUpdateCompletedUtc;
    public bool CanSetPublicContentForParticipants, IsQuizResponded;
    public string ContentPreviewImgPath, ContentFilePath;
    public List<DbHelper.Content.ContentTagInfo> TagInfoForContent, ContentTagsInfo;
    public DbHelper.Content.ProgramContentInfo ProgramContentInfo;
    public List<DbHelper.WorkshopEvents.WorkshopEventInfo> WorkshopListInfo;
    public List<DbHelper.Content.QuizInfo> ContentQuizInfo = null;
    public int ContentUserId;
    public DbHelper.Content.UserContentInfo UserContentInfo = null;

    public class FormFields {
      public const string ContentId = "ContentId";
      public const string ContentGuid = "ContentGuid";
      public const string ContentTypeId = "ContentTypeId";
      public const string AuthorUserId = "AuthorUserId";
      public const string ContentTitle = "ContentTitle";
      public const string ContentHtml = "ContentHtml";
      public const string ContentSummary = "ContentSummary";
      public const string ShowToParticipants = "ShowToParticipants";
      public const string ShowToPartners = "ShowToPartners";
      public const string ContentUrl = "ContentUrl";
      public const string ContentTagId = "ContentTagId";
      public const string ScheduledSendDateUtc = "ScheduledSendDateUtc";
      public const string IsContentListed = "IsContentListed";
      public const string WorkshopEventId = "WorkshopEventId";
      public const string QuizData = "QuizData";
      public const string QuizQuestion = "QuizQuestion";
      public const string QuizOption = "QuizOption";
      public const string QuizCorrectOption = "QuizCorrectOption";
      public const string IsPublished = "IsPublished";
      public const string QuestionIndex = "QuestionIndex";
      public const string LearningActionText = "LearningActionText";
    }
    public class AjaxAction {
      public const string UpdateContent = "UpdateContent";
      public const string UploadCoverImage = "UploadCoverImage";
      public const string UploadFile = "UploadFile";
      public const string DeleteContent = "DeleteContent";
      public const string SubmitQuizResponses = "SubmitQuizResponses";
      public const string SubmitLearningAction = "SubmitLearningAction";
      public const string UpdateItemCompleted = "UpdateItemCompleted";
    }
    public class AjaxReturnData {
      public const string ContentGuid = "ContentGuid";
      public const string QuizResults = "QuizResults";
    }
    public class QuizQuestion {
      public string Question { get; set; }
      public List<string> Options { get; set; }
      public int CorrectOptionIndex { get; set; }
    }
    public class QuizResponses {
      public int QuestionId { get; set; }
      public int OptionId { get; set; }
    }

    public IActionResult OnGet() => Process();
    public IActionResult OnPost() => Process();

    private IActionResult Process() {

      PageTitle = IsParticipantView ? "" : "Microlearning Details";

      if (IsNewContent) {

        if (IsProgramView) {
          CanAddContent = SessionHelper.AppAccess.Content.CanAddContentToProgram(ProgramInfo);
        } else {
          CanAddContent = SessionHelper.AppAccess.Content.CanAddContent();
        }

        CanChangeAuthor = CanAddContent;
        CanSetPublicContentForParticipants = CanAddContent;
        CanEditContent = CanAddContent;
        CanEditIsPublished = CanEditContent;
        CurrentContentType = DbHelper.Content.ContentTypeEnum.Text; // DefaultValue
        CanEditQuiz = true;

        if (!CanAddContent) {
          SetRedirect("You do not have access to create microlearning.");
          return new EmptyResult();
        }

      } else {

        var canViewContentItem = false;

        if (IsProgramView && !IsParticipantView) {
          canViewContentItem = SessionHelper.AppAccess.Content.CanViewProgramContent(ProgramInfo);
        } else {
          canViewContentItem = SessionHelper.AppAccess.Content.CanViewContentItem(ContentInfo);
        }

        if (!canViewContentItem) {
          SetRedirect("You do not have access to view this microlearning.");
          return new EmptyResult();
        }

        CanChangeAuthor = SessionHelper.AppAccess.Content.CanChangeAuthor(ContentInfo);
        CanSetPublicContentForParticipants = SessionHelper.AppAccess.Content.CanSetPublicContentForParticipants(ContentInfo);
        CanDeleteContentItem = SessionHelper.AppAccess.Content.CanDeleteContentItem(ContentInfo);
        CanEditContent = SessionHelper.AppAccess.Content.CanEditContentItem(ContentInfo);
        CanEditIsPublished = SessionHelper.AppAccess.Content.CanEditIsPublished(ContentInfo);
        CanEditQuiz = SessionHelper.AppAccess.Content.CanEditQuiz(ContentInfo);
        CurrentContentType = ContentInfo.ContentType;

        if (CurrentContentType == DbHelper.Content.ContentTypeEnum.Image) {

          ContentFilePath = PathHelper.Content.GetContentFile(ContentInfo.ContentGuid, PathHelper.Content.ContentFileType.Image);
          ContentPreviewImgPath = PathHelper.Images.ContentImageFile(ContentInfo.ContentGuid, PathHelper.Images.ContentCoverImageSize.Thumbnail);

        } else if (CurrentContentType == DbHelper.Content.ContentTypeEnum.Document) {

          ContentPreviewImgPath = PathHelper.Images.DocumentPreview();
          ContentFilePath = PathHelper.Content.GetContentFile(ContentInfo.ContentGuid, PathHelper.Content.ContentFileType.Document);
        }

        if (ContentFilePath != null && ContentFilePath.IndexOf("?") == -1) {
          ContentFilePath += "?t=" + DateTime.Now.Ticks;
        }

        ContentTagsInfo = DbHelper.Content.GetTagsInfoByContentId(ContentInfo);

        if (IsParticipantView) {

          // Update viewed content only for Participants
          ContentUserId = DbHelper.Content.UpsertContentViewed(null, ContentInfo, SessionHelper.UserInfo);
          UserContentInfo = DbHelper.Content.GetUserContentInfo(ContentUserId);
          CanSubmitQuizResponses = SessionHelper.AppAccess.Content.CanSubmitQuizResponses(ContentInfo, UserContentInfo);
          CanSubmitLearningAction = SessionHelper.AppAccess.Content.CanSubmitLearningAction(ContentInfo, UserContentInfo);
          CanUpdateCompletedUtc = SessionHelper.AppAccess.Content.CanUpdateCompletedUtc(ContentInfo, UserContentInfo);
          IsQuizResponded = UserContentInfo != null && UserContentInfo.IsCompleted;

          if (ContentInfo.ContentType == DbHelper.Content.ContentTypeEnum.Text || ContentInfo.ContentType == DbHelper.Content.ContentTypeEnum.Url || ContentInfo.ContentType == DbHelper.Content.ContentTypeEnum.Video) {
            UpsertCompleted();
          }
        }

        if (CurrentContentType == DbHelper.Content.ContentTypeEnum.Quiz) {
          ContentQuizInfo = DbHelper.Content.GetQuizContentInfo(ContentInfo);
        }
      }

      if (IsProgramView && !IsParticipantView) {

        CanEditContent = SessionHelper.AppAccess.Content.CanEditProgramContent(ProgramInfo, ContentInfo, IsNewContent);
        WorkshopListInfo = DbHelper.WorkshopEvents.GetWorkshopsInProgram(ProgramInfo.ProgramJobId);
        ProjectInfo = DbHelper.Projects.GetProjectInfoOrNull(ProgramInfo.ProgramJobNumber);
        MenuThirdLayerActive_Programs = ProjectMenuIsActive = true;

        if (!IsNewContent) {
          ProgramContentInfo = DbHelper.Content.GetProgramContent(ProgramInfo, ContentInfo);
        }
      }

      if (CanChangeAuthor) {
        PartnerList = DbHelper.AlbertCoaches.GetCoachInfoList(false, DbHelper.AbleUser.RegisteredFilter.OnlyRegistered);
      }

      TagInfoForContent = DbHelper.Content.GetContentTagsInfo();

      if (SystemWeb.IsHttpPost) {

        AjaxSubmitHelper.Process(ajax => {

          switch (PageAjaxAction) {

            case AjaxAction.UpdateContent:
              if ((!IsNewContent && !CanEditContent) || (IsNewContent && !CanAddContent)) {
                ajax.AddDialogMessage("Update not allowed.");
                return;
              }
              UpdateContent(ajax);
              return;

            case AjaxAction.UploadCoverImage:
              CurrentContentFileType = PathHelper.Content.ContentFileType.CoverImage;
              if (!CanEditContent) {
                ajax.AddDialogMessage("Update not allowed.");
                return;
              }
              SaveContentItem(ajax);
              return;

            case AjaxAction.UploadFile:
              if (!CanEditContent) {
                ajax.AddDialogMessage("Update not allowed.");
                return;
              }
              SaveContentItem(ajax);
              return;

            case AjaxAction.DeleteContent:
              if (!CanDeleteContentItem) {
                ajax.AddDialogMessage("Delete not allowed.");
                return;
              }
              DeleteContent(ajax);
              return;

            case AjaxAction.SubmitQuizResponses:
              if (!CanSubmitQuizResponses) {
                ajax.AddDialogMessage("Submit not allowed.");
                return;
              }
              SubmitQuizResponses(ajax);
              return;

            case AjaxAction.SubmitLearningAction:
              if (!CanSubmitLearningAction) {
                ajax.AddDialogMessage("Submit not allowed.");
                return;
              }
              SubmitLearningAction(ajax);
              return;

            case AjaxAction.UpdateItemCompleted:
              if (CanUpdateCompletedUtc) {
                UpdateItemCompleted();
              }
              return;
          }
        });
        return new EmptyResult();
      }

      return Page();
    }

    public void UpdateItemCompleted() {

      if (ContentInfo.ContentType != DbHelper.Content.ContentTypeEnum.Document && ContentInfo.ContentType != DbHelper.Content.ContentTypeEnum.Image) return;

      UpsertCompleted();
    }

    public bool UpsertCompleted() {
      if (UserContentInfo != null && UserContentInfo.IsCompleted) {
        return false;
      }

      return DbHelper.Content.UpsertContentCompleted(null, ContentInfo, SessionHelper.UserInfo) > 0;
    }

    public string GetRequiredFieldIconHtml() {
      return WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.requiredField, "Required field.", "This field must be filled for it to be public or published.");
    }

    void SubmitQuizResponses(AjaxSubmitHelper ajax) {

      if (ContentQuizInfo == null) {
        ajax.AddBadField(FormFields.QuizData, "Quiz not found.");
        return;
      }

      var quizData = WebHelper.GetFormValue(FormFields.QuizData);
      if (string.IsNullOrEmpty(quizData)) {

        ajax.AddBadField(FormFields.QuizData, "No responses received.");
        return;

      } else {
        try {
          // Deserialize the quiz data
          var deserializedResponses = Newtonsoft.Json.JsonConvert.DeserializeObject<List<QuizResponses>>(quizData);

          if (deserializedResponses == null || !deserializedResponses.Any()) {
            ajax.AddBadField(FormFields.QuizData, "Quiz must contain at least one response");
            return;
          }

          var responsesList = new List<DbHelper.Content.UserResponsesInfo>();
          int correctResponses = 0;
          int totalQuestions = ContentQuizInfo.Count;
          // Validate the deserialized data
          foreach (var response in deserializedResponses) {
            var questionInfo = ContentQuizInfo.Where(x => x.QuestionId == response.QuestionId).FirstOrDefault();
            if (questionInfo != null) {
              var correctResponse = questionInfo.Options.Where(x => x.IsCorrect).FirstOrDefault();
              if (correctResponse != null) {
                responsesList.Add(new DbHelper.Content.UserResponsesInfo(response.QuestionId, response.OptionId));
                if (correctResponse.OptionId == response.OptionId) {
                  correctResponses++;
                }
              }
            }
          }

          if (responsesList.Count != deserializedResponses.Count || responsesList.Count != totalQuestions) {
            ajax.AddBadField(FormFields.QuizQuestion, "All questions must be answered");
            return;
          }

          if (ajax.BadFieldCount > 0) return;

          double quizScore = 0;
          if (correctResponses > 0) {
            quizScore = (double)correctResponses / (double)totalQuestions * 100;
          }

          // Save info to db
          bool updatedResponses = DbHelper.Content.UpdateUserQuizResponses(ContentUserId, quizScore, responsesList);

          if (updatedResponses) {
            UserContentInfo = DbHelper.Content.GetUserContentInfo(ContentUserId);
            IsQuizResponded = true;
            ajax.AddReturnValue(AjaxReturnData.QuizResults, GetParticipantQuizResultsHtml());
          }
          return;

        } catch (Newtonsoft.Json.JsonException ex) {
          var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
          telemetry?.Exception(ex)
            .WithOperation("SubmitQuizResponses_DeserializeQuizData")
            .FromSession()
            .WithProperty("ContentId", ContentInfo.ContentId)
            .WithProperty("ContentGuid", ContentInfo.ContentGuid.ToString())
            .Track();
          ajax.AddBadField(FormFields.QuizQuestion, $"Invalid quiz data format - {ex}");
          return;
        }
      }
    }

    void SubmitLearningAction(AjaxSubmitHelper ajax) {

      string learningActionText = WebHelper.GetFormValue(FormFields.LearningActionText);

      bool submitted = DbHelper.Content.UpsertContentLearningAction(null, ContentInfo, SessionHelper.UserInfo, learningActionText);

      if (submitted) {
        ajax.SetReloadPage("Your learning action has been submitted", AjaxSubmitHelper.PageMessageType.SuccessToast);
      } else {
        ajax.SetReloadPage("Couldn't submit learning action", AjaxSubmitHelper.PageMessageType.ErrorToast);
      }
      return;
    }

    public string ContentTypeHtml() {

      string html = "", labelText = "Type:";

      if (IsNewContent) {

        var optionList = new List<WebHelper.ButtonGroupButton>();

        foreach (DbHelper.Content.ContentTypeEnum ct in Enum.GetValues(typeof(DbHelper.Content.ContentTypeEnum))) {
          optionList.Add(new WebHelper.ButtonGroupButton(WebHelper.Content.GetContentTypeTextDisplay(ct), ct.ToString(), null, WebHelper.Content.GetContentTypeIconEnum(ct)));
        }

        html = WebHelper.GetButtonGroup(
            labelText,
            FormFields.ContentTypeId,
            optionList,
            CurrentContentType.ToString(),
            !CanAddContent,
            "", 1);

      } else {

        html = WebHelper.GetTextDisplayRow(labelText, 11, GetContentTypeBadge());
      }
      return html;
    }

    public string GetContentTypeBadge() {
      return $@"
          <div class=""content-type-badge"">
            <span class=""badge"">{WebHelper.GetIconHtml(WebHelper.Content.GetContentTypeIconEnum(CurrentContentType), "mr5")} {WebHelper.Content.GetContentTypeTextDisplay(CurrentContentType)}</span>
          </div>";
    }

    void DeleteContent(AjaxSubmitHelper ajax) {
      bool deletedContent = DbHelper.Content.DeleteContent(ContentInfo);
      if (deletedContent) {
        ajax.SetRedirectUrl(PathHelper.Pages.Content(), "Microlearning successfully deleted.");
        return;
      } else {
        ajax.AddErrorToast("Microlearning couldn't be deleted");
      }
    }

    void SaveContentItem(AjaxSubmitHelper ajax) {

      var uploadedFile = SystemWeb.GetRequestFile("image") ?? SystemWeb.GetRequestFile("file");
      if (uploadedFile == null) return;

      var fileExtension = System.IO.Path.GetExtension(uploadedFile.FileName);
      var contentGuid = ajax.CheckGuid(FormFields.ContentGuid, "Microlearning Guid", true, "Something went wrong creating the Microlearning item.");

      if (contentGuid == null) {
        ajax.AddDialogMessage("Microlearning not found");
        return;
      }

      if (CurrentContentFileType != PathHelper.Content.ContentFileType.CoverImage) {

        ContentInfo = DbHelper.Content.GetContentInfo(contentGuid.Value, SessionHelper.UserInfo, SessionHelper.UserRole);

        if (ContentInfo.ContentType != CurrentContentType) throw new ApplicationException("ContentInfo.ContentType != CurrentContentType");

        if (ContentInfo.ContentType == DbHelper.Content.ContentTypeEnum.Image) {
          CurrentContentFileType = PathHelper.Content.ContentFileType.Image;
        } else if (CurrentContentType == DbHelper.Content.ContentTypeEnum.Document) {
          CurrentContentFileType = PathHelper.Content.ContentFileType.Document;
        }
      }

      PathHelper.Content.RemoveContentFiles(contentGuid, CurrentContentFileType); // Remove old files if existing

      if (CurrentContentFileType == PathHelper.Content.ContentFileType.CoverImage || CurrentContentFileType == PathHelper.Content.ContentFileType.Image) {

        using (var inputStream = uploadedFile.OpenReadStream()) {
          PathHelper.Images.SaveStreamToContentImage(inputStream, contentGuid, CurrentContentFileType);
        }

      } else {

        string fileName = PathHelper.Content.ContentFileServerPathAndName(contentGuid, CurrentContentFileType, fileExtension);

        using (var fileStream = System.IO.File.Create(fileName)) {
          using (var inputStream = uploadedFile.OpenReadStream()) {
            inputStream.CopyTo(fileStream);
          }
        }
      }
    }

    public string GetAuthorHtml() {

      string authorsHtml = "";

      if (CanChangeAuthor) {

        var selectedAuthorUserId = IsNewContent ? userInfo.UserId : ContentInfo.AuthorUserId;

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
        var authorUserGuid = IsNewContent ? userInfo.UserGuid : ContentInfo.AuthorUserGuid;
        var authorFullName = IsNewContent ? userInfo.GetFullName() : ContentInfo.GetAuthorFullName();

        authorsHtml = WebHelper.GetAvatarForTable_User(PathHelper.Images.UserPhoto(authorUserGuid, true), authorFullName, null);
      }

      return WebHelper.GetTextDisplayRow("Author:", 11, authorsHtml);
    }

    public void UpdateContent(AjaxSubmitHelper ajax) {

      if (IsNewContent) {
        // Get Current Content from text
        var contentType = WebHelper.GetFormValue(FormFields.ContentTypeId);

        if (Enum.TryParse<DbHelper.Content.ContentTypeEnum>(contentType, out var parsedContentType)) {
          // Now use the parsed enum to access the corresponding value
          CurrentContentType = parsedContentType;
          ContentInfo.ContentTypeId = DbHelper.Content.ContentTypeIds[CurrentContentType];
        } else {
          ContentInfo.ContentTypeId = DbHelper.Content.ContentTypeIds[CurrentContentType];
        }
      }

      // Keep the flags on top always, so it can determine if certain fields are Required or Optional.
      // Flags section starts
      ContentInfo.ShowToPartners = ajax.CheckFieldBool(FormFields.ShowToPartners, "1");

      if (CanSetPublicContentForParticipants) {
        ContentInfo.ShowToParticipants = ajax.CheckFieldBool(FormFields.ShowToParticipants, "1");
      } else if (IsNewContent) {
        ContentInfo.ShowToParticipants = false;
      }

      if (CanEditIsPublished) {
        var isPublished = ajax.CheckFieldBool(FormFields.IsPublished, "1");
        ContentInfo.PublishedUtc = isPublished ? DateTime.UtcNow : (DateTime?)null;
      }
      // Flags section ends

      var allFieldsAreRequired = ContentInfo.ShowToPartners || ContentInfo.ShowToParticipants;
      var IsUrlRequired = (CurrentContentType == DbHelper.Content.ContentTypeEnum.Url || CurrentContentType == DbHelper.Content.ContentTypeEnum.Video) && allFieldsAreRequired;

      // Can be optional fields
      ContentInfo.ContentHtml = ajax.CheckFieldRegex(FormFields.ContentHtml, "Microlearning Description", AppHelper.Regex.HTML.Replace("]", @"}{\|\*]"), allFieldsAreRequired, "Microlearning Description");
      ContentInfo.ContentUrl = ajax.CheckFieldRegex(FormFields.ContentUrl, "Microlearning Link", AppHelper.Regex.URL, IsUrlRequired, "Please enter a valid Link.");

      // Required fields always
      ContentInfo.ContentTitle = ajax.CheckFieldRegex(FormFields.ContentTitle, "Microlearning Title", AppHelper.Regex.GeneralText, true, "Please enter a Microlearning Title.");
      ContentInfo.ContentSummary = ajax.CheckFieldRegex(FormFields.ContentSummary, "Microlearning Summary", AppHelper.Regex.GeneralText, true, "Microlearning Summary");

      var selectedContentTagList = WebHelper.GetFormValue(FormFields.ContentTagId).ToIntList();
      if (allFieldsAreRequired) {
        if (selectedContentTagList.IsNullOrEmpty()) {
          ajax.AddBadField(FormFields.ContentTagId, "Please select at least one Tag.");
        } else {
          // Check that all selected tags exist in tags table
          bool allTagsAreValid = selectedContentTagList.All(selectedTag => TagInfoForContent.Any(tag => tag.TagId == selectedTag));
          if (!allTagsAreValid) {

            ajax.AddBadField(FormFields.ContentTagId, "Please select at least one valid Tag.");
          }
        }
      }

      if (CurrentContentType == DbHelper.Content.ContentTypeEnum.Video) {
        bool isValidYoutubeUrl = PathHelper.IsValidYoutubeUrl(ContentInfo.ContentUrl);
        if (!isValidYoutubeUrl) {
          ajax.AddBadField(FormFields.ContentUrl, "Please enter a valid Youtube link.");
        }
      }

      var quizQuestions = new List<QuizQuestion>();
      if (CurrentContentType == DbHelper.Content.ContentTypeEnum.Quiz && CanEditQuiz) {

        ContentQuizInfo = new List<DbHelper.Content.QuizInfo>();
        var quizData = WebHelper.GetFormValue(FormFields.QuizData);

        if (string.IsNullOrEmpty(quizData)) {

          ajax.AddBadField(FormFields.QuizData, "Please add at least one question to the quiz");

        } else {
          try {
            // Deserialize the quiz data
            quizQuestions = Newtonsoft.Json.JsonConvert.DeserializeObject<List<QuizQuestion>>(quizData);

            if (quizQuestions == null || !quizQuestions.Any()) {
              ajax.AddBadField(FormFields.QuizData, "Quiz must contain at least one question");
              return;
            }

            // Validate the deserialized data
            foreach (var question in quizQuestions) {
              if (string.IsNullOrWhiteSpace(question.Question) || question.Options == null || question.CorrectOptionIndex < 0 || question.CorrectOptionIndex >= question.Options.Count) {
                ajax.AddBadField(FormFields.QuizQuestion, "Invalid quiz format");
                return;
              }

              // Create the quiz options list
              var questionOptions = new List<DbHelper.Content.QuizOptionsInfo>();
              int optionIndex = 0;
              foreach (var option in question.Options) {
                bool isCorrect = optionIndex == question.CorrectOptionIndex;
                questionOptions.Add(new DbHelper.Content.QuizOptionsInfo(option, isCorrect, optionIndex));
                optionIndex++;
              }

              // Add to the quiz info list
              ContentQuizInfo.Add(new DbHelper.Content.QuizInfo(question.Question, questionOptions));
            }

          } catch (Newtonsoft.Json.JsonException ex) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation("UpdateContent_DeserializeQuizData")
              .FromSession()
              .WithProperty("ContentId", ContentInfo?.ContentId)
              .WithProperty("ContentGuid", ContentInfo?.ContentGuid.ToString())
              .Track();
            ajax.AddBadField(FormFields.QuizQuestion, $"Invalid quiz data format - {ex}");
            return;
          }
        }
      }

      if (CanChangeAuthor && !PartnerList.IsNullOrEmpty()) {

        var selectedAuthorId = ajax.CheckFieldID(FormFields.AuthorUserId, "Author", true, "Please select an Author.");

        if (!PartnerList.Exists(x => x.UserId == selectedAuthorId)) {
          ajax.AddBadField(FormFields.AuthorUserId, "Please select a valid Author");
        }

        ContentInfo.AuthorUserId = selectedAuthorId;

      } else if (IsNewContent) {
        ContentInfo.AuthorUserId = userInfo.UserId;
      }

      if (ajax.BadFieldCount > 0) return;

      bool contentUpdated = false;

      if (IsNewContent) {
        ContentInfo = DbHelper.Content.AddContent(ContentInfo, SessionHelper.UserInfo, SessionHelper.UserRole);
        contentUpdated = ContentInfo != null;
      } else {
        contentUpdated = DbHelper.Content.UpdateContent(ContentInfo);
      }

      if (contentUpdated) {

        if (IsProgramView && CanEditContent && ProgramInfo != null) {
          var scheduledSendDateUtc = ajax.GetDatePickerToUtc(FormFields.ScheduledSendDateUtc, SessionHelper.GetSessionTimeZone(), "Scheduled Send Date", false, "Please provide a date.");
          var isContentListed = ajax.CheckFieldBool(FormFields.IsContentListed, "1");
          var workshopEventId = ajax.CheckFieldIDOrNull(FormFields.WorkshopEventId, "Microlearning", false, "Please select a Microlearning to attach to the email.");

          if (workshopEventId != null) {
            if (WorkshopListInfo.IsNullOrEmpty() || !WorkshopListInfo.Exists(x => x.WorkshopEventId == workshopEventId)) {
              ajax.AddErrorToast("Select a valid Workshop");
              return;
            }
          }

          var newProgramContent = new DbHelper.Content.ProgramContentInfo(ProgramInfo.ProgramJobId, scheduledSendDateUtc, 0, isContentListed, userInfo.UserId, DateTime.UtcNow, ContentInfo, workshopEventId);
          try {
            DbHelper.Content.UpsertContentToProgram(null, newProgramContent);
          } catch (Exception ex) {
            ajax.AddDialogMessage("Error linking Microlearning to program: " + ex.Message);
          }
        }

        if (CurrentContentType == DbHelper.Content.ContentTypeEnum.Quiz && CanEditQuiz) {
          DbHelper.Content.UpdateQuizContent(ContentInfo, ContentQuizInfo);
        }

        UpdateContentTags(selectedContentTagList);

        if (IsNewContent) {
          SessionHelper.SetNextPageMessageText($"Microlearning created successfully");
        }

        ajax.AddReturnValue(AjaxReturnData.ContentGuid, ContentInfo.ContentGuid);
      }
    }

    private bool UpdateContentTags(List<int> selectedContentTagList) {

      bool doUpdateTags = true; // By default set to true

      if (selectedContentTagList.IsNullOrEmpty()) return false;

      if (ContentTagsInfo != null) {
        var currentTagIds = ContentTagsInfo.Select(tag => tag.TagId).ToList();
        doUpdateTags = selectedContentTagList.Except(currentTagIds).Any() || currentTagIds.Except(selectedContentTagList).Any();
      }

      if (!doUpdateTags) return true; // No need to update

      return DbHelper.Content.UpdateContentTags(ContentInfo, ContentTagsInfo, selectedContentTagList);
    }

    public string GetPublicTooltipInfo(string formName) {

      if (!CanEditContent) return "";

      string tooltipTitle = "", tooltipText = "";

      if (formName == FormFields.ShowToParticipants) {
        tooltipTitle = "Visible to all participants on Able";

      } else if (formName == FormFields.ShowToPartners) {
        tooltipTitle = "Visible to all partners on Able.";

      } else if (formName == FormFields.IsPublished) {

        if (IsNewContent) {

          tooltipTitle = "Publishing this Microlearning will make it available to all users in the roles you selected.";
          tooltipText = "Please note that quizzes will not be editable once a user has submitted responses, but any other microlearning can be edited after publishing.";

        } else {

          if (CurrentContentType == DbHelper.Content.ContentTypeEnum.Quiz) {
            if (ContentInfo.IsRespondedQuiz) {
              tooltipTitle = "This quiz has been responded by users and can no longer be edited.";
            } else {
              tooltipTitle = "This quiz will be unavailable to users if unplublished.";
            }
          } else {
            tooltipTitle = "This Microlearning will be unavailable to users if unplublished.";
          }
        }

        if (IsProgramView) {
          tooltipTitle += " Partners in the program will still be able to see it.";
        }
      }

      return WebHelper.GetIconTooltip(WebHelper.ActionButtonTypeEnum.info, tooltipTitle, tooltipText, "ml5 pb5");
    }

    public string GetQuizQuestionHtml() {
      string html = "";
      int questionIndex = 1;

      if (ContentQuizInfo.IsNullOrEmpty()) return html;

      foreach (var questionInfo in ContentQuizInfo) {

        html += $@"
          <div class=""question mb20"">
            <div class=""mb10"">
              <label>Question:</label>
              {(CanEditQuiz ? $@"<button type=""button"" class=""btn btn-danger btn-xsm btnRemoveQuestion float-right"">Remove Question</button>" : "")}
            </div>
            <input type=""text"" class=""form-control mb10"" name=""{FormFields.QuizQuestion}"" value=""{questionInfo.Question}"" required>
            <div class=""options"">
              <hr/>
              <label class=""mb10"">Options:</label>
              {GetQuizOptionHtml(questionInfo, questionIndex)}
            </div>
            {(CanEditQuiz ? $@"<button type=""button"" class=""btn btn-secondary btn-sm mt10 btnAddOption"">Add Option</button>" : "")}
          </div>";

        questionIndex++;
      }

      return html;
    }

    public string GetQuizOptionHtml(DbHelper.Content.QuizInfo quizInfo, int questionIndex) {

      string html = "";

      foreach (var optionInfo in quizInfo.Options) {

        html += $@"
          <div class=""option flex-inline w100p mb15"">
            <input type=""radio"" title=""Correct Option"" name=""{FormFields.QuizCorrectOption}_{questionIndex}"" {(optionInfo.IsCorrect ? "checked" : "")} required>
            <input type=""text"" class=""form-control ml10 mr10"" name=""{FormFields.QuizOption}_{questionIndex}"" value=""{optionInfo.OptionText}"" required>
            {(CanEditQuiz ? $@"{WebHelper.Content.GetDeleteButtonForTable(WebHelper.Content.CSS.RemoveOptionButtonInTable)}" : "")}
          </div>";
      }

      return html;
    }

    public string GetParticipantQuizHtml() {
      int questionIndex = 1;
      string html = string.Empty;

      if (ContentQuizInfo.IsNullOrEmpty() || !IsParticipantView) return html;

      foreach (var questionInfo in ContentQuizInfo) {

        var respondedInfo = new DbHelper.Content.UserResponsesInfo();
        if (UserContentInfo != null && !UserContentInfo.QuizResponses.IsNullOrEmpty()) {
          respondedInfo = UserContentInfo.QuizResponses.FirstOrDefault(x => x.QuestionId == questionInfo.QuestionId);
        }

        string optionHtml = "";
        var questionOptions = questionInfo.Options.OrderBy(x => x.DisplayOrder);
        foreach (var option in questionOptions) {
          string customClass = "", selectedOption = "";

          if (IsQuizResponded) {
            if (respondedInfo?.OptionId == option.OptionId) {
              selectedOption = option.OptionId.ToString();
              customClass = option.IsCorrect ? "correctResponse" : "incorrectResponse";

            } else if (option.IsCorrect) {
              customClass = "correctOption";
            }
          }

          optionHtml += $@"
            <div class=""option {customClass} {(!selectedOption.IsNullOrEmpty() ? "selected" : "")}"" data-{FormFields.QuizOption}=""{option.OptionId}"">
              {option.OptionText}
            </div>";
        }

        html += $@"
          <div class=""question-container"" data-{FormFields.QuestionIndex}=""{questionIndex - 1}"">
            <div class=""question"" data-{FormFields.QuizQuestion}=""{questionInfo.QuestionId}"">{questionIndex}. {questionInfo.Question}</div>
            <div class=""options"">{optionHtml}</div>
          </div>";

        questionIndex++;
      }
      return html;
    }

    public string GetParticipantQuizResultsHtml() {

      if (ContentQuizInfo.IsNullOrEmpty()
        || !IsParticipantView
        || (UserContentInfo == null && UserContentInfo.QuizResponses.IsNullOrEmpty())) {

        return string.Empty;
      }

      int correctAnswers = 0;
      foreach (var questionInfo in ContentQuizInfo) {
        var respondedInfo = UserContentInfo.QuizResponses.FirstOrDefault(x => x.QuestionId == questionInfo.QuestionId);
        var correctOptionId = questionInfo.Options.FirstOrDefault(x => x.IsCorrect).OptionId;
        if (respondedInfo != null && respondedInfo.OptionId == correctOptionId) {
          correctAnswers++;
        }
      }

      return $@"
        <div class=""results"">
          <div class=""score-circle"">
            <div class=""score-number"">{UserContentInfo.QuizScore?.ToString("0")}%</div>
          </div>
          <div class=""score-text"">Quiz Complete!</div>
          <div class=""score-details"">
            <p>Correct Answers: <span id=""correctAnswers"">{correctAnswers}</span></p>
            <p>Total Questions: <span id=""totalQuestions"">{UserContentInfo.QuizResponses.Count}</span></p>
          </div>
          <div class=""quiz-responses"">
            {GetParticipantQuizHtml()}
          </div>
        </div>";
    }

    public string GetNextLearningButtonHtml() {
      string buttonText = "", buttonPath = "";

      if (!IsParticipantView || ModuleInfo == null) return "";

      int currentIndex = ModuleInfo.ContentInModule.IndexOf(ContentInfo.ContentGuid);
      if (currentIndex >= 0) {
        if (currentIndex != ModuleInfo.ContentInModule.Count - 1) {
          // Get next learning
          var nextContentGuid = ModuleInfo.ContentInModule[currentIndex + 1];
          buttonPath = PathHelper.Pages.ContentDetails(nextContentGuid, ModuleInfo.ModuleGuid);
          buttonText = "Next Learning";

        } else {
          // Finish Module
          buttonPath = PathHelper.Pages.Module(ModuleInfo.ModuleGuid);
          buttonText = "Finish";
        }

      } else {
        return "";
      }

      return $@"
        <div class=""w100p mt30 mb20 h50"">
          <hr />
          <a class=""btn btn-sm btn-primary float-right w200"" href=""{buttonPath}"">{buttonText}</a>
        </div>";
    }
  }
}
