using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Integral.Web.PortalSite.Page_Partials {

  public partial class CoacheeSendEmailModal : AppCode.PageBaseClasses.CoacheeInfoBase {

    public const string FirstName_MergedField = "{{FirstName}}";

    // Server location of attachment uploads (temporary just until sent).
    readonly string uploadBasePath = PathHelper.ServerPaths.UploadTemp.EnsureEndsWith("\\", StringExt.Ensure.IfNotBlank);

    public int FormContentCols = 8;
    public int MaxAttachedFiles = 4;
    private bool CanSendEmailToCoachee;
    public bool CanAttachBookingLinkInEmail, CanSendContentEmail;
    public string EmailFromAddress, EmailFromName;
    public List<DbHelper.Content.ProgramContentInfo> ContentList;

    public class FormFields {
      public const string EmailSubject = "EmailSubject";
      public const string EmailBodyHTML = "EmailBodyHTML";
      public const string FileNameList = "FileNameList";
      public const string FileNameListDelimiter = "|";
      public const string IncludeBookingLink = "IncludeBookingLink";
      public const string IncludeContentLink = "IncludeContentLink";
      public const string ContentId = "ContentId";
    }

    class FormValues {
      public string EmailSubject;
      public string EmailBodyHTML;
      public string FileNameList;
      public List<FileInfo> FilesToAttach;
      public bool IncludeBookingLink;
      public bool IncludeContentLink;
      public int? ContentId;
      public DbHelper.Content.ProgramContentInfo ProgramContentInfo;
    }

    public class AjaxAction {
      public const string Send = "send";
      public const string Upload = "upload";
      public const string Delete = "delete";
    }

    protected void Page_Load(object sender, EventArgs e) {

      CanSendEmailToCoachee = SessionHelper.AppAccess.Participants.CanSendEmailToCoachee(CoacheeInfo);
      CanAttachBookingLinkInEmail = SessionHelper.AppAccess.Participants.CanAttachBookingLinkInEmail(CoacheeInfo);

      EmailFromAddress = userInfo.ComputedSenderEmailAddress;
      EmailFromName = userInfo.ComputedSenderEmailName;

      ContentList = DbHelper.Content.GetProgramContentListForEmail(ProgramInfo, SessionHelper.UserInfo);
      CanSendContentEmail = SessionHelper.AppAccess.Content.CanSendContentEmailToCoachee(CoacheeInfo) && !ContentList.IsNullOrEmpty();

      if (SystemWeb.IsHttpPost) {
        AjaxSubmitHelper.Process(ajax => {
          DoPost(ajax);
        });
        WebHelper.EndRequest();
        return;
      }
    }

    void DoPost(AjaxSubmitHelper ajax) {

      if (ajax.Action == AjaxAction.Upload) {

        UploadFile();
        return;

      } else if (ajax.Action == AjaxAction.Delete) {

        DeleteFile();
        return;

      } else {

        if (!CanSendEmailToCoachee) {
          ajax.AddDialogMessage("Update not allowed.");
          return;
        }
        SendEmail(ajax);
      }

    }

    void UploadFile() {

      var files = Request.Files;
      if (files == null) return;

      string path = GetUploadPath();
      string output = string.Empty;

      Directory.CreateDirectory(path);

      for (int fn = 0; fn < files.Count; fn++) {
        var file = files[fn];
        file.SaveAs(path + file.FileName);
        output += file.FileName + "\n"; // FilePond uses this for verification of upload.
      }

      WebHelper.WriteAndEnd(output);
    }

    string GetUploadPath() {
      // User Guid is used as the upload subfolder name.
      return uploadBasePath + SessionHelper.GetUserInfoOrNull().UserGuid.ToStringNoBraces() + "\\";
    }

    void DeleteFile() {

      string fileName = WebHelper.FilePond.GetDeleteFilename();
      string filePath = Path.Combine(GetUploadPath(), fileName);

      try {
        File.Delete(filePath);
      } catch (Exception) { } // ignore

      WebHelper.WriteAndEnd("deleted " + filePath);
    }


    void SendEmail(AjaxSubmitHelper ajax) {

      // Form validation.
      var formValues = new FormValues();
      formValues.EmailSubject = ajax.CheckFieldRegex(FormFields.EmailSubject, "Email Subject", AppHelper.Regex.GeneralText.Replace("]", @"}{\|\*]"), true, "Email Subject is required.");
      formValues.EmailBodyHTML = ajax.CheckFieldRegex(FormFields.EmailBodyHTML, "Email Body", AppHelper.Regex.HTML.Replace("]", @"}{\|\*]"), true, "Invalid Characters in Email Body.");
      formValues.FileNameList = ajax.CheckFieldRegex(FormFields.FileNameList, "Email Subject", AppHelper.Regex.FileName.Replace(FormFields.FileNameListDelimiter, ""), false, "Invalid character in file list.");
      formValues.IncludeBookingLink = ajax.CheckFieldBool(FormFields.IncludeBookingLink, "1");
      formValues.IncludeContentLink = ajax.CheckFieldBool(FormFields.IncludeContentLink, "1");

      if (formValues.IncludeContentLink && CanSendContentEmail) {

        formValues.ContentId = ajax.CheckFieldIDOrNull(FormFields.ContentId, "Microlearning", true, "Please select a Microlearning to attach to the email.");

        DbHelper.Content.ProgramContentInfo programContent = null;

        if (formValues.ContentId != null) {
          programContent = ContentList.FirstOrDefault(x => x.ContentInfo.ContentId == formValues.ContentId);
        }

        if (programContent == null) {
          ajax.AddBadField(FormFields.ContentId, "Select a valid Microlearning to attach to the email.");
          return;
        } else {
          formValues.ProgramContentInfo = programContent;
        }
      }

      if (ajax.BadFieldCount > 0) return;

      // Check all files were uploaded correctly.
      if (!CheckFilesExist(ajax, formValues)) return;

      string emailSubject = formValues.EmailSubject.Replace(FirstName_MergedField, CoacheeInfo.FirstName);
      string bodyHTML = formValues.EmailBodyHTML.Replace(FirstName_MergedField, CoacheeInfo.FirstName);
      string msg = "";

      if (formValues.IncludeBookingLink) {
        if (!CanAttachBookingLinkInEmail) {
          msg += "<br/> The coaching booking link wasn't attached, because the coachee doesn't have any sessions to book";
        } else {
          bodyHTML += "<br/>" + AlbertEmails.GetEmailLinkButton(PathHelper.Pages.CoacheeSessionBooking(CoacheeInfo.CoacheeUID, true), "Book Session");
        }
      } else if (formValues.IncludeContentLink && formValues.ProgramContentInfo != null) {
        bodyHTML += "<br/>" + AlbertEmails.GetCustomEmailContentItemHtml(formValues.ProgramContentInfo.ContentInfo);
      }

      bool success = AlbertEmails.SendGenericEmail(
        ProjectInfo,
        emailSubject, "", bodyHTML,
        false,
        EmailFromName, EmailFromAddress,
        formValues.FilesToAttach, out _,
        new System.Net.Mail.MailAddress(CoacheeInfo.EmailAddress, CoacheeInfo.GetFullName())
      );

      if (success) {
        DbHelper.EmailHistory.AddEmail(null, CoacheeInfo.CoacheeId, CoacheeInfo.ProgramJobId.GetValueOrDefault(ProgramInfo.ProgramJobId), false, "", userInfo.EmailAddress, CoacheeInfo.EmailAddress, emailSubject, null);
        SetRedirect(PathHelper.Pages.CoacheeEdit(PathHelper.CoacheeTabEnum.email, CoacheeInfo.CoacheeId), $"Email successfully sent. {msg}");
        return;
      }

      ajax.AddDialogMessage("Failed to send email to some recipients.");
      return;
    }

    bool CheckFilesExist(AjaxSubmitHelper ajax, FormValues formValues) {

      string path = GetUploadPath();
      var fileNameArray = formValues.FileNameList.Split(FormFields.FileNameListDelimiter.ToCharArray());

      if (!formValues.FileNameList.IsNullOrEmpty()) {
        formValues.FilesToAttach = new List<FileInfo>();
        foreach (var fileName in fileNameArray) {
          if (!File.Exists(path + fileName)) {
            ajax.AddDialogMessage("Can't find file:<br/>" + fileName + "<br/>Please try removing it and uploading it again.");
            return false;
          }
          formValues.FilesToAttach.Add(new FileInfo(path + fileName));
        }
      }
      return true;
    }

  }
}
