<%@ Page Language="C#" AutoEventWireup="true" CodeFile="SurveyQuestions.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.SurveyQuestions" MasterPageFile="~/MasterPages/AdminLTE.Master" %>

<%@ Import Namespace="Integral.Web" %>

<%@ Register TagPrefix="ID" TagName="SurveyForm" Src="~/UserControls/SurveyForm.ascx" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <ID:SurveyForm runat="server" />

</asp:Content>
