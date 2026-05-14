using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class EmailHistory {

      private static EmailHistoryList GetEmailHistoryList(
        int? topOrNullForAll,
        string sqlExtraJoins,
        string sqlWhereConditions,
        string sqlOrderBy,
        int? offsetRows = null,
        int? fetchRows = null,
        params SqlParameter[] sqlWhereParams) {

        if (topOrNullForAll != null && (fetchRows != null || offsetRows != null)) throw new ArgumentException("Must specify only Top or Fetch, not both.");
        if (fetchRows != null && offsetRows == null) throw new ArgumentException("Fetch provided without Offset.");
        if (offsetRows != null && fetchRows == null) throw new ArgumentException("Offset provided without Fetch.");

        var emailHistoryList = new EmailHistoryList(offsetRows, fetchRows);

        string sqlTop = topOrNullForAll == null ? "" : ($"TOP {topOrNullForAll}");
        string sqlFetch = fetchRows == null ? "" : ($" OFFSET {offsetRows} ROWS FETCH NEXT {fetchRows} ROWS ONLY");
        string sql = $@"
          SELECT {sqlTop}
            eh.EmailHistoryId, eh.SentUtc, eh.RecipientCoacheeId, eh.ProgramJobId,
            eh.IsProgramEmail, eh.MailChimpMessageID, eh.ContentFileGUID, eh.ContentFileCreatedUtc,
            eh.AddrFrom, eh.AddrTo, eh.Subject, eh.AttachedFileNames
          FROM al_EmailHistory eh
          {sqlExtraJoins.EmptyIfNull()}
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true).EmptyIfNull()}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true).EmptyIfNull()}
          {sqlFetch}";

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddRange(sqlWhereParams);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                emailHistoryList.Items.Add(new EmailHistoryInfo(
                  dr.GetLong("EmailHistoryId"),
                  dr.GetDateTime("SentUtc"),
                  dr.GetIntOrNull("RecipientCoacheeId"),
                  dr.GetIntOrNull("ProgramJobId"),
                  dr.GetBoolFromInt("IsProgramEmail"),
                  dr.GetString("MailChimpMessageID"),
                  dr.GetGuid("ContentFileGUID"),
                  dr.GetDateTimeOrNull("ContentFileCreatedUtc"),
                  dr.GetString("AddrFrom"),
                  dr.GetString("AddrTo"),
                  dr.GetString("Subject"),
                  dr.GetString("AttachedFileNames")
                ));
              }
            }
          }
        }
        return emailHistoryList;
      }

      public static EmailHistoryList GetEmailHistoryForCoachee(int coacheeId) {
        return GetEmailHistoryList(null, "",
          "RecipientCoacheeId = @CoacheeId",
          "SentUtc DESC",
          null, null,
          Common.NewSqlParameter("@CoacheeId", coacheeId));
      }

      // Add email history record and return the content GUID assigned.
      // Another process runs periodically to obtain email content from MailChimp
      // (using MailChimpMessageID) and stores the content in files.
      public static Guid AddEmail(SqlTransaction trans, int coacheeId, int programId, bool isProgramEmail, string mailChimpMessageId, string addrFrom, string addrTo, string subject, List<string> fileNames) {

        return (Guid)Common.GetScalarQuery(trans,
          @"INSERT INTO al_EmailHistory (RecipientCoacheeId, ProgramJobId, IsProgramEmail, MailChimpMessageID, AddrFrom, AddrTo, Subject, AttachedFileNames)
          OUTPUT INSERTED.ContentFileGUID
          VALUES (@RecipientCoacheeId, @ProgramJobId, @IsProgramEmail, @MailChimpMessageID, @AddrFrom, @AddrTo, @Subject, @AttachedFileNames)",
          Common.NewSqlParameter("@RecipientCoacheeId", coacheeId),
          Common.NewSqlParameter("@ProgramJobId", programId),
          Common.NewSqlParameter("@IsProgramEmail", isProgramEmail),
          Common.NewSqlParameter("@MailChimpMessageID", mailChimpMessageId.LimitLengthTo(300)),
          Common.NewSqlParameter("@AddrFrom", addrFrom.LimitLengthTo(100)),
          Common.NewSqlParameter("@AddrTo", addrTo.LimitLengthTo(100)),
          Common.NewSqlParameter("@Subject", subject.LimitLengthTo(300)),
          Common.NewSqlParameter("@AttachedFileNames", (fileNames == null || fileNames.Count == 0 ? "" : fileNames.ToArray().Join(", ")).LimitLengthTo(500))
        );
      }

      public class EmailHistoryList {
        public int? OffsetRows { get; internal set; }
        public int? FetchRows { get; internal set; }
        public int TotalRows { get; internal set; }
        public string sql { get; internal set; }
        public List<EmailHistoryInfo> Items { get; internal set; }
        public EmailHistoryList(int? offsetRows, int? fetchRows) {
          OffsetRows = offsetRows;
          FetchRows = fetchRows;
          TotalRows = 0;
          Items = new List<EmailHistoryInfo>();
        }
      }

      public class EmailHistoryInfo {

        public long EmailHistoryId { get; private set; }
        public DateTime SentUtc { get; private set; }
        public int? RecipientCoacheeId { get; private set; }
        public int? ProgramJobId { get; private set; }
        public bool IsProgramEmail { get; private set; }
        public string MailChimpMessageID { get; private set; }
        public Guid ContentFileGUID { get; private set; }
        public DateTime? ContentFileCreatedUtc { get; private set; }
        public string AddrFrom { get; private set; }
        public string AddrTo { get; private set; }
        public string Subject { get; private set; }
        public string AttachedFileNames { get; private set; }

        public EmailHistoryInfo(
          long emailHistoryId,
          DateTime sentUtc,
          int? recipientCoacheeId,
          int? programJobId,
          bool isProgramEmail,
          string mailChimpMessageId,
          Guid contentFileGUID,
          DateTime? contentFileCreatedUtc,
          string addrFrom,
          string addrTo,
          string subject,
          string attachedFileNames
        ) {
          this.EmailHistoryId = emailHistoryId;
          this.SentUtc = sentUtc;
          this.RecipientCoacheeId = recipientCoacheeId;
          this.ProgramJobId = programJobId;
          this.IsProgramEmail = isProgramEmail;
          this.MailChimpMessageID = mailChimpMessageId;
          this.ContentFileGUID = contentFileGUID;
          this.ContentFileCreatedUtc = contentFileCreatedUtc;
          this.AddrFrom = addrFrom;
          this.AddrTo = addrTo;
          this.Subject = subject;
          this.AttachedFileNames = attachedFileNames;
        }
      }

    }
  }
}

