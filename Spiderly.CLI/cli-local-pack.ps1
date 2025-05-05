dotnet tool uninstall --global Spiderly.CLI
dotnet pack
dotnet tool install --global --add-source ./nupkg Spiderly.CLI

Read-Host -Prompt "Press Enter to exit"