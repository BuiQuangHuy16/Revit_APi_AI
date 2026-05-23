using Aplication.Commands;
using Aplication.Commands.AutoDimColumns;
using Aplication.Commands.DuplicateLegend;
using Aplication.Commands.DuplicateSheet;
using Aplication.Commands.ExportSchedule;
using Aplication.Commands.LegendAssociate;
using Aplication.Commands.QuickSelect;
using Nice3point.Revit.Toolkit.External;
using Serilog;
using Serilog.Events;

namespace Aplication
{
    /// <summary>
    ///     Application entry point
    /// </summary>
    [UsedImplicitly]
    public class Application : ExternalApplication
    {
        public override void OnStartup()
        {
            CreateLogger();
            CreateRibbon();
        }

        public override void OnShutdown()
        {
            Log.CloseAndFlush();
        }

        private void CreateRibbon()
        {
            var panel = Application.CreatePanel("Commands", "Aplication");

            panel.AddPushButton<StartupCommand>("Hello World")
                .SetImage("/Aplication;component/Resources/Icons/RibbonIcon16.png")
                .SetLargeImage("/Aplication;component/Resources/Icons/RibbonIcon32.png");

            panel.AddPushButton<DuplicateSheetsCommand>("Duplicate\nSheets")
                .SetImage("/Aplication;component/Resources/Icons/RibbonIcon16.png")
                .SetLargeImage("/Aplication;component/Resources/Icons/RibbonIcon32.png");

            panel.AddPushButton<DuplicateLegendCommand>("Duplicate\nLegend")
                .SetImage("/Aplication;component/Resources/Icons/RibbonIcon16.png")
                .SetLargeImage("/Aplication;component/Resources/Icons/RibbonIcon32.png");

            panel.AddPushButton<LegendAssociateCommand>("Legend\nAssociate")
                .SetImage("/Aplication;component/Resources/Icons/RibbonIcon16.png")
                .SetLargeImage("/Aplication;component/Resources/Icons/RibbonIcon32.png");

            panel.AddPushButton<AutoDimColumnsCommand>("Auto Dim\nColumns")
                .SetImage("/Aplication;component/Resources/Icons/RibbonIcon16.png")
                .SetLargeImage("/Aplication;component/Resources/Icons/RibbonIcon32.png");

            panel.AddPushButton<QuickSelectCommand>("Quick\nSelect")
                .SetImage("/Aplication;component/Resources/Icons/RibbonIcon16.png")
                .SetLargeImage("/Aplication;component/Resources/Icons/RibbonIcon32.png");

            panel.AddPushButton<ExportScheduleCommand>("Export\nSchedule")
                .SetImage("/Aplication;component/Resources/Icons/RibbonIcon16.png")
                .SetLargeImage("/Aplication;component/Resources/Icons/RibbonIcon32.png");
        }

        private static void CreateLogger()
        {
            const string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Debug(LogEventLevel.Debug, outputTemplate)
                .MinimumLevel.Debug()
                .CreateLogger();

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var exception = (Exception)args.ExceptionObject;
                Log.Fatal(exception, "Domain unhandled exception");
            };
        }
    }
}