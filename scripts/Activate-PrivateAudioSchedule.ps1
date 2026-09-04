[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'
$subscriptionId = 'c2f8383e-2c4e-4822-82a7-506b2e2ddf38'
$prefix = "/subscriptions/$subscriptionId/resourceGroups/AARProduction/providers/Microsoft.App/jobs"
$apiVersion = '2025-01-01'
$account = az account show -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $account.id -ne $subscriptionId) { throw 'Select the AskARabbi subscription first.' }
$token = az account get-access-token --resource https://management.azure.com/ -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Azure sign-in is required.' }
$headers = @{ Authorization = "Bearer $($token.accessToken)" }
$token = $null

function Invoke-JobArm([string]$Method, [string]$Path, [object]$Body = $null) {
    $request = @{ Method = $Method; Uri = "https://management.azure.com${Path}?api-version=$apiVersion"; Headers = $headers; ContentType = 'application/json' }
    if ($null -ne $Body) { $request.Body = $Body | ConvertTo-Json -Depth 40 -Compress }
    try { Invoke-RestMethod @request }
    catch { throw "Job configuration request failed (HTTP $([int]$_.Exception.Response.StatusCode)). No credentials or provider bodies were logged." }
}

function Set-Trigger([string]$Id, [object]$Configuration, [string]$Trigger) {
    # Preserve the complete configuration and existing secret values only in memory.
    $secrets = Invoke-JobArm 'POST' "$Id/listSecrets"
    $Configuration.secrets = @($secrets.value | ForEach-Object {
        if ([string]::IsNullOrEmpty($_.value)) { throw 'A runtime secret cannot be preserved; inspect its original provider before continuing.' }
        @{ name = $_.name; value = $_.value }
    })
    $Configuration.triggerType = $Trigger
    if ($Trigger -eq 'Manual') {
        $Configuration.scheduleTriggerConfig = $null
        $Configuration | Add-Member -Force NoteProperty manualTriggerConfig @{ parallelism = 1; replicaCompletionCount = 1 }
    }
    else {
        $Configuration.manualTriggerConfig = $null
        $Configuration | Add-Member -Force NoteProperty scheduleTriggerConfig @{ cronExpression = '5 8 * * 0'; parallelism = 1; replicaCompletionCount = 1 }
        $Configuration.replicaTimeout = 3600
    }
    $null = Invoke-JobArm 'PATCH' $Id @{ properties = @{ configuration = $Configuration } }
}

try {
    $oldId = "$prefix/askarabbi-weekly-dvar-torah"
    $newId = "$prefix/askarabbi-weekly-dvar-torah-vnet"
    $old = Invoke-JobArm 'GET' $oldId
    $new = Invoke-JobArm 'GET' $newId
    if ($old.properties.configuration.triggerType -eq 'Manual' -and $new.properties.configuration.triggerType -eq 'Schedule') {
        Write-Output 'The private narration job already owns the schedule.'
        return
    }
    if ($old.properties.configuration.triggerType -ne 'Schedule' -or $new.properties.configuration.triggerType -ne 'Manual' -or $old.properties.configuration.scheduleTriggerConfig.cronExpression -ne '5 8 * * 0') {
        throw 'The job triggers do not match the expected pre-cutover state; inspect before changing them.'
    }
    foreach ($id in @($oldId, $newId)) {
        $executions = Invoke-JobArm 'GET' "$id/executions"
        if (@($executions.value | Where-Object { $_.properties.status -in @('Running', 'Processing', 'Pending') }).Count -gt 0) {
            throw 'Wait for the active generator execution to finish before changing its timer.'
        }
    }
    if ($new.properties.provisioningState -ne 'Succeeded' -or $new.properties.environmentId -notlike '*/askarabbi-production-private-env') {
        throw 'The replacement generator is not ready in the expected private environment.'
    }
    if (!$PSCmdlet.ShouldProcess('AskARabbi weekly generation', 'Disable the old timer, then enable the private-audio generator for Sunday 08:05 UTC')) { return }
    Set-Trigger $oldId $old.properties.configuration 'Manual'
    $old = Invoke-JobArm 'GET' $oldId
    if ($old.properties.configuration.triggerType -ne 'Manual') { throw 'The original timer was not disabled; the replacement remains manual.' }
    Set-Trigger $newId $new.properties.configuration 'Schedule'
    $new = Invoke-JobArm 'GET' $newId
    if ($new.properties.configuration.triggerType -ne 'Schedule') { throw 'The replacement timer did not activate. Inspect it before restoring either schedule.' }
    Write-Output 'The old job is Manual; the private-audio job owns the Sunday 08:05 UTC schedule. Runtime secrets were preserved.'
}
finally { $headers.Clear(); $old = $null; $new = $null }
