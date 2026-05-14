using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public partial class Questions {

      public const int GlobalQnGroupId_LMP_Standard = 22; // LMP Standard Question Group ID.

      const string CodeListCodeSeparator = "|";
      const string CodeListValueSeparator = "^";
      const int StuffedCodeColumnCount = 7; // The number of code colun values STUFFed in the query.
      const int StuffedAnsMultiColumnCount = 4; // The number of multi column values STUFFed in the query.

      public static bool AddToSurveyFromGlobal(int SurveyId, int GblQuestionId, SqlConnection conn, SqlTransaction trans = null) {
        using (var cmd = new SqlCommand(@"
        INSERT INTO sv_360_Questions (SurveyId, GblQuestionId)
        VALUES (@SurveyId, @GblQuestionId)
        ", conn, trans)) {
          cmd.Parameters.Add("@SurveyId", SqlDbType.Int).Value = SurveyId;
          cmd.Parameters.Add("@GblQuestionId", SqlDbType.Int).Value = GblQuestionId;
          cmd.ExecuteNonQuery();
        }
        return true;
      }

      public static QuestionList GetQuestionsForPageAdmin(int OrgId, int SurveyId, int pageNum, bool IsSelf, int QnSet) {
        var qns = GetSurveyQuestions(OrgId, SurveyId, "", "");
        return GetQuestionsForPage(qns, pageNum, IsSelf, QnSet);
      }
      public static QuestionList GetQuestionsForPagePublic(string SurveyUID, string PartUID, int? pageNum = null, bool IsSelf = true, int QnSet = 1) {
        var qns = GetSurveyQuestions(0, 0, SurveyUID, PartUID);
        return GetQuestionsForPage(qns, pageNum, IsSelf, QnSet);
      }
      private static QuestionList GetQuestionsForPage(QuestionList SurveyQnList, int? pageNum = null, bool IsSelf = true, int QnSet = 1) {
        // pageNum = null means get all questions for all pages.

        var pageQnList = new QuestionList();
        pageQnList.AnswerTypes = SurveyQnList.AnswerTypes;
        pageQnList.TotalPages = 0;
        pageQnList.ThisPageNum = 0;
        if (SurveyQnList.PartInfo.PartId == 0) {
          pageQnList.PartInfo.IsSelf = IsSelf;
          pageQnList.PartInfo.QnSet = QnSet;
        }
        int prevPageId = 0;
        int thisPageNum = 0;
        int qnAutoNum = 0;
        // TODO check conditionals to work out if requested page number needs adjusting due to skipped page(s).
        // Go thru all questions, skipping what shouldn't be displayed
        // and compile a final output question list for just the current page.
        for (int index = 0; index < SurveyQnList.Questions.Count; index++) {
          var qnInfo = SurveyQnList.Questions[index];
          if (IsQuestionDisplayable(qnInfo, SurveyQnList.PartInfo.IsSelf, SurveyQnList.PartInfo.QnSet)) {
            qnAutoNum += qnInfo.AutoNumberStep;
            qnInfo.AutoNumber = qnAutoNum;
            qnInfo.DisplayNumber = (qnInfo.ShowAutoNumber ? qnInfo.AutoNumber.ToString() : "") + qnInfo.CustomNumber;
            if (prevPageId != qnInfo.PageId) {
              prevPageId = qnInfo.PageId;
              thisPageNum++;
              if (thisPageNum == pageNum) pageQnList.ThisPageNum = thisPageNum;
              pageQnList.TotalPages++;
            }
            if (pageNum == null || thisPageNum == pageNum) {
              pageQnList.Questions.Add(qnInfo); // Add question for this page.
            }
          } // displayable question
        } // for
        return pageQnList;
      }

      private static bool IsQuestionDisplayable(SurveyQuestionInfo qnInfo, bool IsSelf, int QnSet) {
        if (qnInfo.Hidden) return false;
        if (qnInfo.HideForLevel[QnSet - 1]) return false;
        if ((IsSelf ? qnInfo.QuestionTextSelf : qnInfo.QuestionTextRater).EmptyIfNull().ToLower() == "na") return false;
        return true;
      }

      private static QuestionList GetSurveyQuestions(int OrgId = 0, int SurveyId = 0, string SurveyUID = "", string PartUID = "") {

        if (SurveyId > 0 && OrgId <= 0 || SurveyId <= 0 && OrgId > 0
        || !SurveyUID.IsNullOrEmpty() && PartUID.IsNullOrEmpty()
        || SurveyUID.IsNullOrEmpty() && !PartUID.IsNullOrEmpty())
          throw new ArgumentException("Either pass valid pair of SurveyId+OrgId or SurveyUniqueId+ParticipantUniqueId.");

        var whereList = new List<string>();
        if (!SurveyUID.IsNullOrEmpty()) whereList.Add("s.sv_uniqueid = @SurveyUniqueId");
        if (SurveyId > 0) whereList.Add("q.SurveyId = @SurveyId");
        if (OrgId > 0) whereList.Add("s.sv_OrgId = @OrgId");

        var qnList = new QuestionList();

        string sql = $@"
          SELECT
            p.PartId, p.PartUID, p.Name, p.Email, p.IsSelf, p.QuestionSet,
            q.QuestionId, q.SurveyId, q.PageId, q.Sort, q.AutoNumber, q.AutoNumberStep, q.ShowAutoNumber, q.CustomNumber,
            q.QuestionTextFull1, q.QuestionTextFullOther1, q.ShowQnText, q.AnswerTypeId, q.IsHeading, gq.GblQnGroupId,
            q.Required, q.AddTextBox, q.Hidden, q.HideForLevel1, q.HideForLevel2, q.HideForLevel3, q.HideForLevel4,
            q.HideForLevel5, q.HideForLevel6, q.HideForLevel7, q.HideForLevel8, q.HideForLevel9, q.HideForLevel10,
            t.GblAnswerTypeId, t.InputType,
            a.AnswerId, a.AnswerCode, a.AnswerCodeId, a.AnswerText, a.AnswerValue, a.AnswerAbbrev, a.AnswerScore,
            c.CodeList, am.AnsMultiList
          FROM sv_360_Questions q
          INNER JOIN sv_Survey s ON s.sv_id = q.SurveyId
          LEFT OUTER JOIN sv_360_AnswerTypes t ON q.AnswerTypeId = t.AnswerTypeId
          LEFT OUTER JOIN sv_GblQuestions gq ON q.GblQuestionId = gq.GblQuestionId
          OUTER APPLY (
            SELECT TOP 1 p.PartId, p.UniqueId AS PartUID, p.Name, p.Email, p.IsSelf, p.QuestionSet
            FROM sv_360_Participants p
            WHERE p.SurveyId = s.sv_id
            AND p.UniqueId = @ParticipantUniqueId
          ) AS p
          OUTER APPLY (
            SELECT TOP 1
              a.AnswerId, a.Answer AS AnswerCode, a.CodeId AS AnswerCodeId,
              c.Value AS AnswerValue, c.ValueAbbreviated AS AnswerAbbrev, c.ValueScore AS AnswerScore,
              ta.ta_textanswer AS AnswerText
            FROM sv_Answers a
            LEFT OUTER JOIN sv_360_TextAnswers ta ON ta.AnswerId = a.AnswerId
            LEFT OUTER JOIN sv_360_Codes c ON c.CodeId = a.CodeId
            WHERE a.QuestionId = q.QuestionId
              AND a.ParticipantId = p.PartId
          ) AS a
          OUTER APPLY (
            SELECT (STUFF((
            SELECT '{CodeListCodeSeparator}' + CAST(c.CodeId AS VARCHAR(20))
                  + '{CodeListValueSeparator}' + CAST(ISNULL(c.Code, '') AS VARCHAR(20))
                  + '{CodeListValueSeparator}' + c.Value
                  + '{CodeListValueSeparator}' + c.ValueAbbreviated
                  + '{CodeListValueSeparator}' + CAST(ISNULL(c.ValueScore, '') AS VARCHAR(20))
                  + '{CodeListValueSeparator}' + c.ValueTextOther
                  + '{CodeListValueSeparator}' + c.Memo
            FROM sv_360_Codes c
            WHERE c.AnswerTypeId = t.AnswerTypeId
            ORDER BY c.Code
            FOR XML PATH (''), TYPE, ROOT).value('root[1]', 'nvarchar(max)'), 1, 1, '')) as CodeList
          ) AS c
          OUTER APPLY (
            SELECT (STUFF((
                SELECT /* '{CodeListCodeSeparator}' + CAST(am.AnswerId AS VARCHAR(20))
                      + '{CodeListValueSeparator}' + CAST(am.ParticipantId AS VARCHAR(20)) */
                      + '{CodeListCodeSeparator}' + CAST(ISNULL(am.CodeId, '') AS VARCHAR(20))
                      + '{CodeListValueSeparator}' + CAST(ISNULL(am.AnsMultiValue, '') AS VARCHAR(20))
                      + '{CodeListValueSeparator}' + amc.Value
                      + '{CodeListValueSeparator}' + amc.ValueAbbreviated
                FROM sv_AnswersMulti am
                INNER JOIN sv_360_Codes amc ON amc.CodeId = am.CodeId
                WHERE am.AnswerId = a.AnswerId
                FOR XML PATH (''), TYPE, ROOT
              ).value('root[1]', 'nvarchar(max)'), 1, 1, '')) AS AnsMultiList
          ) AS am
          WHERE {whereList.Join(" AND ")}
          ORDER BY q.Sort;";

        bool initDone = false;

        Common.Query(sql,
          dr => {
            if (!initDone) {
              qnList.TotalPages = 0;
              qnList.PartInfo.PartId = dr.GetInt("PartId", 0);
              qnList.PartInfo.PartUID = dr.GetString("PartUID", "");
              qnList.PartInfo.Name = dr.GetString("Name", "");
              qnList.PartInfo.Email = dr.GetString("Email", "");
              qnList.PartInfo.IsSelf = dr.GetIntOrDefault("IsSelf", 1) == 1 ? true : false;
              qnList.PartInfo.QnSet = dr.GetInt("QuestionSet", 1);
              initDone = true;
            }
            qnList.AddQuestion(dr);
          },
          Common.NewSqlParameter("SurveyId", SurveyId),
          Common.NewSqlParameter("OrgId", OrgId),
          Common.NewSqlParameter("SurveyUniqueId", SurveyUID),
          Common.NewSqlParameter("ParticipantUniqueId", PartUID)
        );

        return qnList;
      }

      public class QuestionList {
        public int ThisPageNum = 0;
        public int TotalPages = 0;
        public QuestionListPartInfo PartInfo;
        public List<SurveyQuestionInfo> Questions;
        public Dictionary<int, List<CodeInfo>> AnswerTypes;

        public QuestionList() {
          Questions = new List<SurveyQuestionInfo>();
          AnswerTypes = new Dictionary<int, List<CodeInfo>>();
          PartInfo = new QuestionListPartInfo();
        }

        public void AddQuestion(SqlDataReader dr) {

          var qnInfo = new SurveyQuestionInfo(
            questionId: dr.GetInt("QuestionId"),
            surveyId: dr.GetInt("SurveyId"),
            pageId: dr.GetIntOrDefault("PageId", 0),
            gblQuestionGroupId: dr.GetIntOrNull("GblQnGroupId"),
            sort: dr.GetInt("Sort"),
            autoNumber: dr.GetInt("AutoNumber"),
            autoNumberStep: dr.GetInt("AutoNumberStep"),
            showAutoNumber: dr.GetBoolFromInt("ShowAutoNumber"),
            customNumber: dr.GetString("CustomNumber"),
            isHeading: dr.GetBoolFromInt("IsHeading"),
            questionTextSelf: dr.GetString("QuestionTextFull1"),
            questionTextRater: dr.GetString("QuestionTextFullOther1"),
            showQuestionText: dr.GetBoolFromInt("ShowQnText"),
            answerTypeId: dr.GetIntOrNull("AnswerTypeId"),
            gblAnswerTypeId: dr.GetIntOrNull("GblAnswerTypeId"),
            inputType: dr.GetString("InputType", ""),
            isRequired: dr.GetBoolFromInt("Required"),
            addTextBox: dr.GetBoolFromInt("AddTextBox"),
            isHidden: dr.GetBoolFromInt("Hidden"),
            hideForLevel: new bool[] {
              dr.GetBoolFromBit("HideForLevel1"),
              dr.GetBoolFromBit("HideForLevel2"),
              dr.GetBoolFromBit("HideForLevel3"),
              dr.GetBoolFromBit("HideForLevel4"),
              dr.GetBoolFromBit("HideForLevel5"),
              dr.GetBoolFromBit("HideForLevel6"),
              dr.GetBoolFromBit("HideForLevel7"),
              dr.GetBoolFromBit("HideForLevel8"),
              dr.GetBoolFromBit("HideForLevel9"),
              dr.GetBoolFromBit("HideForLevel10")
            },
            answerId: dr.GetIntOrNull("AnswerId"),
            answerCode: dr.GetIntOrNull("AnswerCode"),
            answerCodeId: dr.GetIntOrNull("AnswerCodeId"),
            answerValue: dr.GetString("AnswerValue"),
            answerAbbrev: dr.GetString("AnswerAbbrev"),
            answerScore: dr.GetDecimalOrNull("AnswerScore"),
            answerText: dr.GetString("AnswerText")
          );

          // Add multiple-choice answer data.
          var ansMultiList = new List<AnswersMulti>();
          qnInfo.AnswersMulti = ansMultiList;
          string ansMultiString = dr.GetString("AnsMultiList");
          if (!ansMultiString.IsNullOrEmpty()) {
            var multiArray = ansMultiString.Split(CodeListCodeSeparator.ToCharArray());
            for (int iMulti = 0; iMulti < multiArray.Length; iMulti++) {
              string multiItem = multiArray[iMulti];
              var valArray = multiItem.Split(CodeListValueSeparator.ToCharArray());
              int codeId, code;
              if (valArray != null && valArray.Length == StuffedAnsMultiColumnCount) {
                int.TryParse(valArray[0], out codeId);
                int.TryParse(valArray[1], out code);
                if (codeId > 0) {
                  ansMultiList.Add(new AnswersMulti() {
                    AnswerId = qnInfo.AnswerId.GetValueOrDefault(),
                    ParticipantId = PartInfo.PartId,
                    AnswerCode = code,
                    AnswerCodeId = codeId,
                    AnswerValue = valArray[2],
                    AnswerAbbrev = valArray[3]
                  });
                }
              }
            }
          }

          Questions.Add(qnInfo);
          // Create and/or reference the Answer Type.
          if (qnInfo.AnswerTypeId != null) {
            if (!AnswerTypes.ContainsKey((int)qnInfo.AnswerTypeId)) {
              // Create new AnswerType.
              var codeList = dr.GetString("CodeList");
              var codes = new List<CodeInfo>();
              if (!codeList.IsNullOrEmpty() && codeList.Length > 0) {
                var codeArray = codeList.Split(CodeListCodeSeparator.ToCharArray());
                double avgTextLength = 0, avgAbbrevLength = 0;
                for (int iCode = 0; iCode < codeArray.Length; iCode++) {
                  // Break out string of "code,value,valueabbrev".
                  string codeItem = codeArray[iCode];
                  var codeValues = codeItem.Split(CodeListValueSeparator.ToCharArray());
                  int codeId = 0, code = 0;
                  string codeText = "", codeTextAbbrev = "";
                  string codeTextOther = "", codeTextMemo = "";
                  decimal? codeValueScore = null;
                  decimal valueTemp;
                  if (codeValues != null && codeValues.Length == StuffedCodeColumnCount) {
                    int.TryParse(codeValues[0], out codeId);
                    int.TryParse(codeValues[1], out code);
                    codeText = codeValues[2];
                    codeTextAbbrev = codeValues[3];
                    if (!codeValues[4].IsNullOrEmpty() && decimal.TryParse(codeValues[4], out valueTemp)) codeValueScore = valueTemp;
                    codeTextOther = codeValues[5];
                    codeTextMemo = codeValues[6];
                  }
                  avgTextLength += codeText.Length;
                  avgAbbrevLength += codeTextAbbrev.Length;
                  // Add to CodeInfo list.
                  codes.Add(new CodeInfo(codeId, code, codeText, codeTextAbbrev, codeValueScore, codeTextOther, codeTextMemo));
                }
                // Get average lengths of both text fields.
                // If the "abbreviated" field is longer than the text field, swap them around.
                // This is because some answertypes have them around the "wrong" way,
                // and we definitely want the shorter text in the "CodeTextAbbrev" field.
                avgTextLength /= codes.Count;
                avgAbbrevLength /= codes.Count;
                if (avgAbbrevLength > avgTextLength) {
                  foreach (var code in codes) {
                    string temp = code.CodeText;
                    code.CodeText = code.CodeTextAbbrev;
                    code.CodeTextAbbrev = temp;
                  }
                }
              }
              // Add to AnswerType cache.
              AnswerTypes.Add((int)qnInfo.AnswerTypeId, codes);
            }
            qnInfo.Codes = AnswerTypes[(int)qnInfo.AnswerTypeId];
          }
        }
      }

      public class QuestionListPartInfo {
        public int PartId = 0;
        public string PartUID = "";
        public string Name = "";
        public string Email = "";
        public bool IsSelf = true;
        public int QnSet = 1;
      }

      public class AnswersMulti {
        //public int? AnsMultiId;
        public int ParticipantId;
        public int AnswerId;
        public int AnswerCode;
        public int AnswerCodeId;
        public string AnswerValue;
        public string AnswerAbbrev;
      }

      public class SurveyQuestionInfo {
        public int QuestionId;
        public int SurveyId;
        public int PageId;
        public int? GblQuestionGroupId;
        public int Sort;
        public int AutoNumber;
        public int AutoNumberStep;
        public bool ShowAutoNumber;
        public string CustomNumber;
        public string DisplayNumber;
        public bool IsHeading;
        public string QuestionTextSelf;
        public string QuestionTextRater;
        public bool ShowQuestionText;
        public int? AnswerTypeId;
        public int? GblAnswerTypeId;
        public string InputType;
        public bool Required;
        public bool AddTextBox;
        public bool Hidden;
        public bool[] HideForLevel;
        public int? AnswerId;
        public int? AnswerCode;
        public int? AnswerCodeId;
        public string AnswerValue;
        public string AnswerAbbrev;
        public decimal? AnswerScore;
        public string AnswerText;
        public List<AnswersMulti> AnswersMulti;
        public List<CodeInfo> Codes;

        public SurveyQuestionInfo(int questionId, int surveyId, int pageId, int? gblQuestionGroupId, int sort,
          int autoNumber, int autoNumberStep, bool showAutoNumber, string customNumber,
          bool isHeading, string questionTextSelf, string questionTextRater, bool showQuestionText,
          int? answerTypeId, int? gblAnswerTypeId, string inputType, bool isRequired, bool addTextBox, bool isHidden, bool[] hideForLevel,
          int? answerId, int? answerCode, int? answerCodeId,
          string answerValue, string answerAbbrev, decimal? answerScore, string answerText
        ) {

          this.QuestionId = questionId;
          this.SurveyId = surveyId;
          this.PageId = pageId;
          this.GblQuestionGroupId = gblQuestionGroupId;
          this.Sort = sort;
          this.AutoNumber = autoNumber;
          this.AutoNumberStep = autoNumberStep;
          this.ShowAutoNumber = showAutoNumber;
          this.CustomNumber = customNumber;
          this.IsHeading = isHeading;
          this.QuestionTextSelf = questionTextSelf;
          this.QuestionTextRater = questionTextRater;
          this.ShowQuestionText = showQuestionText;
          this.AnswerTypeId = answerTypeId;
          this.GblAnswerTypeId = gblAnswerTypeId;
          this.InputType = inputType;
          this.Required = isRequired;
          this.AddTextBox = addTextBox;
          this.Hidden = isHidden;
          this.HideForLevel = hideForLevel;
          this.AnswerId = answerId;
          this.AnswerCode = answerCode;
          this.AnswerCodeId = answerCodeId;
          this.AnswerValue = answerValue;
          this.AnswerAbbrev = answerAbbrev;
          this.AnswerScore = answerScore;
          this.AnswerText = answerText;
        }
        public CodeInfo GetCodeInfoByCodeOrNull(int Code) {
          foreach (var codeInfo in Codes) {
            if (codeInfo.Code == Code) return codeInfo;
          }
          return null;
        }
      }

      public class CodeInfo {
        public int CodeId;
        public int Code;
        public string CodeText;
        public string CodeTextAbbrev;
        public decimal? CodeValueScore;
        public string CodeTextOther;
        public string CodeTextMemo;
        public CodeInfo(int codeId, int code, string codeText, string codeTextAbbrev, decimal? codeValueScore, string codeTextOther, string codeTextMemo) {
          this.CodeId = codeId;
          this.Code = code;
          this.CodeText = codeText;
          this.CodeTextAbbrev = codeTextAbbrev;
          this.CodeValueScore = codeValueScore;
          this.CodeTextOther = codeTextOther;
          this.CodeTextMemo = codeTextMemo;
        }
      }

      public class SurveyConditionalInfo {
        public int SurveyId;
        public int QuestionId;
        public int TriggerAnsCodeId;
        public int SkipToPageId;
        public SurveyConditionalInfo(int SurveyId, int QuestionId, int TriggerAnsCodeId, int SkipToPageId) {
          this.SurveyId = SurveyId;
          this.QuestionId = QuestionId;
          this.TriggerAnsCodeId = TriggerAnsCodeId;
          this.SkipToPageId = SkipToPageId;
        }
      }


      public class ReportQuestionInfo {
        public int SurveyId { get; private set; }
        public int? RptQnGrpHeadingSort { get; private set; } // Also Dimension code.
        public string RptQnGrpHeading { get; private set; } // Also Dimension heading.
        public int QuestionId { get; private set; }
        public int GblQuestionId { get; private set; }
        public int Sort { get; private set; }
        public int AutoNumber { get; private set; }
        public string QuestionText { get; private set; }
        public string QuestionTextForRater { get; private set; }
        public QuestionScores Scores { get; set; }
        public ReportQuestionInfo(
          int surveyId,
          int? rptQnGrpHeadingSort,
          string rptQnGrpHeading,
          int questionId,
          int gblQuestionId,
          int sort,
          int autoNumber,
          string questionText,
          string questionTextForRater,
          // providing scores is optional.
          QuestionScores scores = null
        ) {
          SurveyId = surveyId;
          RptQnGrpHeadingSort = rptQnGrpHeadingSort;
          RptQnGrpHeading = rptQnGrpHeading;
          QuestionId = questionId;
          GblQuestionId = gblQuestionId;
          Sort = sort;
          AutoNumber = autoNumber;
          QuestionText = questionText;
          QuestionTextForRater = questionTextForRater;
          Scores = scores;
          if (Scores == null) Scores = new QuestionScores();
        }
        public void AccumulateScores(ReportQuestionInfo qnInfo) {
          if (qnInfo != null) this.Scores.AccumulateScores(qnInfo.Scores);
        }

      }

      public class QuestionScores {
        public ScoreParam ScoreSelf { get; private set; }
        public ScoreParam ScoreRater { get; private set; }
        // TODO: Add Dictionary<int, ScoreParam> ScoreFeedback, with key as Enum FeedbackCode { Manager = 1, Peer = 2, DirectReport = 3 }
        public ScoreParam ScorePreviousSelf { get; private set; }
        public ScoreParam ScorePreviousRater { get; private set; }
        public ScoreParam ScoreBenchSelf { get; private set; }
        public ScoreParam ScoreBenchRater { get; private set; }
        public QuestionScores() {
          InitQuestionScores(null, null, null, null, null, null);
        }
        public QuestionScores(QuestionScores scores) {
          InitQuestionScores(
            scores.ScoreSelf,
            scores.ScoreRater,
            scores.ScorePreviousSelf,
            scores.ScorePreviousRater,
            scores.ScoreBenchSelf,
            scores.ScoreBenchRater
          );
        }
        public QuestionScores(
          ScoreParam scoreSelf,
          ScoreParam scorePreviousSelf,
          ScoreParam scoreBenchSelf
        ) {
          InitQuestionScores(scoreSelf, null, scorePreviousSelf, null, scoreBenchSelf, null);
        }
        public QuestionScores(
          ScoreParam scoreSelf,
          ScoreParam scoreRater,
          ScoreParam scorePreviousSelf,
          ScoreParam scorePreviousRater,
          ScoreParam scoreBenchSelf,
          ScoreParam scoreBenchRater
        ) {
          InitQuestionScores(scoreSelf, scoreRater, scorePreviousSelf, scorePreviousRater, scoreBenchSelf, scoreBenchRater);
        }

        private void InitQuestionScores(
          ScoreParam scoreSelf,
          ScoreParam scoreRater,
          ScoreParam scorePreviousSelf,
          ScoreParam scorePreviousRater,
          ScoreParam scoreBenchSelf,
          ScoreParam scoreBenchRater
        ) {
          // Note use "new ScoreParam(x)" so that the scores objects are not just pointers to the originals.
          ScoreSelf = new ScoreParam(scoreSelf);
          ScoreRater = new ScoreParam(scoreRater);
          ScorePreviousSelf = new ScoreParam(scorePreviousSelf);
          ScorePreviousRater = new ScoreParam(scorePreviousRater);
          ScoreBenchSelf = new ScoreParam(scoreBenchSelf);
          ScoreBenchRater = new ScoreParam(scoreBenchRater);
        }
        public void AccumulateScores(QuestionScores questionScores) {
          this.ScoreSelf.AccumulateScore(questionScores.ScoreSelf);
          this.ScoreRater.AccumulateScore(questionScores.ScoreRater);
          this.ScorePreviousSelf.AccumulateScore(questionScores.ScorePreviousSelf);
          this.ScorePreviousRater.AccumulateScore(questionScores.ScorePreviousRater);
          this.ScoreBenchSelf.AccumulateScore(questionScores.ScoreBenchSelf);
          this.ScoreBenchRater.AccumulateScore(questionScores.ScoreBenchRater);
        }
      }

      public class ScoreParam {
        public double? Sum { get; private set; }
        public int? Count { get; private set; }
        public double? Avg { get; private set; }

        // Note, three ways to provide the scores to the constructor:
        // 1. Only sum and count are provided, in which case avg is calculated as sum / count (only if both aren't null and count > 0).
        // 2. Only avg is provided, in which case sum and count are null, and can't be determined.
        // 3. All 3 are provided.
        public ScoreParam(double? sum, int? count) {
          setFields(sum, count, null);
        }
        public ScoreParam(double? avg) {
          setFields(null, null, avg);
        }
        public ScoreParam(double? sum, int? count, double? avg) {
          setFields(sum, count, avg);
        }
        public ScoreParam(ScoreParam scoreParam) {
          if (scoreParam != null) setFields(scoreParam.Sum, scoreParam.Count, scoreParam.Avg);
        }

        private void setFields(double? sum, int? count, double? avg) {
          this.Sum = sum;
          this.Count = count;
          this.Avg = avg;
          // Calc avg if necessary.
          if (avg == null) calcAvg();
        }
        private void calcAvg() {
          if (Sum != null && Count != null && Count > 0) {
            Avg = Sum / Count; // calculate avg
          } else {
            Avg = null; // can't calculate
          }
        }

        public void ClearScore() {
          this.Avg = null;
          this.Sum = null;
          this.Count = null;
        }

        public void SetScore(double? avg) {
          this.Avg = avg;
          this.Sum = null; // unknown
          this.Count = null; // unknown
        }
        public void SetScore(double? sum, int? count) {
          this.Sum = sum;
          this.Count = count;
          calcAvg();
        }
        public void SetScore(ScoreParam score) {
          if (score == null) {
            this.ClearScore();
          } else {
            this.Avg = score.Avg;
            this.Sum = score.Sum;
            this.Count = score.Count;
          }
        }

        public void AccumulateScore(ScoreParam scoreParam) {
          if (scoreParam == null) return;
          AccumulateScore(scoreParam.Sum, scoreParam.Count);
        }
        public void AccumulateScore(double? addToSum, int? addToCount) {
          if (addToSum != null) {
            if (this.Sum == null) this.Sum = 0;
            this.Sum += addToSum;
          }
          if (addToCount != null) {
            if (this.Count == null) this.Count = 0;
            this.Count += addToCount;
          }
          calcAvg();
        }
      } // ScoreParam


    }
  }
}
