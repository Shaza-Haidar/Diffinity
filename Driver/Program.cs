using Diffinity;
using System.Diagnostics;

internal class Program
{
    static void Main(string[] args)
    {
        var Corewell = new DbServer("Corewell", Environment.GetEnvironmentVariable("connectionString"));
        var CMH = new DbServer("CMH", Environment.GetEnvironmentVariable("cmhCs"));
        var Albany = new DbServer("Albany", Environment.GetEnvironmentVariable("albanymedcs"));
        var TGH = new DbServer("TGH", Environment.GetEnvironmentVariable("tghcs"));
        var Dev002 = new DbServer("dec29v3", Environment.GetEnvironmentVariable("dev2Cs"));

        // ---- to compare one stored procedure from one database to all other databases showing unchanged, use the following method
        string reportPath = DbComparer.CompareOneProcVsAll(Dev002, new[] { Albany, TGH, CMH }, "patientApp.spGetShortcuts5", filter: DbObjectFilter.ShowUnchanged);

        // ---- to compare one database to all other databases, use the following method:
        // string reportPath = DbComparer.CompareOneVsAll(CMH, Albany, TGH, Dev002);

        //=== To show unchanged objects in the report ===
        //string reportPath = DbComparer.CompareOneVsAll(Corewell, new[] { CMH, DEV002, alb }, filter: DbObjectFilter.ShowUnchanged);

        Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });

        #region Comparison of two databases
        //var DEV002 = new DbServer("DEV002", Environment.GetEnvironmentVariable("dev2Cs"));
        //var Corewell = new DbServer("Corewell", Environment.GetEnvironmentVariable("connectionString"));
        //var CMH      = new DbServer("CMH", Environment.GetEnvironmentVariable("cmhCs"));

        //string IndexPage = DbComparer.Compare(Corewell,CMH);
        //var psi = new ProcessStartInfo
        //{
        //    FileName = IndexPage,
        //    UseShellExecute = true
        //};
        #endregion

        #region Optional
        // You can optionally pass any of the following parameters:
        // logger: your custom ILogger instance
        // outputFolder: path to save the results (string)
        // makeChange: whether to apply changes (ComparerAction.ApplyChanges,ComparerAction.DoNotApplyChanges)
        // filter: filter rules (DbObjectFilter.ShowUnchanged,DbObjectFilter.HideUnchanged)
        // run: execute comparison on specific dbObject(Run.Proc,Run.View,Run.Table,Run.ProcView,Run.ProcTable,Run.ViewTable,Run.All)
        //
        // Example:
        // string IndexPage = DbComparer.Compare(MyDbV1, MyDbV2, logger: myLogger, outputFolder: "customPath", makeChange: true);
        #endregion

    }
}
