// Copyright (c) SharpCrafters s.r.o. All rights reserved.
// This project is not open source. Please see the LICENSE.md file in the repository root for details.

using PostSharp.Engineering.BuildTools;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Build.Solutions;
using PostSharp.Engineering.BuildTools.Docker;
using MetalamaDependencies = PostSharp.Engineering.BuildTools.Dependencies.Definitions.MetalamaDependencies.V2027_0;

var preferredVersions = MetalamaDependencies.Family.PreferredVersions;

// The .NET 11 SDK, which global.json names as the main SDK of the product and which the build agent installs. The
// version is a literal instead of a member of the product family, because the .NET 11 SDK is still a preview and
// PostSharp.Engineering names only released feature bands. Keep it equal to the constant of the same name in the
// Metalama repository, and move both to MetalamaDependencies.Family.PreferredVersions.DotNetSdk once the .NET 11
// SDK is released. The solution targets net8.0, which the SDK compiles from the targeting packs that it restores
// from NuGet.
const string dotNet11SdkVersion = "11.0.100-preview.7.26381.103";

// The .NET 10 SDK, which stays installed beside the .NET 11 one, because the build tool of this repository targets
// net10.0 and the .NET 11 SDK carries no .NET 10 runtime. The version comes from the product family, so that it
// matches the feature band that the Visual Studio version of the family installs.
var dotNet10SdkVersion = preferredVersions.DotNetSdk.V_10_0;

var product = new Product(MetalamaDependencies.NopCommerce)
{
    OverriddenBuildAgentRequirements = new ContainerRequirements( ContainerHostKind.Windows )
    {
        Components =
        [
            new DotNetComponent( dotNet11SdkVersion, DotNetComponentKind.Sdk ),
            new DotNetComponent( dotNet10SdkVersion, DotNetComponentKind.Sdk ),
            new DotNetComponent( preferredVersions.DotNetRuntime.V_8_0, DotNetComponentKind.AspNetCoreRuntime ),
        ]
    },
    GenerateNuGetConfig = true,
    DotNetSdkVersion = new DotNetSdkVersion( dotNet11SdkVersion ) { AllowPrerelease = true },
    Solutions = [new DotNetSolution("src\\NopCommerce.sln")],
};

return new EngineeringApp(product).Run(args);
