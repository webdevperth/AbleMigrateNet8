using System;
using System.Collections.Generic;

namespace Integral.Web {

  public partial class DbHelper : HelperBase<DbHelper> {

    public class XeroTaxType {

      private const string TblPfx = "xt";

      private static string TaxTypeId_INPUT = null;
      private static string TaxTypeId_OUTPUT = null;
      private static string TaxTypeId_EXEMPTEXPENSES = null;
      private static string TaxTypeId_EXEMPTOUTPUT = null;
      private static Dictionary<string, XeroTaxTypeInfo> LookupById = new Dictionary<string, XeroTaxTypeInfo>(StringComparer.InvariantCultureIgnoreCase);

      static XeroTaxType() {
        ReadFromDb();
      }

      private static void ReadFromDb() {

        Common.Query("SELECT XeroTaxType, TaxTypeName FROM al_XeroTaxTypes",
          null, dr => {
            var xt = new XeroTaxTypeInfo(dr.GetString("XeroTaxType"), dr.GetString("TaxTypeName"));
            if (xt.XeroTaxType == ConfigHelper.XeroTaxTypeValue[ConfigHelper.XeroTaxType.Input]) {
              if (!TaxTypeId_INPUT.IsNullOrEmpty()) throw new ApplicationException("More than one XeroTaxType 'INPUT'.");
              TaxTypeId_INPUT = xt.XeroTaxType;
            } else if (xt.XeroTaxType == ConfigHelper.XeroTaxTypeValue[ConfigHelper.XeroTaxType.Output]) {
              if (!TaxTypeId_OUTPUT.IsNullOrEmpty()) throw new ApplicationException("More than one XeroTaxType 'OUTPUT'.");
              TaxTypeId_OUTPUT = xt.XeroTaxType;
            } else if (xt.XeroTaxType == ConfigHelper.XeroTaxTypeValue[ConfigHelper.XeroTaxType.ExemptExpenses]) {
              if (!TaxTypeId_EXEMPTEXPENSES.IsNullOrEmpty()) throw new ApplicationException("More than one XeroTaxType 'EXEMPTEXPENSES'.");
              TaxTypeId_EXEMPTEXPENSES = xt.XeroTaxType;
            } else if (xt.XeroTaxType == ConfigHelper.XeroTaxTypeValue[ConfigHelper.XeroTaxType.ExemptOutput]) {
              if (!TaxTypeId_EXEMPTOUTPUT.IsNullOrEmpty()) throw new ApplicationException("More than one XeroTaxType 'EXEMPTOUTPUT'.");
              TaxTypeId_EXEMPTOUTPUT = xt.XeroTaxType;
            }
            LookupById.Add(xt.XeroTaxType, xt);
          });

        if (TaxTypeId_INPUT.IsNullOrEmpty()) throw new ApplicationException("'INPUT' not found in XeroTaxTypes.");
        if (TaxTypeId_OUTPUT.IsNullOrEmpty()) throw new ApplicationException("'OUTPUT' not found in XeroTaxTypes.");
        if (TaxTypeId_EXEMPTEXPENSES.IsNullOrEmpty()) throw new ApplicationException("'EXEMPTEXPENSES' not found in XeroTaxTypes.");
        if (TaxTypeId_EXEMPTOUTPUT.IsNullOrEmpty()) throw new ApplicationException("'EXEMPTOUTPUT' not found in XeroTaxTypes.");

      }

      public static XeroTaxTypeInfo GetXeroTaxTypeInfoOrNull(string xeroTaxTypeId) {
        if (!LookupById.ContainsKey(xeroTaxTypeId)) return null;
        return LookupById[xeroTaxTypeId];
      }

      private static List<XeroTaxTypeInfo> GetXeroTaxTypeList() {
        return new List<XeroTaxTypeInfo>(LookupById.Values);
      }

      public static string GetCostTaxTypeFromGSTApplicable(bool gstApplicable) {
        return gstApplicable ? TaxTypeId_INPUT : TaxTypeId_EXEMPTEXPENSES;
      }
      public static bool? GetGSTApplicableFromCostTaxTypeOrNull(string costTaxType) {
        if (costTaxType.IsNullOrEmpty()) return null;
        return costTaxType.ToLower() == TaxTypeId_INPUT.ToLower();
      }

      public static string GetInvoiceTaxTypeFromGSTApplicable(bool gstApplicable) {
        return gstApplicable ? TaxTypeId_OUTPUT : TaxTypeId_EXEMPTOUTPUT;
      }
      public static bool? GetGSTApplicableFromInvoiceTaxTypeOrNull(string invoiceTaxType) {
        if (invoiceTaxType.IsNullOrEmpty()) return null;
        return invoiceTaxType.ToLower() == TaxTypeId_OUTPUT.ToLower();
      }

      public static string GetQuoteTaxTypeFromGSTApplicable(bool gstApplicable) {
        return gstApplicable ? TaxTypeId_OUTPUT : TaxTypeId_EXEMPTOUTPUT;
      }
      public static bool? GetGSTApplicableFromQuoteTaxTypeOrNull(string quoteTaxType) {
        if (quoteTaxType.IsNullOrEmpty()) return null;
        return quoteTaxType.ToLower() == TaxTypeId_OUTPUT.ToLower();
      }

      public class XeroTaxTypeInfo {

        public string XeroTaxType { get; private set; }
        public string TaxTypeName { get; private set; }

        public XeroTaxTypeInfo(string xeroTaxType, string taxTypeName) {
          this.XeroTaxType = xeroTaxType;
          this.TaxTypeName = taxTypeName;
        }
      }

    }
  }
}

