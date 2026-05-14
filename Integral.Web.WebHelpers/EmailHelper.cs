using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using Mandrill;
using Mandrill.Model;
using Integral.Web.Services;

namespace Integral.Web {

  public class EmailHelper {

    private static readonly MandrillApi _mandrillApi;

    static EmailHelper() {


      // Per Mandrill docs, we should use a common API instance for all emails.
      _mandrillApi = new MandrillApi(ConfigHelper.MandrillAPIKey);
    }

    // Returns just the domain part from an email address (everything after the @) or "" if invalid string.
    public static string GetEmailDomain(string emailAddr) {
      if (!emailAddr.IsNullOrEmpty()) {
        var pos = emailAddr.IndexOf("@");
        if (pos >= 0 && pos < emailAddr.Length - 1) {
          return emailAddr.Substring(pos + 1);
        }
      }
      return "";
    }

    public static bool IsRecipientEmailForTesting(string email) {
      try {
        return Regex.IsMatch(email, ConfigHelper.TestEmailExceptionRegex, RegexOptions.IgnoreCase | RegexOptions.Singleline);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation(nameof(IsRecipientEmailForTesting))
          .WithProperty(ApplicationInsightsConstants.Email, email)
          .Track();
      }
      return false;
    }

    public static bool IsEmailAllowed() {
      if (IsMaxEmailsCheckForRequestDisabled()) return true;
      int? maxEmails = GetTestEmailLimitForRequest();
      if (maxEmails == null) return true; // No maximum has been set.
      return GetEmailsSentThisRequest() < maxEmails;
    }

    // Important: RespectOverrideRecipient_Default is the default setting, it will respect "OverrideRecipientEmail" in appSettings if present.
    //            AlwaysSendToLiveRecipient_ForTesting will IGNORE "OverrideRecipientEmail" and always send to the ACTUAL recipient.
    public enum EmailRecipientOption { RespectRecipientOverride_Default, SendToRealRecipient_ForTesting }

    public static bool SendSingleWithMergeTemplate(
      List<MailAddress> addrTos,
      List<MailAddress> addrCCs,
      List<MailAddress> addrBCCs,
      string preferredSenderEmailName, string preferredSenderEmailAddress,
      DbHelper.Projects.ProjectInfoBrief projectInfoBrief,
      bool allowProjectOverrideSender,
      string subject,
      string templateName,
      Dictionary<string, string> mergePairs,
      out MandrillSentResult sendResult,
      List<FileInfo> filesToAttach = null,
      EmailRecipientOption emailRecipientOption = EmailRecipientOption.RespectRecipientOverride_Default) {

      if (addrTos.IsNullOrEmpty()) throw new ArgumentException("addrTos must contain at least one address.");

      if (!IsEmailAllowed()) {
        sendResult = new MandrillSentResult(true);
        return true; // Test limit reached, but pretend it was sent.
      }

      IncrementEmailsSentThisRequest();

      subject = WebHelper.GetDevOrStagingSiteTagText().EnsureEndsWith(" ", StringExt.Ensure.IfNotBlank) + subject;

      GetSenderFromProjectOrDefault(
        preferredSenderEmailName, preferredSenderEmailAddress,
        projectInfoBrief, allowProjectOverrideSender,
        out string senderEmailName, out string senderEmailAddress);

      var message = new MandrillMessage {
        MergeLanguage = MandrillMessageMergeLanguage.Mailchimp,
        FromName = senderEmailName,
        FromEmail = senderEmailAddress,
        Subject = subject,
        InlineCss = false,
        ViewContentLink = true
      };

      if (filesToAttach != null && filesToAttach.Count > 0) {
        foreach (var fileInfo in filesToAttach) {
          try {
            message.Attachments.Add(new MandrillAttachment("application/octet-stream", fileInfo.Name, File.ReadAllBytes(fileInfo.FullName)));
          } catch (Exception ex) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(ex)
              .WithOperation(nameof(SendSingleWithMergeTemplate))
              .WithOperationContext("AttachFile")
              .WithProperty(ApplicationInsightsConstants.FileName, fileInfo?.Name)
              .WithProperty(ApplicationInsightsConstants.FilePath, fileInfo?.FullName)
              .Track();
            // Ignore for now.
          }
        }
      }

      // ValidateRecipients required so dev & staging don't sent to real people.
      ValidateRecipients(addrTos, emailRecipientOption);
      ValidateRecipients(addrCCs, emailRecipientOption);
      ValidateRecipients(addrBCCs, emailRecipientOption);

