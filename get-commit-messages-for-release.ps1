git log $(git describe --tags --abbrev=0)..HEAD --pretty=format:"- %s"

Read-Host -Prompt "Press Enter to exit"