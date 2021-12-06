#!/bin/bash
echo 'Restoring solution packages'
dotnet restore Dgraph4Net.sln

echo 'Building solution'
dotnet build -c Release Dgraph4Net.sln

echo 'Packing solution'
mkdir -p /tmp/build-packages
dotnet pack -c Release --no-build -o /tmp/build-packages -p:IncludeSymbols=false Dgraph4Net.sln

echo 'Retrieving packages version'
D4NC=`sed -n -e 's/\(.*\)<[Vv]ersion>\(.*\)<\/[Vv]ersion>\(.*\)/\2/p' src/Dgraph4Net.Core/Dgraph4Net.Core.csproj`
D4N=`sed -n -e 's/\(.*\)<[Vv]ersion>\(.*\)<\/[Vv]ersion>\(.*\)/\2/p' src/Dgraph4Net/Dgraph4Net.csproj`
D4NIC=`sed -n -e 's/\(.*\)<[Vv]ersion>\(.*\)<\/[Vv]ersion>\(.*\)/\2/p' src/Dgraph4Net.Identity.Core/Dgraph4Net.Identity.Core.csproj`
D4NI=`sed -n -e 's/\(.*\)<[Vv]ersion>\(.*\)<\/[Vv]ersion>\(.*\)/\2/p' src/Dgraph4Net.Identity/Dgraph4Net.Identity.csproj`

echo 'Retrieving nuget key'
Key="`<~/nuget.key`"

echo 'Publishing package Dgraph4Net.Core'
dotnet nuget push /tmp/build-packages/Dgraph4Net.Core.$D4NC.nupkg -k $Key -s https://api.nuget.org/v3/index.json --skip-duplicate

echo 'Publishing package Dgraph4Net'
dotnet nuget push /tmp/build-packages/Dgraph4Net.$D4N.nupkg -k $Key -s https://api.nuget.org/v3/index.json --skip-duplicate

echo 'Publishing package Dgraph4Net.Identity.Core'
dotnet nuget push /tmp/build-packages/Dgraph4Net.Identity.Core.$D4NIC.nupkg -k $Key -s https://api.nuget.org/v3/index.json --skip-duplicate

echo 'Publishing package Dgraph4Net.Identity'
dotnet nuget push /tmp/build-packages/Dgraph4Net.Identity.$D4NI.nupkg -k $Key -s https://api.nuget.org/v3/index.json --skip-duplicate
