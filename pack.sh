#!/bin/bash
dotnet build -c Release ./src/Dgraph4Net.Core/Dgraph4Net.Core.csproj
dotnet build -c Release ./src/DGraph4Net/Dgraph4Net.csproj
dotnet build -c Release ./tools/Dgraph4Net.Tools/Dgraph4Net.Tools.csproj

dotnet pack -c Release -o ./build-packages -p:IncludeSymbols=false ./src/Dgraph4Net.Core/Dgraph4Net.Core.csproj
dotnet pack -c Release -o ./build-packages -p:IncludeSymbols=false ./src/DGraph4Net/Dgraph4Net.csproj
dotnet pack -c Release -o ./build-packages -p:IncludeSymbols=false ./tools/Dgraph4Net.Tools/Dgraph4Net.Tools.csproj
