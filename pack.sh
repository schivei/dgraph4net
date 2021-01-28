#!/bin/bash
dotnet tool install -g dotnet-version-cli

D4NC=`dotnet version -f src/Dgraph4Net.Core/Dgraph4Net.Core.csproj | grep '    ' | cut -d' ' -f 9`
D4N=`dotnet version -f src/Dgraph4Net/Dgraph4Net.csproj | grep '    ' | cut -d' ' -f 9`
D4NIC=`dotnet version -f src/Dgraph4Net.Identity.Core/Dgraph4Net.Identity.Core.csproj | grep '    ' | cut -d' ' -f 9`
D4NI=`dotnet version -f src/Dgraph4Net.Identity/Dgraph4Net.Identity.csproj | grep '    ' | cut -d' ' -f 9`

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

dotnet nuget push ./build-packages/Dgraph4Net.Core.$D4NC.nupkg -k $1 -s https://api.nuget.org/v3/index.json --skip-duplicate
dotnet nuget push ./build-packages/Dgraph4Net.$D4N.nupkg -k $1 -s https://api.nuget.org/v3/index.json --skip-duplicate
dotnet nuget push ./build-packages/Dgraph4Net.Identity.Core.$D4NIC.nupkg -k $1 -s https://api.nuget.org/v3/index.json --skip-duplicate
dotnet nuget push ./build-packages/Dgraph4Net.Identity.$D4NI.nupkg -k $1 -s https://api.nuget.org/v3/index.json --skip-duplicate