      foreach (var addr in addrTos) message.AddTo(addr.Address, addr.DisplayName, MandrillMailAddressType.To);
      if (addrCCs != null) foreach (var addr in addrCCs) message.AddTo(addr.Address, addr.DisplayName, MandrillMailAddressType.Cc);
      if (addrBCCs != null) foreach (var addr in addrBCCs) message.AddTo(addr.Address, addr.DisplayName, MandrillMailAddressType.Bcc);

      if (mergePairs != null) foreach (var mergeVar in mergePairs) message.AddGlobalMergeVars(mergeVar.Key, mergeVar.Value);

      sendResult = new MandrillSentResult(message, _mandrillApi.Messages.SendTemplateAsync(message, templateName).Result);

      return sendResult.Sent;
    }

    private static void GetSenderFromProjectOrDefault(
      string preferredSenderEmailName, string preferredSenderEmailAddress,
      DbHelper.Projects.ProjectInfoBrief projectInfoBrief, bool allowProjectOverrideSender,
      out string senderEmailName, out string senderEmailAddress) {

      if (allowProjectOverrideSender && projectInfoBrief != null && !projectInfoBrief.OverrideSenderEmailAddress.IsNullOrEmpty()) {

        // Project override always takes priority if set.
        senderEmailName = projectInfoBrief.OverrideSenderEmailName;
        senderEmailAddress = projectInfoBrief.OverrideSenderEmailAddress;

      } else if (!preferredSenderEmailAddress.IsNullOrEmpty()) {

        // Preferred sender if provided for the specific use case (e.g. sending from coach, sharing a survey).
        senderEmailName = preferredSenderEmailName;
        senderEmailAddress = preferredSenderEmailAddress;

      } else if (projectInfoBrief != null && !projectInfoBrief.ComputedSenderEmailAddress.IsNullOrEmpty()) {

        // Fall back to computed sender if possible.
        senderEmailName = projectInfoBrief.ComputedSenderEmailName;
        senderEmailAddress = projectInfoBrief.ComputedSenderEmailAddress;

      } else {

        // If all else fails, use config default.
        senderEmailName = ConfigHelper.Email_Coordinator_FullName;
        senderEmailAddress = ConfigHelper.Email_Coordinator_Address;
      }
    }

    public class MandrillSentResult {
      public MandrillMessage Message;
      public string Id;
      public string RejectReason;
      public string Status;
      public bool Sent;
      public string StatusAndReason => Status + RejectReason.EnsureStartsWith(", ", true);
      public MandrillSentResult(bool sent) {
        Sent = sent;
      }
      public MandrillSentResult(MandrillMessage message, IList<MandrillSendMessageResponse> mandrillSendResult) {
        if (mandrillSendResult == null || mandrillSendResult.Count == 0) return;
        var result = mandrillSendResult[0]; // Just examine first result.
        Message = message;
        Id = result.Id;
        RejectReason = result.RejectReason;
        Status = result.Status.ToString();
        Sent = result.Status == MandrillSendMessageResponseStatus.Sent
          || result.Status == MandrillSendMessageResponseStatus.Queued
          || result.Status == MandrillSendMessageResponseStatus.Scheduled;
      }
    }

    public static string GetTestRedirectionEmailAddr(string sourceEmailAddr, string integralEmailAddr) {
      // e.g. where integralEmail is "andrew@integral.global",
      // turns source "someone@somewhere.com" into "someone_somewhere.com-andrew@idtestmail.com"
      string idAccount;
      int idDomainPos = integralEmailAddr.IndexOf("@" + ConfigHelper.EmailTestRedirectionIDAccountDomain, StringComparison.InvariantCultureIgnoreCase);
      if (idDomainPos > 0)
        idAccount = integralEmailAddr.Substring(0, idDomainPos);
      else
        idAccount = "andrew"; // fallback

      return sourceEmailAddr.Replace("@", "_") + "-" + idAccount + "@" + ConfigHelper.EmailTestRedirectionDomain;
    }

