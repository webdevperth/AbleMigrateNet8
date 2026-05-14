using System;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class UserLoginSession {

      public static SessionInfo GetSession(Guid loginSessionGuid) {

        SessionInfo sessionInfo = null;

        Common.Query(@"
          SELECT uls.CreatedUtc, uls.LastRequestUtc, uls.UserId, uls.SettingsJson
          FROM al_UserLoginSession uls
          WHERE uls.LoginSessionGuid = @LoginSessionGuid",
          dr => {
            sessionInfo = new SessionInfo(
              loginSessionGuid,
              dr.GetDateTime("CreatedUtc"),
              dr.GetDateTime("LastRequestUtc"),
              dr.GetIntOrNull("UserId"),
              dr.GetString("SettingsJson")
            );
          },
          Common.NewSqlParameter("LoginSessionGuid", loginSessionGuid)
        );

        return sessionInfo;
      }

      public static SessionInfo GetNewSessionInfo(string settingsJson) {
        return new SessionInfo(Guid.Empty, DateTime.UtcNow, DateTime.UtcNow, null, settingsJson);
      }

      // Note this has to do both create and update atomically, so that concurrent attempts
      // by the same user/browser session don't accidentally create multiple rows for the same session.
      public static void CreateSession(SessionInfo sessionInfo) {

        if (sessionInfo == null) throw new ArgumentException(nameof(sessionInfo), "sessionInfo is required.");
        if (sessionInfo.SettingsJson.IsNullOrEmpty()) throw new ArgumentException(nameof(sessionInfo.SettingsJson), "settingsJson is required.");

        var now = DateTime.UtcNow;

        // Find session by guid. Returns the found guid or a new one.
        // A new one will be created if the given guid is not found or it is Guid.Empty.
        sessionInfo.SessionGuid = (Guid)Common.GetScalarQuery(@"
          INSERT INTO al_UserLoginSession (UserId, SettingsJson, CreatedUtc, LastRequestUtc)
          OUTPUT INSERTED.LoginSessionGuid
          VALUES (@UserId, @SettingsJson, @CreatedUtc, @LastRequestUtc);",
          Common.NewSqlParameter("CreatedUtc", now),
          Common.NewSqlParameter("LastRequestUtc", now),
          Common.NewSqlParameter("UserId", sessionInfo.LoggedInUserId),
          Common.NewSqlParameter("SettingsJson", sessionInfo.SettingsJson)
        );
      }

      public static void UpdateLoggedInUserId(Guid loginSessionGuid, int? userId) {
        if (loginSessionGuid == Guid.Empty) return;
        Common.GetNonQueryInt($@"
          UPDATE al_UserLoginSession
             SET UserId = @UserId
          WHERE LoginSessionGuid = @LoginSessionGuid",
          Common.NewSqlParameter("LoginSessionGuid", loginSessionGuid),
          Common.NewSqlParameter("UserId", userId)
        );
      }

      public static void UpdateSettingsJson(Guid loginSessionGuid, string settingsJson) {
        if (loginSessionGuid == Guid.Empty) return;
        Common.GetNonQueryInt(@"
          UPDATE al_UserLoginSession
          SET SettingsJson = @SettingsJson
          WHERE LoginSessionGuid = @LoginSessionGuid",
          Common.NewSqlParameter("LoginSessionGuid", loginSessionGuid),
          Common.NewSqlParameter("SettingsJson", settingsJson)
        );
      }

      public static void UpdateLastRequestUtc(SessionInfo sessionInfo) {
        if (sessionInfo == null || sessionInfo.SessionGuid == Guid.Empty) return;
        Common.GetNonQueryInt($@"
          UPDATE al_UserLoginSession
             SET LastRequestUtc = GETUTCDATE()
          WHERE LoginSessionGuid = @LoginSessionGuid",
          Common.NewSqlParameter("LoginSessionGuid", sessionInfo.SessionGuid)
        );
      }

      // Purge expired and unused sessions from the al_UserLoginSession table.
      // Note different # days expiry for logged in sessions vs anonymous sessions.
      public static int DeleteOldSessions(int anonymousSessionTimeoutDays, int loginSessionTimeoutDays) {
        return Common.GetNonQueryInt(@"
          DELETE FROM al_UserLoginSession
          WHERE UserId IS NOT NULL AND LastRequestUtc < @LoginTimeoutUtc
             OR UserId IS NULL AND LastRequestUtc < @AnonymousTimeoutUtc",
          Common.NewSqlParameter("AnonymousTimeoutUtc", DateTime.UtcNow.AddDays(-anonymousSessionTimeoutDays)),
          Common.NewSqlParameter("LoginTimeoutUtc", DateTime.UtcNow.AddDays(-loginSessionTimeoutDays))
        );
      }

      public class SessionInfo {

        public Guid SessionGuid { get; internal set; }
        public DateTime CreatedUtc { get; private set; }
        public DateTime LastRequestUtc { get; internal set; }
        public int? LoggedInUserId { get; private set; }
        public string SettingsJson { get; private set; }

        public SessionInfo(Guid sessionGuid, DateTime createdUtc, DateTime lastRequestUtc, int? loggedInUserId, string settingsJson) {
          SessionGuid = sessionGuid;
          CreatedUtc = createdUtc;
          LastRequestUtc = lastRequestUtc;
          LoggedInUserId = loggedInUserId;
          SettingsJson = settingsJson;
        }
      }


    }
  }
}

