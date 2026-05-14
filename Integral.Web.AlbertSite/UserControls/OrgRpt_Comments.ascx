<%@ Control Language="C#" AutoEventWireup="true" CodeFile="OrgRpt_Comments.ascx.cs" Inherits="Integral.Web.PortalSite.UserControls.OrgRpt_Comments" %>

<%@ Import Namespace="Integral.Web" %>
<%@ Import Namespace="Integral.Web.PortalSite.Reports" %>

<style>

  .OrgRptComments { }
  .OrgRptComments .previousScore { color: #aaa; }
  .OrgRptComments .nav-tabs { border-bottom: none; border-left: 1px solid #F1F2F4; }
  .OrgRptComments .rowTitle .colQns { border: 1px solid #EEEFF1; border-right: none; padding-top: 12px; height: 42px; padding-right: 15px; }
  .OrgRptComments .rowTitle .colData { border-bottom: 1px solid #EEEFF1; padding-left: 0; }
  .OrgRptComments .rowTitle .nav > li > a,
  .OrgRptComments .rowTitle .nav > li > a:hover { background: #FAFBFB; border-color: transparent; border-top: 1px solid #F1F2F4;
                                                  padding: 0px 30px 0 20px; line-height: 40px; border-left: none; border-radius: 0; }
  .OrgRptComments .rowTitle .nav > li.active > a,
  .OrgRptComments .rowTitle .nav > li.active > a:hover { background: none; }
  .OrgRptComments .rowTitle .nav > li:after { content: "";  background-repeat: no-repeat; background-size: cover;
                                              position: absolute; right: -10px; top: 0; width: 28px; height: 100%; z-index: 1 }
  .OrgRptComments .rowTitle .nav > li:after { background-image: url(/images/htmlreports/OrgRptTabAngle-Middle-RightActive.png); }
  .OrgRptComments .rowTitle .nav > li.active:after { background-image: url(/images/htmlreports/OrgRptTabAngle-Middle-LeftActive.png); }
  .OrgRptComments .rowTitle .nav > li:last-child:after { background-image: url(/images/htmlreports/OrgRptTabAngle-Right-Inactive.png); }
  .OrgRptComments .rowTitle .nav > li.active:last-child:after { background-image: url(/images/htmlreports/OrgRptTabAngle-Right-Active.png); }

  .OrgRptComments .rowBody,
  .OrgRptComments .rowBody > .colData { border-left: 1px solid #F1F2F4; }
  .OrgRptComments .rowBody > .colQns { padding-left: 0; }
  .OrgRptComments .rowBody > .colData { padding: 15px 30px; overflow-y: scroll; height: calc(100vh - 370px); }
  .OrgRptComments .qnLink { padding: 10px; display: flex; align-items: start; text-decoration: none; border-left: 2px solid transparent; margin-right: -15px; border-bottom: 1px solid #F1F2F4; }
  .OrgRptComments .qnLink:hover { text-decoration: none; }
  .OrgRptComments .qnNum { display: inline-block; background: #F5F5FB; padding: 3px 10px; border-radius: 8px; }
  .OrgRptComments .qnText { display: inline-block; width: 200px; margin-left: 20px; vertical-align: top; color: #616E86; line-height: 160%; }
  .OrgRptComments .qnLink.active { border-left: 2px solid #6C66DA; background: #F7F7FB; }
  .OrgRptComments .qnLink.active .qnNum { background: #E7E5F9; }
  .OrgRptComments .qnLink.active .qnText { color: #344564; }

  .OrgRptComments .themeTable { width: 100%; }
  .OrgRptComments .themeTable td { padding: 5px; }
  .OrgRptComments .themeTable tr { }
  .OrgRptComments .themeTable td.colText { border-bottom: 1px solid #F1F2F4; border-right: 1px solid #F1F2F4; }
  .OrgRptComments .themeTable tr:first-child td.colText { color: #344564 }
  .OrgRptComments .themeTable td.colCount { width: 55px; text-align: right; }
  .OrgRptComments .themeTable td.colBar { width: 130px; }
  .OrgRptComments .themeTable td.colBar .bar { display: inline-block; overflow: hidden; height: 15px; background: #6C66DA; width: 0px; }

</style>

<div class="container-fluid">
  <div class="OrgRptComments row">
    <div class="col-md-11">

      <div class="row rowTitle">
        <div class="col-md-3 colQns">Questions</div>
        <div class="col-md-9 colData">
          <ul class="nav nav-tabs">
            <% if (reportData.OpenTextThemesExist) { %>
              <li class="tabMode tabThemes active" data-mode="themes"><a href="#">Themes</a></li>
            <% } %>
            <% if (reportParticipantInfo.CanAccessTextResponses) { %>
              <li class="tabMode tabResponses <%= !reportData.OpenTextThemesExist ? "active" : "" %>" data-mode="responses"><a href="#">Responses</a></li>
            <% } %>
          </ul>
        </div>
      </div>

      <div class="row rowBody">
        <div class="col-md-3 colQns"><%= GetQuestions(@"
<a href=""#"" class=""qnLink [active]"">
  <div class=""qnNum"">[qnNum]</div>
  <div class=""qnText"">[qnText]</div>
</a>
        ") %></div>
        <div class="col-md-9 colData">

        </div>
      </div>

    </div>
  </div>
</div>

<script>

  (function ($) {

    $(document).ready(function () {

      $(".OrgRptComments .colData .nav > li > a").click(function (evt) {
        evt.preventDefault();
        var li = $(this).parent();
        li.addClass("active").siblings().removeClass("active");
        LoadData();
      });

      $(".OrgRptComments .qnLink").click(function (evt) {
        evt.preventDefault();
        link = $(this);
        link.addClass("active").siblings().removeClass("active");
        LoadData();
      });

      LoadData();

    });

    function LoadData() {

      var dataPane = $(".OrgRptComments .rowBody .colData");
      if (dataPane.length != 1) return;

      var qnActive = $(".OrgRptComments .qnLink.active");
      if (qnActive.length != 1) return;

      var qnNum = parseInt(qnActive.text());
      if (qnNum == null || isNaN(qnNum)) return;

      var modeTab = $(".OrgRptComments .tabMode.active");
      if (modeTab.length != 1) return;

      var tabMode = modeTab.data("mode");

      dataPane.html("Loading...");
      GetControlContent("OrgRpt_Comments", "&qn=" + qnNum + "&tabmode=" + tabMode,
        function (html) {
          dataPane.html(html);
        }
      );

    }

  })(jQuery);

</script>

