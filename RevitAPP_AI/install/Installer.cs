using Installer;
using WixSharp;
using WixSharp.CommonTasks;
using WixSharp.Controls;

const string outputName = "RevitAPP_AI";
const string projectName = "RevitAPP_AI";

var versioning = Versioning.CreateFromVersionStringAsync(args[0]);
var project = new Project
{
    OutDir = "output",
    Name = projectName,
    Platform = Platform.x64,
    UI = WUI.WixUI_FeatureTree,
    MajorUpgrade = MajorUpgrade.Default,
    GUID = new Guid("24B6350A-D1D0-48CC-A47C-22716B9C535B"),
    BannerImage = @"install\Resources\Icons\BannerImage.png",
    BackgroundImage = @"install\Resources\Icons\BackgroundImage.png",
    Version = versioning.VersionPrefix,
    ControlPanelInfo =
    {
        Manufacturer = Environment.UserName,
        ProductIcon = @"install\Resources\Icons\ShellIcon.ico"
    }
};

var wixEntities = Generator.GenerateWixEntities(args[1..]);
project.RemoveDialogsBetween(NativeDialogs.WelcomeDlg, NativeDialogs.CustomizeDlg);

BuildSingleUserMsi();
BuildMultiUserUserMsi();

void BuildSingleUserMsi()
{
    project.Scope = InstallScope.perUser;
    project.OutFileName = $"{outputName}-{versioning.Version}-SingleUser";
    project.Dirs =
    [
        new InstallDir(@"%AppDataFolder%\Autodesk\Revit\Addins\", wixEntities)
    ];
    project.BuildMsi();
}

void BuildMultiUserUserMsi()
{
    project.Scope = InstallScope.perMachine;
    project.OutFileName = $"{outputName}-{versioning.Version}-MultiUser";
    project.Dirs =
    [
        new InstallDir(versioning.VersionPrefix.Major >= 2027 ? @"%ProgramFiles%\Autodesk\Revit\Addins" : @"%CommonAppDataFolder%\Autodesk\Revit\Addins", wixEntities)
    ];
    project.BuildMsi();
}