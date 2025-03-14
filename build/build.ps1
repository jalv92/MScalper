# Script de compilación PowerShell
# Este script se encargará de compilar el proyecto

dotnet new classlib -n OrderFlowScalper -o ../output/temp
Copy-Item ../src/**/*.cs ../output/temp/

# Añadir referencias
# dotnet add ../output/temp/OrderFlowScalper.csproj reference ../src/References/NinjaTrader.Client.dll
# dotnet add ../output/temp/OrderFlowScalper.csproj reference ../src/References/NinjaTrader.Core.dll

# Compilar
# dotnet build ../output/temp/OrderFlowScalper.csproj -c Release

# Copiar DLL compilada
# Copy-Item ../output/temp/bin/Release/net48/OrderFlowScalper.dll ../output/ 