<%@ Page Language="C#" AutoEventWireup="true" CodeFile="ParticipantSurvey.aspx.cs"
    Inherits="Integral.Web.PortalSite.Pages_Albert.ParticipantSurvey" MasterPageFile="~/MasterPages/Public.Master" %>

<%@ Import Namespace="Integral.Web" %>

<%@ Register TagPrefix="ID" TagName="SurveyForm" Src="~/UserControls/SurveyForm.ascx" %>

<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="BodyContent">

  <ID:SurveyForm runat="server" />

</asp:Content>
