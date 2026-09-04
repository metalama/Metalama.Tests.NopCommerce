// Copyright (c) SharpCrafters s.r.o. All rights reserved.
// This project is not open source. Please see the LICENSE.md file in the repository root for details.

using PostSharp.Engineering.BuildTools;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Build.Solutions;
using PostSharp.Engineering.BuildTools.Docker;
using MetalamaDependencies = PostSharp.Engineering.BuildTools.Dependencies.Definitions.MetalamaDependencies.V2027_0;

// The only .NET SDK of the build agent, and the one pinned in global.json. The version comes from the product
// family, so that it matches the feature band that the Visual Studio version of the family installs. The solution
// targets net8.0, which the .NET 10 SDK compiles from the targeting packs that it restores from NuGet.
var dotNetSdkVersion = MetalamaDependencies.Family.PreferredVersions.DotNetSdk.V_10_0;

var product = new Product(MetalamaDependencies.NopCommerce)
{
    OverriddenBuildAgentRequirements = new ContainerRequirements( ContainerHostKind.Windows )
    {
        Components =
        [
            new DotNetComponent( dotNetSdkVersion, DotNetComponentKind.Sdk ),
            new DotNetComponent( MetalamaDependencies.Family.PreferredVersions.DotNetRuntime.V_8_0, DotNetComponentKind.AspNetCoreRuntime ),
        ]
    },
    GenerateNuGetConfig = true,
    DotNetSdkVersion = new DotNetSdkVersion( dotNetSdkVersion ),
    Solutions = [new DotNetSolution("src\\NopCommerce.sln")],
};

return new EngineeringApp(product).Run(args);
