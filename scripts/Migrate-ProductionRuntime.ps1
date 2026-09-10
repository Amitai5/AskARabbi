[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Bootstrap', 'Configure', 'RestrictNetwork', 'ActivateSchedule', 'Retire', 'RetireEnvironments')][string]$Phase,
    [switch]$IncludeOldSubnet,
    [switch]$ConfirmFunctionalChecks,
    [string]$SubscriptionId = 'c2f8383e-2c4e-4822-82a7-506b2e2ddf38'
)

$ErrorActionPreference = 'Stop'
$apiVersion = '2025-02-02-preview'
$prefix = "/subscriptions/$SubscriptionId/resourceGroups/AARProduction"
$environmentId = "$prefix/providers/Microsoft.App/managedEnvironments/askarabbi-containerapps-production"
$apiId = "$prefix/providers/Microsoft.App/containerApps/askarabbi-api-production"
$jobId = "$prefix/providers/Microsoft.App/jobs/askarabbi-dvar-torah-production"
$sourceApiId = "$prefix/providers/Microsoft.App/containerApps/askarabbi-api-vnet"
$sourceJobId = "$prefix/providers/Microsoft.App/jobs/askarabbi-weekly-dvar-torah-vnet"
$oldJobId = "$prefix/providers/Microsoft.App/jobs/askarabbi-weekly-dvar-torah"
$subnetId = "$prefix/providers/Microsoft.Network/virtualNetworks/askarabbi-production-vnet/subnets/container-apps-production"
$oldSubnetId = "$prefix/providers/Microsoft.Network/virtualNetworks/askarabbi-production-vnet/subnets/container-apps"
$account = az account show -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $account.id -ne $SubscriptionId) { throw 'Select the expected Azure subscription.' }
$token = az account get-access-token --resource https://management.azure.com/ -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Azure sign-in is required.' }
$headers = @{ Authorization = "Bearer $($token.accessToken)" }
$token = $null

function Invoke-Arm([string]$Method, [string]$Id, [object]$Body = $null, [string]$Version = $apiVersion, [bool]$AllowMissing = $false) {
    $parameters = @{ Method = $Method; Uri = "https://management.azure.com${Id}?api-version=$Version"; Headers = $headers; ContentType = 'application/json' }
    if ($null -ne $Body) { $parameters.Body = ConvertTo-Json -InputObject $Body -Depth 80 -Compress }
    try { Invoke-RestMethod @parameters }
    catch {
        $status = [int]$_.Exception.Response.StatusCode
        if ($AllowMissing -and $status -eq 404) { return $null }
        throw "ARM $Method failed for $Id (HTTP $status). Request bodies and secrets are not logged."
    }
}

function Copy-Secrets([string]$Id) {
    $result = Invoke-Arm 'POST' "$Id/listSecrets"
    foreach ($secret in $result.value) {
        if ([string]::IsNullOrEmpty($secret.value)) { throw 'A source secret cannot be transferred in memory.' }
        @{ name = $secret.name; value = $secret.value }
    }
}

function Set-Value([object]$Container, [string]$Name, [string]$Value) {
    $Container.env = @($Container.env | Where-Object { $_.name -ne $Name }) + @(@{ name = $Name; value = $Value })
}

function Grant-Role([string]$Principal, [string]$Role, [string]$Scope) {
    az role assignment create --assignee-object-id $Principal --assignee-principal-type ServicePrincipal --role $Role --scope $Scope --only-show-errors -o none
    if ($LASTEXITCODE -ne 0) { throw "Failed to grant $Role on $Scope." }
}

function Assert-Idle([string]$Id) {
    $executions = Invoke-Arm 'GET' "$Id/executions"
    if (@($executions.value | Where-Object { $_.properties.status -in @('Running', 'Pending', 'Processing') }).Count -gt 0) {
        throw "A job execution is still active: $Id"
    }
}

