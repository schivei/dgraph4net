dotnet build -c Release
dotnet pack -c Release -o .\build-packages -p:IncludeSymbols=false
set /p Key=<%userprofile%\nuget.key
dotnet nuget push .\build-packages\*.nupkg -k %Key% -s https://api.nuget.org/v3/index.json --skip-duplicate
