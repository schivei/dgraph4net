dotnet build -c Release
dotnet pack -c Release -o .\build-packages -p:IncludeSymbols=false
