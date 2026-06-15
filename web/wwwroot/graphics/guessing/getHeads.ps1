Get-ChildItem -Directory | ForEach-Object {
    $parentFolderName = $_.Name

    $headFile = Get-ChildItem -Path $_.FullName -Filter "Head.webp" -File |
        Select-Object -First 1

    if ($headFile) {
        Copy-Item $headFile.FullName "$parentFolderName.webp" -Force
    }
}