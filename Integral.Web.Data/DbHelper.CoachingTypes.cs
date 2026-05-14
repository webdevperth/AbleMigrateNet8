using Integral.Integrations;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class AlbertCoachingTypes {


      private static List<CoachingTypeInfo> coachingTypeList;
      private static Dictionary<int, CoachingTypeInfo> coachingTypeById;
      private static List<string> intercomValueList;

      public static CoachingTypeInfo ReservedType_NoCoaching = null;
      public static CoachingTypeInfo ReservedType_Hybrid = null;

      static AlbertCoachingTypes() {


        RefreshCoachingTypeNames();

      }

      static void RefreshCoachingTypeNames() {

        // TODO: Thread safety lock in case we need to call this while running to refresh the list.

        coachingTypeList = new List<CoachingTypeInfo>();
        coachingTypeById = new Dictionary<int, CoachingTypeInfo>();
        intercomValueList = new List<string>();

        AddCoachingTypesFromDb();

      }

      static void AddCoachingTypesFromDb() {

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          string sql = @"

            SELECT
              ct.CoachingTypeId, ct.CoachingTypeName, ct.CoachingTypeIntercomValue, ct.UIHidden,
              cts.SessionNumber,
              st.CoachingSessionTypeId, st.SessionTypeDisplayName, st.SessionTypeCalendlyName, st.InPerson, st.DurationMins, st.CalendlyUrlSlug
            FROM al_CoachingTypes ct
            LEFT OUTER JOIN al_CoachingTypeSessions cts ON ct.CoachingTypeId = cts.CoachingTypeId
            LEFT OUTER JOIN al_CoachingSessionTypes st ON cts.CoachingSessionTypeId = st.CoachingSessionTypeId
            ORDER BY ct.ListSort, cts.SessionNumber";

          using (var cmd = new SqlCommand(sql, conn)) {
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                // Find existing or add new coaching type.
                int coachingTypeId = dr.GetInt("CoachingTypeId");
                CoachingTypeInfo ct = GetCoachingTypeByIdOrNull(coachingTypeId);
                if (ct == null) {
                  ct = new CoachingTypeInfo(coachingTypeId, dr.GetString("CoachingTypeIntercomValue"), dr.GetBoolFromInt("UIHidden"), dr.GetBoolFromInt("InPerson"));
                  coachingTypeList.Add(ct);
                  coachingTypeById.Add(coachingTypeId, ct);
                  intercomValueList.Add(ct.IntercomFieldValue);
                }
                if (dr.GetIntOrDefault("DurationMins", 0) == 0) {
                  // A coaching type with a session duration of null/zero is the "No Coaching" type.
                  if (ReservedType_NoCoaching == null) ReservedType_NoCoaching = ct; // Remember the "No Coaching" type.
                  else if (ReservedType_NoCoaching.CoachingTypeId != ct.CoachingTypeId) throw new ApplicationException("There is more than one CoachingTypeSession with zero duration."); // There can be only one.
                } else {
                  // Add session info to the coaching type.
                  // Note a unique index prevents two SessionNumbers being the same for a coaching type.
                  int sessionNumber = dr.GetInt("SessionNumber");
                  bool inPerson = dr.GetBoolFromInt("InPerson");
                  int durationMins = dr.GetInt("DurationMins");
                  if (sessionNumber < 1) throw new ApplicationException("Session Number must be greater than zero.");
                  if (durationMins < 1) throw new ApplicationException("DurationMins must be greater than zero.");
                  if (ct.SessionTypes.Count == 0 && sessionNumber != 1) throw new ApplicationException("First Session number must be 1.");
                  ct.AddSessionType(sessionNumber,
                    dr.GetInt("CoachingSessionTypeId"), dr.GetString("SessionTypeDisplayName"), dr.GetString("SessionTypeCalendlyName"),
                    durationMins, inPerson, dr.GetString("CalendlyUrlSlug"));
                }
                if (ct.IntercomFieldValue.Contains("Hybrid")) {
                  if (ReservedType_Hybrid == null) ReservedType_Hybrid = ct;
                  else if (ReservedType_Hybrid.CoachingTypeId != ct.CoachingTypeId) throw new ApplicationException("There is more than one CoachingType containing string 'Hybrid'.");
                }
              }
            }
          }
          if (ReservedType_NoCoaching == null) throw new ApplicationException("CoachingType 'No Coaching' (i.e. no sessions) not found.");
          if (ReservedType_Hybrid == null) throw new ApplicationException("CoachingType 'Hybrid' not found.");
        }
      }

      public static List<CoachingTypeInfo> GetCoachingTypeList() {
        return coachingTypeList;
      }

      public static List<string> GetIntercomValueList() {
        return intercomValueList;
      }

      public static CoachingTypeInfo GetType_NoCoaching() {
        return ReservedType_NoCoaching;
      }

      public static string GetIntercomValue_NoCoaching() {
        return ReservedType_NoCoaching.IntercomFieldValue;
      }

      public static string GetIntercomValue_Hybrid() {
        return ReservedType_Hybrid.IntercomFieldValue;
      }

      public static CoachingTypeInfo GetCoachingTypeByIdOrNull(int? coachingTypeId) {
        if (coachingTypeId == null || !coachingTypeById.ContainsKey((int)coachingTypeId)) return null;
        return coachingTypeById[(int)coachingTypeId];
      }
      public static CoachingTypeInfo GetCoachingTypeById(int coachingTypeId) {
        var ct = GetCoachingTypeByIdOrNull(coachingTypeId);
        if (ct == null) throw new ArgumentException("Invalid CoachingTypeId: " + coachingTypeId);
        return ct;
      }

      public static CoachingTypeInfo GetCoachingTypeByIntercomValueOrNull(string intercomFieldValue) {
        if (!intercomFieldValue.IsNullOrEmpty()) {
          foreach (var ct in coachingTypeList) {
            if (intercomFieldValue.Equals(ct.IntercomFieldValue, StringComparison.InvariantCultureIgnoreCase)) return ct;
          }
        }
        return null;
      }
      public static CoachingTypeInfo GetCoachingTypeByIntercomValue(string intercomFieldValue) {
        var ct = GetCoachingTypeByIntercomValueOrNull(intercomFieldValue);
        if (ct == null) throw new ArgumentException("Invalid CoachingType value: '" + intercomFieldValue + "'");
        return ct;
      }

      public static string GetIntercomValueOrNull(int? coachingTypeId) {
        if (coachingTypeId == null) return null;
        if (!coachingTypeById.ContainsKey((int)coachingTypeId)) throw new ArgumentException("coachingTypeId not found: " + coachingTypeById);
        return coachingTypeById[(int)coachingTypeId].IntercomFieldValue;
      }

      // Each coaching type contains one or more session types.
      // Session types define the attributes of each session - in person or not, and duration in minutes.
      // If a coachee has more sessions than the number of sessions defined in a coaching type,
      // the properties of the last session type is carried forward. e.g. if there are 2 sessions types,
      // and the coachee is on session #3, then the attributes of session type 2 is used.
      // In the database, if there is a gap between session numbers, the gap is filled in
      // by repeating the lower session type to fill in the gap. e.g. if the db contains rows
      // for session numbers { 1, 2, 6, 7 } then we repeat the attributes of session #2 to get 3, 4 and 5.
      public class CoachingTypeInfo {

        public int CoachingTypeId { get; private set; }
        public bool UIHidden { get; private set; }
        public string IntercomFieldValue { get; private set; }
        public bool SessionTypeInPerson { get; private set; }
        public List<SessionTypeInfo> SessionTypes { get; private set; }

        public CoachingTypeInfo(
          int coachingTypeId,
          string intercomFieldValue,
          bool uiHidden,
          bool sessionTypeInPerson
        ) {
          this.CoachingTypeId = coachingTypeId;
          this.IntercomFieldValue = intercomFieldValue;
          this.UIHidden = uiHidden;
          this.SessionTypeInPerson = sessionTypeInPerson;
          SessionTypes = new List<SessionTypeInfo>(); // The item numbers (0, ...) correspond to the session numbers (1, ...) in the db.
        }

        public bool IsNoCoaching {
          get {
            return SessionTypes == null || SessionTypes.Count == 0;
          }
        }

        internal void AddSessionType(int sessionNumber,
          int coachingSessionTypeId, string sessionTypeDisplayName, string sessionTypeCalendlyName,
          int durationMins, bool inPerson, string calendlyUrlSlug) {

          // Given session number must be 1+, which corresponds to SessionType list index 0+.
          if (sessionNumber < 1) throw new ArgumentException("sessionNumber must be greater than zero.");
          if (durationMins < 1) throw new ArgumentException("durationMins must be greater than zero.");

          var newSessionType = new SessionTypeInfo(coachingSessionTypeId, sessionTypeDisplayName, sessionTypeCalendlyName, durationMins, inPerson, calendlyUrlSlug);
          int sessionTypeIndexToAdd = sessionNumber - 1;
          if (SessionTypes.Count == 0) SessionTypes.Add(newSessionType);
          if (sessionNumber <= SessionTypes.Count) {
            // Replace an element
            SessionTypes[sessionNumber - 1] = newSessionType;
          } else {
            if (sessionNumber > SessionTypes.Count + 1) {
              // Fill in elements up to session number - 1
              var sessionTypeToFill = SessionTypes[SessionTypes.Count - 1];
              while (SessionTypes.Count < sessionNumber) SessionTypes.Add(sessionTypeToFill); // SessionTypes.Add(new SessionTypeInfo(sessionTypeToFill)) ?
            }
            SessionTypes.Add(newSessionType);
          }
        }

        public SessionTypeInfo GetSessionType(int sessionNumber) {
          if (sessionNumber < 1) throw new ArgumentException("sessionNumber must be > 0");
          if (SessionTypes.Count == 0) return null;
          if (sessionNumber > SessionTypes.Count) return SessionTypes[SessionTypes.Count - 1];
          return SessionTypes[sessionNumber - 1];
        }

        // Return a list of session durations in minutes for the defined number of sessions.
        public List<int> GetDurations() {
          if (SessionTypes == null) return new List<int>();
          return GetDurations(SessionTypes.Count);
        }

        // Return a list of session durations in minutes for an arbitrary number of sessions.
        public List<int> GetDurations(int sessionCount) {
          var durations = new List<int>();
          for (int i = 1; i <= sessionCount; i++) durations.Add(GetSessionType(i).DurationMins);
          return durations;
        }

        public List<string> GetEventNames(int sessionCount) {
          var eventNames = new List<string>();
          for (int i = 1; i <= sessionCount; i++) {
            var sessionType = GetSessionType(i);
            eventNames.Add(Calendly.GetBookingUrlEventName(sessionType.InPerson, sessionType.DurationMins));
          }
          return eventNames;
        }
      }

      public class SessionTypeInfo {
        public int CoachingSessionTypeId { get; private set; }
        public string DisplayName { get; private set; }
        public bool InPerson { get; private set; }
        public int DurationMins { get; private set; }
        public string CalendlyTypeName { get; private set; }
        public string CalendlyUrlSlug { get; private set; }
        public SessionTypeInfo(int coachingSessionTypeId, string displayName, string calendlyTypeName, int durationMins, bool inPerson, string calendlyUrlSlug) {
          this.CoachingSessionTypeId = coachingSessionTypeId;
          this.DisplayName = displayName;
          this.DurationMins = durationMins;
          this.InPerson = inPerson;
          this.CalendlyTypeName = calendlyTypeName;
          this.CalendlyUrlSlug = calendlyUrlSlug;
        }
      }

    }
  }
}

