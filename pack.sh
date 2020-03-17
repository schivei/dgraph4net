#!/bin/bash
dotnet build -c Release
mkdir -p ../packages &2> /dev/null
dotnet pack -c Release -o ../packages -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
dotnet nuget push ../packages/*.nupkg -k $1 -s https://api.nuget.org/v3/index.json --skip-duplicate
