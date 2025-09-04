// Copyright (c) SharpCrafters s.r.o. All rights reserved.
// This project is not open source. Please see the LICENSE.md file in the repository root for details.

using PostSharp.Engineering.BuildTools;
using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Build.Solutions;
using PostSharp.Engineering.BuildTools.Dependencies.Definitions;
using MetalamaDependencies = PostSharp.Engineering.BuildTools.Dependencies.Definitions.MetalamaDependencies.V2026_0;

var product = new Product(MetalamaDependencies.NopCommerce)
{
    Solutions = [new DotNetSolution("src\\NopCommerce.sln")],
    Dependencies = [DevelopmentDependencies.PostSharpEngineering, MetalamaDependencies.Metalama],
};

return new EngineeringApp(product).Run(args);
