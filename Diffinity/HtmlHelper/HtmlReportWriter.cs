using ColorCode;
using Diffinity.TableHelper;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using static Diffinity.DbObjectHandler;
using System.Net;



namespace Diffinity.HtmlHelper;

public static class HtmlReportWriter
{
    private static readonly object _colorizerLock = new object();
    private const string CopyIcon = @"<svg viewBox=""0 0 24 24"" width=""20"" height=""20"" fill=""currentColor"" aria-hidden=""true"" class=""icon-copy""><path d=""M16 1H4c-1.1 0-2 .9-2 2v12h2V3h12V1zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c-1.1-.9-2-2-2-2zm0 16H8V7h11v14z""/></svg>";
    private const string CheckIcon = @"<svg viewBox=""0 0 24 24"" width=""20"" height=""20"" fill=""currentColor"" aria-hidden=""true"" class=""icon-check""><path d=""M9 16.2l-3.5-3.5 1.4-1.4L9 13.4l7.1-7.1 1.4 1.4L9 16.2z""/></svg>";
    private const string SmallCopyIcon = @"<svg viewBox=""0 0 24 24"" width=""14"" height=""14"" fill=""currentColor"" aria-hidden=""true"" class=""icon-copy""><path d=""M16 1H4c-1.1 0-2 .9-2 2v12h2V3h12V1zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c-1.1-.9-2-2-2-2zm0 16H8V7h11v14z""/></svg>";
    private const string SmallCheckIcon = @"<svg viewBox=""0 0 24 24"" width=""14"" height=""14"" fill=""currentColor"" aria-hidden=""true"" class=""icon-check""><path d=""M9 16.2l-3.5-3.5 1.4-1.4L9 13.4l7.1-7.1 1.4 1.4L9 16.2z""/></svg>";
    private const string IndexTemplate = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>Diffinity Report</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            max-width: 95%;
            margin: 40px auto;
            padding: 20px;
            background-color: #fff;
            color: #333;
        }
        h1,h2 {
            color: #36454F;
            text-align: center;
            margin-bottom: 10px;
            margin-top: 0;
        }
        h5{
            color: #2C3539;
            text-align: center;
            margin-bottom:10px;
            font-weight: normal;
            margin-top: 0;
        }

        table.conn {
            width: 90%;
            margin: 0 auto 24px auto;
            border-collapse: collapse;
            margin: 20px auto 24px auto;
        }

        table.conn th,
        table.conn td {
            border-bottom: 1px solid #ddd;
            padding: 12px 14px;
            text-align: left;
            white-space: nowrap;
        }
        
        table.conn th {
            background-color: #dedede; 
            color: black;   
            text-align: left;
            font-weight: 600;
            font-size: 1rem;

        }

        
        table.conn td {
            color: #333;               
        }

        table.summary {
            width: 90%;
            border-collapse: collapse;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            margin: 30px auto;
        }

        table.summary th {
            background-color: #dedede; 
            color: black;
            padding: 14px;
            text-align: right;
            font-weight: 600;
            font-size: 1rem;
        }

        table.summary td {
            padding: 12px 14px;
            text-align: right;
            border-bottom: 1px solid #ddd;
            white-space: nowrap;
        }
        table.summary td#left {
            text-align: left;
        }

        table.summary tr:hover {
            background-color: #f5f5f5;
        }

        table.summary a {
            color: #36454F;
            text-decoration: none;
            font-weight: 600;
            padding: 4px 8px;
            display: inline-block;
        }

        table.summary a:hover {
            text-decoration: underline;
            color: #778899;
        }

        .btn {
            display: block;
            width: 220px;
            margin: 20px auto;
            padding: 12px 0;
            background-color: #36454F;
            color: white;
            font-weight: 600;
            text-align: center;
            text-decoration: none;
            border-radius: 6px;
            box-shadow: 0 3px 8px rgba(236, 49, 127, 0.25);
            transition: background-color 0.3s ease;
            font-size: 1rem;
        }

        .btn:hover {
            background-color: #778899;
        }

        .logo {
            display: block;
            margin: 0 auto 20px auto;
            width: 100px;    
            margin: 0 auto 10px auto;
        }

    </style>
</head>
<body>
    {logo}
    <h1>{title}</h1>
    <h5>{Date}</h5>
    <h5>{Duration}</h5>
    {connectionsTable}
    {summaryTable}
    {ignoredIndex}
