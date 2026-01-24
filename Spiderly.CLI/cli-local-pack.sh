#!/bin/bash

dotnet tool uninstall --global Spiderly.CLI
dotnet pack

latest=$(ls -t ./nupkg/*.nupkg 2>/dev/null | head -1)
version=$(echo "$latest" | grep -oP '(?<=Spiderly\.CLI\.)\d+\.\d+\.\d+(-[a-z0-9\.]+)?(?=\.nupkg)')

dotnet tool install --global --add-source ./nupkg Spiderly.CLI --version "$version"

read -p "Press Enter to exit"
