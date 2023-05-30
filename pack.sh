#!/bin/bash
dotnet build -c Release ./src/Dgraph4Net.Core/Dgraph4Net.Core.csproj
dotnet build -c Release ./src/DGraph4Net/Dgraph4Net.csproj
dotnet build -c Release ./tools/Dgraph4Net.Tools/Dgraph4Net.Tools.csproj

dotnet pack -c Release -o ./build-packages -p:IncludeSymbols=false ./src/Dgraph4Net.Core/Dgraph4Net.Core.csproj
dotnet pack -c Release -o ./build-packages -p:IncludeSymbols=false ./src/DGraph4Net/Dgraph4Net.csproj
dotnet pack -c Release -o ./build-packages -p:IncludeSymbols=false ./tools/Dgraph4Net.Tools/Dgraph4Net.Tools.csproj

Key="`<~/nuget.key`"

echo 'Publishing package Dgraph4Net.Core'
dotnet nuget push /tmp/build-packages/Dgraph4Net.Core.*.nupkg -k $Key -s https://api.nuget.org/v3/index.json --skip-duplicate

echo 'Publishing package Dgraph4Net'
dotnet nuget push /tmp/build-packages/Dgraph4Net.*.nupkg -k $Key -s https://api.nuget.org/v3/index.json --skip-duplicate

echo 'Publishing package Dgraph4Net.Identity.Core'
dotnet nuget push /tmp/build-packages/Dgraph4Net.Tools.*.nupkg -k $Key -s https://api.nuget.org/v3/index.json --skip-duplicate