    // Ensure email recipients conform to rules if not running on production, to avoid emailing clients.
    // 1. If a request-specific override address is specified, use that.
    // 2. Otherwise, if an override address is specified in app config, use that.
    // 3. If emailRecipientOption = SendToRealRecipient_ForTesting, ignore the above overrides.
    // 4. Finally, check that all recipients match the hardcoded regex tests in ConfigHelper.DevEmailRecipient_AllowedRegex[].
    //    Remove any recipients that do not match.
    private static void ValidateRecipients(List<MailAddress> recipientList, EmailRecipientOption emailRecipientOption = EmailRecipientOption.RespectRecipientOverride_Default) {

      if (recipientList.IsNullOrEmpty()) return;

      foreach (var recipient in recipientList) {
        if (!IsValidEmailFormat(recipient.Address)) throw new ArgumentException($"Recipient \"{recipient.Address}\" is not a valid email address.");
      }

      if (ConfigHelper.IsLiveServer) return;

      if (emailRecipientOption == EmailRecipientOption.RespectRecipientOverride_Default) {

        // Check for overrides - request-level takes priority over config.
        string recipientOverrideAddress = GetRecipientOverrideAddressForRequest().ValueIfNullOrEmpty(ConfigHelper.EmailRecipientOverrideAddress);

        // If override exists, replace all recipient addresses.
        if (!recipientOverrideAddress.IsNullOrEmpty()) {
          for (var i = 0; i < recipientList.Count; i++) {
            recipientList[i] = new MailAddress(recipientOverrideAddress, recipientList[i].DisplayName);
          }
        }
      }

      // Final check for valid email addresses, remove any that don't pass.
      recipientList.RemoveAll(recip => {
        foreach (string regex in ConfigHelper.DevEmailRecipient_AllowedRegexList) {
          if (recip.Address.RegexIsMatch(regex, RegexOptions.IgnoreCase)) return false; // Passed a check, keep this address.
        }
        return true; // Failed all checks, drop the address.
      });

      if (recipientList.IsNullOrEmpty()) throw new ArgumentException("No recipient addresses passed AllowedRegex tests.");
    }

    public static bool IsValidEmailFormat(string emailAddress) {
      if (emailAddress.IsNullOrEmpty()) return false;
      return emailAddress.RegexIsMatch(AppHelper.Regex.Email, RegexOptions.IgnoreCase);
    }

