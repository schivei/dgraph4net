dotnet build -c Release
md ..\packages 2> nul
dotnet pack -c Release -o ..\packages -p:IncludeSymbols=false
dotnet nuget push ..\packages\*.nupkg -k %1 -s https://api.nuget.org/v3/index.json --skip-duplicate