function Set-Trigger([string]$Id, [string]$Trigger) {
    $job = Invoke-Arm 'GET' $Id
    $config = $job.properties.configuration
    $config.secrets = @(Copy-Secrets $Id)
    $config.triggerType = $Trigger
    $config.PSObject.Properties.Remove('scheduleTriggerConfig')
    $config.PSObject.Properties.Remove('manualTriggerConfig')
    if ($Trigger -eq 'Schedule') {
        $config | Add-Member NoteProperty scheduleTriggerConfig @{ cronExpression = '5 8 * * 0'; parallelism = 1; replicaCompletionCount = 1 }
    }
    else { $config | Add-Member NoteProperty manualTriggerConfig @{ parallelism = 1; replicaCompletionCount = 1 } }
    $null = Invoke-Arm 'PUT' $Id @{ location = $job.location; identity = @{ type = 'SystemAssigned' }; properties = @{ environmentId = $job.properties.environmentId; workloadProfileName = 'Consumption'; configuration = $config; template = $job.properties.template } }
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        $updated = Invoke-Arm 'GET' $Id
        if ($updated.properties.provisioningState -eq 'Succeeded' -and $updated.properties.configuration.triggerType -eq $Trigger) { return }
        Start-Sleep -Seconds 3
    }
    throw "The $Trigger trigger was not confirmed for $Id. Do not enable another schedule."
}

