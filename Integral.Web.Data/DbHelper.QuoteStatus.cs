using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class AbleQuoteStatus {


      // These apptags must be assigned in the db, one to each row.
      public enum AppTagEnum {
        draft = 1,
        accepted = 3,
        lost = 4,
        client = 10
      }

      public static List<QuoteStatusInfo> StatusList { get; private set; } = new List<QuoteStatusInfo>();
      private static Dictionary<int, QuoteStatusInfo> _statusById = new Dictionary<int, QuoteStatusInfo>();
      private static Dictionary<AppTagEnum, QuoteStatusInfo> _statusByAppTag = new Dictionary<AppTagEnum, QuoteStatusInfo>();

      static AbleQuoteStatus() {


        RefreshQuoteStatusNames();

      }

      static void RefreshQuoteStatusNames() {

        // TODO: Thread safety lock in case we need to call this while running to refresh the list.

        StatusList.Clear();
        _statusById.Clear();
        _statusByAppTag.Clear();

        AddQuoteStatusFromDb();

      }

      static void AddQuoteStatusFromDb() {

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          string sql = @"

          SELECT QuoteStatusId, QuoteStatusText, AppTag
          FROM al_QuoteStatus
          ORDER BY DisplaySort, QuoteStatusText";

          using (var cmd = new SqlCommand(sql, conn)) {
            cmd.Parameters.AddInt("@id", 0);
            conn.Open();
            using (SqlDataReader dr = cmd.ExecuteReader()) {
              while (dr.Read()) {
                int quoteStatusId = dr.GetInt("QuoteStatusId");
                string quoteStatusText = dr.GetString("QuoteStatusText");
                string appTag = dr.GetString("AppTag");
                if (!Enum.TryParse(appTag, out AppTagEnum appTagEnum)) {
                  throw new Exception($"Invalid AppTag in table: '{appTag}'");
                }
                var quoteStatus = new QuoteStatusInfo(quoteStatusId, quoteStatusText);
                StatusList.Add(quoteStatus);
                _statusById.Add(quoteStatusId, quoteStatus);
                if (_statusByAppTag.ContainsKey(appTagEnum)) throw new Exception($"Duplicate AppTag '{appTag}'");
                _statusByAppTag.Add(appTagEnum, quoteStatus);
              }
            }
          }
          if (StatusList.Count != Enum.GetValues(typeof(AppTagEnum)).Length) {
            throw new Exception("One or more AppTags are missing. Required AppTags: " + string.Join(", ", Enum.GetNames(typeof(AppTagEnum))));
          }
        }
      }

      public static QuoteStatusInfo GetStatusByIdOrNull(int? statusId) {
        if (statusId == null || !_statusById.ContainsKey((int)statusId)) return null;
        return _statusById[(int)statusId];
      }
      public static QuoteStatusInfo GetStatus(AppTagEnum appTag) {
        return _statusByAppTag[appTag];
      }

      public class QuoteStatusInfo {
        public int QuoteStatusId { get; private set; }
        public string QuoteStatusText { get; private set; }
        public QuoteStatusInfo(int quoteStatusId, string quoteStatusText) {
          this.QuoteStatusId = quoteStatusId;
          this.QuoteStatusText = quoteStatusText;
        }
      }

    }
  }
}