    public const string SupportEmailCSS = @"
      <style>
        body { font-family: roboto,arial,sans-serif; font-size: 14px; font-weight: 400; color: #111; line-height: 1.3; }
        table { border-collapse: collapse; }
        table, thead, tbody, tr, td { font: inherit; font-family: inherit; font-size: inherit; color: inherit; line-height: inherit; }
        td { padding: 5px; border: 2px solid #ddd; vertical-align: top; }
        pre,td { font-family: consolas,monospace; font-size: 13px; line-height: 1.3; font-weight: 400; }
        h3 { font-size: 11pt; }
      </style>";

    public static string GetSupportEmailDetailsHtml(Exception ex) {
      var html = new StringBuilder();
      if (ex != null) html.AppendLine($"<h3>Error: {ex.Message}</h3>");
      html.AppendLine("<table>");
      html.AppendLine($"<tr><td>Time:</td><td>{DateTime.UtcNow.UtcToTZ(ConfigHelper.DefaultTimeZoneInfo).ToString("dd/MM/yyyy hh:mm:ss")} {ConfigHelper.DefaultTimeZoneAbbrev}</td></tr>");
      html.AppendLine($"<tr><td>User:</td><td>{SessionHelper.GetUserEmailOrNull().HTMLEncode()}</td></tr>");
      // Migration: re-add these.
      //html.AppendLine($"<tr><td>URL:</td><td>{SystemWeb.RequestMethod}{(SystemWeb.IsAjaxPost ? " AJAX" : "")} {SystemWeb.RequestUrlLeftPart(UriPartial.Authority).HTMLEncode()}{SystemWeb.RequestRawUrl.HTMLEncode()}</td></tr>");
      //html.AppendLine($"<tr><td>Headers:</td><td>{SystemWeb.RequestHeadersAsString.URLDecode().Replace("&", "<br>")}</td></tr>");
      //html.AppendLine($"<tr><td>Form:</td><td>{SystemWeb.RequestFormAsString.URLDecode().HTMLEncode()}</td></tr>");
      html.AppendLine($"<tr><td>Body:</td><td>{WebHelper.GetRequestBody().HTMLEncode()}</td></tr>");
      html.AppendLine("</table>");
      if (ex != null) html.AppendLine($"<h3>Stack Trace:</h3>{LogHelper.GetStackTraceFormattedHtml(ex, true)}");
      html.AppendLine($"<h3>Latest SQL:</h3><pre>{LogHelper.GetLatestSqlQueryText()}</pre>");
      return html.ToString();
    }

    // Note internal support emails use SMTP, as they're infrequent and it's probably
    // more reliable in case something goes wrong communicating with Mandrill API.
    private static bool SendInternalEmail(
      Exception ex,
      string subject,
      string bodyHtml,
      bool includeSupportDetails,
      System.Collections.Specialized.NameValueCollection headers,
      params MailAddress[] toAddrs) {

      if (toAddrs == null || toAddrs.Length == 0) {
        toAddrs = new MailAddress[] { new MailAddress(ConfigHelper.InternalSupportRecipientEmailAddress) };
      }

      if (!IsEmailAllowed()) return true; // Test limit reached, but pretend it was sent.

      subject = WebHelper.GetDevOrStagingSiteTagText().EnsureEndsWith(" ", StringExt.Ensure.IfNotBlank) + "Able Support" + subject.EnsureStartsWith(": ", true);

      bodyHtml = SupportEmailCSS + bodyHtml;
      if (includeSupportDetails) bodyHtml += "<br/><br/>" + GetSupportEmailDetailsHtml(ex);

      var toAddrList = new List<MailAddress>(toAddrs);

      using (var mailMsg = new MailMessage {
        From = new MailAddress(ConfigHelper.InternalSupportFromEmailAddress),
        Subject = subject,
        Body = bodyHtml,
        IsBodyHtml = true
      }) {

        if (headers != null && headers.Count > 0) {
          // Add headers.
          mailMsg.Headers.Add(headers);
        }

        ValidateRecipients(toAddrList);
        if (toAddrList.IsNullOrEmpty()) throw new ArgumentException("No valid addresses in recipient list.");
        foreach (var to in toAddrList) mailMsg.To.Add(to);

        using (var kitClient = new MailKit.Net.Smtp.SmtpClient()) {
          var kitMessage = new MimeKit.MimeMessage();
          kitMessage.Headers.Add("X-MC-InlineCSS", "false");
          kitMessage.From.Add(new MimeKit.MailboxAddress(ConfigHelper.Email_Coordinator_FullName, ConfigHelper.Email_Coordinator_Address));
          foreach (var addr in mailMsg.To) {
            kitMessage.To.Add(new MimeKit.MailboxAddress(addr.DisplayName, addr.Address));
          }
          kitMessage.Subject = mailMsg.Subject;
          kitMessage.Body = new MimeKit.TextPart(MimeKit.Text.TextFormat.Html) {
            Text = mailMsg.Body
          };
          try {
            kitClient.Connect(ConfigHelper.SMTPServer, ConfigHelper.SMTPPort, ConfigHelper.SMTPUseSSL);
            kitClient.Authenticate(ConfigHelper.SMTPAuthUsername, ConfigHelper.SMTPAuthPassword);
            kitClient.Send(kitMessage);
            kitClient.Disconnect(true);
          } catch (Exception smtpEx) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(smtpEx)
              .WithOperation(nameof(SendInternalEmail))
              .WithOperationContext("SMTP")
              .WithProperty(ApplicationInsightsConstants.Subject, subject)
              .WithProperty(ApplicationInsightsConstants.ToAddressCount, (object)toAddrList?.Count)
              .WithProperty(ApplicationInsightsConstants.SMTPServer, ConfigHelper.SMTPServer)
              .Track();
            return false;
          }
        }
        return true;
      }
    }

    public static bool SendInternalEmail(string subject, string messageHtml, params MailAddress[] toAddrs) {
      try {
        return SendInternalEmail(null, subject, messageHtml, false, null, toAddrs);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation(nameof(SendInternalEmail))
          .WithProperty(ApplicationInsightsConstants.Subject, subject)
          .WithProperty(ApplicationInsightsConstants.ToAddressCount, (object)toAddrs?.Length)
          .Track();
        return false;
      }
    }

    public static bool SendInternalSupportEmail(string subject, string messageHtml, params MailAddress[] toAddrs) {
      try {
        return SendInternalEmail(null, subject, messageHtml, true, null, toAddrs);
      } catch (Exception ex) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(ex)
          .WithOperation(nameof(SendInternalSupportEmail))
          .WithProperty(ApplicationInsightsConstants.Subject, subject)
          .WithProperty(ApplicationInsightsConstants.ToAddressCount, (object)toAddrs?.Length)
          .Track();
        return false;
      }
    }

    public static bool SendInternalSupportEmail(Exception ex, string subject, string messageHtml = "", params MailAddress[] toAddrs) {
      try {
        return SendInternalEmail(ex, subject, messageHtml, true, null, toAddrs);
      } catch (Exception sendEx) {
        var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
        telemetry?.Exception(sendEx)
          .WithOperation(nameof(SendInternalSupportEmail))
          .WithOperationContext("WithException")
          .WithProperty(ApplicationInsightsConstants.Subject, subject)
          .WithProperty(ApplicationInsightsConstants.ToAddressCount, (object)toAddrs?.Length)
          .WithProperty(ApplicationInsightsConstants.OriginalException, ex?.Message)
          .Track();
        return false;
      }
    }