</body>
</html>
";
    private const string ComparisonTemplate = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>Diffinity Report</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            max-width: 90%;
            margin: 40px auto;
            padding: 20px;
            background-color: #fff;
            color: #333;
        }
        h1 {
            color: #36454F;
            text-align: center;
            margin-bottom: 40px;
        }
        table {
            width: 100%;
            border-collapse: collapse;
        }
        th, td {
            padding: 6px 2px;
            border-bottom: 1px solid #ddd;
            text-align: left;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }
        th {
            background-color: #dedede;
            font-weight: 600;
            font-size: 1rem;
        }
        .match {
            color: green;
            font-weight: 600;
        }
        .diff {
            color: red;
            font-weight: 600;
        }
        a {
            color: #36454F;
            text-decoration: none;
            font-weight: 600;
        }
        a:hover {
            text-decoration: underline;
        }
        .top-nav {
            display: flex;
            justify-content: center;
            gap: 60px; /* controls spacing between links */
            margin-bottom: 40px;
        }
        
        .top-nav a {
            position: relative;
            color: #36454F;
            text-decoration: none;
            font-weight: 600;
            font-size: 1.2rem;
            padding-bottom: 6px;
        }
        
        .top-nav a::after {
            content: '';
            position: absolute;
            left: 0;
            bottom: 0;
            width: 100%;
            height: 3px;
            background-color: #36454F;
            transform: scaleX(0);
            transform-origin: bottom left;
            transition: transform 0.3s ease;
        }
        
        .top-nav a:hover::after {
            transform: scaleX(1);
        }
        .copy-btn {
            margin: 0px 0px 0px 8px;
            background-color: transparent;
            color: #555; 
            border: none;
            font-size : 15px;
            padding: 6px 8px;
            border-radius: 4px;
            cursor: pointer; }
       .copy-btn:hover {
            background-color: #f0f0f0; 
            color: #000; 
       }
       .copy-btn .icon-check {
            display: none;
       }
       .copy-btn.copied .icon-check {
            display: inline-block;
       }
       .copy-btn.copied .icon-copy {
            display: none;
       }
        .return-btn {
            display: block;
            width: 220px;
            margin: 0 auto;
            padding: 12px 0;
            background-color: #36454F;
            color: white;
            font-weight: 600;
            text-align: center;
            text-decoration: none;
            border-radius: 6px;
            box-shadow: 0 3px 8px rgba(236, 49, 127, 0.25);
            transition: background-color 0.3s ease;
            font-size: 1rem;
        }
        .return-btn:hover {
            background-color: #778899;
        }
        .copy-selected {
            float: right;
            margin: 0px 12px 0px 50px;
            background-color: #36454F;
            color: white;
            border: none;
            font-size : 15px;
            padding: 10px 12px;
            border-radius: 4px;
            cursor: pointer;
            box-shadow: 0 2px 6px rgba(236, 49, 127, 0.2);
        }
        .copy-selected:hover {
            background-color: #778899;
        }
        .name-copy-btn {
            margin: 0 0 0 0px;
            background-color: transparent;
            color: #888; 
            border: none;
            font-size: 13px; 
            padding: 2px 4px; 
            border-radius: 3px;
            cursor: pointer;
            vertical-align: middle;
            display: inline-flex;
            align-items: center;
        }
        .name-copy-btn:hover {
            background-color: #f0f0f0; 
            color: #666; 
        }
        .name-copy-btn .icon-check {
            display: none;
        }
        .name-copy-btn.copied .icon-check {
            display: inline-block;
        }
        .name-copy-btn.copied .icon-copy {
            display: none;
        }
        .pick { display:inline-flex; align-items:center; gap:8px; font-size:1rem; }
        .pick a { font-size:1rem; }           /* ensure same size as lone <a> */
        .pick input { margin-right:4px; }      /* tiny space before “View” */
        .copy-Section { display:flex; gap:12px; justify-content:flex-end; margin:10px 0 6px 0; }
        .hdr { display:inline-flex; align-items:center; gap:8px; }

          /* Visually dim a completed row */
          .row-done { background-color:#eee !important; }
          .row-done td { background-color:#eee !important; opacity:.6; }

          /* keep checkbox column neat */
          .done-col { text-align:center; width:80px; }
          .done-col input { vertical-align:middle; }
          .tag {
            display: inline-block;
            background-color: #e3f2fd;
            color: #1976d2;
            padding: 1px 4px;
            margin: 1px 2px;
            border-radius: 8px;
            font-size: 0.65rem;
            font-weight: 500;
            white-space: nowrap;
          }
        table {
            table-layout: auto;
            width: 100%;
        }

        table th:first-child,
        table td:first-child {
            width: 40px;
            text-align: center;
        }

        table th:nth-child(3),
        table td:nth-child(3) {
            width: 15%;
        }

        table th:nth-child(4),
        table td:nth-child(4) {
            width: 15%;
        }

        table th:nth-child(5),
        table td:nth-child(5) {
            width: 15%;
        }

        table th:last-child,
        table td:last-child {
            width: 80px;
            text-align: center;
        }

        .tag-container {
            display: flex;
            flex-wrap: wrap;
            gap: 2px;
            margin-top: 2px;
        }
    </style>
</head>
<body>
    <h1>{MetaData} Comparison Summary</h1>
    <h1>[{source}] vs [{destination}] </h1>
    {nav}
    {NewTable}
    {UnchangedTable}
    {copySection}
<table>
    <tr>
        <th></th>
        <th>{MetaData} Name</th>
        <th> <label class=""hdr""><input type=""checkbox"" id= {selectAllSrc}><span>{source} Original</span></label> </th>
        <th> <label class=""hdr""><input type=""checkbox"" id= {selectAllDst}><span>{destination} Original</span></label> </th>
        <th>Changes</th>
        <th class=""done-col""></th>
    </tr>
";
    private const string IgnoredTemplate = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <titleDiffinity Report</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            max-width: 1000px;
            margin: 40px auto;
            padding: 20px;
            background-color: #fff;
            color: #333;
        }
        h1 {
            color: #36454F;
            text-align: center;
            margin-bottom: 40px;
        }
        table {
            width: 80%;
            border-collapse: collapse;
            margin:auto;
        }
        th, td {
            padding: 12px 16px;
            border-bottom: 1px solid #ddd;
            text-align: left;
            white-space: nowrap;
        }
        th {
            background-color: #dedede;
            font-weight: 600;
            font-size: 1rem;
        }
        .match {
            color: green;
            font-weight: 600;
        }
        .diff {
            color: red;
            font-weight: 600;
        }
        a {
            color: #36454F;
            text-decoration: none;
            font-weight: 600;
        }
        a:hover {
            text-decoration: underline;
        }
        .top-nav {
            display: flex;
            justify-content: center;
            gap: 60px; /* controls spacing between links */
            margin-bottom: 40px;
        }
        
        .top-nav a {
            position: relative;
            color: #36454F;
            text-decoration: none;
            font-weight: 600;
            font-size: 1.2rem;
            padding-bottom: 6px;
        }
       .copy-btn .icon-check {
            display: none;
       }
       .copy-btn.copied .icon-check {
            display: inline-block;
       }
       .copy-btn.copied .icon-copy {
            display: none;
       }
        .top-nav a::after {
            content: '';
            position: absolute;
            left: 0;
            bottom: 0;
            width: 100%;
            height: 3px;
            background-color: #36454F;
            transform: scaleX(0);
            transform-origin: bottom left;
            transition: transform 0.3s ease;
        }
        
        .top-nav a:hover::after {
            transform: scaleX(1);
        }
        .return-btn {
            display: block;
            width: 220px;
            margin: 0 auto;
            padding: 12px 0;
            background-color: #36454F;
            color: white;
            font-weight: 600;
            text-align: center;
            text-decoration: none;
            border-radius: 6px;
            box-shadow: 0 3px 8px rgba(236, 49, 127, 0.25);
            transition: background-color 0.3s ease;
            font-size: 1rem;
        }
        .return-btn:hover {
            background-color: #778899;
        }
    </style>
</head>
<body>
    <h1>Ignored Summary</h1>
    {nav}
    <table>
        <tr>
            <th></th>
            <th>Name</th>
        </tr>
    ";
    private const string BodyTemplate = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>Diffinity Report</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 40px auto;
            width: 90%;
            background-color: #fff;
            color: #222;
            padding: 20px 40px;
            border-radius: 10px;
            box-shadow: 0 4px 10px rgba(0,0,0,0.07);
}
        h1 {
            color: #36454F;
            text-align: center;
            margin-bottom: 40px;
        }
        div {
            background-color: #f9f9f9;
            padding: 20px;
            border-radius: 8px;
            overflow-x: auto;
            font-size: 1rem;
            line-height: 1.5;
            border: 1px solid #ddd;
            white-space: pre-wrap;
            word-wrap: break-word;
            color: #222;
            margin-bottom: 40px;
        }
        table {width: 100%;
            border-collapse: collapse;
            margin-bottom: 40px;
            font-size: 1rem;
            border: 1px solid #ddd;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 3px 8px rgba(0, 0, 0, 0.05);
        }
       
        th, td {padding: 12px 16px;
            text-align: left;
            border-bottom: 1px solid #eee;
            white-space: nowrap;
        }
        
        th {background - color: #36454F;
            color: #36454F;
            font-weight: 600;
            text-transform: uppercase;
            font-size: 1rem;
        }
        
        tr:nth-child(even) {background - color: #f9f9f9;
        }
        
        tr:hover {background - color: #f1f1f1;
        }
        
        td {color: #222;
        }
        .return-btn {
            display: block;
            width: 220px;
            margin: 0 auto;
            padding: 12px 0;
            background-color: #36454F;
            color: white;
            font-weight: 600;
            text-align: center;
            text-decoration: none;
            border-radius: 6px;
            box-shadow: 0 3px 8px rgba(236, 49, 127, 0.25);
            transition: background-color 0.3s ease;
            font-size: 1rem;
        }
        .red {color: red;}
        .copy-btn {
            float: right;
            margin: 0px 12px 0px 50px;
            background-color: transparent;
            color: #555; 
            border: none;
            font-size : 15px;
            padding: 10px 12px;
            border-radius: 4px;
            cursor: pointer;}
        .copy-btn:hover {
            background-color: #f0f0f0; 
            color: #000; 
               }
       .copy-btn .icon-check {
            display: none;
       }
       .copy-btn.copied .icon-check {
            display: inline-block;
       }
       .copy-btn.copied .icon-copy {
            display: none;
       }
        .use{
            color: #36454F;
            font-weight : bold;
            float: left;
            font-size: 20px
        }
        .return-btn:hover {
            background-color: #778899;
        }
        .difference {
            background-color: #fff3b0;  
            color: #000;              
        }
        .source {
            background-color: #d4edda; 
            color: #000;
        }
        .destination {
            background-color: #f8d7da;  
            color: #000;
        }
        .side-by-side {
            width: 100%;
            display: flex;
            gap: 20px;
            flex-wrap: wrap;
        }
        .side-by-side > div {
            flex: 1;
            min-width: 300px;
            margin-bottom: 0;  
        }
        .db-block {
            flex: 1;
            min-width: 300px;
            background: none;
            border: none;
            padding: 0;
            margin-bottom: 40px; 
        }
        .code-scroll {
            height: 400px;      
            overflow-y: auto;   
            overflow-x: auto;    
            margin-top: 10px;
        }
    </style>
  </head>";
    private const string DifferencesTemplate = @"<!DOCTYPE html>
       <html>
       <head>
       <meta charset='utf-8' />
       <title>Diffinity Report</title>
       <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 40px auto;
            max-width: 1600px;
            background-color: #fff;
            color: #222;
            padding: 20px 40px;
            border-radius: 10px;
            box-shadow: 0 4px 10px rgba(0,0,0,0.07);
        }
        h1 {
            color: #36454F;
            text-align: center;
            margin-bottom: 40px;
        }
        .diff-wrapper {
            display: flex;
            gap: 20px;
        }
        .pane {
            width: 48%;
            background-color: #f9f9f9;
            padding: 0;
            border-radius: 8px;
            overflow: hidden;
            height: auto;
            border: 1px solid #ddd;
            box-shadow: 0 3px 8px rgba(0, 0, 0, 0.05);
        }
        .pane h2 {
            margin: 0;
            padding: 12px 16px;
            background-color: #f0f0f0;
            color: black;
            border-radius: 8px 8px 0 0;
            text-align: center;
            font-size: 1rem;
        }
       .code-scroll {
            height: 60vh;
            overflow: auto; 
            width: 100%;
        }
        .code-block {
            display: grid;
            grid-template-columns: 50px 1fr;
            font-size: 0.95rem;
            line-height: 1.4;
            padding: 10px;
            white-space: pre;
        }

         .line-number {
             color: #999;
             text-align: right;
             padding-right: 10px;
             user-select: none;
         }
         .line-text {
              white-space: pre;        
              font-family: Consolas;
              overflow-wrap: break-word;
              width: 100%;
         }
        .copy-btn {
              float: right;
              margin: 3px 12px 0px 50px;
              background-color: transparent;
              color: #555; 
              border: none;
              top: -2px;
              font-size : 15px;
              padding: 10px 12px;
              border-radius: 4px;
              cursor: pointer; }
        .copy-btn:hover {
            background-color: #f0f0f0; 
            color: #000; 
        }
       .copy-btn .icon-check {
            display: none;
       }
       .copy-btn.copied .icon-check {
            display: inline-block;
       }
       .copy-btn.copied .icon-copy {
            display: none;
       }
       .return-btn {
            display: block;
            width: 220px;
            margin: 0 auto;
            padding: 12px 0;
            background-color: #36454F;
            color: white;
            font-weight: 600;
            text-align: center;
            text-decoration: none;
            border-radius: 6px;
            box-shadow: 0 3px 8px rgba(236, 49, 127, 0.25);
            transition: background-color 0.3s ease;
            font-size: 1rem;
        } 
       .return-btn:hover {
            background-color: #778899;
        }
        .inserted { background-color: #c6f6c6; }
        .deleted { background-color: #f6c6c6; }
        .imaginary { background-color: #eee; color: #999; }
        .modified { background-color: #fff3b0; }
        .copy-btn-small.copied .icon-copy {
        display: none;
    }
    .name-copy-btn {
        margin: 0 0 0 8px;
        background-color: transparent;
        color: #888; 
        border: none;
        font-size: 13px; 
        padding: 2px 4px; 
        border-radius: 3px;
        cursor: pointer;
        vertical-align: middle;
        display: inline-flex;
        align-items: center;
    }
    .name-copy-btn:hover {
        background-color: #f0f0f0; 
        color: #666; 
    }
    .name-copy-btn .icon-check {
        display: none;
    }
    .name-copy-btn.copied .icon-check {
        display: inline-block;
    }
    .name-copy-btn.copied .icon-copy {
        display: none;
    }
    </style>
        </head>
        <body>";

    #region Index Report Writer (2dbs)
    /// <summary>
    /// Writes the main index summary HTML page linking to individual reports for procedures, views, and tables.
    /// </summary>
    public static string WriteIndexSummary(DbServer source, DbServer destination,string outputPath, long Duration, string? ignoredIndexPath = null, string? procIndexPath = null, string? viewIndexPath = null, string? tableIndexPath = null, string? udtIndexPath = null, int? procCount = 0, int? viewCount = 0 , int? tableCount = 0, int? udtCount = 0, string? procsCountText = null, string? viewsCountText = null, string? tablesCountText = null, string? udtsCountText = null, string? functionIndexPath = null,int functionCount = 0,string? functionsCountText = null)
    {
        string sourceImagesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HtmlHelper", "images");
        string destImagesPath = Path.Combine(outputPath, "images");

        if (Directory.Exists(sourceImagesPath))
        {
            Directory.CreateDirectory(destImagesPath);
            foreach (string file in Directory.GetFiles(sourceImagesPath))
            {
                string fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(destImagesPath, fileName), true);
            }
        }

        // Extract server and database names from connection strings
        var sourceBuilder = new SqlConnectionStringBuilder(source.connectionString);
        var destinationBuilder = new SqlConnectionStringBuilder(destination.connectionString);

        string sourceServer = sourceBuilder.DataSource;
        string destinationServer = destinationBuilder.DataSource;

        string sourceDatabase = sourceBuilder.InitialCatalog;
        string destinationDatabase = destinationBuilder.InitialCatalog;

        string connectionsTable = $@"
<table class=""conn"">
  <tr>
    <th>Connection</th>
    <th>Server</th>
    <th>Database</th>
  </tr>
  <tr>
    <td>{source.name}</td>
    <td>{sourceServer}</td>
    <td>{sourceDatabase}</td>
  </tr>
  <tr>
    <td>{destination.name}</td>
    <td>{destinationServer}</td>
    <td>{destinationDatabase}</td>
  </tr>
</table>";

        StringBuilder html = new StringBuilder();
        DateTime date = DateTime.Now;
        string Date = date.ToString("MM/dd/yyyy hh:mm tt");
        TimeSpan ts = TimeSpan.FromMilliseconds(Duration);
        string formattedDuration = ts.TotalMinutes >= 1 ? $"{ts.TotalMinutes:F1} minutes" : $"{ts.TotalSeconds:F0} seconds";

        (int newCount, int unchangedCount, int changedCount, int tenantCount) ParseCounts(string countText)
        {
            if (string.IsNullOrWhiteSpace(countText)) return (0, 0, 0, 0);
            countText = countText.Trim('(', ')');
            var parts = countText.Split('/');
            if (parts.Length == 4)
            {
                return (
                    int.TryParse(parts[0], out int n) ? n : 0,
                    int.TryParse(parts[1], out int u) ? u : 0,
                    int.TryParse(parts[2], out int c) ? c : 0,
                    int.TryParse(parts[3], out int t) ? t : 0
                );
            }
            else if (parts.Length == 3)
            {
                return (
                    int.TryParse(parts[0], out int n) ? n : 0,
                    0,
                    int.TryParse(parts[1], out int c) ? c : 0,
                    int.TryParse(parts[2], out int t) ? t : 0
                );
            }

            return (0, 0, 0, 0);
        }
        StringBuilder summaryTable = new StringBuilder();
        summaryTable.AppendLine(@"<table class=""summary"">");
        summaryTable.AppendLine(@"<caption style=""font-size: 1.2rem; font-weight: 600; color: #36454F; padding: 10px; text-align: center;"">Change Log</caption>");
        bool legendHasUnchanged = new[] { procsCountText, viewsCountText, tablesCountText, udtsCountText }
            .Any(t => !string.IsNullOrWhiteSpace(t) && t.Count(ch => ch == '/') == 3);

        int colspan = legendHasUnchanged ? 4 : 3;
        summaryTable.AppendLine($@"
    <tr>
        <th style=""text-align:left;""></th>
        <th colspan=""{colspan}"" style=""text-align:center; border-right: 2px solid #bbb;"">{source.name}</th>
        <th colspan=""{colspan}"" style=""text-align:center;"">{destination.name}</th>
    </tr>
    <tr>
        <th style=""text-align:left;""></th>
        <th style=""text-align:center;"">New</th>
        {(legendHasUnchanged ? @"<th style=""text-align:center;"">Unchanged</th>" : "")}
        <th style=""text-align:center;"">Changed</th>
        <th style=""text-align:center; border-right: 2px solid #bbb;"">Tenant-specific</th>
        <th style=""text-align:center;"">New</th>
        {(legendHasUnchanged ? @"<th style=""text-align:center;"">Unchanged</th>" : "")}
        <th style=""text-align:center;"">Changed</th>
        <th style=""text-align:center;"">Tenant-specific</th>
    </tr>");

        string Cell(int count, string href) =>
            count > 0 ? $@"<a href=""{href}"">{count}</a>" : "";

        void AddRow(string label, string? indexPath, string? countText)
        {
            if (string.IsNullOrWhiteSpace(indexPath)) return;
            var (newC, unchangedC, changedC, tenantC) = ParseCounts(countText);
            summaryTable.AppendLine($@"
    <tr>
        <td id=""left""><a href=""{indexPath}"">{label}</a></td>
        <td style=""text-align:center;"">{Cell(newC, indexPath + "#new")}</td>
        {(legendHasUnchanged ? $@"<td style=""text-align:center;"">{Cell(unchangedC, indexPath + "#unchanged")}</td>" : "")}
        <td style=""text-align:center;"">{Cell(changedC, indexPath + "#changed")}</td>
        <td style=""text-align:center; border-right: 2px solid #bbb;"">{Cell(tenantC, indexPath + "#tenant")}</td>
        <td style=""text-align:center;"">{Cell(newC, indexPath + "#new")}</td>
        {(legendHasUnchanged ? $@"<td style=""text-align:center;"">{Cell(unchangedC, indexPath + "#unchanged")}</td>" : "")}
        <td style=""text-align:center;"">{Cell(changedC, indexPath + "#changed")}</td>
        <td style=""text-align:center;"">{Cell(tenantC, indexPath + "#tenant")}</td>
    </tr>");
        }

        if (udtCount > 0) AddRow("UDTs", udtIndexPath, udtsCountText);
        if (tableCount > 0) AddRow("Tables", tableIndexPath, tablesCountText);
        if (viewCount > 0) AddRow("Views", viewIndexPath, viewsCountText);
        if (procCount > 0) AddRow("Procedures", procIndexPath, procsCountText);
        if (functionCount > 0) AddRow("Functions", functionIndexPath, functionsCountText);

        summaryTable.AppendLine("</table>");

        string ignoredIndex = string.IsNullOrWhiteSpace(ignoredIndexPath)
            ? ""
            : $@"<a href=""{ignoredIndexPath}"" class=""btn"">Ignored</a>";

        html.Append(
            IndexTemplate
              .Replace("{logo}", @"<img src=""images/diffinitylogo.png"" class=""logo"" alt=""Diffinity Logo"" />")
              .Replace("{title}", $"Diffinity Report<br><h2 style=\"color:#36454F;text-align:center;margin-top:4px\">{source.name} vs {destination.name}</h2>")
              .Replace("{connectionsTable}", connectionsTable)
              .Replace("{summaryTable}", summaryTable.ToString())
              .Replace("{ignoredIndex}", ignoredIndex)
              .Replace("{Date}", Date)
              .Replace("{Duration}", formattedDuration)
        );
        string indexPath = Path.Combine(outputPath, $"{source.name}_{destination.name}.html");
        File.WriteAllText(indexPath, html.ToString());
        return indexPath;
    }
    #endregion

    #region Index Report Writer (set of dbs)
    public static string WriteMultiDestinationIndexSummary(
        DbServer source,
        List<DbServer> destinations,
        string outputPath,
        long Duration,
        List<DestinationReportData> destinationReports,
        string? ignoredIndexPath = null)
    {
        string sourceImagesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HtmlHelper", "images");
        string destImagesPath = Path.Combine(outputPath, "images");
        if (Directory.Exists(sourceImagesPath))
        {
            Directory.CreateDirectory(destImagesPath);
            foreach (string file in Directory.GetFiles(sourceImagesPath))
                File.Copy(file, Path.Combine(destImagesPath, Path.GetFileName(file)), true);
        }

        DateTime date = DateTime.Now;
        string Date = date.ToString("MM/dd/yyyy hh:mm tt");
        TimeSpan ts = TimeSpan.FromMilliseconds(Duration);
        string formattedDuration = ts.TotalMinutes >= 1 ? $"{ts.TotalMinutes:F1} minutes" : $"{ts.TotalSeconds:F0} seconds";

        string destNames = string.Join(", ", destinations.Select(d => d.name));
        string title = $"Diffinity Report<br><h2 style=\"color:#36454F;text-align:center;margin-top:4px\">{source.name} vs {destNames}</h2>";

        // Connections table
        var sourceBuilder = new SqlConnectionStringBuilder(source.connectionString);
        StringBuilder connectionsTable = new StringBuilder();
        connectionsTable.AppendLine(@"<table class=""conn""><tr><th>Connection</th><th>Server</th><th>Database</th></tr>");
        connectionsTable.AppendLine($"<tr><td>{source.name}</td><td>{sourceBuilder.DataSource}</td><td>{sourceBuilder.InitialCatalog}</td></tr>");
        foreach (var dest in destinations)
        {
            var db = new SqlConnectionStringBuilder(dest.connectionString);
            connectionsTable.AppendLine($"<tr><td>{dest.name}</td><td>{db.DataSource}</td><td>{db.InitialCatalog}</td></tr>");
        }
        connectionsTable.AppendLine("</table>");

        // Check if any destination has unchanged counts
        bool legendHasUnchanged = destinationReports
            .SelectMany(d => new[] { d.ProcsCountText, d.ViewsCountText, d.TablesCountText, d.UdtsCountText })
            .Any(t => !string.IsNullOrWhiteSpace(t) && t.Count(ch => ch == '/') == 3);

        int colspan = legendHasUnchanged ? 4 : 3;

        // Build summary table
        StringBuilder summaryTable = new StringBuilder();
        summaryTable.AppendLine(@"<table class=""summary"">");
        summaryTable.AppendLine(@"<caption style=""font-size: 1.2rem; font-weight: 600; color: #36454F; padding: 10px; text-align: center;"">Change Log</caption>");

        // Header row 1 — destination names
        summaryTable.Append(@"<tr><th style=""text-align:left;""></th>");
        for (int i = 0; i < destinations.Count; i++)
        {
            string borderStyle = i < destinations.Count - 1 ? "border-right: 2px solid #bbb;" : "";
            summaryTable.Append($@"<th colspan=""{colspan}"" style=""text-align:center; {borderStyle}"">{source.name} vs {destinations[i].name}</th>");
        }
        summaryTable.AppendLine("</tr>");

        // Header row 2 — New / (Unchanged) / Changed / Tenant-specific repeated per destination
        summaryTable.Append(@"<tr><th style=""text-align:left;""></th>");
        for (int i = 0; i < destinations.Count; i++)
        {
            string borderStyle = i < destinations.Count - 1 ? "border-right: 2px solid #bbb;" : "";
            summaryTable.Append(@"<th style=""text-align:center;"">New</th>");
            if (legendHasUnchanged)
                summaryTable.Append(@"<th style=""text-align:center;"">Unchanged</th>");
            summaryTable.Append(@"<th style=""text-align:center;"">Changed</th>");
            summaryTable.Append($@"<th style=""text-align:center; {borderStyle}"">Tenant-specific</th>");
        }
        summaryTable.AppendLine("</tr>");

        string Cell(int count, string href) =>
            count > 0 ? $@"<a href=""{href}"">{count}</a>" : "";

        (int newC, int unchangedC, int changedC, int tenantC) ParseCounts(string countText)
        {
            if (string.IsNullOrWhiteSpace(countText)) return (0, 0, 0, 0);
            countText = countText.Trim('(', ')');
            var parts = countText.Split('/');
            if (parts.Length == 4)
                return (int.TryParse(parts[0], out int n) ? n : 0, int.TryParse(parts[1], out int u) ? u : 0, int.TryParse(parts[2], out int c) ? c : 0, int.TryParse(parts[3], out int t) ? t : 0);
            else if (parts.Length == 3)
                return (int.TryParse(parts[0], out int n) ? n : 0, 0, int.TryParse(parts[1], out int c) ? c : 0, int.TryParse(parts[2], out int t) ? t : 0);
            return (0, 0, 0, 0);
        }

        // Data rows — one per object type
        var objectTypes = new (string Label, Func<DestinationReportData, (int Count, string? Path, string? CountText)> Getter)[]
                {
            ("UDTs",       d => (d.UdtCount,      d.UdtIndexPath,      d.UdtsCountText)),
            ("Tables",     d => (d.TableCount,     d.TableIndexPath,    d.TablesCountText)),
            ("Views",      d => (d.ViewCount,      d.ViewIndexPath,     d.ViewsCountText)),
            ("Procedures", d => (d.ProcCount,      d.ProcIndexPath,     d.ProcsCountText)),
            ("Functions",  d => (d.FunctionCount,  d.FunctionIndexPath, d.FunctionsCountText)),
                };

        foreach (var (label, getter) in objectTypes)
        {
            bool anyHasData = destinationReports.Any(d => { var (count, path, _) = getter(d); return count > 0 && !string.IsNullOrWhiteSpace(path); });
            if (!anyHasData) continue;

            summaryTable.Append($@"<tr><td id=""left"">{label}</td>");
            for (int i = 0; i < destinationReports.Count; i++)
            {
                var (count, objIndexPath, countText) = getter(destinationReports[i]);
                string borderStyle = i < destinationReports.Count - 1 ? "border-right: 2px solid #bbb;" : "";
                var (newC, unchangedC, changedC, tenantC) = ParseCounts(countText);
                summaryTable.Append($@"<td style=""text-align:center;"">{Cell(newC, $"{objIndexPath}#new")}</td>");
                if (legendHasUnchanged)
                    summaryTable.Append($@"<td style=""text-align:center;"">{Cell(unchangedC, $"{objIndexPath}#unchanged")}</td>");
                summaryTable.Append($@"<td style=""text-align:center;"">{Cell(changedC, $"{objIndexPath}#changed")}</td>");
                summaryTable.Append($@"<td style=""text-align:center; {borderStyle}"">{Cell(tenantC, $"{objIndexPath}#tenant")}</td>");
            }
            summaryTable.AppendLine("</tr>");
        }

        summaryTable.AppendLine("</table>");

        string ignoredIndex = string.IsNullOrWhiteSpace(ignoredIndexPath) ? "" : $@"<a href=""{ignoredIndexPath}"" class=""btn"">Ignored</a>";

        string html = IndexTemplate
            .Replace("{logo}", @"<img src=""images/diffinitylogo.png"" class=""logo"" alt=""Diffinity Logo"" />")
            .Replace("{title}", title)
            .Replace("{connectionsTable}", connectionsTable.ToString())
            .Replace("{summaryTable}", summaryTable.ToString())
            .Replace("{ignoredIndex}", ignoredIndex)
            .Replace("{Date}", Date)
            .Replace("{Duration}", formattedDuration);

        string indexPath = Path.Combine(outputPath, $"{source.name}_vs_all.html");
        File.WriteAllText(indexPath, html);
        return indexPath;
    }
    #endregion

    #region Summary Report Writer
    /// <summary>
    /// Writes a detailed summary report comparing objects (procedures, views, tables) between source and destination.
    /// </summary>
    public static (string html, string countObjects) WriteSummaryReport(DbServer sourceServer, DbServer destinationServer, string summaryPath, List<dbObjectResult> results, DbObjectFilter filter, Run run, bool isIgnoredEmpty, string ignoredCount, Dictionary<string, string> tagColors, string? overrideReturnPage = null)
    {
        if (results == null || results.Count == 0)
        {
            string emptyCount = filter == DbObjectFilter.ShowUnchanged ? "(0/0/0/0)" : "(0/0/0)";
            return (string.Empty, emptyCount);
        }

        results = results.OrderBy(r => r.schema).ThenBy(r => r.Name).ToList();
        StringBuilder html = new();
        var result = results[0];
        string returnPage = overrideReturnPage ?? $"../{sourceServer.name}_{destinationServer.name}.html";
        html.Append(ComparisonTemplate.Replace("{source}", sourceServer.name).Replace("{destination}", destinationServer.name).Replace("{MetaData}", result.Type).Replace("{nav}", BuildNav(run, isIgnoredEmpty, ignoredCount)).Replace("{selectAllSrc}", "chk-src-all").Replace("{selectAllDst}", "chk-dst-all"));
        html.AppendLine(@"
        <script>
          const STORE = sessionStorage; 

          function toggleRow(cb){
            const tr = cb.closest('tr');
            tr.classList.toggle('row-done', cb.checked);
            if (!cb.dataset.key) return;
            STORE.setItem(cb.dataset.key, cb.checked ? '1' : '0');
          }

          function restoreAll(){
            document.querySelectorAll('input.mark-done').forEach(cb => {
              const key = cb.dataset.key;
              if (!key) return;
              const v = STORE.getItem(key);
              if (v === '1') {
                cb.checked = true;
                cb.closest('tr')?.classList.add('row-done');
              }
            });
          }

          // Function for copying object names
          function copyName(button) {
            const container = button.closest('td');
            const codeBlock = container.querySelector('.copy-target');
            const text = codeBlock?.innerText.trim();
            
            if (!text) {
              alert('Nothing to copy!');
              return;
            }
            
            navigator.clipboard.writeText(text).then(() => {
              button.classList.add('copied');
              const key = button.dataset.key;
              if (key) {
                STORE.setItem(key, '1');
              }
            }).catch(err => {
              console.error('Copy failed:', err);
              alert('Failed to copy!');
            });
          }

          // Restore copied name states
          function restoreNameCopiedStates() {
            document.querySelectorAll('.name-copy-btn').forEach(btn => {
              const key = btn.dataset.key;
              if (key && STORE.getItem(key) === '1') {
                btn.classList.add('copied');
              }
            });
          }

          document.addEventListener('DOMContentLoaded', function() {
            restoreAll();
            restoreNameCopiedStates();
          });
        </script>"

);

        #region 1-Create the new table
        var newObjects = results.Where(r => r.IsDestinationEmpty && !r.IsTenantSpecific).ToList();
        int newObjectsCount = newObjects.Count();
        if (newObjects.Any())
        {
            StringBuilder newTable = new StringBuilder();
            newTable.AppendLine($@"
                <div class=""copy-Section"" style=""display:flex;justify-content:flex-end;margin:10px 0 6px 0;"">
                    <button id=""copyNew"" class=""copy-selected"">Copy Selected</button>
                </div>
                <h2 id=""new"" style=""color: #2C3539;"">New {result.Type}s in {sourceServer.name} ({newObjectsCount}) : </h2>
                <table>
                <tr>
                    <th></th>
                    <th>{result.Type} Name</th>
                    <th><label class=""hdr""><input type=""checkbox"" id=""chk-new-all""><span>Select All</span></label></th>
                    <th></th>
                    <th class=""done-col""></th>
                </tr>");

            int newCount = 1;
            foreach (var item in newObjects)
            {

                string copyPayload = item.Type == "Table"
                    ? CreateTableScript(item.schema, item.Name, item.SourceTableInfo, item.SourceForeignKeys, item.SourceIndexes)
                    : item.SourceBody;

                string sourceLink = $@"<a href=""{item.SourceFile}"">View</a>";
                string checkboxCell = $@"<label class='pick'><input type='checkbox' class='sel-new'></label>";
                string copyButton = $@"<button class=""copy-btn"" onclick=""copyPane(this)"">{CopyIcon}{CheckIcon}</button><br>
                <span class=""copy-target copy-new"" style=""display:none;"">{copyPayload}</span>";
                string copyNameButton = $@"<button class=""name-copy-btn"" onclick=""copyName(this)"" data-key=""name-copied|{item.schema}.{item.Name}"">{SmallCopyIcon}{SmallCheckIcon}</button><span class=""copy-target"" style=""display:none;"">{item.schema}.{item.Name}</span>"; string tagsHtml = item.Tags.Any()
                    ? $@"<div class=""tag-container"">{string.Join("", item.Tags.Select(tag =>
                    {
                        string bgColor = tagColors.ContainsKey(tag) ? tagColors[tag] : "#e3f2fd";
                        string textColor = GetTextColor(bgColor);
                        return $@"<span class=""tag"" style=""background-color: {bgColor}; color: {textColor};"">{tag}</span>";
                    }))}</div>"
                    : "";
                newTable.Append($@"<tr data-key=""new|{result.Type}|{item.schema}.{item.Name}"">
                        <td>{newCount}</td>
                        <td>
                            <div>{item.schema}.{item.Name}{copyNameButton}</div>
                            {tagsHtml}
                        </td>
                        <td>{checkboxCell} {sourceLink}</td>
                        <td>{copyButton}</td>
                        <td class=""done-col"">
                                    <input type=""checkbox""
                                           class=""mark-done""
                                           onchange=""toggleRow(this)""
                                           data-key=""new|{result.Type}|{item.schema}.{item.Name}"">
                                </td>
                                </tr>");
                newCount++;
            }

            newTable.Append("</table><br><br>");
            newTable.AppendLine(
                @"<script>
                    function copyPane(button) {
                        const container = button.closest('tr');
                        const codeBlock = container.querySelector('.copy-target');
                        const text = codeBlock?.innerText.trim();

                        navigator.clipboard.writeText(text).then(() => {
                            button.classList.add('copied'); 
                            setTimeout(() => button.classList.remove('copied'), 2000); 
                        }).catch(err => {
                            console.error('Copy failed:', err);
                            alert('Failed to copy!');
                        });
                     }

                const newAll = document.getElementById('chk-new-all');
                if (newAll) {
                    newAll.addEventListener('change', () => {
                        document.querySelectorAll('.sel-new').forEach(cb => cb.checked = newAll.checked);
                    });
                }

                const btnNew = document.getElementById('copyNew');
                if (btnNew) {
                    btnNew.addEventListener('click', () => {
                        const rows = Array.from(document.querySelectorAll('table tr')).slice(1);
                        const parts = [];
        
                        rows.forEach(tr => {
                            const newCb = tr.querySelector('.sel-new');
                            if (newCb && newCb.checked) {
                                const span = tr.querySelector('.copy-new');
                                const txt = (span && span.textContent ? span.textContent : '').trim();
                                if (txt) parts.push(txt);
                            }
                        });
        
                        const blob = parts.length ? parts.join('\nGO\n\n') + '\nGO' : '';
                        if (!blob) {
                            alert('No items selected.');
                            return;
                        }
        
                        navigator.clipboard.writeText(blob).then(() => {
                            const old = btnNew.textContent;
                            btnNew.textContent = 'Copied!';
                            setTimeout(() => btnNew.textContent = old, 1200);
                        }).catch(err => {
                            console.error('Copy failed:', err);
                            alert('Failed to copy!');
                        });
                    });
                }
                </script>"
            );
            html.Replace("{NewTable}", newTable.ToString());

        }
        else
        {
            html.Replace("{NewTable}", "");
        }
        #endregion

        #region 2-Create the Unchanged Objects Table
        var unchangedObjects = results.Where(r => r.IsEqual && filter == DbObjectFilter.ShowUnchanged && !r.IsDestinationEmpty && !r.IsTenantSpecific).ToList();
        int equalCount = unchangedObjects.Count();
        if (unchangedObjects.Any())
        {
            StringBuilder unchangedTable = new StringBuilder();
            unchangedTable.AppendLine($@"<h2 id=""unchanged"" style=""color: #2C3539;"">Unchanged {result.Type}s ({equalCount}) : </h2>
                <table>
                <tr>
                    <th></th>
                    <th>{result.Type} Name</th>
                    <th></th>
                    <th></th>
                    <th class=""done-col""></th>
                </tr>");

            int newCount = 1;
            foreach (var item in unchangedObjects)
            {
                string copyPayload = item.Type == "Table"
                    ? CreateTableScript(item.schema, item.Name, item.SourceTableInfo, item.SourceForeignKeys, item.SourceIndexes)
                    : item.SourceBody;

                string copyNameButton = $@"<button class=""name-copy-btn"" onclick=""copyName(this)"" data-key=""name-copied|{item.schema}.{item.Name}"">{SmallCopyIcon}{SmallCheckIcon}</button><span class=""copy-target"" style=""display:none;"">{item.schema}.{item.Name}</span>";
                string sourceLink = $@"<a href=""{item.SourceFile}"">View</a";
                string copyButton = $@"<button class=""copy-btn"" onclick=""copyPane(this)"">{CopyIcon}{CheckIcon}</button><br>
                <span class=""copy-target"" style=""display:none;"">{copyPayload}</span>";
                string tagsHtml = item.Tags.Any()
                    ? $@"<div class=""tag-container"">{string.Join("", item.Tags.Select(tag =>
                    {
                        string bgColor = tagColors.ContainsKey(tag) ? tagColors[tag] : "#e3f2fd";
                        string textColor = GetTextColor(bgColor);
                        return $@"<span class=""tag"" style=""background-color: {bgColor}; color: {textColor};"">{tag}</span>";
                    }))}</div>"
                    : "";
                unchangedTable.Append($@"<tr data-key=""Unchanged|{result.Type}|{item.schema}.{item.Name}"">
                                <td>{newCount}</td>
                                <td>
                                    <div>{item.schema}.{item.Name}{copyNameButton}</div>
                                    {tagsHtml}
                                </td>
                                <td>{sourceLink}</td>
                                <td>{copyButton}</td>
                                <td class=""done-col"">
                                    <input type=""checkbox""
                                           class=""mark-done""
                                           onchange=""toggleRow(this)""
                                           data-key=""Unchanged|{result.Type}|{item.schema}.{item.Name}"">
                                </td>
                                </tr>");
                newCount++;
            }

            unchangedTable.Append("</table><br><br>");
            unchangedTable.AppendLine(
                @"<script>
                    function copyPane(button) {
                        const container = button.closest('tr');
                        const codeBlock = container.querySelector('.copy-target');
                        const text = codeBlock?.innerText.trim();

                        navigator.clipboard.writeText(text).then(() => {
                            button.classList.add('copied'); 
                            setTimeout(() => button.classList.remove('copied'), 2000); 
                        }).catch(err => {
                            console.error('Copy failed:', err);
                            alert('Failed to copy!');
                        });
                     }
                </script>"
            );
            html.Replace("{UnchangedTable}", unchangedTable.ToString());

        }
        else
        {
            html.Replace("{UnchangedTable}", "");
        }


        #endregion

        #region 3-Create the Changed Objects Table
        var changedObjects = results.Where(r => !r.IsDestinationEmpty && !r.IsEqual && !r.IsTenantSpecific).ToList();
        int notEqualCount = changedObjects.Count();
        string copySection = changedObjects.Any() ? $@"<div class=""copy-Section"" style=""display:flex;justify-content:flex-end;margin:10px 0 6px 0;"">
        <button id=""copyAll"" class=""copy-selected"">Copy Selected</button>
      </div>"
           : string.Empty;
        html.Replace("{copySection}", copySection);
        int Number = 1;
        html.AppendLine($@"<h2 id=""changed"" style = ""color: #2C3539;"">Changed {result.Type}s ({notEqualCount}) :</h2>");
        foreach (var item in changedObjects)
        {
            // For tables: generate ALTER scripts for comparison
            // For procs/views: use body as-is
            string sourceCopy = item.SourceBody;
            string destCopy = item.DestinationBody;
            string copyNameButton = $@"<button class=""name-copy-btn"" onclick=""copyName(this)"" data-key=""name-copied|{item.schema}.{item.Name}"">{SmallCopyIcon}{SmallCheckIcon}</button><span class=""copy-target"" style=""display:none;"">{item.schema}.{item.Name}</span>";
            string tagsHtml = item.Tags.Any()
                ? $@"<div class=""tag-container"">{string.Join("", item.Tags.Select(tag =>
                {
                    string bgColor = tagColors.ContainsKey(tag) ? tagColors[tag] : "#e3f2fd";
                    string textColor = GetTextColor(bgColor);
                    return $@"<span class=""tag"" style=""background-color: {bgColor}; color: {textColor};"">{tag}</span>";
                }))}</div>"
                : "";
            if (item.Type == "Table")
            {
                // Source copy button gets script to alter the SOURCE table (make it match destination)
                sourceCopy = CreateAlterTableScript(item.schema, item.Name, item.SourceTableInfo, item.DestinationTableInfo, item.SourceForeignKeys, item.DestinationForeignKeys, item.SourceIndexes, item.DestinationIndexes);

                // Destination copy button gets script to alter the DESTINATION table (make it match source)
                destCopy = CreateAlterTableScript(item.schema, item.Name, item.DestinationTableInfo, item.SourceTableInfo, item.DestinationForeignKeys, item.SourceForeignKeys, item.DestinationIndexes, item.SourceIndexes);
            }

            // Prepare file links
            string sourceColumn = item.SourceFile != null ? $@"<label class='pick'>
    <input type='checkbox' class='sel-src'> <a href=""{item.SourceFile}"">View</a></label>
    <button class=""copy-btn"" onclick=""copyPane(this)"">{CopyIcon}{CheckIcon}</button>
    <span class=""copy-target copy-src"" style=""display:none;"">{sourceCopy}</span>" : "—";

            string destinationColumn = item.DestinationFile != null ? $@"<label class='pick'>
    <input type='checkbox' class='sel-dst'><a href=""{item.DestinationFile}"">View</a></label>
    <button class=""copy-btn"" onclick=""copyPane(this)"">{CopyIcon}{CheckIcon}</button>
    <span class=""copy-target copy-dst"" style=""display:none;"">{destCopy}</span>" : "—";

            string differencesColumn = item.DifferencesFile != null ? $@"<a href=""{item.DifferencesFile}"">View</a>" : "—";
            string newColumn = item.NewFile != null ? $@"<a href=""{item.NewFile}"">View</a>" : "—";


            html.Append($@"<tr data-key=""changed|{result.Type}|{item.schema}.{item.Name}"">
                <td>{Number}</td>
                <td>
                    <div>{item.schema}.{item.Name}{copyNameButton}</div>
                    {tagsHtml}
                </td>
                <td>{sourceColumn}</td>
                <td>{destinationColumn}</td>
                <td>{differencesColumn}</td>
                <td class=""done-col"">
                    <input type=""checkbox"" class=""mark-done""
                           onchange=""toggleRow(this)""
                           data-key=""changed|{result.Type}|{item.schema}.{item.Name}"">
                </td>
                </tr>");
            Number++;


        }
        html.AppendLine(
        @"<script>
        function copyPane(button) {
          const codeBlock = button.parentElement.querySelector('.copy-target');
          const text = codeBlock?.innerText.trim();
          if (!text) {
            alert('Nothing to copy!');
            return;
          }
          navigator.clipboard.writeText(text).then(() => {
            button.classList.add('copied');
            setTimeout(() => button.classList.remove('copied'), 2000);
          }).catch(err => {
            console.error('Copy failed:', err);
            alert('Failed to copy!');
          });
        }
        </script>
        <script>
          (function() {
            // per-column select-all stays the same...
            const srcAll = document.getElementById('chk-src-all');
            const dstAll = document.getElementById('chk-dst-all');
            if (srcAll) {
              srcAll.addEventListener('change', () => {
                document.querySelectorAll('.sel-src').forEach(cb => cb.checked = srcAll.checked);
              });
            }
            if (dstAll) {
              dstAll.addEventListener('change', () => {
                document.querySelectorAll('.sel-dst').forEach(cb => cb.checked = dstAll.checked);
              });
            }

            function getText(el) {
              // Prefer textContent; trim trailing/leading whitespace
              return (el && el.textContent ? el.textContent : '').trim();
            }

            function collectAll() {
              const rows = Array.from(document.querySelectorAll('table tr')).slice(1); // skip header
              const parts = [];

              rows.forEach(tr => {
                const srcCb = tr.querySelector('.sel-src');
                const dstCb = tr.querySelector('.sel-dst');

                if (srcCb && srcCb.checked) {
                  const span = tr.querySelector('.copy-src');
                  const txt = getText(span);
                  if (txt) parts.push(txt);
                }
                if (dstCb && dstCb.checked) {
                  const span = tr.querySelector('.copy-dst');
                  const txt = getText(span);
                  if (txt) parts.push(txt);
                }
              });

              // Join with GO batches, only if we actually have parts
              return parts.length ? parts.join('\nGO\n\n') + '\nGO' : '';
            }

            const btn = document.getElementById('copyAll');
            if (btn) {
              btn.addEventListener('click', () => {
                const blob = collectAll();
                if (!blob) {
                  alert('No items selected.');
                  return;
                }
                navigator.clipboard.writeText(blob).then(() => {
                  const old = btn.textContent;
                  btn.textContent = 'Copied!';
                  setTimeout(() => btn.textContent = old, 1200);
                }).catch(err => {
                  console.error('Copy failed:', err);
                  alert('Failed to copy!');
                });
              });
            }
          })();
        </script>"
);
        html.Append($@" </table>
                       <br>
                       {{copytnt}}");
        #endregion

        #region 4-Tenant-Specific Table
        var tenantSpecific = results.Where(r => r.IsTenantSpecific).ToList();
        int tenantCount = tenantSpecific.Count();
        string copytenant = tenantSpecific.Any() ? $@"<div class=""copy-Section"" style=""display:flex;justify-content:flex-end;margin:10px 0 6px 0;"">
        <button id=""copytnt"" class=""copy-selected"">Copy Selected</button>
      </div>"
           : string.Empty;
        html.Replace("{copytnt}", copytenant);

        if (tenantSpecific.Any())
        {
            html.AppendLine($@"<br><h2 id=""tenant"" style=""color: #2C3539;"">Tenant Specific {result.Type}s ({tenantCount}) :</h2>"); 
            html.AppendLine(@"
        <table>
          <tr>
            <th></th>
            <th>Name</th>
            <th><label class='hdr'><input type='checkbox' id='chk-tnt-src'><span>" + sourceServer.name + @" Original</span></label></th>
            <th><label class='hdr'><input type='checkbox' id='chk-tnt-dst'><span>" + destinationServer.name + @" Original</span></label></th>
            <th class='done-col'></th>
          </tr>");

            int tsNum = 1;
            foreach (var item in tenantSpecific)
            {
                // Build the copy payloads per type
                string sourceCopy = item.SourceBody;
                string destCopy = item.DestinationBody;

                if (item.Type == "Table")
                {
                    sourceCopy = CreateAlterTableScript(item.schema, item.Name, item.DestinationTableInfo, item.SourceTableInfo, item.DestinationForeignKeys, item.SourceForeignKeys, item.DestinationIndexes, item.SourceIndexes);
                    destCopy = CreateAlterTableScript(item.schema, item.Name, item.SourceTableInfo, item.DestinationTableInfo, item.SourceForeignKeys, item.DestinationForeignKeys, item.SourceIndexes, item.DestinationIndexes);
                }

                string copyNameButton = $@"<button class=""name-copy-btn"" onclick=""copyName(this)"" data-key=""name-copied|{item.schema}.{item.Name}"">{SmallCopyIcon}{SmallCheckIcon}</button><span class=""copy-target"" style=""display:none;"">{item.schema}.{item.Name}</span>";
                string sourceColumn = !string.IsNullOrWhiteSpace(item.SourceFile) ? $@"<label class='pick'>
              <input type='checkbox' class='tnt-src'> <a href=""{item.SourceFile}"">View</a></label>
              <button class=""copy-btn"" onclick=""copyPane(this)"">{CopyIcon}{CheckIcon}</button>
              <span class=""copy-target copy-src"" style=""display:none;"">{sourceCopy}</span>" : "—";

                string tagsHtml = item.Tags.Any()
                    ? $@"<div class=""tag-container"">{string.Join("", item.Tags.Select(tag =>
                    {
                        string bgColor = tagColors.ContainsKey(tag) ? tagColors[tag] : "#e3f2fd";
                        string textColor = GetTextColor(bgColor);
                        return $@"<span class=""tag"" style=""background-color: {bgColor}; color: {textColor};"">{tag}</span>";
                    }))}</div>"
                    : "";
                string destinationColumn = !string.IsNullOrWhiteSpace(item.DestinationFile) ? $@"<label class='pick'>
              <input type='checkbox' class='tnt-dst'><a href=""{item.DestinationFile}"">View</a></label>
              <button class=""copy-btn"" onclick=""copyPane(this)"">{CopyIcon}{CheckIcon}</button>
              <span class=""copy-target copy-dst"" style=""display:none;"">{destCopy}</span>" : "—";

                html.Append($@"
          <tr data-key=""tenant|{item.Type}|{item.schema}.{item.Name}"">
            <td>{tsNum}</td>
            <td>
                <div>{item.schema}.{item.Name}{copyNameButton}</div>
                {tagsHtml}
            </td>
            <td>{sourceColumn}</td>
            <td>{destinationColumn}</td>
            <td class=""done-col"">
              <input type=""checkbox""
                     class=""mark-done""
                     onchange=""toggleRow(this)""
                     data-key=""tenant|{item.Type}|{item.schema}.{item.Name}"">
            </td>
          </tr>");
                tsNum++;
            }

            html.AppendLine(@"
        </table>
        <script>
          (function() {
            const srcAll = document.getElementById('chk-tnt-src');
            const dstAll = document.getElementById('chk-tnt-dst');

            if (srcAll) {
              srcAll.addEventListener('change', () => {
                document.querySelectorAll('.tnt-src').forEach(cb => cb.checked = srcAll.checked);
              });
            }
            if (dstAll) {
              dstAll.addEventListener('change', () => {
                document.querySelectorAll('.tnt-dst').forEach(cb => cb.checked = dstAll.checked);
              });
            }

            function getText(el) { return (el && el.textContent ? el.textContent : '').trim(); }

            function collectAll() {
              const section = document.getElementById('chk-tnt-src')?.closest('table')?.parentElement || document.body;
              const rows = Array.from(section.querySelectorAll('table tr')).slice(1);
              const parts = [];

              rows.forEach(tr => {
                const srcCb = tr.querySelector('.tnt-src');
                const dstCb = tr.querySelector('.tnt-dst');

                if (srcCb && srcCb.checked) {
                  const span = tr.querySelector('.copy-src');
                  const txt = getText(span);
                  if (txt) parts.push(txt);
                }
                if (dstCb && dstCb.checked) {
                  const span = tr.querySelector('.copy-dst');
                  const txt = getText(span);
                  if (txt) parts.push(txt);
                }
              });

              return parts.length ? parts.join('\nGO\n\n') + '\nGO' : '';
            }

            const btn = document.getElementById('copytnt');
            if (btn) {
              btn.addEventListener('click', () => {
                const blob = collectAll();
                if (!blob) { alert('No items selected.'); return; }
                navigator.clipboard.writeText(blob).then(() => {
                  const old = btn.textContent;
                  btn.textContent = 'Copied!';
                  setTimeout(() => btn.textContent = old, 1200);
                }).catch(err => {
                  console.error('Copy failed:', err);
                  alert('Failed to copy!');
                });
              });
            }
          })();
        </script>");
        }
        html.Append($@" </table>
                       <br>");
        #endregion

        #region 5-Update counts in the nav bar
        string countObjects = filter == DbObjectFilter.ShowUnchanged ? $"({newObjectsCount}/{equalCount}/{notEqualCount}/{tenantCount})" : $"({newObjectsCount}/{notEqualCount}/{tenantCount})";
        #endregion
        html.Append($@"
        <a href=""{returnPage}"" class=""return-btn"">Return to Index</a>
        </body>
        </html>");

        return (html.ToString(), countObjects);
    }
    #endregion

    #region Ignored Report Writer
    public static DbComparer.summaryReportDto WriteIgnoredReport(string outputFolder, HashSet<string> ignoredObjects, Run run, DbServer source, DbServer destination, string? overrideReturnPage = null)
    {
        #region 1- Setup folder structure for reports
        Directory.CreateDirectory(outputFolder);
        string ignoredFolderPath = Path.Combine(outputFolder, "Ignored");
        Directory.CreateDirectory(ignoredFolderPath);
        #endregion

        StringBuilder html = new();
        string returnPage = overrideReturnPage ?? $"../{source.name}_{destination.name}.html";
        string ignoredCount = ignoredObjects.Count().ToString();
        html.Append(IgnoredTemplate.Replace("{nav}", BuildNav(run, false, ignoredCount)));

        #region Create the Ignored Table
        int Number = 1;
        html.AppendLine($@"<h2 style = ""color: #2C3539; margin-left:100px"">Ignored Objects :</h2>");
        foreach (var item in ignoredObjects)
        {
            html.Append($@"<tr>
                    <td>{Number}</td>
                    <td>{item}</td>
                     </tr>");
            Number++;
        }
        html.Append($@"</table>
                       <br>
                       <a href=""{returnPage}"" class=""return-btn"">Return to Index</a>
                       </body>
                       </html>");

        return new DbComparer.summaryReportDto
        {
            path = "Ignored/index.html",
            fullPath = Path.Combine(ignoredFolderPath, "index.html"),
            html = html.ToString()
        };
        #endregion
    }
    #endregion

    #region Individual Procedure Body Writer
    /// <summary>
    /// Writes the HTML page showing the body of a single procedure/view/table, with copy functionality.
    /// </summary>
    public static void WriteBodyHtml(string filePath, string title, string body, string returnPage, string ? createTableScript=null)
    {
        StringBuilder html = new StringBuilder();
        html.AppendLine(BodyTemplate.Replace("{title}", title));
        string coloredCode = HighlightSql(body);
        string toCopy = coloredCode;
        //string escapedBody = EscapeHtml(body);

        if (title.Contains("Table"))
        {
            coloredCode = body;
            toCopy = createTableScript;
        }


        html.AppendLine($@"<body>
        <h1>{title}</h1>
            <div>
            <span class=""use"">Use {title}</span> 
            <button class='copy-btn' onclick='copyPane(this)'>{CopyIcon}{CheckIcon}</button>
            {coloredCode}
            <span class=""copy-target"" style=""display:none;"">{toCopy}</span> 
            </div>

            <script>
                function copyPane(button) {{
                    const bodyContainer = button.closest('body'); 
                    const codeBlock = bodyContainer.querySelector('.copy-target'); 
                    const text = codeBlock?.innerText.trim();

                    navigator.clipboard.writeText(text).then(() => {{
                        button.classList.add('copied');
                        setTimeout(() => button.classList.remove('copied'), 2000);
                    }}).catch(err => {{
                        console.error('Copy failed:', err);
                        alert('Failed to copy!');
                    }});
                }}
            </script>
            <a href=""{returnPage}"" class=""return-btn"">Return to Summary</a>
            </body>
            </html>");
        File.WriteAllText(filePath, html.ToString());
    }
    #endregion

    #region Differences Writer
    /// <summary>
    /// Generates a side-by-side HTML diff view for a procedure/view/table.
    /// </summary>
    public static void DifferencesWriter(string differencesPath, string sourceName, string destinationName, string sourceBody, string destinationBody, string title, string Name, string returnPage)
    {
        var differ = new Differ();
        string[] sourceBodyColored = NoBlanks(HighlightSql(sourceBody));
        string[] destinationBodyColored = NoBlanks(HighlightSql(destinationBody));
        var sideBySideBuilder = new SideBySideDiffBuilder(differ);
        var model = sideBySideBuilder.BuildDiffModel(string.Join("\n", destinationBodyColored), string.Join("\n", sourceBodyColored));

        var html = new StringBuilder();
        html.AppendLine(DifferencesTemplate.Replace("{title}", title));

        // Source block
        html.AppendLine(@$"<h1>{Name}<button class=""name-copy-btn"" onclick=""copyName(this)"">{SmallCopyIcon}{SmallCheckIcon}</button><span class=""copy-target"" style=""display:none;"">{Name}</span></h1>
                         <div class='diff-wrapper'>
                        <div class='pane'>
                        <button class='copy-btn' data-target='left'>{CopyIcon}{CheckIcon}</button>   
                        <h2>{sourceName}</h2>
                        <div class='code-scroll' id='left'><div class='code-block'>

");
        foreach (var line in model.NewText.Lines)
        {
            string css = GetCssClass(line.Type);
            string lineNumber = line.Position == 0 ? "" : line.Position.ToString();
            html.AppendLine(@$"<div class='line-number'>{lineNumber}</div>
                            <div class='line-text {css}'>{line.Text}</div>");
        }
        // Destination block
        html.Append($@"</div></div></div>
                        <div class='pane'>
                        <button class='copy-btn' data-target='right'>{CopyIcon}{CheckIcon}</button>                        
                        <h2>{destinationName}</h2>
                        <div class='code-scroll' id='right'><div class='code-block'>
                        ");
        foreach (var line in model.OldText.Lines)
        {
            string css = GetCssClass(line.Type);
            string lineNumber = line.Position == 0 ? "" : line.Position.ToString();
            html.AppendLine(@$"<div class='line-number'>{lineNumber}</div>
                           <div class='line-text {css}'>{line.Text}</div>");
        }

        // Scroll sync script
        html.AppendLine(@$"</div></div></div></div><br>
                 <a href=""{returnPage}"" class=""return-btn"">Return to Summary</a>
              
                <script>
                function copyName(button) {{
                    const container = button.closest('h1');
                    const codeBlock = container.querySelector('.copy-target');
                    const text = codeBlock?.innerText.trim();

                    navigator.clipboard.writeText(text).then(() => {{
                        button.classList.add('copied'); 
                        setTimeout(() => button.classList.remove('copied'), 2000); 
                    }}).catch(err => {{
                        console.error('Copy failed:', err);
                        alert('Failed to copy!');
                    }});
                }}
                document.querySelectorAll('.copy-btn').forEach(button => {{
                    button.addEventListener('click', () => {{
                        const targetId = button.getAttribute('data-target');
                        const block = document.getElementById(targetId);
                        const text = Array.from(block.querySelectorAll('.line-text'))
                                          .map(line => line.textContent)
                                          .join('\n');
                        navigator.clipboard.writeText(text).then(() => {{
                            button.classList.add('copied'); 
                            setTimeout(() => button.classList.remove('copied'), 2000); 
                        }});
                    }});
                }});
                </script>
                 <script>
                 const blocks = document.querySelectorAll('.code-scroll');
                 
                 function syncScroll(source, target) {{
                     target.scrollTop = source.scrollTop;
                     target.scrollLeft = source.scrollLeft;
                 }}

                 if (blocks.length === 2) {{
                     let isSyncingScroll = false;

                     blocks[0].addEventListener('scroll', () => {{
                         if (isSyncingScroll) return;
                         isSyncingScroll = true;
                         syncScroll(blocks[0], blocks[1]);
                         isSyncingScroll = false;
                     }});

                     blocks[1].addEventListener('scroll', () => {{
                         if (isSyncingScroll) return;
                         isSyncingScroll = true;
                         syncScroll(blocks[1], blocks[0]);
                         isSyncingScroll = false;
                     }});
                 }}
                 </script>

                 </body>
                 </html>");
        File.WriteAllText(differencesPath, html.ToString());

        #region local functions
        string[] NoBlanks(string s)
        {
            if (string.IsNullOrEmpty(s)) return Array.Empty<string>();

            // Normalize line endings first
            s = s.Replace("\r\n", "\n");

            // Trim leading blank/whitespace lines so line 1 is real content
            s = s.TrimStart('\n', '\r', ' ', '\t');

            // Split into lines for DiffPlex rendering
            return s.Split('\n');
        }
        #endregion
    }

    /// <summary>
    /// Prints table column details with to show the differences 
    /// </summary>
    public static void TableDifferencesWriter(string filePath, string sourceName, string destinationName,List<tableDto> sourceTable, List<tableDto> destinationTable, List<string> differences,string title, string objectName, string returnPage,List<ForeignKeyDto> sourceFKs = null, List<ForeignKeyDto> destFKs = null, List<IndexDto> sourceIndexes = null, List<IndexDto> destIndexes = null)
    {
        var html = new StringBuilder();
        html.AppendLine(BodyTemplate.Replace("{title}", title));

        // Extract schema and table name from objectName (format: "schema.table")
        string[] parts = objectName.Split('.');
        string schema = parts.Length > 1 ? parts[0] : "dbo";
        string table = parts.Length > 1 ? parts[1] : objectName;

        string sourceAlterScript = CreateAlterTableScript(schema, table, sourceTable, destinationTable, sourceFKs, destFKs, sourceIndexes, destIndexes);
        string destAlterScript = CreateAlterTableScript(schema, table, destinationTable, sourceTable, destFKs, sourceFKs, destIndexes, sourceIndexes);

        html.AppendLine($@"
    <body>
    <h1>{title} for {objectName}<button class=""name-copy-btn"" onclick=""copyName(this)"">{SmallCopyIcon}{SmallCheckIcon}</button><span class=""copy-target"" style=""display:none;"">{objectName}</span></h1>
        <div class='side-by-side'>
        <div class='db-block'>
            <span class='use'>{sourceName}</span>
            <button class='copy-btn' onclick='copyTableScript(""sourceScript"")'>{CopyIcon}{CheckIcon}</button>
            <div class='code-scroll' id='left'>
                <table>
                    <tr><th style='width:10px; padding:0;'></th><th>Column Name</th><th>Column Type</th><th>Is Nullable</th><th>Max Length</th><th>Is Primary Key</th><th>Is Foreign Key</th></tr>");

        var destTableHtml = new StringBuilder();
        destTableHtml.AppendLine($@"
    <div class='db-block'>
        <span class='use'>{destinationName}</span>
        <button class='copy-btn' onclick='copyTableScript(""destScript"")'>{CopyIcon}{CheckIcon}</button>
        <div class='code-scroll' id='right'>
            <table>
                <tr><th style='width:10px; padding:0;'></th><th>Column Name</th><th>Column Type</th><th>Is Nullable</th><th>Max Length</th><th>Is Primary Key</th><th>Is Foreign Key</th></tr>");

        // Track remaining columns that are not yet output
        var sourceRemaining = new Queue<tableDto>(sourceTable);
        var destRemaining = new Queue<tableDto>(destinationTable);

        int rowNum = 0;

        // Output rows until both queues are empty
        while (sourceRemaining.Any() || destRemaining.Any())
        {
            tableDto srcCol = sourceRemaining.Any() ? sourceRemaining.Peek() : null;
            tableDto destCol = destRemaining.Any() ? destRemaining.Peek() : null;

            bool srcHas = srcCol != null;
            bool destHas = destCol != null;

            if (srcHas && destHas && srcCol.columnName.Equals(destCol.columnName, StringComparison.OrdinalIgnoreCase))
            {
                sourceRemaining.Dequeue();
                destRemaining.Dequeue();
            }
            else if (srcHas && (!destHas || !destinationTable.Select(c => c.columnName).Contains(srcCol.columnName, StringComparer.OrdinalIgnoreCase)))
            {
                srcCol = sourceRemaining.Dequeue();
                destCol = null;
            }
            else if (destHas && (!srcHas || !sourceTable.Select(c => c.columnName).Contains(destCol.columnName, StringComparer.OrdinalIgnoreCase)))
            {
                destCol = destRemaining.Dequeue();
                srcCol = null;
            }
            else
            {
                srcCol = sourceRemaining.Any() ? sourceRemaining.Dequeue() : null;
                destCol = destRemaining.Any() ? destRemaining.Dequeue() : null;
            }

            if (srcCol != null)
            {
                string nameCss = destCol == null ? "source" :
                    (destCol.columnType != srcCol.columnType || destCol.isNullable != srcCol.isNullable ||
                        destCol.maxLength != srcCol.maxLength || destCol.isPrimaryKey != srcCol.isPrimaryKey ||
                        destCol.isForeignKey != srcCol.isForeignKey) ? "difference" : "";

                string typeCss = destCol != null && srcCol.columnType != destCol.columnType ? "difference" : "";
                string nullCss = destCol != null && srcCol.isNullable != destCol.isNullable ? "difference" : "";
                string lenCss = destCol != null && srcCol.maxLength != destCol.maxLength ? "difference" : "";
                string pkCss = destCol != null && srcCol.isPrimaryKey != destCol.isPrimaryKey ? "difference" : "";
                string fkCss = destCol != null && srcCol.isForeignKey != destCol.isForeignKey ? "difference" : "";

                string nullableDisplay = srcCol.isNullable?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true ? "YES" : "";
                string pkDisplay = srcCol.isPrimaryKey?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true ? "YES" : "";
                string fkDisplay = srcCol.isForeignKey?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true ? "YES" : "";

                // Generate script with BOTH options
                string colAlter = GenerateColumnAlterScript(schema, table, srcCol, destCol, sourceTable, destinationTable, sourceFKs, destFKs);

                string copyBtn = string.IsNullOrWhiteSpace(colAlter) ? "" :
                    $@"<button class='copy-btn-small' onclick='copyColumnScript(""src-col-{rowNum}"")'>{CopyIcon}{CheckIcon}</button>
                <span id='src-col-{rowNum}' style='display:none;'>{colAlter}</span>";

                html.AppendLine($@"
        <tr>
            <td style='text-align:center; width:10px;padding:0;'>{copyBtn}</td>
            <td class='{nameCss}'>{srcCol.columnName}</td>
            <td class='{typeCss}'>{srcCol.columnType}</td>
            <td class='{nullCss}'>{nullableDisplay}</td>
            <td class='{lenCss}'>{srcCol.maxLength}</td>
            <td class='{pkCss}'>{pkDisplay}</td>
            <td class='{fkCss}'>{fkDisplay}</td>
        </tr>");
            }
            else
            {
                if (destCol != null)
                {
                    var sbBoth = new StringBuilder();

                    sbBoth.AppendLine($"-- Add column to source [{schema}].[{table}]");
                    var len = NormalizeLen(destCol.columnType, destCol.maxLength);
                    var nullability = (destCol.isNullable?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true) ? "NULL" : "NOT NULL";
                    sbBoth.AppendLine($"ALTER TABLE [{schema}].[{table}] ADD [{destCol.columnName}] {destCol.columnType}{len} {nullability};");
                    sbBoth.AppendLine();
                    sbBoth.AppendLine($"-- Drop column from destination [{schema}].[{table}]");

                    var colFKs = destFKs?.Where(fk => fk.ColumnName.Equals(destCol.columnName, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (colFKs != null && colFKs.Any())
                    {
                        var constraintNames = colFKs.Select(fk => fk.ConstraintName).Distinct();
                        foreach (var cName in constraintNames)
                        {
                            sbBoth.AppendLine($"ALTER TABLE [{schema}].[{table}] DROP CONSTRAINT [{cName}];");
                        }
                    }

                    sbBoth.AppendLine($"ALTER TABLE [{schema}].[{table}] DROP COLUMN [{destCol.columnName}];");

                    string copyBtn = $@"<button class='copy-btn-small' onclick='copyColumnScript(""src-col-{rowNum}"")'>{CopyIcon}{CheckIcon}</button>
<span id='src-col-{rowNum}' style='display:none;'>{sbBoth}</span>";

                    html.AppendLine($@"<tr><td style='text-align:center; width:50px;'>{copyBtn}</td><td colspan='6' class='missing'>&nbsp;</td></tr>");
                }
                else
                {
                    html.AppendLine("<tr><td></td><td colspan='6' class='missing'>&nbsp;</td></tr>");
                }
            }

            if (destCol != null)
            {
                string nameCss = srcCol == null ? "destination" :
                    (srcCol.columnType != destCol.columnType || srcCol.isNullable != destCol.isNullable ||
                        srcCol.maxLength != destCol.maxLength || srcCol.isPrimaryKey != destCol.isPrimaryKey ||
                        srcCol.isForeignKey != destCol.isForeignKey) ? "difference" : "";

                string typeCss = srcCol != null && destCol.columnType != srcCol.columnType ? "difference" : "";
                string nullCss = srcCol != null && destCol.isNullable != srcCol.isNullable ? "difference" : "";
                string lenCss = srcCol != null && destCol.maxLength != srcCol.maxLength ? "difference" : "";
                string pkCss = srcCol != null && destCol.isPrimaryKey != srcCol.isPrimaryKey ? "difference" : "";
                string fkCss = srcCol != null && destCol.isForeignKey != srcCol.isForeignKey ? "difference" : "";

                string nullableDisplay = destCol.isNullable?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true ? "YES" : "";
                string pkDisplay = destCol.isPrimaryKey?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true ? "YES" : "";
                string fkDisplay = destCol.isForeignKey?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true ? "YES" : "";

                // Generate script with BOTH options
                string colAlter = GenerateColumnAlterScript(schema, table, destCol, srcCol, destinationTable, sourceTable, destFKs, sourceFKs);

                string copyBtn = string.IsNullOrWhiteSpace(colAlter) ? "" :
                    $@"<button class='copy-btn-small' onclick='copyColumnScript(""dst-col-{rowNum}"")'>{CopyIcon}{CheckIcon}</button>
                <span id='dst-col-{rowNum}' style='display:none;'>{colAlter}</span>";

                destTableHtml.AppendLine($@"
        <tr>
            <td style='text-align:center; width:10px;padding:0; '>{copyBtn}</td>
            <td class='{nameCss}'>{destCol.columnName}</td>
            <td class='{typeCss}'>{destCol.columnType}</td>
            <td class='{nullCss}'>{nullableDisplay}</td>
            <td class='{lenCss}'>{destCol.maxLength}</td>
            <td class='{pkCss}'>{pkDisplay}</td>
            <td class='{fkCss}'>{fkDisplay}</td>
        </tr>");
            }
            else
            {
                // Column missing in destination
                if (srcCol != null)
                {
                    var sbBoth = new StringBuilder();

                    sbBoth.AppendLine($"-- Add column to destination [{schema}].[{table}]");
                    var len = NormalizeLen(srcCol.columnType, srcCol.maxLength);
                    var nullability = (srcCol.isNullable?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true) ? "NULL" : "NOT NULL";
                    sbBoth.AppendLine($"ALTER TABLE [{schema}].[{table}] ADD [{srcCol.columnName}] {srcCol.columnType}{len} {nullability};");
                    sbBoth.AppendLine();
                    sbBoth.AppendLine($"-- Drop column from source [{schema}].[{table}]");
                    sbBoth.AppendLine($"ALTER TABLE [{schema}].[{table}] DROP COLUMN [{srcCol.columnName}];");

                    string copyBtn = $@"<button class='copy-btn-small' onclick='copyColumnScript(""dst-col-{rowNum}"")'>{CopyIcon}{CheckIcon}</button>
                    <span id='dst-col-{rowNum}' style='display:none;'>{sbBoth}</span>";

                    destTableHtml.AppendLine($@"<tr><td style='text-align:center; width:50px;'>{copyBtn}</td><td colspan='6' class='missing'>&nbsp;</td></tr>");
                }
                else
                {
                    destTableHtml.AppendLine("<tr><td></td><td colspan='6' class='missing'>&nbsp;</td></tr>");
                }
            }

            rowNum++;
        }

        html.AppendLine("</table>");
        html.AppendLine(PrintIndexTable(schema, table, sourceIndexes, differences, destIndexes, isSourceSide: true, copyIdPrefix: "src-idx"));
        html.AppendLine("</div></div>");
        destTableHtml.AppendLine("</table>");
        destTableHtml.AppendLine(PrintIndexTable(schema, table, destIndexes, differences, sourceIndexes, isSourceSide: false, copyIdPrefix: "dst-idx"));
        destTableHtml.AppendLine("</div></div>");
        html.AppendLine(destTableHtml.ToString());

        // Hidden spans containing the full table ALTER scripts
        html.AppendLine($@"
    </div>
    
    <span id='sourceScript' style='display:none;'>{sourceAlterScript}</span>
    <span id='destScript' style='display:none;'>{destAlterScript}</span>
    
    <a href='{returnPage}' class='return-btn'>Return to Summary</a>
    
    <style>
    .copy-btn-small {{
        background-color: transparent;
        color: #555; 
        border: none;
        font-size: 14px;
        padding: 4px 6px;
        border-radius: 4px;
        cursor: pointer;
    }}
    .copy-btn-small:hover {{
        background-color: #f0f0f0; 
        color: #000; 
    }}
    .copy-btn-small .icon-check {{
        display: none;
    }}
    .copy-btn-small.copied .icon-check {{
        display: inline-block;
    }}
    .copy-btn-small.copied .icon-copy {{
        display: none;
    }}
.name-copy-btn {{margin: 0 0 0 8px;
        background-color: transparent;
        color: #888; 
        border: none;
        font-size: 13px; 
        padding: 2px 4px; 
        border-radius: 3px;
        cursor: pointer;
        vertical-align: middle;
        display: inline-flex;
        align-items: center;
    }}
    .name-copy-btn:hover {{background - color: #f0f0f0; 
        color: #666; 
    }}
    .name-copy-btn .icon-check {{display: none;
    }}
    .name-copy-btn.copied .icon-check {{display: inline-block;
    }}
    .name-copy-btn.copied .icon-copy {{display: none;
    }}
    </style>    
    <script>
    function copyName(button) {{
        const container = button.closest('h1');
        const codeBlock = container.querySelector('.copy-target');
        const text = codeBlock?.innerText.trim();

        navigator.clipboard.writeText(text).then(() => {{
            button.classList.add('copied'); 
            setTimeout(() => button.classList.remove('copied'), 2000); 
        }}).catch(err => {{
            console.error('Copy failed:', err);
            alert('Failed to copy!');
        }});
    }}
    // Copy full table script (header buttons)
    function copyTableScript(scriptId) {{
        const scriptElement = document.getElementById(scriptId);
        const text = scriptElement.textContent.trim();
        
        navigator.clipboard.writeText(text).then(() => {{
            const buttons = document.querySelectorAll('.copy-btn');
            buttons.forEach(btn => {{
                if (btn.getAttribute('onclick').includes(scriptId)) {{
                    btn.classList.add('copied');
                    setTimeout(() => btn.classList.remove('copied'), 2000);
                }}
            }});
        }}).catch(err => {{
            console.error('Copy failed:', err);
            alert('Failed to copy!');
        }});
    }}
    
    // Copy individual column script
    function copyColumnScript(scriptId) {{
        const scriptElement = document.getElementById(scriptId);
        const text = scriptElement.textContent.trim();
        
        navigator.clipboard.writeText(text).then(() => {{
            const buttons = document.querySelectorAll('.copy-btn-small');
            buttons.forEach(btn => {{
                if (btn.getAttribute('onclick').includes(scriptId)) {{
                    btn.classList.add('copied');
                    setTimeout(() => btn.classList.remove('copied'), 2000);
                }}
            }});
        }}).catch(err => {{
            console.error('Copy failed:', err);
            alert('Failed to copy!');
        }});
    }}
    
    // Scroll sync
    const blocks = document.querySelectorAll('.code-scroll');
    function syncScroll(src, tgt) {{ tgt.scrollTop = src.scrollTop; tgt.scrollLeft = src.scrollLeft; }}
    if (blocks.length === 2) {{
        let isSyncing = false;
        blocks[0].addEventListener('scroll', () => {{ if(isSyncing) return; isSyncing = true; syncScroll(blocks[0], blocks[1]); isSyncing = false; }});
        blocks[1].addEventListener('scroll', () => {{ if(isSyncing) return; isSyncing = true; syncScroll(blocks[1], blocks[0]); isSyncing = false; }});
    }}
    </script>
    </body>
    </html>");

        File.WriteAllText(filePath, html.ToString());
    }
    #endregion

    #region Helpers

    /// <summary>
    /// Shared helper to normalize column length strings (e.g., handling MAX and nvarchar division).
    /// </summary>
    private static string NormalizeLen(string type, string lenStr)
    {
        if (string.IsNullOrWhiteSpace(lenStr)) return "";
        if (!int.TryParse(lenStr, out var len)) return "";

        if (len == -1) return "(MAX)";

        if (type.Equals("nvarchar", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("nchar", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("varchar", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("char", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("varbinary", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("binary", StringComparison.OrdinalIgnoreCase))
        {
            return $"({len})";
        }

        return "";
    }
    public static string CreateTableScript(string schema, string table, List<tableDto> cols, List<ForeignKeyDto> foreignKeys = null, List<IndexDto> indexes = null)
    {
        if (cols == null || cols.Count == 0)
            return $"-- Table [{schema}].[{table}] has no columns?";

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE [{schema}].[{table}] (");

        for (int i = 0; i < cols.Count; i++)
        {
            var c = cols[i];
            var len = NormalizeLen(c.columnType, c.maxLength);
            var identity = c.isIdentity?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true
                ? $" IDENTITY({c.identitySeed ?? "1"},{c.identityIncrement ?? "1"})"
                : "";
            var nullability = (c.isNullable?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true) ? "NULL" : "NOT NULL";
            var comma = (i < cols.Count - 1) ? "," : "";
            sb.AppendLine($"    [{c.columnName}] {c.columnType}{len}{identity} {nullability}{comma}");
        }

        // Add PK constraint
        var pkCols = cols.Where(x => x.isPrimaryKey?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true)
                         .Select(x => $"[{x.columnName}]")
                         .ToList();
        if (pkCols.Any())
        {
            sb.AppendLine($",   CONSTRAINT [PK_{table}] PRIMARY KEY ({string.Join(", ", pkCols)})");
        }

        // Add FK constraints
        if (foreignKeys != null && foreignKeys.Any())
        {
            // Group by constraint name to handle composite foreign keys
            var fkGroups = foreignKeys.GroupBy(fk => fk.ConstraintName);

            foreach (var fkGroup in fkGroups)
            {
                var fkList = fkGroup.ToList();
                var fkColumns = string.Join(", ", fkList.Select(fk => $"[{fk.ColumnName}]"));
                var refColumns = string.Join(", ", fkList.Select(fk => $"[{fk.ReferencedColumn}]"));
                var refSchema = fkList.First().ReferencedSchema;
                var refTable = fkList.First().ReferencedTable;

                sb.AppendLine($",   CONSTRAINT [{fkGroup.Key}] FOREIGN KEY ({fkColumns}) REFERENCES [{refSchema}].[{refTable}]({refColumns})");
            }
        }

        sb.AppendLine(");");

        if (indexes != null && indexes.Any())
        {
            sb.AppendLine();
            foreach (var index in indexes)
            {
                sb.AppendLine(BuildCreateIndexStatement(schema, table, index));
            }
        }

        return sb.ToString();
    }

    public static string CreateAlterTableScript(string schema, string table, List<tableDto> sourceColumns, List<tableDto> targetColumns, List<ForeignKeyDto> sourceFKs = null, List<ForeignKeyDto> targetFKs = null, List<IndexDto> sourceIndexes = null, List<IndexDto> targetIndexes = null)
    {
        if (sourceColumns == null || targetColumns == null)
            return $"-- Cannot generate ALTER script for [{schema}].[{table}]";

        var sb = new StringBuilder();
        sb.AppendLine($"-- ALTER script [{schema}].[{table}]");
        sb.AppendLine();

        // Create dictionaries for easy lookup
        var sourceDict = sourceColumns.ToDictionary(c => c.columnName.ToLower(), c => c);
        var targetDict = targetColumns.ToDictionary(c => c.columnName.ToLower(), c => c);

        // Detect Primary Key differences
        var sourcePKs = sourceColumns
            .Where(c => "YES".Equals(c.isPrimaryKey, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.columnName.ToLower())
            .ToHashSet();
        var targetPKs = targetColumns
            .Where(c => "YES".Equals(c.isPrimaryKey, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.columnName.ToLower())
            .ToHashSet();

        // Detect Foreign Key differences
        var sourceFKDict = (sourceFKs ?? new List<ForeignKeyDto>())
            .GroupBy(fk => fk.ConstraintName)
            .ToDictionary(g => g.Key, g => g.ToList());

        var targetFKDict = (targetFKs ?? new List<ForeignKeyDto>())
            .GroupBy(fk => fk.ConstraintName)
            .ToDictionary(g => g.Key, g => g.ToList());

        var toDrop = new List<tableDto>();
        var toAdd = new List<tableDto>();
        var toModify = new List<(tableDto source, tableDto target)>();
        // Track FKs dropped in Step 1 so we don't drop them again in Step 6
        var alreadyDroppedFKs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Find columns to DROP (exist in source but not in target)
        foreach (var sourceCol in sourceColumns)
        {
            if (!targetDict.ContainsKey(sourceCol.columnName.ToLower()))
            {
                toDrop.Add(sourceCol);
            }
        }

        // Find columns to ADD or MODIFY
        foreach (var targetCol in targetColumns)
        {
            string key = targetCol.columnName.ToLower();

            if (!sourceDict.ContainsKey(key))
            {
                // Column exists in target but not in source - ADD it
                toAdd.Add(targetCol);
            }
            else
            {
                // Column exists in both - check if modified
                var sourceCol = sourceDict[key];

                bool isModified =
                    !string.Equals(sourceCol.columnType, targetCol.columnType, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(sourceCol.isNullable, targetCol.isNullable, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(sourceCol.maxLength, targetCol.maxLength, StringComparison.OrdinalIgnoreCase);

                if (isModified)
                {
                    toModify.Add((sourceCol, targetCol));
                }
            }
        }

        bool hasChanges = false;

        // DROP Foreign Keys that reference columns we're about to drop or modify
        if (sourceFKs != null && sourceFKs.Any())
        {
            var columnsBeingModified = toDrop.Select(c => c.columnName.ToLower())
                .Union(toModify.Select(m => m.source.columnName.ToLower()))
                .ToHashSet();

            var fksToDropFirst = sourceFKs
                .Where(fk => columnsBeingModified.Contains(fk.ColumnName.ToLower()))
                .Select(fk => fk.ConstraintName)
                .Distinct();

            foreach (var fkName in fksToDropFirst)
            {
                sb.AppendLine($"-- Drop FK constraint that references column being modified");
                sb.AppendLine($"ALTER TABLE [{schema}].[{table}] DROP CONSTRAINT [{fkName}];");
                alreadyDroppedFKs.Add(fkName); 
                hasChanges = true;
            }

            if (fksToDropFirst.Any())
                sb.AppendLine();
        }

        // DROP columns
        if (toDrop.Any())
        {
            foreach (var col in toDrop)
            {
                sb.AppendLine($"ALTER TABLE [{schema}].[{table}] DROP COLUMN [{col.columnName}];");
                hasChanges = true;
            }
            sb.AppendLine();
        }

        // Handle Primary Key changes (Drop old, Add new)
        if (!sourcePKs.SetEquals(targetPKs))
        {
            // If source has a PK, drop it
            if (sourcePKs.Any())
            {
                sb.AppendLine($"-- Dropping old Primary Key");
                sb.AppendLine($"ALTER TABLE [{schema}].[{table}] DROP CONSTRAINT [PK_{table}];");
                hasChanges = true;
            }

            // If target has a PK, add it
            if (targetPKs.Any())
            {
                sb.AppendLine($"-- Adding new Primary Key");
                var pkCols = targetColumns
                    .Where(c => "YES".Equals(c.isPrimaryKey, StringComparison.OrdinalIgnoreCase))
                    .Select(x => $"[{x.columnName}]");
                sb.AppendLine($"ALTER TABLE [{schema}].[{table}] ADD CONSTRAINT [PK_{table}] PRIMARY KEY ({string.Join(", ", pkCols)});");
                hasChanges = true;
            }
            sb.AppendLine();
        }

        // MODIFY columns (ALTER COLUMN)
        if (toModify.Any())
        {
            foreach (var (sourceCol, targetCol) in toModify)
            {
                var len = NormalizeLen(targetCol.columnType, targetCol.maxLength);
                var nullability = (targetCol.isNullable?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true) ? "NULL" : "NOT NULL";
                sb.AppendLine($"ALTER TABLE [{schema}].[{table}] ALTER COLUMN [{targetCol.columnName}] {targetCol.columnType}{len} {nullability};");
                hasChanges = true;
            }
            sb.AppendLine();
        }

        // ADD columns
        if (toAdd.Any())
        {
            foreach (var col in toAdd)
            {
                var len = NormalizeLen(col.columnType, col.maxLength);
                var nullability = (col.isNullable?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true) ? "NULL" : "NOT NULL";
                sb.AppendLine($"ALTER TABLE [{schema}].[{table}] ADD [{col.columnName}] {col.columnType}{len} {nullability};");
                hasChanges = true;
            }
            sb.AppendLine();
        }

        // Handle Foreign Key changes
        // Drop FKs that exist in source but not in target (or have changed)
        var fksToDrop = sourceFKDict.Keys.Except(targetFKDict.Keys).ToList();

        // Also drop FKs that exist in both but have different definitions
        foreach (var fkName in sourceFKDict.Keys.Intersect(targetFKDict.Keys))
        {
            var sourceFKList = sourceFKDict[fkName];
            var targetFKList = targetFKDict[fkName];

            // Compare FK definitions
            bool fkChanged = !AreForeignKeysEqual(sourceFKList, targetFKList);
            if (fkChanged && !fksToDrop.Contains(fkName))
            {
                fksToDrop.Add(fkName);
            }
        }

        foreach (var fkName in fksToDrop)
        {
            if (alreadyDroppedFKs.Contains(fkName)) continue; 

            sb.AppendLine($"-- Drop foreign key constraint");
            sb.AppendLine($"ALTER TABLE [{schema}].[{table}] DROP CONSTRAINT [{fkName}];");
            hasChanges = true;
        }


        if (fksToDrop.Any())
            sb.AppendLine();

        // Add FKs that exist in target but not in source (or were modified)
        var fksToAdd = targetFKDict.Keys.Except(sourceFKDict.Keys).ToList();

        // Also add back FKs that were dropped due to changes
        foreach (var fkName in sourceFKDict.Keys.Intersect(targetFKDict.Keys))
        {
            if (fksToDrop.Contains(fkName) && !fksToAdd.Contains(fkName))
            {
                fksToAdd.Add(fkName);
            }
        }

        foreach (var fkName in fksToAdd)
        {
            if (!targetFKDict.ContainsKey(fkName)) continue;

            var fkList = targetFKDict[fkName];
            var fkColumns = string.Join(", ", fkList.Select(fk => $"[{fk.ColumnName}]"));
            var refColumns = string.Join(", ", fkList.Select(fk => $"[{fk.ReferencedColumn}]"));
            var refSchema = fkList.First().ReferencedSchema;
            var refTable = fkList.First().ReferencedTable;

            sb.AppendLine($"-- Add foreign key constraint");
            sb.AppendLine($"ALTER TABLE [{schema}].[{table}] ADD CONSTRAINT [{fkName}] FOREIGN KEY ({fkColumns}) REFERENCES [{refSchema}].[{refTable}]({refColumns});");
            hasChanges = true;
        }

        if (fksToAdd.Any())
            sb.AppendLine();

        var sourceIndexMap = (sourceIndexes ?? new List<IndexDto>()).ToDictionary(i => i.IndexName, StringComparer.OrdinalIgnoreCase);
        var targetIndexMap = (targetIndexes ?? new List<IndexDto>()).ToDictionary(i => i.IndexName, StringComparer.OrdinalIgnoreCase);
        var indexesToDrop = new List<string>();
        var indexesToAdd = new List<IndexDto>();

        foreach (var sourceIndex in sourceIndexMap)
        {
            if (!targetIndexMap.TryGetValue(sourceIndex.Key, out var targetIndex) || !TableIndexComparer.AreDefinitionsEqual(sourceIndex.Value, targetIndex))
            {
                indexesToDrop.Add(sourceIndex.Key);
            }
        }

        foreach (var targetIndex in targetIndexMap)
        {
            if (!sourceIndexMap.TryGetValue(targetIndex.Key, out var sourceIndex) || !TableIndexComparer.AreDefinitionsEqual(sourceIndex, targetIndex.Value))
            {
                indexesToAdd.Add(targetIndex.Value);
            }
        }

        foreach (var indexName in indexesToDrop)
        {
            sb.AppendLine($"DROP INDEX [{indexName}] ON [{schema}].[{table}];");
            hasChanges = true;
        }

        if (indexesToDrop.Any())
        {
            sb.AppendLine();
        }

        foreach (var index in indexesToAdd)
        {
            sb.AppendLine(BuildCreateIndexStatement(schema, table, index));
            hasChanges = true;
        }

        if (indexesToAdd.Any())
        {
            sb.AppendLine();
        }

        if (!hasChanges)
        {
            sb.AppendLine("-- No structural changes needed");
        }

        return sb.ToString();
    }

    // Helper method to compare two FK definitions
    private static bool AreForeignKeysEqual(List<ForeignKeyDto> fk1, List<ForeignKeyDto> fk2)
    {
        if (fk1.Count != fk2.Count) return false;

        // Sort both lists by column name for comparison
        var sorted1 = fk1.OrderBy(f => f.ColumnName).ToList();
        var sorted2 = fk2.OrderBy(f => f.ColumnName).ToList();

        for (int i = 0; i < sorted1.Count; i++)
        {
            if (!string.Equals(sorted1[i].ColumnName, sorted2[i].ColumnName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(sorted1[i].ReferencedSchema, sorted2[i].ReferencedSchema, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(sorted1[i].ReferencedTable, sorted2[i].ReferencedTable, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(sorted1[i].ReferencedColumn, sorted2[i].ReferencedColumn, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
    /// <summary>
    /// Generates an ALTER statement for a single column
    /// </summary>
    private static string GenerateColumnAlterScript(string schema, string table, tableDto currentCol, tableDto targetCol, List<tableDto> currentTable, List<tableDto> targetTable, List<ForeignKeyDto> currentFKs = null, List<ForeignKeyDto> targetFKs = null)
    {
        if (currentCol == null) return "";

        var sb = new StringBuilder();
        string key = currentCol.columnName.ToLower();

        // Check if column exists in target
        bool existsInTarget = targetTable?.Any(c => c.columnName.Equals(currentCol.columnName, StringComparison.OrdinalIgnoreCase)) == true;

        // Check if this column has any FK constraints
        var columnFKs = currentFKs?.Where(fk => fk.ColumnName.Equals(currentCol.columnName, StringComparison.OrdinalIgnoreCase)).ToList();
        bool hasFKConstraint = columnFKs != null && columnFKs.Any();

        // Check if this column has any FK constraints in TARGET
        var targetColumnFKs = targetFKs?.Where(fk => fk.ColumnName.Equals(currentCol.columnName, StringComparison.OrdinalIgnoreCase)).ToList();
        bool targetHasFKConstraint = targetColumnFKs != null && targetColumnFKs.Any();

        if (!existsInTarget)
        {
            // Column exists in current but not in target: Show BOTH options
            sb.AppendLine($"-- Add column to target [{schema}].[{table}]");
            var len = NormalizeLen(currentCol.columnType, currentCol.maxLength);
            var nullability = (currentCol.isNullable?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true) ? "NULL" : "NOT NULL";
            sb.AppendLine($"ALTER TABLE [{schema}].[{table}] ADD [{currentCol.columnName}] {currentCol.columnType}{len} {nullability};");
            sb.AppendLine();
            sb.AppendLine($"-- Drop column from source [{schema}].[{table}]");

            // First drop any FK constraints on this column
            if (hasFKConstraint)
            {
                var constraintNames = columnFKs.Select(fk => fk.ConstraintName).Distinct();
                foreach (var constraintName in constraintNames)
                {
                    sb.AppendLine($"-- Drop FK constraint before dropping column");
                    sb.AppendLine($"ALTER TABLE [{schema}].[{table}] DROP CONSTRAINT [{constraintName}];");
                }
            }
            sb.AppendLine($"-- Drop column from [{schema}].[{table}]");
            sb.AppendLine($"ALTER TABLE [{schema}].[{table}] DROP COLUMN [{currentCol.columnName}];");
        }
        else if (targetCol != null)
        {
            // Both have the column - check if modified
            bool isModified =
                !string.Equals(currentCol.columnType, targetCol.columnType, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(currentCol.isNullable, targetCol.isNullable, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(currentCol.maxLength, targetCol.maxLength, StringComparison.OrdinalIgnoreCase);

            // Check if FK status changed
            bool fkStatusChanged = !string.Equals(currentCol.isForeignKey, targetCol.isForeignKey, StringComparison.OrdinalIgnoreCase);

            if (isModified || fkStatusChanged)
            {
                // Drop FK constraints before altering the column
                if (hasFKConstraint)
                {
                    var constraintNames = columnFKs.Select(fk => fk.ConstraintName).Distinct();
                    foreach (var constraintName in constraintNames)
                    {
                        sb.AppendLine($"-- Drop FK constraint before altering column");
                        sb.AppendLine($"ALTER TABLE [{schema}].[{table}] DROP CONSTRAINT [{constraintName}];");
                    }
                }

                // Only ALTER COLUMN if data type, nullability, or length changed
                if (isModified)
                {
                    var len = NormalizeLen(targetCol.columnType, targetCol.maxLength);
                    var nullability = (targetCol.isNullable?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true) ? "NULL" : "NOT NULL";
                    sb.AppendLine($"-- Alter column [{currentCol.columnName}] in [{schema}].[{table}]");
                    sb.AppendLine($"ALTER TABLE [{schema}].[{table}] ALTER COLUMN [{targetCol.columnName}] {targetCol.columnType}{len} {nullability};");
                }

                if (targetHasFKConstraint)
                {
                    var targetFKGroups = targetColumnFKs.GroupBy(fk => fk.ConstraintName);

                    foreach (var fkGroup in targetFKGroups)
                    {
                        var fkList = fkGroup.ToList();
                        var fkColumns = string.Join(", ", fkList.Select(fk => $"[{fk.ColumnName}]"));
                        var refColumns = string.Join(", ", fkList.Select(fk => $"[{fk.ReferencedColumn}]"));
                        var refSchema = fkList.First().ReferencedSchema;
                        var refTable = fkList.First().ReferencedTable;

                        sb.AppendLine($"-- Add FK constraint");
                        sb.AppendLine($"ALTER TABLE [{schema}].[{table}] ADD CONSTRAINT [{fkGroup.Key}] FOREIGN KEY ({fkColumns}) REFERENCES [{refSchema}].[{refTable}]({refColumns});");
                    }
                }
            }
        }

        // Check if this column exists in target but not in current: ADD COLUMN
        bool existsInCurrent = currentTable?.Any(c => c.columnName.Equals(currentCol.columnName, StringComparison.OrdinalIgnoreCase)) == true;
        if (existsInTarget && !existsInCurrent)
        {
            // ADD COLUMN
            var len = NormalizeLen(currentCol.columnType, currentCol.maxLength);
            var nullability = (currentCol.isNullable?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true) ? "NULL" : "NOT NULL";
            sb.AppendLine($"-- Add column to [{schema}].[{table}]");
            sb.AppendLine($"ALTER TABLE [{schema}].[{table}] ADD [{currentCol.columnName}] {currentCol.columnType}{len} {nullability};");

            // Add FK constraints if they should exist
            if (targetHasFKConstraint)
            {
                var targetFKGroups = targetColumnFKs.GroupBy(fk => fk.ConstraintName);

                foreach (var fkGroup in targetFKGroups)
                {
                    var fkList = fkGroup.ToList();
                    var fkColumns = string.Join(", ", fkList.Select(fk => $"[{fk.ColumnName}]"));
                    var refColumns = string.Join(", ", fkList.Select(fk => $"[{fk.ReferencedColumn}]"));
                    var refSchema = fkList.First().ReferencedSchema;
                    var refTable = fkList.First().ReferencedTable;

                    sb.AppendLine($"-- Add FK constraint for new column");
                    sb.AppendLine($"ALTER TABLE [{schema}].[{table}] ADD CONSTRAINT [{fkGroup.Key}] FOREIGN KEY ({fkColumns}) REFERENCES [{refSchema}].[{refTable}]({refColumns});");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Maps DiffPlex line change types to CSS class names.
    /// </summary>
    static string GetCssClass(ChangeType type)
    {
        return type switch
        {
            ChangeType.Inserted => "inserted",
            ChangeType.Deleted => "deleted",
            ChangeType.Imaginary => "imaginary",
            ChangeType.Modified => "modified",
            _ => ""
        };
    }

    /// <summary>
    /// Highlights SQL source code by applying syntax coloring for display purposes.
    /// </summary>
    static string HighlightSql(string sqlCode)
    {
        if (sqlCode == null) return null;

        // ColorCode is not thread-safe during language compilation.
        // We lock here to prevent "Key already added" exceptions in Parallel loops.
        lock (_colorizerLock)
        {
            var colorizer = new CodeColorizer();
            string coloredCode = colorizer.Colorize(sqlCode, Languages.Sql)
                .Replace(@"<div style=""color:Black;background-color:White;""><pre>", "")
                .Replace("</div>", "");
            return coloredCode;
        }
    }
    /// <summary>
    /// Write the nav section in the comparison summary pages
    /// </summary>
    static string BuildNav(Run run, bool isIgnoredEmpty, string count)
    {
        string proceduresPath = "../Procedures/index.html";
        string viewsPath = "../Views/index.html";
        string tablesPath = "../Tables/index.html";
        string udtsPath = "../UDTs/index.html";
        string ignoredPath = "../Ignored/index.html";
        string functionsPath = "../Functions/index.html";
        var sb = new StringBuilder();
        sb.AppendLine(@"<nav class=""top-nav"">");
        switch (run)
        {
            case Run.Proc:
                sb.AppendLine($@"  <a href=""{proceduresPath}"">Procedures {{procsCount}}</a>");
                break;
            case Run.View:
                sb.AppendLine($@"  <a href=""{viewsPath}"">Views {{viewsCount}}</a>");
                break;
            case Run.Table:
                sb.AppendLine($@"  <a href=""{tablesPath}"">Tables {{tablesCount}}</a>");
                break;
            case Run.Udt:
                sb.AppendLine($@"  <a href=""{udtsPath}"">Udts {{udtsCount}}</a>");
                break;
            case Run.Function:
                sb.AppendLine($@"  <a href=""{functionsPath}"">Functions {{functionsCount}}</a>");
                break;
            case Run.ProcView:
                sb.AppendLine($@"  <a href=""{viewsPath}"">Views {{viewsCount}}</a>");
                sb.AppendLine($@"  <a href=""{proceduresPath}"">Procedures {{procsCount}}</a>");
                break;
            case Run.ProcTable:
                sb.AppendLine($@"  <a href=""{tablesPath}"">Tables {{tablesCount}}</a>");
                sb.AppendLine($@"  <a href=""{proceduresPath}"">Procedures {{procsCount}}</a>");
                break;
            case Run.ViewTable:
                sb.AppendLine($@"  <a href=""{tablesPath}"">Tables {{tablesCount}}</a>");
                sb.AppendLine($@"  <a href=""{viewsPath}"">Views {{viewsCount}}</a>");
                break;

            case Run.All:
                sb.AppendLine($@"  <a href=""{udtsPath}"">Udts {{udtsCount}}</a>");
                sb.AppendLine($@"  <a href=""{tablesPath}"">Tables {{tablesCount}}</a>");
                sb.AppendLine($@"  <a href=""{viewsPath}"">Views {{viewsCount}}</a>");
                sb.AppendLine($@"  <a href=""{proceduresPath}"">Procedures {{procsCount}}</a>");
                sb.AppendLine($@"  <a href=""{functionsPath}"">Functions {{functionsCount}}</a>");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(run), run, "Invalid Run option");
        }
        if (!isIgnoredEmpty)
        {
            sb.AppendLine($@"  <a href=""{ignoredPath}"">Ignored({count})</a>");
        }
        sb.AppendLine("</nav>");
        return sb.ToString();
    }

    /// <summary>
    /// Prints table column details with optional difference highlighting.
    /// </summary>
    public static string PrintTableInfo(string schema, string table, List<tableDto> tableInfo, List<string>? differences, List<IndexDto>? indexes = null)
    {
        differences ??= new List<string>();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(@"<table border='1'>
       <tr>
       <th>Column Name</th>
       <th>Column Type</th>
       <th>Is Nullable</th>
       <th>Max Length</th>
       <th>Is Primary Key</th>
       <th>Is Foreign Key</th>
       </tr>");

        foreach (tableDto column in tableInfo)
        {
            string ColumnName = $"<td>{column.columnName}</td>";
            if (differences.Contains(column.columnName))
            {
                ColumnName = $@"<td class=""red"">{column.columnName}</td>";
            }
            string ColumnType = $"<td>{column.columnType}</td>";
            if (differences.Contains(column.columnType))
            {
                ColumnType = $@" <td class=""red"">{column.columnType}</td>";
            }
            string nullableDisplay = column.isNullable?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true ? "YES" : "";

            string isNullable = $"<td>{nullableDisplay}</td>";
            if (differences.Contains(column.isNullable))
            {
                isNullable = $@"<td class=""red"">{nullableDisplay}</td>";
            }
            string maxLength = $"<td>{column.maxLength}</td>";
            if (differences.Contains(column.maxLength))
            {
                maxLength = $@"<td class=""red"">{column.maxLength}</td>";
            }
            string pkDisplay = column.isPrimaryKey?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true ? "YES" : "";
            string fkDisplay = column.isForeignKey?.Equals("YES", StringComparison.OrdinalIgnoreCase) == true ? "YES" : "";

            string isPrimaryKey = $"<td>{pkDisplay}</td>";
            if (differences.Contains(column.isPrimaryKey))
            {
                isPrimaryKey = $@"<td class=""red"">{pkDisplay}</td>";
            }
            string isForeignKey = $"<td>{fkDisplay}</td>";
            if (differences.Contains(column.isForeignKey))
            {
                isForeignKey = $@"<td class=""red"">{fkDisplay}</td>";
            }
            sb.AppendLine($@"<tr>
            {ColumnName}
            {ColumnType}
            {isNullable}
            {maxLength}
            {isPrimaryKey}
            {isForeignKey}
            </tr>");
        }

        sb.AppendLine("</table>");
        sb.AppendLine(PrintIndexTable(schema, table, indexes, differences, null, isSourceSide: true, copyIdPrefix: "idx"));
        return sb.ToString();

    }

    private static string PrintIndexTable(string schema, string table, List<IndexDto>? indexes, List<string>? differences, List<IndexDto>? comparisonIndexes, bool isSourceSide, string copyIdPrefix)
    {
        if (indexes == null || !indexes.Any())
        {
            return "<h3>Indexes</h3><p>No non-primary-key indexes found.</p>";
        }

        differences ??= new List<string>();
        comparisonIndexes ??= new List<IndexDto>();
        var comparisonMap = comparisonIndexes.ToDictionary(i => i.IndexName, StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        sb.AppendLine(@"<h3>Indexes</h3>");
        sb.AppendLine(@"<table border='1'>
       <tr>
       <th style='width:10px; padding:0;'></th>
       <th>Index Name</th>
       <th>Type</th>
       <th>Unique</th>
       <th>Unique Constraint</th>
       <th>Key Columns</th>
       <th>Included Columns</th>
       <th>Filter</th>
       </tr>");

        int rowNum = 0;
        foreach (var index in indexes)
        {
            string marker = TableIndexComparer.ToMarker(index.IndexName);
            string rowClass = "";
            if (comparisonMap.TryGetValue(index.IndexName, out var comparisonIndex))
            {
                if (!TableIndexComparer.AreDefinitionsEqual(index, comparisonIndex))
                {
                    rowClass = @" class=""difference""";
                }
            }
            else if (differences.Contains(marker))
            {
                rowClass = isSourceSide ? @" class=""source""" : @" class=""destination""";
            }

            string copyId = $"{copyIdPrefix}-{rowNum}";
            string copyBtn = $@"<button class='copy-btn-small' onclick='copyColumnScript(""{copyId}"")'>{CopyIcon}{CheckIcon}</button>
                <span id='{copyId}' style='display:none;'>{BuildCreateIndexStatement(schema, table, index)}</span>";

            sb.AppendLine($@"<tr{rowClass}>
            <td style='text-align:center; width:10px;padding:0;'>{copyBtn}</td>
            <td>{index.IndexName}</td>
            <td>{index.IndexType}</td>
            <td>{(index.IsUnique ? "YES" : "")}</td>
            <td>{(index.IsUniqueConstraint ? "YES" : "")}</td>
            <td>{index.KeyColumns}</td>
            <td>{index.IncludedColumns}</td>
            <td>{index.FilterDefinition}</td>
            </tr>");
            rowNum++;
        }

        sb.AppendLine("</table>");
        return sb.ToString();
    }

    private static string BuildCreateIndexStatement(string schema, string table, IndexDto index)
    {
        var unique = index.IsUnique ? "UNIQUE " : "";
        var clustered = string.Equals(index.IndexType, "CLUSTERED", StringComparison.OrdinalIgnoreCase) ? "CLUSTERED " :
            string.Equals(index.IndexType, "NONCLUSTERED", StringComparison.OrdinalIgnoreCase) ? "NONCLUSTERED " : "";
        var includeClause = string.IsNullOrWhiteSpace(index.IncludedColumns) ? "" : $" INCLUDE ({index.IncludedColumns})";
        var filterClause = string.IsNullOrWhiteSpace(index.FilterDefinition) ? "" : $" WHERE {index.FilterDefinition}";

        return $"CREATE {unique}{clustered}INDEX [{index.IndexName}] ON [{schema}].[{table}] ({index.KeyColumns}){includeClause}{filterClause};";
    }
    private static string GetTextColor(string backgroundColor)
    {
        if (string.IsNullOrWhiteSpace(backgroundColor))
            return "#000000";

        var cssColorMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        {"red", "FF0000"}, {"blue", "0000FF"}, {"green", "008000"},
        {"yellow", "FFFF00"}, {"orange", "FFA500"}, {"purple", "800080"},
        {"pink", "FFC0CB"}, {"black", "000000"}, {"white", "FFFFFF"},
        {"gray", "808080"}, {"grey", "808080"}, {"brown", "A52A2A"}
    };

        backgroundColor = backgroundColor.TrimStart('#');

        if (cssColorMap.ContainsKey(backgroundColor))
        {
            backgroundColor = cssColorMap[backgroundColor];
        }

        if (backgroundColor.Length != 6)
            return "#000000";

        try
        {
            int r = Convert.ToInt32(backgroundColor.Substring(0, 2), 16);
            int g = Convert.ToInt32(backgroundColor.Substring(2, 2), 16);
            int b = Convert.ToInt32(backgroundColor.Substring(4, 2), 16);

            double luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;

            return luminance > 0.5 ? "#000000" : "#ffffff";
        }
        catch
        {
            return "#000000";
        }
    }

    public class DestinationReportData
    {
        public string? ProcIndexPath { get; set; }
        public string? ViewIndexPath { get; set; }
        public string? TableIndexPath { get; set; }
        public string? UdtIndexPath { get; set; }
        public string? FunctionIndexPath { get; set; }
        public int ProcCount { get; set; }
        public int ViewCount { get; set; }
        public int TableCount { get; set; }
        public int UdtCount { get; set; }
        public int FunctionCount { get; set; }
        public string? ProcsCountText { get; set; }
        public string? ViewsCountText { get; set; }
        public string? TablesCountText { get; set; }
        public string? UdtsCountText { get; set; }
        public string? FunctionsCountText { get; set; }
    }
    #endregion

}
