#tool nuget:?package=vswhere&version=2.6.7
#tool nuget:?package=NUnit.ConsoleRunner&version=3.4.0

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("build-target", "Default");
var version = Argument("build-version", "1.0.0.0");
var configuration = Argument("build-configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

MSBuildSettings mspacksettings;
DotNetCoreMSBuildSettings dnbuildsettings;
DotNetCorePackSettings dnpacksettings;

bool windowsAndroidSupported;

private void PackProject(string filePath)
{
    // Windows and Linux dotnet tool does not allow building of .NET
    // projects, as such we must call msbuild on these platforms.
    if (IsRunningOnWindows())
        DotNetCorePack(filePath, dnpacksettings);
    else
        MSBuild(filePath, mspacksettings);
}

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Prep")
    .Does(() =>
{
    mspacksettings = new MSBuildSettings();
    mspacksettings.Verbosity = Verbosity.Minimal;
    mspacksettings.Configuration = configuration;
    mspacksettings = mspacksettings.WithProperty("Version", version);
    mspacksettings = mspacksettings.WithTarget("Pack");

    dnbuildsettings = new DotNetCoreMSBuildSettings();
    dnbuildsettings = dnbuildsettings.WithProperty("Version", version);

    dnpacksettings = new DotNetCorePackSettings();
    dnpacksettings.MSBuildSettings = dnbuildsettings;
    dnpacksettings.Verbosity = DotNetCoreVerbosity.Minimal;
    dnpacksettings.Configuration = configuration;

    if (IsRunningOnWindows())
    {
        // Get a version of msbuild with the mobile development workload, null on failure
        DirectoryPath vsLatest = VSWhereLatest(new VSWhereLatestSettings { Requires = "Component.Xamarin"});

        if (vsLatest != null)
        {
            Information(vsLatest.FullPath);
            var files = GetFiles(vsLatest.FullPath + "/**/MSBuild.exe");
            if (files.Any())
            {
                var msbuildPath = files.First();
                Information($"Using MSBuild at \"{msbuildPath}\".");
                mspacksettings.ToolPath = msbuildPath;
                windowsAndroidSupported = true;
            }
        }
    }
});

Task("BuildDesktopGL")
    .IsDependentOn("Prep")
    .Does(() =>
{
    DotNetCoreRestore("MonoGame.Framework.DesktopGL.sln");
    PackProject("MonoGame.Framework/MonoGame.Framework.DesktopGL.csproj");
});

Task("BuildWindowsDX")
    .IsDependentOn("Prep")
    .Does(() =>
{
    DotNetCoreRestore("MonoGame.Framework.WindowsDX.sln");
    PackProject("MonoGame.Framework/MonoGame.Framework.WindowsDX.csproj");
});

Task("BuildAndroid")
    .IsDependentOn("Prep")
    .Does(() =>
{
    if (IsRunningOnWindows())
    {
        if (windowsAndroidSupported)
        {
            DotNetCoreRestore("MonoGame.Framework/MonoGame.Framework.AndroidCore.csproj");
            MSBuild("MonoGame.Framework/MonoGame.Framework.AndroidCore.csproj", mspacksettings);
        }
        else
        {
            Warning("MSBuild not found or Xamarin is not installed. Skipping Android build.");
        }
    } 
    else
    {
        Warning("Android build is only supported on Windows");
    }
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("BuildDesktopGL")
    .IsDependentOn("BuildWindowsDX")
    .IsDependentOn("BuildAndroid");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
