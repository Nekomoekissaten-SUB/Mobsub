Set-Location ..
dotnet publish -c Release -r win-x64
Copy-Item .\bin\Release\net10.0\win-x64\publish\Mobsub.AutomationBridge.dll F:\Software\Editor\Aegisub\ -Force
Copy-Item mobsub_bridge.lua F:\Software\Editor\Aegisub\automation\include\ -Force
Copy-Item mobsub_bridge_gen.lua F:\Software\Editor\Aegisub\automation\include\ -Force