try {
    $environment = Invoke-Arm 'GET' $environmentId
    if ($environment.properties.provisioningState -ne 'Succeeded') { throw 'The new environment is not ready.' }

    if ($Phase -eq 'Bootstrap') {
        foreach ($id in @($apiId, $jobId)) {
            if ($null -ne (Invoke-Arm 'GET' $id $null $apiVersion $true)) { throw "Bootstrap never overwrites an existing resource: $id" }
        }
        $api = Invoke-Arm 'PUT' $apiId @{
            location = 'centralus'; identity = @{ type = 'SystemAssigned' }
            properties = @{
                managedEnvironmentId = $environmentId; workloadProfileName = 'Consumption'
                configuration = @{ activeRevisionsMode = 'Single'; ingress = @{ external = $true; targetPort = 80; allowInsecure = $false; transport = 'auto' } }
                template = @{ containers = @(@{ name = 'api'; image = 'mcr.microsoft.com/k8se/quickstart:latest'; resources = @{ cpu = 0.25; memory = '0.5Gi' } }); scale = @{ minReplicas = 0; maxReplicas = 5 } }
            }
        }
        $job = Invoke-Arm 'PUT' $jobId @{
            location = 'centralus'; identity = @{ type = 'SystemAssigned' }
            properties = @{
                environmentId = $environmentId; workloadProfileName = 'Consumption'
                configuration = @{ triggerType = 'Manual'; replicaTimeout = 3600; replicaRetryLimit = 2; manualTriggerConfig = @{ parallelism = 1; replicaCompletionCount = 1 } }
                template = @{ containers = @(@{ name = 'dvar-torah-generator'; image = 'mcr.microsoft.com/k8se/quickstart:latest'; resources = @{ cpu = 0.5; memory = '1Gi' } }) }
            }
        }
        foreach ($principal in @($api.identity.principalId, $job.identity.principalId)) {
            if ([string]::IsNullOrWhiteSpace($principal)) { throw 'Managed identity is not ready.' }
            Grant-Role $principal 'AcrPull' "$prefix/providers/Microsoft.ContainerRegistry/registries/askarabbiacrprod"
            Grant-Role $principal 'Cognitive Services OpenAI User' "$prefix/providers/Microsoft.CognitiveServices/accounts/AARProduction-OpenAI"
        }
        $blobScope = "$prefix/providers/Microsoft.Storage/storageAccounts/askarabbiaudioprod/blobServices/default/containers/dvar-torah-audio"
        Grant-Role $api.identity.principalId 'Storage Blob Data Reader' $blobScope
        Grant-Role $job.identity.principalId 'Storage Blob Data Contributor' $blobScope
        Grant-Role $job.identity.principalId 'Cognitive Services Speech User' "$prefix/providers/Microsoft.CognitiveServices/accounts/askarabbi-speech-prod"
        foreach ($scope in @($apiId, $jobId)) { Grant-Role 'da6ab1ec-6800-42d9-923d-8e2cdcd73228' 'Container Apps Contributor' $scope }
        Grant-Role 'da6ab1ec-6800-42d9-923d-8e2cdcd73228' 'Reader' $environmentId
        Write-Output 'Bootstrap complete. Old runtime and DNS remain unchanged.'
        return
    }

    if ($Phase -eq 'Configure') {
        $sourceApi = Invoke-Arm 'GET' $sourceApiId
        $sourceJob = Invoke-Arm 'GET' $sourceJobId
        $targetApi = Invoke-Arm 'GET' $apiId
        if ($targetApi.properties.configuration.ingress.customDomains.Count -gt 0) { throw 'Do not overwrite an API after custom-domain cutover.' }
        $apiConfiguration = $sourceApi.properties.configuration
        if ($apiConfiguration.runtime.dotnet.autoConfigureDataProtection -ne $true) { throw 'Source API managed Data Protection must be enabled.' }
        $apiConfiguration.secrets = @(Copy-Secrets $sourceApiId)
        $apiConfiguration.ingress.customDomains = @()
        $apiConfiguration.ingress.PSObject.Properties.Remove('fqdn')
        $apiConfiguration.ingress.traffic = @(@{ latestRevision = $true; weight = 100 })
        $apiTemplate = $sourceApi.properties.template
        $apiTemplate.PSObject.Properties.Remove('revisionSuffix')
        if ($apiTemplate.scale.minReplicas -ne 0) { throw 'Preserve the approved scale-to-zero setting.' }
        Set-Value $apiTemplate.containers[0] 'AllowedHosts' "api.askarabbi.ai;$($targetApi.properties.configuration.ingress.fqdn)"
        $jobConfiguration = $sourceJob.properties.configuration
        $jobConfiguration.secrets = @(Copy-Secrets $sourceJobId)
        $jobConfiguration.triggerType = 'Manual'
        $jobConfiguration.PSObject.Properties.Remove('scheduleTriggerConfig')
        $jobConfiguration | Add-Member -Force NoteProperty manualTriggerConfig @{ parallelism = 1; replicaCompletionCount = 1 }
        $jobConfiguration.replicaTimeout = 3600
        if (@($sourceJob.properties.template.containers[0].env | Where-Object { $_.name -eq 'DvarTorahAudio__Enabled' -and $_.value -eq 'true' }).Count -ne 1) { throw 'The source generator must have narration enabled.' }
        Set-Value $sourceJob.properties.template.containers[0] 'DvarTorahAudio__SpeechServiceUri' 'https://askarabbi-speech-prod.cognitiveservices.azure.com/'
        $null = Invoke-Arm 'PUT' $apiId @{ location = 'centralus'; identity = @{ type = 'SystemAssigned' }; properties = @{ managedEnvironmentId = $environmentId; workloadProfileName = 'Consumption'; configuration = $apiConfiguration; template = $apiTemplate } }
        $null = Invoke-Arm 'PUT' $jobId @{ location = 'centralus'; identity = @{ type = 'SystemAssigned' }; properties = @{ environmentId = $environmentId; workloadProfileName = 'Consumption'; configuration = $jobConfiguration; template = $sourceJob.properties.template } }
        # ARM PUT is asynchronous; an immediate GET can still return the bootstrap revision.
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            $configured = Invoke-Arm 'GET' $apiId
            if ($configured.properties.configuration.runtime.dotnet.autoConfigureDataProtection -eq $true -and $configured.properties.template.containers[0].image -eq $apiTemplate.containers[0].image) { break }
            Start-Sleep -Seconds 3
        }
        if ($configured.properties.configuration.runtime.dotnet.autoConfigureDataProtection -ne $true) { throw 'Managed Data Protection was not retained. Do not cut over.' }
        Write-Output 'Copied the live VNet API and generator, including immutable images and narration settings. Secrets stayed in memory.'
        return
    }

    if ($Phase -eq 'RestrictNetwork') {
        $subnets = @($subnetId)
        if ($IncludeOldSubnet) { $subnets += $oldSubnetId }
        foreach ($id in $subnets) {
            $subnet = Invoke-Arm 'GET' $id $null '2024-05-01'
            foreach ($service in @('Microsoft.AzureCosmosDB', 'Microsoft.CognitiveServices')) {
                if ($service -notin $subnet.properties.serviceEndpoints.service) { throw "Missing service endpoint $service on $id" }
            }
        }
        $cosmosRules = @($subnets | ForEach-Object { @{ id = $_; ignoreMissingVNetServiceEndpoint = $false } })
        $aiRules = @($subnets | ForEach-Object { @{ id = $_; ignoreMissingVnetServiceEndpoint = $false } })
        $null = Invoke-Arm 'PATCH' "$prefix/providers/Microsoft.DocumentDB/databaseAccounts/askarabbi-production-mongodb" @{ properties = @{ publicNetworkAccess = 'Enabled'; isVirtualNetworkFilterEnabled = $true; virtualNetworkRules = $cosmosRules; ipRules = @(); networkAclBypass = 'None'; networkAclBypassResourceIds = @() } } '2025-04-15'
        $null = Invoke-Arm 'PATCH' "$prefix/providers/Microsoft.CognitiveServices/accounts/AARProduction-OpenAI" @{ properties = @{ publicNetworkAccess = 'Enabled'; networkAcls = @{ defaultAction = 'Deny'; bypass = 'None'; ipRules = @(); virtualNetworkRules = $aiRules } } } '2025-06-01'
        $speechId = "$prefix/providers/Microsoft.CognitiveServices/accounts/askarabbi-speech-prod"
        $speech = Invoke-Arm 'GET' $speechId $null '2025-06-01'
        if ($speech.sku.name -ne 'F0') { throw 'Speech tier changed; this migration must not upgrade it.' }
        # Cognitive Services service endpoints also route Speech; the existing F0 account needs a subnet allow-list.
        # Speech does not support the networkAcls.bypass property supported by OpenAI.
        $null = Invoke-Arm 'PATCH' $speechId @{ properties = @{ publicNetworkAccess = 'Enabled'; networkAcls = @{ defaultAction = 'Deny'; ipRules = @(); virtualNetworkRules = $aiRules } } } '2025-06-01'
        Write-Output "Subnet restrictions requested for MongoDB, OpenAI, and the existing F0 Speech account: $($subnets.Count) approved subnet(s). Verify provisioning and data-plane checks before declaring completion. Blob is unchanged."
        return
    }

    if ($Phase -eq 'ActivateSchedule') {
        if (-not $ConfirmFunctionalChecks) { throw 'Confirm the new runtime passed dependency tests before transferring the timer.' }
        foreach ($id in @($oldJobId, $sourceJobId, $jobId)) { Assert-Idle $id }
        Set-Trigger $oldJobId 'Manual'
        Set-Trigger $sourceJobId 'Manual'
        Set-Trigger $jobId 'Schedule'
        $alertId = "$prefix/providers/Microsoft.Insights/scheduledQueryRules/askarabbi-weekly-dvar-torah-failures"
        $alert = Invoke-Arm 'GET' $alertId $null '2023-12-01'
        foreach ($criterion in $alert.properties.criteria.allOf) {
            $criterion.query = $criterion.query.Replace('askarabbi-weekly-dvar-torah', 'askarabbi-dvar-torah-production')
        }
        $null = Invoke-Arm 'PATCH' $alertId @{ properties = @{ criteria = $alert.properties.criteria } } '2023-12-01'
        Write-Output 'Only the consolidated generator is scheduled; existing failure monitoring now follows it.'
        return
    }

    if (-not $ConfirmFunctionalChecks) { throw 'Retirement requires completed dependency and custom-domain functional checks.' }
    $api = Invoke-Arm 'GET' $apiId
    $job = Invoke-Arm 'GET' $jobId
    if ($api.properties.provisioningState -ne 'Succeeded' -or $api.properties.latestReadyRevisionName -ne $api.properties.latestRevisionName) { throw 'New API revision is not ready.' }
    if ($api.properties.managedEnvironmentId -ne $environmentId -or $job.properties.environmentId -ne $environmentId) { throw 'New runtime is not in the consolidated environment.' }
    if ($api.properties.configuration.runtime.dotnet.autoConfigureDataProtection -ne $true) { throw 'New API lost managed Data Protection.' }
    if ($job.properties.configuration.triggerType -ne 'Schedule') { throw 'New generator is not scheduled.' }
    $domain = @($api.properties.configuration.ingress.customDomains | Where-Object { $_.name -eq 'api.askarabbi.ai' -and $_.bindingType -eq 'SniEnabled' })
    if ($domain.Count -ne 1) { throw 'The existing domain does not have a secured binding on the new API.' }
    $dns = Resolve-DnsName 'api.askarabbi.ai' -Type CNAME -Server 1.1.1.1
    if ($api.properties.configuration.ingress.fqdn -notin $dns.NameHost) { throw 'Public DNS is not pointing to the new API.' }
    $health = Invoke-WebRequest -Uri 'https://api.askarabbi.ai/health' -TimeoutSec 60 -UseBasicParsing
    if ($health.StatusCode -ne 200) { throw 'Production HTTPS health check failed.' }

    # Exact retired runtime allowlist only: data services and the shared VNet are deliberately excluded.
    $retiredIds = @($oldJobId, $sourceJobId, "$prefix/providers/Microsoft.App/containerApps/askarabbi-api", $sourceApiId)
    if ($Phase -eq 'RetireEnvironments') {
        foreach ($id in $retiredIds) {
            if ($null -ne (Invoke-Arm 'GET' $id $null $apiVersion $true)) { throw "An old app/job still exists: $id" }
        }
        $oldEnvironmentIds = @(
            "$prefix/providers/Microsoft.App/managedEnvironments/askarabbi-production-containerappenv",
            "$prefix/providers/Microsoft.App/managedEnvironments/askarabbi-production-private-env"
        )
        # This builder is known to belong to the retired non-VNet environment, not the shared ACR.
        $builderId = "$prefix/providers/Microsoft.App/builders/artifact-builder8cc4"
        $builder = Invoke-Arm 'GET' $builderId $null $apiVersion $true
        if ($null -ne $builder) {
            if ($builder.properties.environmentId -ne $oldEnvironmentIds[0]) { throw 'Builder ownership changed; do not delete it.' }
            $null = Invoke-Arm 'DELETE' $builderId
            Write-Output 'Retirement requested for the verified old-environment builder.'
        }
        $apps = Invoke-Arm 'GET' "$prefix/providers/Microsoft.App/containerApps"
        $jobs = Invoke-Arm 'GET' "$prefix/providers/Microsoft.App/jobs"
        foreach ($resource in @($apps.value) + @($jobs.value)) {
            if ($resource.properties.environmentId -in $oldEnvironmentIds -or $resource.properties.managedEnvironmentId -in $oldEnvironmentIds) {
                throw "Another runtime still uses an old environment: $($resource.id)"
            }
        }
        foreach ($id in $oldEnvironmentIds) {
            if ($null -ne (Invoke-Arm 'GET' $id $null $apiVersion $true)) {
                $null = Invoke-Arm 'DELETE' $id
                Write-Output "Environment retirement requested: $id"
            }
        }
        return
    }
    foreach ($id in $retiredIds) {
        $resource = Invoke-Arm 'GET' $id $null $apiVersion $true
        if ($null -eq $resource) { continue }
        if ($id -like '*/jobs/*') {
            Assert-Idle $id
            if ($resource.properties.configuration.triggerType -ne 'Manual') { throw "Old timer remains enabled: $id" }
        }
        $principal = $resource.identity.principalId
        $null = Invoke-Arm 'DELETE' $id
        if (-not [string]::IsNullOrEmpty($principal)) {
            $roles = az role assignment list --assignee-object-id $principal --all --fill-principal-name false -o json | ConvertFrom-Json
            if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate retired managed-identity roles.' }
            foreach ($role in $roles) {
                if ($role.scope.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
                    az role assignment delete --ids $role.id --only-show-errors
                    if ($LASTEXITCODE -ne 0) { throw 'Could not remove a retired managed-identity role.' }
                }
            }
        }
        Write-Output "Retirement requested: $id"
    }
    Write-Output 'Old apps/jobs are retiring. Confirm they are gone before deleting their two old environments and unused old app subnet.'
}
finally {
    $headers.Clear()
    $apiConfiguration = $null
    $jobConfiguration = $null
}
