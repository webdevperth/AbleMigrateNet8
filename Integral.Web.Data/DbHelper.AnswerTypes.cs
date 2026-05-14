using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Integral.Web.Services;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class AnswerTypes {

      public class InputTypes {
        // List of supported input types.

        #region This code rarely changes.
        public readonly Ids Id;
        public readonly string Value;
        private static Dictionary<string, InputTypes> RefByValue = new Dictionary<string, InputTypes>();
        private InputTypes(Ids id, String value) {
          this.Id = id;
          this.Value = value;
          RefByValue.Add(value, this);
        }
        public override String ToString() {
          return Value;
        }
        public static Ids? GetId(string value) {
          if (!value.IsNullOrEmpty() && RefByValue.ContainsKey(value)) return RefByValue[value].Id;
          return null;
        }
        #endregion

        public enum Ids {
          // Unique enums for the string values below. These "id"s can be used in code in switch statements.
          // However do NOT store the ids, they are not permanent references. Only store the string values below.
          Dropdown = 1,
          OptionsSingle = 2,
          OptionsMultiple = 3,
          Scale = 4,
          TextSingleLine = 5,
          TextMultiline = 6,
          Ranked = 7
        }
        public static readonly InputTypes DROPDOWN = new InputTypes(Ids.Dropdown, "d");
        public static readonly InputTypes OPTIONS_SINGLE = new InputTypes(Ids.OptionsSingle, "o");
        public static readonly InputTypes OPTIONS_MULTIPLE = new InputTypes(Ids.OptionsMultiple, "mul");
        public static readonly InputTypes SCALE = new InputTypes(Ids.Scale, "scale");
        public static readonly InputTypes TEXT_SINGLELINE = new InputTypes(Ids.TextSingleLine, "t");
        public static readonly InputTypes TEXT_MULTILINE = new InputTypes(Ids.TextMultiline, "a");
        public static readonly InputTypes RANKED = new InputTypes(Ids.Ranked, "ranked");
      }

      public static class ReservedDescriptors {
        public const string Date = "Date";
        public const string Person = "Person";
        public const string Feedback = "Feedback";
        public const string Assessedlevel = "Assessedlevel";
        public const string OpenEnded = "Open ended";
      }

      public static class ReservedGblAnsTypeIds {
        public const int DateCodes = 2812;
      }

      public static string NonCopyDescriptorsForSQL = "'" +
        ReservedDescriptors.Date + "','" +
        ReservedDescriptors.Person + "','" +
        ReservedDescriptors.Feedback + "','" +
        ReservedDescriptors.Assessedlevel + "'";

      public static List<string> ReservedDescrList = new List<string>();
      public static string ReservedDescrForSQL = "";
      public static Dictionary<string, string> ReservedDescriptorTable = GetReservedDescriptorTable();

      private static Dictionary<string, string> GetReservedDescriptorTable() {
        var dic = new Dictionary<string, string>();
        foreach (var field in typeof(ReservedDescriptors).GetFields()) {
          string fieldName = field.Name;
          string fieldValue = field.GetRawConstantValue().ToString();
          ReservedDescrList.Add(fieldValue);
          dic.Add(fieldName, fieldValue);
        }
        ReservedDescrForSQL = "'" + string.Join("','", ReservedDescrList) + "'";
        return dic;
      }

      public static int AddAnswerTypeBasic(SqlConnection conn, SqlTransaction trans,
        int SurveyId, int? GblAnswerTypeId, string AnswerTypeDescr, InputTypes InputType, bool IsScale, bool IsDemographic) {

        int newAnswerTypeId = 0;

        using (var cmd = new SqlCommand(@"
        INSERT INTO sv_360_AnswerTypes
        (SurveyId,GblAnswerTypeId,AnswerType,AnswerTypeDescr,InputType,IsScale,IsDemographic)
        OUTPUT INSERTED.AnswerTypeId VALUES
        (@SurveyId,@GblAnswerTypeId,@AnswerType,@AnswerTypeDescr,@InputType,@IsScale,@IsDemographic)
        ", conn, trans)) {
          cmd.Parameters.Add("@SurveyId", SqlDbType.Int).Value = SurveyId;
          cmd.Parameters.Add("@GblAnswerTypeId", SqlDbType.Int).Value = GblAnswerTypeId.OrDBNull();
          cmd.Parameters.Add("@AnswerType", SqlDbType.Int).Value = 0;
          cmd.Parameters.Add("@AnswerTypeDescr", SqlDbType.VarChar, 50).Value = AnswerTypeDescr.LimitLengthTo(50).EmptyIfNull();
          cmd.Parameters.Add("@InputType", SqlDbType.VarChar, 20).Value = InputType.Value;
          cmd.Parameters.Add("@IsScale", SqlDbType.TinyInt).Value = IsScale ? 1 : 0;
          cmd.Parameters.Add("@IsDemographic", SqlDbType.TinyInt).Value = IsDemographic ? 1 : 0;
          newAnswerTypeId = (int)cmd.ExecuteScalar();
        }
        return newAnswerTypeId;
      }

      public class IntakeInfo {
        public int CodeId { get; private set; }
        public int Code { get; private set; }
        public string IntakeTitle { get; private set; }
        public DateTime? IntakeCloseDateUTC_Self { get; private set; }
        public DateTime? IntakeCloseDateUTC_Rater { get; private set; }
        public DateTime? ScheduledStartDateUtc { get; private set; }
        public IntakeInfo(
          int codeId,
          int code,
          string intakeTitle,
          DateTime? intakeCloseDateUTC_Self,
          DateTime? IntakeCloseDateUTC_Rater,
          DateTime? scheduledStartDateUtc
        ) {
          this.CodeId = codeId;
          this.Code = code;
          this.IntakeTitle = intakeTitle;
          this.IntakeCloseDateUTC_Self = intakeCloseDateUTC_Self;
          this.IntakeCloseDateUTC_Rater = IntakeCloseDateUTC_Rater;
          this.ScheduledStartDateUtc = scheduledStartDateUtc;
        }
      }

      public static IntakeInfo GetIntake(SqlTransaction trans, int surveyId, int dateCodeNumber) {

        IntakeInfo result = null;

        Common.Query(trans, @"
          SELECT dc.CodeId, dc.Code, dc.Value, dc.IntakeCloseDate, dc.IntakeCloseDateSelf, dc.ScheduledStartDateUtc
          FROM sv_360_AnswerTypes dt
          INNER JOIN sv_360_Codes dc ON dt.AnswerTypeId = dc.AnswerTypeId
          WHERE dt.SurveyId = @SurveyId
            AND dt.AnswerTypeDescr = 'date'
            AND dc.Code = @DateCodeNumber",
          dr => {
            result = new AnswerTypes.IntakeInfo(
              dr.GetInt("CodeId"),
              dr.GetInt("Code"),
              dr.GetString("Value"),
              dr.GetDateTimeOrNull("IntakeCloseDate"),
              dr.GetDateTimeOrNull("IntakeCloseDateSelf"),
              dr.GetDateTimeOrNull("ScheduledStartDateUtc")
            );
          },
          Common.NewSqlParameter("@SurveyId", surveyId),
          Common.NewSqlParameter("@DateCodeNumber", dateCodeNumber)
        );

        return result;
      }

      public static IntakeInfo AddDateCode(
        int surveyId,
        string intakeTitle,
        DateTime intakeCloseDateLocalTime_Self,
        DateTime intakeCloseDateLocalTime_Rater,
        DateTime? scheduledStartDateUtc) {

        using (var conn = new SqlConnection(ConfigHelper.IntegralDbConnectionString)) {
          conn.Open();
          return AddDateCode(conn, null, surveyId, intakeTitle, intakeCloseDateLocalTime_Self, intakeCloseDateLocalTime_Rater, scheduledStartDateUtc);
        }
      }

      // Note: Connection conn must be *open* before calling.
      public static IntakeInfo AddDateCode(SqlConnection conn, SqlTransaction trans,
        int SurveyId,
        string IntakeTitle,
        DateTime IntakeCloseDateLocalTime_Self,
        DateTime IntakeCloseDateLocalTime_Rater,
        DateTime? scheduledStartDateUtc) {

        int newCodeId = 0;
        int newCode = 0;

        string sql = @"

        DECLARE @AnswerTypeId INT = (
          SELECT AnswerTypeId
          FROM sv_360_AnswerTypes
          WHERE SurveyId = @SurveyId AND AnswerTypeDescr = 'date');
        INSERT INTO sv_360_Codes
          (
            AnswerTypeId,
            Code, Value,
            IntakeCloseDate, IntakeCloseDateSelf, ScheduledStartDateUtc
          )
          OUTPUT INSERTED.CodeId, INSERTED.Code
          VALUES
          (
            @AnswerTypeId,
            ( SELECT ISNULL(MAX(c.Code) + 1, 1) FROM sv_360_Codes c WHERE c.AnswerTypeId = @AnswerTypeId ),
            @IntakeTitle,
            @CloseDate, @CloseDateSelf, @ScheduledStartDateUtc
          )";

        using (var cmd = new SqlCommand(sql, conn, trans)) {
          cmd.Parameters.Add("@SurveyId", SqlDbType.Int).Value = SurveyId;
          cmd.Parameters.Add("@IntakeTitle", SqlDbType.VarChar, 100).Value = IntakeTitle.LimitLengthTo(100).EmptyIfNull();
          cmd.Parameters.Add("@CloseDate", SqlDbType.DateTime).Value = IntakeCloseDateLocalTime_Rater; // TODO: use UTC in db
          cmd.Parameters.Add("@CloseDateSelf", SqlDbType.DateTime).Value = IntakeCloseDateLocalTime_Self; // TODO: use UTC in db
          cmd.Parameters.AddDateTime("@ScheduledStartDateUtc", scheduledStartDateUtc); // TODO: use UTC in db
          using (SqlDataReader dr = cmd.ExecuteReader()) {
            if (dr.Read()) {
              newCodeId = dr.GetInt("CodeId");
              newCode = dr.GetInt("Code");
            }
          }
        }
        return new IntakeInfo(newCodeId, newCode, IntakeTitle, IntakeCloseDateLocalTime_Self, IntakeCloseDateLocalTime_Rater, scheduledStartDateUtc);
      }

      public static int AddCode(SqlConnection conn, SqlTransaction trans,
        int AnswerTypeId, int? GblCodeId, int Code, string Value, string ValueAbbreviated,
        string ValueTextOther, double? ValueScore, string Memo) {

        int newCodeId = 0;

        using (var cmd = new SqlCommand(@"
        INSERT INTO sv_360_Codes
        (AnswerTypeId,GblCodeId,Code,Value,ValueAbbreviated,ValueTextOther,ValueScore,Memo)
        OUTPUT INSERTED.AnswerTypeId VALUES
        (@AnswerTypeId,@GblCodeId,@Code,@Value,@ValueAbbreviated,@ValueTextOther,@ValueScore,@Memo)
        ", conn, trans)) {
          cmd.Parameters.Add("@AnswerTypeId", SqlDbType.Int).Value = AnswerTypeId;
          cmd.Parameters.Add("@GblCodeId", SqlDbType.Int).Value = GblCodeId.OrDBNull();
          cmd.Parameters.Add("@Code", SqlDbType.Int).Value = Code;
          cmd.Parameters.Add("@Value", SqlDbType.VarChar, 200).Value = Value.LimitLengthTo(200).EmptyIfNull();
          cmd.Parameters.Add("@ValueAbbreviated", SqlDbType.VarChar, 100).Value = ValueAbbreviated.LimitLengthTo(100).EmptyIfNull();
          cmd.Parameters.Add("@ValueTextOther", SqlDbType.VarChar, 200).Value = ValueTextOther.LimitLengthTo(200).EmptyIfNull();
          cmd.Parameters.Add("@ValueScore", SqlDbType.Float).Value = ValueScore.OrDBNull();
          cmd.Parameters.Add("@Memo", SqlDbType.Text).Value = Memo.LimitLengthTo(5000).EmptyIfNull();
          try {
            newCodeId = (int)cmd.ExecuteScalar();
          } catch (Exception e) {
            var telemetry = ServiceLocator.Instance.GetRequiredService<ITelemetryService>();
            telemetry?.Exception(e)
              .WithOperation(nameof(AddCode))
              .WithOperationContext("AnswerTypes")
              .WithProperty(DalApplicationInsightsConstants.AnswerTypeId, AnswerTypeId)
              .WithProperty(DalApplicationInsightsConstants.GblCodeId, GblCodeId)
              .WithProperty(DalApplicationInsightsConstants.Code, Code)
              .WithProperty(DalApplicationInsightsConstants.Value, Value)
              .Track();

          }
        }
        return newCodeId;
      }

    }

  }
}
