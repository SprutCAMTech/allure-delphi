using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using SprutCAMTech.Builder.MsDelphi;
using SprutCAMTech.BuildSystem.ManagerObject;
using SprutCAMTech.BuildSystem.SettingsReader;
using SprutCAMTech.BuildSystem.SettingsReader.Object;
using SprutCAMTech.Cleaner.Common;
using SprutCAMTech.HashGenerator;
using SprutCAMTech.HashGenerator.Common;
using SprutCAMTech.PackageManager.Nuget;
using SprutCAMTech.ProjectCache.Common;
using SprutCAMTech.Restorer.Nuget;
using SprutCAMTech.VersionManager.Common;
using SprutCAMTech.Package;
using SprutCAMTech.BuildSystem.Info;
using SprutCAMTech.BuildSystem.Variants;
using Newtonsoft.Json.Linq;

/// <inheritdoc />
class BuildSpaceSettings : SettingsObject
{
    const StringComparison IGNCASE = StringComparison.CurrentCultureIgnoreCase;

    const string DEVFEED = "https://nexus.office.sprut.ru/repository/dev-feed/index.json";
    const string MSTFEED = "https://nexus.office.sprut.ru/repository/master-feed/index.json";

    ReaderJson readerJson;

    /// <inheritdoc />
    /// <param name="configFiles"> Json configuration file paths </param>
    public BuildSpaceSettings(string[] configFiles) : base() {
        readerJson = new ReaderJson(Build.Logger);
        readerJson.ReadRules(configFiles);

        ReaderLocalVars = readerJson.LocalVars;
        ReaderDefines = readerJson.Defines;
        Projects = GetProjectList(configFiles);

        RegisterBSObjects();
    }

    /// <summary>
    /// Reads a list of projects from configFiles
    /// </summary>
    /// <param name="configFiles"> Json configuration file paths </param>
    private HashSet<string> GetProjectList(string[] configFiles) {
        var resultList = new HashSet<string>();
        foreach (var config in configFiles) {
            if (!File.Exists(config)) 
                continue;

            var jsonObj = JObject.Parse(File.ReadAllText(config));
            if (jsonObj.TryGetValue("projects", IGNCASE, out var jprojs)) {
                var configDir = Path.GetDirectoryName(config) + "";
                foreach (var jproj in jprojs) {
                    var projPath = Path.GetFullPath(Path.Combine(configDir, jproj.ToString()));
                    if (File.Exists(projPath) && !resultList.Contains(projPath))
                        resultList.Add(projPath);
                }
            }
        }
        return resultList;
    }

    /// <summary>
    /// Register Build System control objects
    /// </summary>
    private void RegisterBSObjects() {
        Variants = new() {
            new() {
                Name = "Debug_x64",
                Configurations = new() { [Variant.NodeConfig] = "Debug" },
                Platforms =      new() { [Variant.NodePlatform] = "Win64" }
            },
            new() {
                Name = "Release_x64",
                Configurations = new() { [Variant.NodeConfig] = "Release" },
                Platforms =      new() { [Variant.NodePlatform] = "Win64" }
            },
            new() {
                Name = "Debug_x32",
                Configurations = new() { [Variant.NodeConfig] = "Debug" },
                Platforms =      new() { [Variant.NodePlatform] = "Win32" }
            },
            new() {
                Name = "Release_x32",
                Configurations = new() { [Variant.NodeConfig] = "Release" },
                Platforms =      new() { [Variant.NodePlatform] = "Win32" }
            }
        };

        ManagerProps = new List<IManagerProp> {
            builderDelphiRelease,
            builderDelphiDebug,
            packageManagerNuget,
            versionManagerCommon,
            projectCacheCommon,
            hashGeneratorCommon,
            restorerNuget,
            cleanerCommon,
            cleanerCommonDelphi
        };

        foreach (var variant in Variants) {
            if (variant.Name.StartsWith("Release")) {
                ManagerNames.Add("builder_delphi", variant.Name, "builder_delphi_release");
            } else {
                ManagerNames.Add("builder_delphi", variant.Name, "builder_delphi_debug");
            }
            ManagerNames.Add("package_manager", variant.Name, "package_manager_nuget_rc");
            ManagerNames.Add("version_manager", variant.Name, "version_manager_common");
            ManagerNames.Add("project_cache", variant.Name, "project_cache_common");
            ManagerNames.Add("hash_generator", variant.Name, "hash_generator_main");
            ManagerNames.Add("restorer", variant.Name, "restorer_main");
            ManagerNames.Add("cleaner", variant.Name, "cleaner_default_main");
            ManagerNames.Add("cleaner_delphi", variant.Name, "cleaner_delphi_main");
        }
    }

