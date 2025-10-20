param(
    [Parameter(Mandatory=$true)] [string] $CurrentZip,
    [string] $PrevZip = "",
    [string] $OutPatch = "patch.cdpipatch",
    [string] $Version = "",
    [string] $DownloadUrl = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$work = Join-Path $env:TEMP ("cdpipatch_" + [Guid]::NewGuid().ToString())
$curDir = Join-Path $work "current"
$prevDir = Join-Path $work "prev"
$payload = Join-Path $work "payload"

New-Item -ItemType Directory -Path $curDir,$prevDir,$payload -Force | Out-Null

Write-Output "Extracting current zip: $CurrentZip"
if (-not (Test-Path $CurrentZip)) { throw "CurrentZip not found: $CurrentZip" }
Expand-Archive -Path $CurrentZip -DestinationPath $curDir -Force

if (-not [string]::IsNullOrWhiteSpace($PrevZip) -and (Test-Path $PrevZip)) {
    Write-Output "Extracting previous zip: $PrevZip"
    Expand-Archive -Path $PrevZip -DestinationPath $prevDir -Force
} else {
    Write-Output "No previous zip provided or not found — payload will include all files."
}

# helper to get relative path with forward slashes
function Get-RelPath([string]$base, [string]$full) {
    $rel = $full.Substring($base.Length).TrimStart('\','/')
    return $rel -replace '\\','/'
}

$changedFiles = @()

Get-ChildItem -Path $curDir -Recurse -File | ForEach-Object {
    $cur = $_.FullName
    $rel = Get-RelPath $curDir $cur
    $prevPath = Join-Path $prevDir $rel
    $copy = $true
    if (Test-Path $prevPath) {
        $h1 = (Get-FileHash -Path $cur -Algorithm SHA256).Hash
        $h2 = (Get-FileHash -Path $prevPath -Algorithm SHA256).Hash
        if ($h1 -eq $h2) { $copy = $false }
    }
    if ($copy) {
        $dest = Join-Path $payload $rel
        $destDir = Split-Path $dest
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
        Copy-Item -Path $cur -Destination $dest -Force
        $changedFiles += $rel
    }
}

Write-Output ("Changed files count: {0}" -f $changedFiles.Count)
if ($changedFiles.Count -eq 0) {
    Write-Output "No changed files detected. Creating an empty patch (requirements only)."
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    # try to read VERSION file from current dir root
    $vfile = Join-Path $curDir "VERSION"
    if (Test-Path $vfile) {
        $Version = (Get-Content $vfile -Raw).Trim()
    } else {
        # fallback to timestamp
        $Version = (Get-Date).ToString("yyyyMMddHHmm")
    }
}

if ([string]::IsNullOrWhiteSpace($DownloadUrl)) {
    # by default leave blank or put placeholder
    $DownloadUrl = ""
}

$reqPath = Join-Path $payload "requirements.json"
$lines = @()
$lines += "{"
if ([string]::IsNullOrWhiteSpace($DownloadUrl)) {
    # if no url provided, still write version with empty url
    $lines += "    { version = `"$Version`", url=`""`" }"
} else {
    $lines += "    { version = `"$Version`", url=`"$DownloadUrl`" }"
}
$lines += "}"
$lines -join "`r`n" | Out-File -FilePath $reqPath -Encoding utf8

Write-Output "Wrote requirements in custom format to: $reqPath"

$tmpZip = Join-Path $work "patch_payload.zip"
if (Test-Path $tmpZip) { Remove-Item $tmpZip -Force }

if ((Get-ChildItem -Path $payload -Recurse -File | Measure-Object).Count -eq 0) {
    New-Item -ItemType File -Path (Join-Path $payload "._no_changes.txt") -Force | Out-Null
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($payload, $tmpZip)

Copy-Item -Path $tmpZip -Destination $OutPatch -Force
Write-Output "Patch created: $OutPatch"

if ($changedFiles.Count -gt 0) {
    Write-Output "Files included in patch:"
    $changedFiles | ForEach-Object { Write-Output " - $_" }
} else {
    Write-Output "Patch contains only requirements / marker file (no changed binaries)."
}

# Remove-Item -Recurse -Force $work
