#!/bin/bash
echo 'Installing Version Checker'
dotnet tool install -g dotnet-version-cli


echo 'Restoring solution packages'
dotnet restore Dgraph4Net.sln

echo 'Building solution'
dotnet build -c Release Dgraph4Net.sln

echo 'Packing solution'
dotnet pack -c Release --no-build -o ./build-packages -p:IncludeSymbols=false Dgraph4Net.sln

echo 'Retrieving packages version'
D4NC=`dotnet version -f src/Dgraph4Net.Core/Dgraph4Net.Core.csproj | grep '    ' | cut -d' ' -f 9`
D4N=`dotnet version -f src/Dgraph4Net/Dgraph4Net.csproj | grep '    ' | cut -d' ' -f 9`
D4NIC=`dotnet version -f src/Dgraph4Net.Identity.Core/Dgraph4Net.Identity.Core.csproj | grep '    ' | cut -d' ' -f 9`
D4NI=`dotnet version -f src/Dgraph4Net.Identity/Dgraph4Net.Identity.csproj | grep '    ' | cut -d' ' -f 9`

echo 'Publishing package Dgraph4Net.Core'
dotnet nuget push ./build-packages/Dgraph4Net.Core.$D4NC.nupkg -k $1 -s https://api.nuget.org/v3/index.json --skip-duplicate

echo 'Publishing package Dgraph4Net'
dotnet nuget push ./build-packages/Dgraph4Net.$D4N.nupkg -k $1 -s https://api.nuget.org/v3/index.json --skip-duplicate

echo 'Publishing package Dgraph4Net.Identity.Core'
dotnet nuget push ./build-packages/Dgraph4Net.Identity.Core.$D4NIC.nupkg -k $1 -s https://api.nuget.org/v3/index.json --skip-duplicate

echo 'Publishing package Dgraph4Net.Identity'
dotnet nuget push ./build-packages/Dgraph4Net.Identity.$D4NI.nupkg -k $1 -s https://api.nuget.org/v3/index.json --skip-duplicate
