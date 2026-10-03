@echo off
rem Launches the Mahjong Table desktop app (needs the .NET 10 SDK).
cd /d "%~dp0"
dotnet run --project Wpf\MahjongTable.csproj -c Release
