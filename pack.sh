#!/bin/bash
dotnet build -c Release
dotnet pack -c Release -o ./build-packages -p:IncludeSymbols=false
dotnet nuget push ./build-packages/*.nupkg -k $1 -s https://api.nuget.org/v3/index.json --skip-duplicate
