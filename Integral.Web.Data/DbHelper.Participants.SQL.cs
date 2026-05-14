/*
 * Predefined SQL Statements
 */

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.RegularExpressions;

namespace Integral.Web {
  public partial class DbHelper : HelperBase<DbHelper> {
    public partial class Participants {
      public class SQL {

        // This class is for storing long SQL queries as functions
        // that accept the parameters and return the new SqlCommand object.

        public static SqlCommand GetCmdDeleteParticipant(int ParticipantId, SqlConnection conn, SqlTransaction trans = null) {

          string sqlDeleteParticipant = @"
            BEGIN TRANSACTION;

            -- Delete TextAnswers
            DELETE ta
            FROM sv_360_TextAnswers ta
            INNER JOIN sv_Answers a ON ta.AnswerId = a.AnswerId
            WHERE a.ParticipantId = @DeleteParticipantId;

            -- Delete Multi Answers
            DELETE m
            FROM sv_AnswersMulti m
            INNER JOIN sv_Answers a ON m.AnswerId = a.AnswerId
            WHERE a.ParticipantId = @DeleteParticipantId;

            -- Delete Answers
            DELETE sv_Answers WHERE ParticipantId = @DeleteParticipantId;

            -- Delete Participants
            DELETE sv_360_Participants 
            OUTPUT DELETED.PartId
            WHERE PartId = @DeleteParticipantId;

            COMMIT TRANSACTION;
            ";

          var cmd = new SqlCommand(sqlDeleteParticipant, conn, trans);
          cmd.Parameters.AddInt("@DeleteParticipantId", ParticipantId);
          return cmd;
        }
      
      }
    }
  }
}