        BuilderMsDelphiProps builderDelphiRelease => new() {
        Name = "builder_delphi_release",
        MsBuilderPath = readerJson.LocalVars["msbuilder_path"],
        EnvBdsPath = readerJson.LocalVars["env_bds"],
        RsVarsPath = readerJson.LocalVars["rsvars_path"],
        AutoClean = true,
        BuildParams = new Dictionary<string, string?>
        {
            ["-verbosity"] = "normal",
            ["-consoleloggerparameters"] = "ErrorsOnly",
            ["-nologo"] = "true",
            ["/p:DCC_Warnings"] = "false",
            ["/p:DCC_Hints"] = "false",
            ["/p:DCC_MapFile"] = "3",
            ["/p:DCC_AssertionsAtRuntime"] = "true",
            ["/p:DCC_DebugInformation"] = "2",
            ["/p:DCC_DebugDCUs"] = "false",
            ["/p:DCC_IOChecking"] = "true",
            ["/p:DCC_WriteableConstants"] = "true",
            ["/t:build"] = "true",
            ["/p:DCC_Optimize"] = "false",
            ["/p:DCC_GenerateStackFrames"] = "false",
            ["/p:DCC_LocalDebugSymbols"] = "false",
            ["/p:DCC_SymbolReferenceInfo"] = "0",
            ["/p:DCC_IntegerOverflowCheck"] = "false",
            ["/p:DCC_RangeChecking"] = "false"
        }
    };

    BuilderMsDelphiProps builderDelphiDebug {
        get {
            var bdelphi = new BuilderMsDelphiProps(builderDelphiRelease);
            bdelphi.Name = "builder_delphi_debug";
            bdelphi.BuildParams["/p:DCC_GenerateStackFrames"] = "true";
            bdelphi.BuildParams["/p:DCC_LocalDebugSymbols"] = "true";
            bdelphi.BuildParams["/p:DCC_SymbolReferenceInfo"] = "2";
            bdelphi.BuildParams["/p:DCC_IntegerOverflowCheck"] = "true";
            bdelphi.BuildParams["/p:DCC_RangeChecking"] = "true";
            return bdelphi;
        }
    }

    VersionManagerCommonProps versionManagerCommon {
        get {
            var branch = BuildInfo.JenkinsParam(JenkinsInfo.BranchName) + "";
            var vmcp = new VersionManagerCommonProps();
            vmcp.Name = "version_manager_common";
            vmcp.DepthSearch = 2;
            vmcp.StartValue = 1;
            vmcp.DevelopBranchName = branch.EndsWith("develop", IGNCASE) ? branch : "develop";
            vmcp.MasterBranchName =  branch.EndsWith("master", IGNCASE) ? branch : "master";
            vmcp.ReleaseBranchName = branch.EndsWith("release", IGNCASE) ? branch : "release";
            return vmcp;
        }
    }

    ProjectCacheCommonProps projectCacheCommon => new() {
        Name = "project_cache_common",
        IgnorePackageCache = false,
        TempDir = "./hash"
    };

    HashGeneratorCommonProps hashGeneratorCommon => new() {
        Name = "hash_generator_main",
        HashAlgorithmType = HashAlgorithmType.Sha256
    };

    RestorerNugetProps restorerNuget => new() { 
        Name = "restorer_main" 
    };

    CleanerCommonProps cleanerCommon => new() {
        Name = "cleaner_default_main",
        AllBuildResults = true
    };

    CleanerCommonProps cleanerCommonDelphi => new() {
        Name = "cleaner_delphi_main",
        AllBuildResults = true,
        Paths = new Dictionary<string, List<string>>
        {
            ["$project:output_dcu$"] = new() { "*.dcu" }
        }
    };

    PackageManagerNugetProps packageManagerNuget => new() {
        Name = "package_manager_nuget_rc",
        SetStorageInfo = SetStorageInfoFunc
    };

    private StorageInfo SetStorageInfoFunc(PackageAction action, IPackageProps? packageProps) {
        // APIKEY
        var si = new StorageInfo();
        si.ApiKey = readerJson.LocalVars["nuget_api_key"];

        // NUGET SOURCE
        var branch = BuildInfo.JenkinsParam(JenkinsInfo.BranchName) + "";
        if (!string.IsNullOrEmpty(branch))
            si.Url = branch.EndsWith("master", IGNCASE) ? MSTFEED : DEVFEED; // master/develop
        else
            si.Url = readerJson.LocalVars["nuget_source"]; //default

        Build.Logger.debug("SetStorageInfoFunc: branch=" + BuildInfo.JenkinsParam(JenkinsInfo.BranchName));
        Build.Logger.debug("SetStorageInfoFunc: url=" + si.Url);
        Build.Logger.debug("SetStorageInfoFunc: apiKey=" + si.ApiKey);

        return si;
    }
}