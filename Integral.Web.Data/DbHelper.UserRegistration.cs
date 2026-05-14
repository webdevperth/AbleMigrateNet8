using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class UserRegistration {

      public static int CreateRegistration(SqlTransaction trans, string firstName, string lastName, string emailAddress, string companyName, string browserTimeZoneString) {

        emailAddress = emailAddress.TrimWhitespace();
        if (!emailAddress.IsEmailAddress()) throw new ArgumentException("Email address is invalid.");

        return Common.GetScalarQueryInt(trans, @"
            INSERT INTO al_UserRegistration (RegisteredUtc, FirstName, LastName, EmailAddress, CompanyName, BrowserTimeZone)
            OUTPUT INSERTED.UserRegistrationId
            VALUES (@RegisteredUtc, @FirstName, @LastName, @EmailAddress, @CompanyName, @BrowserTimeZone)",
          Common.NewSqlParameter("RegisteredUtc", DateTime.UtcNow),
          Common.NewSqlParameter("FirstName", firstName, 50),
          Common.NewSqlParameter("LastName", lastName, 50),
          Common.NewSqlParameter("EmailAddress", emailAddress, 100),
          Common.NewSqlParameter("CompanyName", companyName, 100),
          Common.NewSqlParameter("BrowserTimeZone", browserTimeZoneString, 100)
        );
      }

      public static RegistrationInfo GetRegistrationByEmail(SqlTransaction trans, string emailAddress) {

        RegistrationInfo regInfo = null;

        Common.Query(trans, @"
          SELECT TOP 1 UserRegistrationId, BrowserTimeZone, RegisteredUtc, FirstName, LastName, EmailAddress, EmailVerifiedUtc, AbleUserId, CompanyName, CompanyOrgId
          FROM al_UserRegistration
          WHERE EmailAddress = @EmailAddress",
          dr => {
            regInfo = new RegistrationInfo(
              dr.GetInt("UserRegistrationId"),
              dr.GetString("BrowserTimeZone"),
              dr.GetDateTime("RegisteredUtc"),
              dr.GetString("FirstName"),
              dr.GetString("LastName"),
              dr.GetString("EmailAddress"),
              dr.GetDateTimeOrNull("EmailVerifiedUtc"),
              dr.GetIntOrNull("AbleUserId"),
              dr.GetString("CompanyName"),
              dr.GetIntOrNull("CompanyOrgId")
            );
          },
          Common.NewSqlParameter("EmailAddress", emailAddress)
        );

        return regInfo;
      }

      public static bool UpdateRegistrationUserId(SqlTransaction trans, string registeredEmailAddress, int? linkToUserId) {
        int rowsAffected = Common.GetNonQueryInt(trans, @"
          UPDATE al_UserRegistration
          SET AbleUserId = @AbleUserId
          WHERE EmailAddress = @EmailAddress",
          Common.NewSqlParameter("EmailAddress", registeredEmailAddress, 100),
          Common.NewSqlParameter("AbleUserId", linkToUserId)
        );
        return rowsAffected > 0;
      }

      public class RegistrationInfo {

        public int UserRegistrationId { get; private set; }
        public DateTime RegisteredUtc { get; private set; }
        public string BrowserTimeZoneString { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string CompanyName { get; private set; }
        public int? CompanyOrgId { get; internal set; }
        public string EmailAddress { get; private set; }
        public DateTime? EmailVerifiedUtc { get; internal set; }
        public int? AbleUserId { get; internal set; }

        public RegistrationInfo(
          int userRegistrationId,
          string browserTimeZoneString,
          DateTime registeredUtc,
          string firstName,
          string lastName,
          string emailAddress,
          DateTime? emailVerifiedUtc,
          int? ableUserId,
          string companyName,
          int? companyOrgId
        ) {
          UserRegistrationId = userRegistrationId;
          RegisteredUtc = registeredUtc;
          BrowserTimeZoneString = browserTimeZoneString;
          FirstName = firstName;
          LastName = lastName;
          CompanyName = companyName;
          CompanyOrgId = companyOrgId;
          EmailAddress = emailAddress;
          EmailVerifiedUtc = emailVerifiedUtc;
          AbleUserId = ableUserId;
        }
      }
    }
  }
}
