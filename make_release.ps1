$staging = "$env:TEMP\DVRouteManager_stage"
$dest = "D:\Documents\DVRouteManager\DVRouteManager.zip"

Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $staging | Out-Null

Copy-Item "D:\Documents\DVRouteManager\DVRouteManager\bin\Debug\DVRouteManager.dll" $staging
Copy-Item "D:\Documents\DVRouteManager\DVRouteManager\bin\Debug\Priority Queue.dll" $staging
Copy-Item "D:\Documents\DVRouteManager\Info.json" $staging

New-Item -ItemType Directory -Force "$staging\Resources\audio" | Out-Null
Copy-Item "D:\Documents\DVRouteManager\DVRouteManager\Resources\audio\*" "$staging\Resources\audio"

Remove-Item $dest -Force -ErrorAction SilentlyContinue
Compress-Archive -Path $staging -DestinationPath $dest -Force
Remove-Item $staging -Recurse -Force

Write-Host "Done: $dest"
Get-Item $dest | Select-Object Name, @{N='Size_KB';E={[math]::Round($_.Length/1KB,1)}}
