$image = Get-ChildItem -File |
    Where-Object Extension -match '\.(png|jpg|jpeg|webp)$' |
    Select-Object -First 1

if ($null -eq $image)
{
    Write-Error "Nie znaleziono pliku graficznego."
    exit 1
}

foreach ($blur in @(200, 120, 75, 50, 30, 15))
{
    $outputFile = "{0}-blur-{1}{2}" -f $image.BaseName, $blur, $image.Extension

    Write-Host "Tworzenie $outputFile"

    magick $image.FullName -blur "0x$blur" $outputFile
}
