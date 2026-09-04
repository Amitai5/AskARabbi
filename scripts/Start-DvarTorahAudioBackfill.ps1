[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][ValidatePattern('^(diaspora|israel):\d{4}-\d{2}-\d{2}$')][string]$WeekKey,
    [string]$JobName = 'askarabbi-weekly-dvar-torah-vnet',
    [string]$ResourceGroup = 'AARProduction',
    [string]$SubscriptionId = 'c2f8383e-2c4e-4822-82a7-506b2e2ddf38'
)

$ErrorActionPreference = 'Stop'
$dateText = $WeekKey.Substring($WeekKey.IndexOf(':') + 1)
$date = [DateOnly]::ParseExact($dateText, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
if ($date.DayOfWeek -ne [DayOfWeek]::Saturday) { throw 'The publication date must be a Saturday.' }

$account = az account show -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $account.id -ne $SubscriptionId) { throw 'Select the expected Azure subscription before starting a backfill.' }
$job = az containerapp job show --name $JobName --resource-group $ResourceGroup --only-show-errors -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'The configured generator job could not be read.' }
$container = @($job.properties.template.containers | Where-Object { $_.name -eq 'dvar-torah-generator' })
if ($container.Count -ne 1) { throw 'Expected exactly one dvar-torah-generator container.' }
if (@($container[0].env | Where-Object { $_.name -eq 'DvarTorahAudio__Enabled' -and $_.value -eq 'true' }).Count -ne 1) {
    throw 'Enable and configure private audio on the generator before requesting a backfill.'
}

# The execution API replaces the template. Retain every existing variable/secret reference,
# and add the backfill selector only to this execution, never to the recurring job.
$container[0].env = @($container[0].env | Where-Object { $_.name -ne 'DvarTorahAudio__BackfillWeekKey' }) + @(@{ name = 'DvarTorahAudio__BackfillWeekKey'; value = $WeekKey })
$body = @{ containers = $job.properties.template.containers }
if ($job.properties.template.initContainers) { $body.initContainers = $job.properties.template.initContainers }
if (!$PSCmdlet.ShouldProcess("$JobName / $WeekKey", 'Generate recording for the existing publication without regenerating text')) { return }

$token = az account get-access-token --resource https://management.azure.com/ -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Azure sign-in is required.' }
$headers = @{ Authorization = "Bearer $($token.accessToken)" }
$token = $null
try {
    $uri = "https://management.azure.com$($job.id)/start?api-version=2025-01-01"
    $execution = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 40 -Compress)
    [pscustomobject]@{ Job = $JobName; WeekKey = $WeekKey; Execution = $execution.name; Id = $execution.id }
}
catch {
    throw "The one-off audio execution could not be started (HTTP $([int]$_.Exception.Response.StatusCode)). Request details and credentials were not logged."
}
finally { $headers.Clear() }
