
(& dotnet nuget locals http-cache -c) | Out-Null
& dotnet run --project "$PSScriptRoot\eng\src\BuildMetalamaTestsNopCommerce.csproj" -- $args
exit $LASTEXITCODE

