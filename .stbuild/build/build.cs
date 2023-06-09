using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Nuke.Common;
using Nuke.Common.CI.Jenkins;
using SprutCAMTech.Logging;
using SprutCAMTech.Logging.Console;
using SprutCAMTech.BuildSystem;
using SprutCAMTech.BuildSystem.Info;
using SprutCAMTech.BuildSystem.SettingsReader;
using SprutCAMTech.BuildSystem.SettingsReader.Object;

/// <inheritdoc />
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
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
    private IBuildSpace BSpace { 
        get
        {
            _buildSpace ??= InitBuildSpace();
            return _buildSpace;
        }
    }

    private IBuildSpace InitBuildSpace() {
        var localJsonFile = Path.Combine(RootDirectory, $"buildspace.{BuildInfo.RunParams[RunInfo.Local]}.json");
        var bsJsonFile = Path.Combine(RootDirectory, "buildspace.json");
        SettingsObject config = new BuildSpaceSettings(new[] {bsJsonFile, localJsonFile});
        return new BuildSpace(Logger, RootDirectory + "//temp", SettingsReaderType.Object, config);
    }

    /// <summary>
    /// Set build constants
    /// </summary>
    private Target SetBuildInfo => _ => _
    .Executes(() => {
        Logger.setMinLevel(SprutCAMTech.Logging.LogLevel.info);

        // params provided to command line
        BuildInfo.RunParams[RunInfo.Variant] = Variant;
        BuildInfo.RunParams[RunInfo.NoRestore] = "false";
        BuildInfo.RunParams[RunInfo.NoCheckRestoredFiles] = "false";
        BuildInfo.RunParams[RunInfo.Local] = Jenkins.Instance != null ? "jenkins" : "local";
        foreach (var runParam in BuildInfo.RunParams)
            Logger.info($"{runParam.Key}: {runParam.Value}");

        // jenkins params
        if (Jenkins.Instance != null) {
            BuildInfo.ReadJenkinsParams();
            foreach (var runParam in BuildInfo.JenkinsParams)
                Logger.info($"{runParam.Key}: {runParam.Value}");
        } else
            BuildInfo.JenkinsParams[JenkinsInfo.BranchName] = "develop"; // dev branch by default
    });

    /// <summary> Restoring build space </summary>
    private Target Restore => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            BSpace.Restore(Variant);
        });

    /// <summary> Parameterized compile </summary>
    private Target Compile => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            BSpace.Projects.Compile(Variant, true);
        });

    /// <summary> Publishing packages </summary>
    private Target Deploy => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            // !! публикация в master feed !!
            BuildInfo.JenkinsParams[JenkinsInfo.BranchName] = "master";
            BSpace.Projects.Compile("Release_x32", true);
            BSpace.Projects.Compile("Release_x64", true);
            BSpace.Projects.Deploy(Variant);
        });

    /// <summary> Cleaning build results </summary>
    private Target Clean => _ => _
        .DependsOn(SetBuildInfo)
        .Executes(() => {
            BSpace.Projects.Clean(Variant);
        });
}