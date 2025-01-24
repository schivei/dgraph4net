dotnet build -c Release .\src\Dgraph4Net.NewtonSoft.Json\Dgraph4Net.NewtonSoft.Json.csproj
dotnet build -c Release .\src\Dgraph4Net.System.Text.Json\Dgraph4Net.System.Text.Json.csproj
dotnet build -c Release .\src\Dgraph4Net\Dgraph4Net.csproj
dotnet build -c Release .\tools\Dgraph4Net.Tools\Dgraph4Net.Tools.csproj

dotnet pack -c Release -o .\build-packages -p:IncludeSymbols=false .\src\Dgraph4Net.NewtonSoft.Json\Dgraph4Net.NewtonSoft.Json.csproj
dotnet pack -c Release -o .\build-packages -p:IncludeSymbols=false .\src\Dgraph4Net.System.Text.Json\Dgraph4Net.System.Text.Json.csproj
dotnet pack -c Release -o .\build-packages -p:IncludeSymbols=false .\src\Dgraph4Net\Dgraph4Net.csproj
dotnet pack -c Release -o .\build-packages -p:IncludeSymbols=false .\tools\Dgraph4Net.Tools\Dgraph4Net.Tools.csproj
