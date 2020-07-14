#!/bin/bash
dotnet restore src/Dgraph4Net.Core/Dgraph4Net.Core.csproj
dotnet restore src/Dgraph4Net/Dgraph4Net.csproj
dotnet restore src/Dgraph4Net.Identity.Core/Dgraph4Net.Identity.Core.csproj
dotnet restore src/Dgraph4Net.Identity/Dgraph4Net.Identity.csproj

dotnet build -c Release --no-restore src/Dgraph4Net.Core/Dgraph4Net.Core.csproj
dotnet build -c Release --no-restore src/Dgraph4Net/Dgraph4Net.csproj
dotnet build -c Release --no-restore src/Dgraph4Net.Identity.Core/Dgraph4Net.Identity.Core.csproj
dotnet build -c Release --no-restore src/Dgraph4Net.Identity/Dgraph4Net.Identity.csproj

dotnet pack -c Release --no-build -o ./build-packages -p:IncludeSymbols=false src/Dgraph4Net.Core/Dgraph4Net.Core.csproj
dotnet pack -c Release --no-build -o ./build-packages -p:IncludeSymbols=false src/Dgraph4Net/Dgraph4Net.csproj
dotnet pack -c Release --no-build -o ./build-packages -p:IncludeSymbols=false src/Dgraph4Net.Identity.Core/Dgraph4Net.Identity.Core.csproj
dotnet pack -c Release --no-build -o ./build-packages -p:IncludeSymbols=false src/Dgraph4Net.Identity/Dgraph4Net.Identity.csproj
dotnet nuget push ./build-packages/*.nupkg -k $1 -s https://api.nuget.org/v3/index.json --skip-duplicate
