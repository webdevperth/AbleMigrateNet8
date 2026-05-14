using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class CoachingSessionTypes {

      public static List<SessionType> SessionTypeList { get; private set; } = new List<SessionType>();
      private static Dictionary<string, SessionType> SessionTypeBySlug = new Dictionary<string, SessionType>();
      private static Dictionary<int, SessionType> SessionTypeById = new Dictionary<int, SessionType>();

      static CoachingSessionTypes() {

        // Load all sesison types into a lookup dictionaries.

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {

          string sql = @"

            SELECT CoachingSessionTypeId, SessionTypeDisplayName, SessionTypeCalendlyName, DurationMins, InPerson, CalendlyUrlSlug, IsGroup
            FROM al_CoachingSessionTypes
            ORDER BY CoachingSessionTypeId

          ";
          using (var cmd = new SqlCommand(sql, conn)) {
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                var st = new SessionType(
                  dr.GetInt("CoachingSessionTypeId"),
                  dr.GetString("SessionTypeDisplayName"),
                  dr.GetString("SessionTypeCalendlyName"),
                  dr.GetInt("DurationMins"),
                  dr.GetBoolFromInt("InPerson"),
                  dr.GetString("CalendlyUrlSlug"),
                  dr.GetBoolFromInt("IsGroup")
                );
                SessionTypeList.Add(st);
                SessionTypeById.Add(st.SessionTypeId, st);
                SessionTypeBySlug.Add(st.CalendlyUrlSlug.ToLower(), st);
              }
            }
          }
        }

      }

      public static SessionType GetSessionTypeByAttributes(bool inPerson, int durationMins) {
        foreach (var sessionType in SessionTypeList) {
          if (sessionType.InPerson == inPerson && sessionType.DurationMins == durationMins) return sessionType;
        }
        return null;
      }

      public static SessionType GetSessionTypeBySlug(string slug) {
        slug = slug.EmptyIfNull().ToLower();
        if (SessionTypeBySlug.ContainsKey(slug)) return SessionTypeBySlug[slug];
        else return null;
      }

      public static SessionType GetSessionTypeById(int sessionTypeId) {
        if (SessionTypeById.ContainsKey(sessionTypeId)) return SessionTypeById[sessionTypeId];
        else return null;
      }

      public class SessionType {
        public int SessionTypeId;
        public string DisplayName;
        public string CalendlyName;
        public int DurationMins;
        public bool InPerson;
        public string CalendlyUrlSlug;
        public bool IsGroup;
        public SessionType(
          int sessionTypeId,
          string displayName,
          string calendlyName,
          int durationMins,
          bool inPerson,
          string calendlyUrlSlug,
          bool isGroup
        ) {
          this.SessionTypeId = sessionTypeId;
          this.DisplayName = displayName;
          this.CalendlyName = calendlyName;
          this.DurationMins = durationMins;
          this.InPerson = inPerson;
          this.CalendlyUrlSlug = calendlyUrlSlug;
          this.IsGroup = isGroup;
        }
      }

      public class StatsForIntercom {
        public int? SessionsAllocated;
        public int TotalSessions;
        public int SessionsBooked;
        public int SessionsUpcoming;
        public DateTime? NextApptDateUTC;
      }

    }
  }
}
