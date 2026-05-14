using System;
using System.Collections.Generic;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class AIMessage {

      private const int TopLatestMessages = 30;

      public static Guid InsertMessage(int userId, string message, bool isFromAI) {

        if (userId == 0) {
          throw new ArgumentNullException("userId is required.");
        } else if (message.IsNullOrEmptyOrWhitespace()) {
          throw new ArgumentNullException("message text is required.");
        }

        return (Guid)Common.GetScalarQuery(@"
            INSERT INTO al_AIMessage (SentUtc, UserId, IsFromAI, MessageBody)
            OUTPUT INSERTED.AIMessageGuid
            VALUES (@SentUtc, @UserId, @IsFromAI, @MessageBody)",
            Common.NewSqlParameter("SentUtc", DateTime.UtcNow),
            Common.NewSqlParameter("UserId", userId),
            Common.NewSqlParameter("IsFromAI", isFromAI),
            Common.NewSqlParameter("MessageBody", message));
      }

      public static List<AIMessageInfo> GetUserLatestAIMessages(int userId) {
        var msgList = new List<AIMessageInfo>();
        Common.Query($@"
          SELECT TOP {TopLatestMessages}
            ai.SentUtc, ai.IsFromAI, ai.MessageBody, ai.AIMessageGuid
          FROM al_AIMessage ai
          WHERE UserId = @UserId
          ORDER BY ai.SentUtc DESC",
          dr => {
            var cmpInfo = new AIMessageInfo(
              dr.GetDateTime("SentUtc"),
              dr.GetBoolFromInt("IsFromAI"),
              dr.GetString("MessageBody"),
              dr.GetGuid("AIMessageGuid")
            );
            msgList.Add(cmpInfo);
          },
          Common.NewSqlParameter("@UserId", userId)
        );
        return msgList;
      }

      public class AIMessageInfo {

        public DateTime SentUtc { get; private set; }
        public bool IsFromAI { get; private set; }
        public string MessageBodyText { get; private set; }
        public Guid AIMessageGuid { get; private set; }

        public AIMessageInfo(
          DateTime sentUtc,
          bool isFromAI,
          string messageBodyText,
          Guid AIMessageGuid
        ) {
          this.SentUtc = sentUtc;
          this.IsFromAI = isFromAI;
          this.MessageBodyText = messageBodyText;
          this.AIMessageGuid = AIMessageGuid;
        }
      }
    }
  }
}