    public static void SetRecipientOverrideAddressForRequest(string recipName, string recipEmail) {
      AppHelper.SetRequestItem(ConfigHelper.RequestItems.Email_RecipientOverrideName, recipName);
      AppHelper.SetRequestItem(ConfigHelper.RequestItems.Email_RecipientOverrideAddress, recipEmail);
    }

    public static string GetRecipientOverrideAddress() {

      // TODO: Implement an override address for current session as well, but OverrideAddressForRequest will take priority if present.

      string recipientOverrideAddress = GetRecipientOverrideAddressForRequest().ValueIfNullOrEmpty(ConfigHelper.EmailRecipientOverrideAddress);
      if (!recipientOverrideAddress.IsEmailAddress()) {
        throw new ApplicationException($"Invalid recipient override email address: '{recipientOverrideAddress}'.");
      }
      return recipientOverrideAddress;
    }

    private static string GetRecipientOverrideAddressForRequest() {
      return AppHelper.GetRequestItemOrNull(ConfigHelper.RequestItems.Email_RecipientOverrideAddress) as string; // Just return the email addr.
    }

    public static void ClearRecipientOverrideForRequest() {
      AppHelper.RemoveRequestItem(ConfigHelper.RequestItems.Email_RecipientOverrideName);
      AppHelper.RemoveRequestItem(ConfigHelper.RequestItems.Email_RecipientOverrideAddress);
    }

    public static int? GetMaxEmailsToSendForRequest() {
      return ("" + AppHelper.GetRequestItemOrNull(ConfigHelper.RequestItems.Email_MaxToSend)).ToIntOrNull();
    }
    public static void SetMaxEmailsToSendForRequest(int maxToSend) {
      AppHelper.SetRequestItem(ConfigHelper.RequestItems.Email_MaxToSend, maxToSend);
    }

    public static int? GetTestEmailLimitForRequest() {
      if (!AppHelper.RequestItemExists(ConfigHelper.RequestItems.Email_MaxToSend)) return null;
      if (!int.TryParse(AppHelper.GetRequestItemOrNull(ConfigHelper.RequestItems.Email_MaxToSend).ToString(), out var maxToSend)) return null;
      return maxToSend;
    }

    public static void ClearMaxEmailsToSendForRequest() {
      AppHelper.RemoveRequestItem(ConfigHelper.RequestItems.Email_MaxToSend);
    }

    public static int GetEmailsSentThisRequest() {
      if (!AppHelper.RequestItemExists(ConfigHelper.RequestItems.Email_SentCount)) AppHelper.SetRequestItem(ConfigHelper.RequestItems.Email_SentCount, (int)0);
      return (int)AppHelper.GetRequestItemOrNull(ConfigHelper.RequestItems.Email_SentCount);
    }
    public static void SetEmailsSentThisRequest(int emailsSent) {
      AppHelper.SetRequestItem(ConfigHelper.RequestItems.Email_SentCount, emailsSent);
    }

    private static int IncrementEmailsSentThisRequest() {
      int emailsSent = GetEmailsSentThisRequest() + 1;
      AppHelper.SetRequestItem(ConfigHelper.RequestItems.Email_SentCount, emailsSent);
      return emailsSent;
    }

    public static void DisableMaxEmailsCheckForRequest() {
      AppHelper.SetRequestItem(ConfigHelper.RequestItems.Email_DisableMaxCheck, true);
    }
    public static void EnableMaxEmailsCheckForRequest() {
      AppHelper.SetRequestItem(ConfigHelper.RequestItems.Email_DisableMaxCheck, false);
    }
    public static bool IsMaxEmailsCheckForRequestDisabled() {
      if (AppHelper.RequestItemExists(ConfigHelper.RequestItems.Email_DisableMaxCheck))
        return (bool)AppHelper.GetRequestItemOrNull(ConfigHelper.RequestItems.Email_DisableMaxCheck);
      return false;
    }

    public static string EnsureHTMLLineBreaks(string textOrHTML) {

      // Ensure that all line breaks end in block html otherwise add a <br> on the end.
      // e.g.
      // "this is a line" gets a <br> on the end, but
      // "<p>this is a line</p>" does not.

      if (textOrHTML.IsNullOrEmpty()) return textOrHTML;
      return Regex.Replace(textOrHTML,
        @"^([^\r\n]*)(?<!</?(?:div|p|ul|ol|li|table|tr|th|td|tbody|tfoot|thead)>|<br ?/?>)(\r?\n)", "$1<br>$2",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
    }

  }
}
