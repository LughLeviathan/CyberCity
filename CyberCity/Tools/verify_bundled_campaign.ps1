param([Parameter(Mandatory=$true)][string]$ProjectRoot)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$source = Get-Content -LiteralPath (Join-Path $root 'Assets/CyberCity/Scripts/Campaign.cs') -Raw
$start = $source.IndexOf('public static string Hash(string text)')
$end = $source.IndexOf('public static Campaign Parse(string json)')
if ($start -lt 0 -or $end -le $start) { throw 'Campaign hashing methods were not found.' }
$methods = $source.Substring($start, $end - $start)
Add-Type -TypeDefinition ("using System; using System.Text; using System.Security.Cryptography; public static class CampaignHashProbe { " + $methods + " }")
$json = [IO.File]::ReadAllText((Join-Path $root 'Assets/CyberCity/Resources/campaign.json'))
$expected = [IO.File]::ReadAllText((Join-Path $root 'Assets/CyberCity/Resources/campaign_hash.txt')).Trim()
if ($source.Contains('BundledHash(local)')) {
    $actual = [CampaignHashProbe]::BundledHash($json)
    $lf = $json.Replace("`r`n", "`n")
    if ([CampaignHashProbe]::BundledHash($lf) -ne [CampaignHashProbe]::BundledHash($lf.Replace("`n", "`r`n"))) { throw 'Line-ending regression.' }
    if ([CampaignHashProbe]::BundledHash($lf.Replace('Professor Byte', 'Professor Bite')) -eq $expected) { throw 'Corrupted content accepted.' }
    if ([CampaignHashProbe]::Hash($lf) -eq [CampaignHashProbe]::Hash($lf.Replace("`n", "`r`n"))) { throw 'Optional hash contract changed.' }
} else {
    $actual = [CampaignHashProbe]::Hash($json)
}
if ($actual -ne $expected) {
    Write-Output 'FAIL: Bundled campaign checksum failed.'
    exit 1
}
Write-Output 'PASS: LF/CRLF bundle verified; changed content rejected; optional hash unchanged.'
exit 0
