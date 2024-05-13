using System.IO;
using Nuke.Common;
using SprutCAMTech.BuildSystem.BuildSpace;
using SprutCAMTech.BuildSystem.BuildSpace.Common;
using SprutCAMTech.BuildSystem.Info;
using SprutCAMTech.BuildSystem.Logging;
using SprutCAMTech.BuildSystem.Loggers;
using SprutCAMTech.BuildSystem.SettingsReader;
using SprutCAMTech.BuildSystem.SettingsReader.Object;

/// <inheritdoc />
public class Build : NukeBuild
{
    /// <summary>
    /// Calling method when running
    /// </summary>
    public static int Main() => Execute<Build>(x => x.Compile);
    
    /// <summary>
    /// Configuration to build - 'Debug' (default) or 'Release'
    /// </summary>
    [Parameter("Settings provided for running build space")]
    public readonly string Variant = "Release_x64";

    /// <inheritdoc/>
    public static ILogger Logger = new LoggerConsole();

    private IBuildSpace? _buildSpace;
    /// <summary> Build Space instance </summary>
    private IBuildSpace BSpace => _buildSpace ??= InitBuildSpace();

    private IBuildSpace InitBuildSpace() {
        var localJsonFile = Path.Combine(RootDirectory, $"buildspace.{BuildInfo.RunParams[RunInfo.Local]}.json");
        var bsJsonFile = Path.Combine(RootDirectory, "buildspace.json");
        SettingsObject config = new BuildSpaceSettings(new[] {bsJsonFile, localJsonFile}, RootDirectory, Logger);
        return new BuildSpaceCommon(Logger, RootDirectory + "//temp", SettingsReaderType.Object, config);
    }

    /// <summary>
    /// Set build constants
    /// </summary>
    private Target SetBuildInfo => _ => _
    .Executes(() => {
        Logger.setMinLevel(SprutCAMTech.BuildSystem.Logging.LogLevel.debug);

        // params provided to command line
        BuildInfo.RunParams[RunInfo.Variant] = Variant;
        BuildInfo.RunParams[RunInfo.NoRestore] = "false";
        BuildInfo.RunParams[RunInfo.NoCheckRestoredFiles] = "false";
        BuildInfo.RunParams[RunInfo.Local] = "local";
        foreach (var runParam in BuildInfo.RunParams)
            Logger.debug($"{runParam.Key}: {runParam.Value}");
        
        // current branch
        BuildInfo.JenkinsParams.Add(JenkinsInfo.BranchName, "develop");
        Logger.debug("Current branch: " + BuildInfo.JenkinsParams[JenkinsInfo.BranchName]);
    });

    /// <summary> Restoring build space </summary>
    private Target Restore => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            BSpace.Projects.Restore(Variant);
        });

    /// <summary> Parameterized compile </summary>
    private Target Compile => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            BSpace.Projects.Compile(Variant, true);
        });

    /// <summary> Compiling projects for all release configurations </summary>
    private Target CompileAllRelease => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            BSpace.Projects.Compile("Release_x32", true);
            BSpace.Projects.Compile("Release_x64", true);
        });

    /// <summary> Publishing packages </summary>
    private Target Deploy => _ => _
        .DependsOn(SetBuildInfo)
        .DependsOn(CompileAllRelease)
        .Executes(() => {
            BSpace.Projects.Deploy(Variant, true);
        });

    /// <summary> Cleaning build results </summary>
    private Target Clean => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            BSpace.Projects.Clean(Variant);
        });
}