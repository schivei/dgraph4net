.\nuget.bat
set /p Key=<%userprofile%\nuget.key
dotnet nuget push .\build-packages\*.nupkg -k %Key% -s https://api.nuget.org/v3/index.json --skip-duplicate
