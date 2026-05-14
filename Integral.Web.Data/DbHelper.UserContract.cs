using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class UserContract {


      static UserContract() { }

      public enum ContractType {
        Unset = 0,
        Casual = 4,
        Contractor = 5
      }

      public static ContractInfo GetLatestContract(SqlTransaction trans, int userId) {

        var resultList = GetContractList(trans, 1, "",
          "uc.UserId = @UserId",
          "uc.SubmittedUtc DESC",
          Common.NewSqlParameter("UserId", userId)
        );

        return resultList.IsNullOrEmpty() ? null : resultList[0];
      }

      public static List<ContractInfo> GetContractList(
        SqlTransaction trans,
        int? topOrNullForAll,
        string sqlExtraJoins,
        string sqlWhereConditions,
        string sqlOrderBy,
        params SqlParameter[] sqlWhereParams
      ) {

        var result = new List<ContractInfo>();

        Common.Query(trans, $@"

          SELECT {(topOrNullForAll == null ? "" : $"TOP {topOrNullForAll}")}
            uc.UserContractId, uc.UserId, uc.UserContractTypeId, uc.SubmittedUtc,
            uc.PostalAddress1, uc.PostalAddress2, uc.PostalPostCode, uc.PostalCountry,
            uc.IDDateOfBirth, uc.IDLicenseOrPassport, uc.IDCountryOfIssue,
            uc.BankAccountName, uc.BankAccountBSB, uc.BankAccountNumber,
            uc.NextOfKinFullName, uc.NextOfKinMobileNumber,
            uc.ABNNumber, uc.BusinessEntityName, uc.Agree_RegisteredABN, uc.Agree_PayOwnSuper, uc.Agree_OwnLiabilityInsurance, uc.Agree_ContractorTerms,
            uc.Agree_IntegralPaySuper, uc.Agree_CasualTerms,
            uct.UserContractTypeName

          FROM al_UserContract uc
          INNER JOIN al_UserContractType uct ON uct.UserContractTypeId = uc.UserContractTypeId
          {sqlExtraJoins}
          {sqlWhereConditions.EnsureStartsWith("WHERE ", true)}
          {sqlOrderBy.EnsureStartsWith("ORDER BY ", true)}",

          dr => {
            result.Add(new ContractInfo(
              dr.GetInt("UserContractId"),
              dr.GetInt("UserId"),
              dr.GetInt("UserContractTypeId"),
              dr.GetString("UserContractTypeName"),
              dr.GetDateTime("SubmittedUtc"),
              dr.GetString("PostalAddress1"),
              dr.GetString("PostalAddress2"),
              dr.GetString("PostalPostCode"),
              dr.GetString("PostalCountry"),
              dr.GetDateTime("IDDateOfBirth"),
              dr.GetString("IDLicenseOrPassport"),
              dr.GetString("IDCountryOfIssue"),
              dr.GetString("BankAccountName"),
              dr.GetString("BankAccountBSB"),
              dr.GetString("BankAccountNumber"),
              dr.GetString("NextOfKinFullName"),
              dr.GetString("NextOfKinMobileNumber"),
              dr.GetString("ABNNumber"),
              dr.GetString("BusinessEntityName"),
              dr.GetBoolFromInt("Agree_RegisteredABN"),
              dr.GetBoolFromInt("Agree_PayOwnSuper"),
              dr.GetBoolFromInt("Agree_OwnLiabilityInsurance"),
              dr.GetBoolFromInt("Agree_ContractorTerms"),
              dr.GetBoolFromInt("Agree_IntegralPaySuper"),
              dr.GetBoolFromInt("Agree_CasualTerms")
            ));
          },

          sqlWhereParams
        );

        return result;
      }

      // Within a transaction:
      // - Create new contract for the user,
      // - update user's ContractTypeId,
      // - update user's IsPartnerActive flag.
      // Return the new ContractId.
      public static int AddContract(AlbertCoaches.AlbertCoachInfo coachInfo, ContractInfo newContractInfo) {

        int newUserContractId = 0;

        Common.UsingTransaction(trans => {

          newUserContractId = Common.GetScalarQueryIntOrNull(trans, @"
            INSERT INTO al_UserContract (
              UserId, UserContractTypeId, SubmittedUtc, PostalAddress1, PostalAddress2, PostalPostCode, PostalCountry,
              IDDateOfBirth, IDLicenseOrPassport, IDCountryOfIssue,
              BankAccountName, BankAccountBSB, BankAccountNumber,
              NextOfKinFullName, NextOfKinMobileNumber,
              ABNNumber, BusinessEntityName, Agree_RegisteredABN, Agree_PayOwnSuper, Agree_OwnLiabilityInsurance, Agree_ContractorTerms,
              Agree_IntegralPaySuper, Agree_CasualTerms
            )
            OUTPUT INSERTED.UserContractId
            VALUES (
              @UserId, @UserContractTypeId, @SubmittedUtc, @PostalAddress1, @PostalAddress2, @PostalPostCode, @PostalCountry,
              @IDDateOfBirth, @IDLicenseOrPassport, @IDCountryOfIssue,
              @BankAccountName, @BankAccountBSB, @BankAccountNumber,
              @NextOfKinFullName, @NextOfKinMobileNumber,
              @ABNNumber, @BusinessEntityName, @Agree_RegisteredABN, @Agree_PayOwnSuper, @Agree_OwnLiabilityInsurance, @Agree_ContractorTerms,
              @Agree_IntegralPaySuper, @Agree_CasualTerms
            );",
            Common.NewSqlParameter("UserId", newContractInfo.UserId),
            Common.NewSqlParameter("UserContractTypeId", newContractInfo.UserContractTypeId),
            Common.NewSqlParameter("SubmittedUtc", newContractInfo.SubmittedUtc),
            Common.NewSqlParameter("PostalAddress1", newContractInfo.PostalAddress1),
            Common.NewSqlParameter("PostalAddress2", newContractInfo.PostalAddress2),
            Common.NewSqlParameter("PostalPostCode", newContractInfo.PostalPostCode),
            Common.NewSqlParameter("PostalCountry", newContractInfo.PostalCountry),
            Common.NewSqlParameter("IDDateOfBirth", newContractInfo.IDDateOfBirth),
            Common.NewSqlParameter("IDLicenseOrPassport", newContractInfo.IDLicenseOrPassport),
            Common.NewSqlParameter("IDCountryOfIssue", newContractInfo.IDCountryOfIssue),
            Common.NewSqlParameter("BankAccountName", newContractInfo.BankAccountName),
            Common.NewSqlParameter("BankAccountBSB", newContractInfo.BankAccountBSB),
            Common.NewSqlParameter("BankAccountNumber", newContractInfo.BankAccountNumber),
            Common.NewSqlParameter("NextOfKinFullName", newContractInfo.NextOfKinFullName),
            Common.NewSqlParameter("NextOfKinMobileNumber", newContractInfo.NextOfKinMobileNumber),
            Common.NewSqlParameter("ABNNumber", newContractInfo.ABNNumber),
            Common.NewSqlParameter("BusinessEntityName", newContractInfo.BusinessEntityName),
            Common.NewSqlParameter("Agree_RegisteredABN", newContractInfo.Agree_RegisteredABN),
            Common.NewSqlParameter("Agree_PayOwnSuper", newContractInfo.Agree_PayOwnSuper),
            Common.NewSqlParameter("Agree_OwnLiabilityInsurance", newContractInfo.Agree_OwnLiabilityInsurance),
            Common.NewSqlParameter("Agree_ContractorTerms", newContractInfo.Agree_ContractorTerms),
            Common.NewSqlParameter("Agree_IntegralPaySuper", newContractInfo.Agree_IntegralPaySuper),
            Common.NewSqlParameter("Agree_CasualTerms", newContractInfo.Agree_CasualTerms)
          ) ?? 0;

          if (newUserContractId == 0) return false;

          if (!AlbertCoaches.UpdateContractTypeId(trans, coachInfo, newContractInfo.UserContractTypeId)) return false;

          return true;
        });

        return newUserContractId;
      }

      public class ContractInfo {

        public int UserContractId { get; private set; }
        public int UserId { get; private set; }
        public DateTime SubmittedUtc { get; private set; }
        public string PostalAddress1 { get; private set; }
        public string PostalAddress2 { get; private set; }
        public string PostalPostCode { get; private set; }
        public string PostalCountry { get; private set; }
        public DateTime IDDateOfBirth { get; private set; }
        public string IDLicenseOrPassport { get; private set; }
        public string IDCountryOfIssue { get; private set; }
        public string BankAccountName { get; private set; }
        public string BankAccountBSB { get; private set; }
        public string BankAccountNumber { get; private set; }
        public string NextOfKinFullName { get; private set; }
        public string NextOfKinMobileNumber { get; private set; }

        // Contract Type
        public int UserContractTypeId { get; private set; }
        public string UserContractTypeName { get; private set; }
        // Casual
        public bool Agree_IntegralPaySuper { get; private set; }
        public bool Agree_CasualTerms { get; private set; }
        // Contractor
        public string ABNNumber { get; private set; }
        public string BusinessEntityName { get; private set; }
        public bool Agree_RegisteredABN { get; private set; }
        public bool Agree_PayOwnSuper { get; private set; }
        public bool Agree_OwnLiabilityInsurance { get; private set; }
        public bool Agree_ContractorTerms { get; private set; }

        public ContractInfo(
          int userContractId, int userId, int userContractTypeId, string userContractTypeName, DateTime submittedUtc,
          string postalAddress1, string postalAddress2, string postalPostCode, string postalCountry,
          DateTime idDateOfBirth, string idLicenseOrPassport, string idCountryOfIssue,
          string bankAccountName, string bankAccountBSB, string bankAccountNumber,
          string nextOfKinFullName, string nextOfKinMobileNumber,
          string abnNumber, string businessEntityName,
          bool agree_RegisteredABN, bool agree_PayOwnSuper, bool agree_OwnLiabilityInsurance, bool agree_ContractorTerms,
          bool agree_IntegralPaySuper, bool agree_CasualTerms
        ) {
          UserContractId = userContractId;
          UserId = userId;
          UserContractTypeId = userContractTypeId;
          UserContractTypeName = userContractTypeName;
          SubmittedUtc = submittedUtc;
          PostalAddress1 = postalAddress1;
          PostalAddress2 = postalAddress2;
          PostalPostCode = postalPostCode;
          PostalCountry = postalCountry;
          IDDateOfBirth = idDateOfBirth;
          IDLicenseOrPassport = idLicenseOrPassport;
          IDCountryOfIssue = idCountryOfIssue;
          BankAccountName = bankAccountName;
          BankAccountBSB = bankAccountBSB;
          BankAccountNumber = bankAccountNumber;
          NextOfKinFullName = nextOfKinFullName;
          NextOfKinMobileNumber = nextOfKinMobileNumber;
          ABNNumber = abnNumber;
          BusinessEntityName = businessEntityName;
          Agree_RegisteredABN = agree_RegisteredABN;
          Agree_PayOwnSuper = agree_PayOwnSuper;
          Agree_OwnLiabilityInsurance = agree_OwnLiabilityInsurance;
          Agree_ContractorTerms = agree_ContractorTerms;
          Agree_IntegralPaySuper = agree_IntegralPaySuper;
          Agree_CasualTerms = agree_CasualTerms;
        }
      }
    }
  }
}
