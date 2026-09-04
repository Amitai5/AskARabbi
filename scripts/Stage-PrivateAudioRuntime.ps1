[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Bootstrap', 'Configure')][string]$Phase,
    [string]$SubscriptionId = 'c2f8383e-2c4e-4822-82a7-506b2e2ddf38',
    [string]$ResourceGroup = 'AARProduction'
)

$ErrorActionPreference = 'Stop'
# The stable 2025-01-01 schema omits runtime.dotnet and silently loses managed session keys.
$apiVersion = '2025-02-02-preview'
$resourcePrefix = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup"
$environmentId = "$resourcePrefix/providers/Microsoft.App/managedEnvironments/askarabbi-production-private-env"
$apiId = "$resourcePrefix/providers/Microsoft.App/containerApps/askarabbi-api-vnet"
$jobId = "$resourcePrefix/providers/Microsoft.App/jobs/askarabbi-weekly-dvar-torah-vnet"
$account = az account show -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $account.id -ne $SubscriptionId) { throw 'Select the expected Azure subscription before staging.' }
$tokenResponse = az account get-access-token --resource https://management.azure.com/ -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Azure sign-in is required.' }
$armHeaders = @{ Authorization = "Bearer $($tokenResponse.accessToken)" }
$tokenResponse = $null

function Invoke-Arm([string]$Method, [string]$ResourceId, [object]$Body = $null, [bool]$AllowNotFound = $false) {
    $parameters = @{
        Method = $Method
        Uri = "https://management.azure.com${ResourceId}?api-version=$apiVersion"
        Headers = $armHeaders
        ContentType = 'application/json'
    }
    if ($null -ne $Body) { $parameters.Body = ConvertTo-Json -InputObject $Body -Depth 60 -Compress }
    try { Invoke-RestMethod @parameters }
    catch {
        if ($AllowNotFound -and [int]$_.Exception.Response.StatusCode -eq 404) { return $null }
        # Do not echo provider bodies: a request may contain existing application secrets.
        throw "ARM $Method failed for $ResourceId (HTTP $([int]$_.Exception.Response.StatusCode)). No request or secret values were logged."
    }
}

function Grant-Role([string]$PrincipalId, [string]$Role, [string]$Scope) {
    az role assignment create --assignee-object-id $PrincipalId --assignee-principal-type ServicePrincipal --role $Role --scope $Scope --only-show-errors -o none
    if ($LASTEXITCODE -ne 0) { throw "Failed to grant $Role on $Scope." }
}

function Set-EnvironmentValue([object]$Container, [string]$Name, [string]$Value) {
    $Container.env = @($Container.env | Where-Object { $_.name -ne $Name }) + @(@{ name = $Name; value = $Value })
}

function Copy-Secrets([string]$SourceId) {
    $response = Invoke-Arm 'POST' "$SourceId/listSecrets"
    foreach ($secret in $response.value) {
        if ([string]::IsNullOrEmpty($secret.value)) { throw 'A source secret cannot be transferred; use its original secret provider instead.' }
        @{ name = $secret.name; value = $secret.value }
    }
}

try {
    $environment = Invoke-Arm 'GET' $environmentId
    if ($environment.properties.provisioningState -ne 'Succeeded') { throw 'Wait for the private environment to finish provisioning.' }

    if ($Phase -eq 'Bootstrap') {
        # No custom domain, production secrets, or timer is attached during bootstrap.
        if ($null -ne (Invoke-Arm 'GET' $apiId $null $true) -or $null -ne (Invoke-Arm 'GET' $jobId $null $true)) {
            throw 'A replacement runtime already exists. Inspect it before recovery; bootstrap never overwrites existing apps or jobs.'
        }
        $api = Invoke-Arm 'PUT' $apiId @{
            location = 'centralus'
            identity = @{ type = 'SystemAssigned' }
            tags = @{ application = 'AskARabbi'; purpose = 'Private-storage API replacement' }
            properties = @{
                managedEnvironmentId = $environmentId
                workloadProfileName = 'Consumption'
                configuration = @{ activeRevisionsMode = 'Single'; ingress = @{ external = $true; targetPort = 80; allowInsecure = $false; transport = 'auto' } }
                template = @{ containers = @(@{ name = 'api'; image = 'mcr.microsoft.com/k8se/quickstart:latest'; resources = @{ cpu = 0.25; memory = '0.5Gi' } }); scale = @{ minReplicas = 0; maxReplicas = 5 } }
            }
        }
        $job = Invoke-Arm 'PUT' $jobId @{
            location = 'centralus'
            identity = @{ type = 'SystemAssigned' }
            tags = @{ application = 'AskARabbi'; purpose = 'Private narration generator' }
            properties = @{
                environmentId = $environmentId
                workloadProfileName = 'Consumption'
                configuration = @{ triggerType = 'Manual'; replicaTimeout = 3600; replicaRetryLimit = 2; manualTriggerConfig = @{ parallelism = 1; replicaCompletionCount = 1 } }
                template = @{ containers = @(@{ name = 'dvar-torah-generator'; image = 'mcr.microsoft.com/k8se/quickstart:latest'; resources = @{ cpu = 0.5; memory = '1Gi' } }) }
            }
        }
        $acrId = "$resourcePrefix/providers/Microsoft.ContainerRegistry/registries/askarabbiacrprod"
        $openAiId = "$resourcePrefix/providers/Microsoft.CognitiveServices/accounts/AARProduction-OpenAI"
        $containerScope = "$resourcePrefix/providers/Microsoft.Storage/storageAccounts/askarabbiaudioprod/blobServices/default/containers/dvar-torah-audio"
        foreach ($principal in @($api.identity.principalId, $job.identity.principalId)) {
            if ([string]::IsNullOrEmpty($principal)) { throw 'Azure has not created the managed identity yet.' }
            Grant-Role $principal 'AcrPull' $acrId
            Grant-Role $principal 'Cognitive Services OpenAI User' $openAiId
        }
        Grant-Role $api.identity.principalId 'Storage Blob Data Reader' $containerScope
        Grant-Role $job.identity.principalId 'Storage Blob Data Contributor' $containerScope
        Grant-Role $job.identity.principalId 'Cognitive Services Speech User' "$resourcePrefix/providers/Microsoft.CognitiveServices/accounts/askarabbi-speech-prod"
        $deploymentPrincipal = 'da6ab1ec-6800-42d9-923d-8e2cdcd73228'
        Grant-Role $deploymentPrincipal 'Container Apps Contributor' $apiId
        Grant-Role $deploymentPrincipal 'Container Apps Contributor' $jobId
        Write-Output 'Bootstrapped replacement resources and scoped managed-identity roles. The original API and timer are unchanged.'
        return
    }

    $sourceApiId = "$resourcePrefix/providers/Microsoft.App/containerApps/askarabbi-api"
    $sourceJobId = "$resourcePrefix/providers/Microsoft.App/jobs/askarabbi-weekly-dvar-torah"
    $sourceApi = Invoke-Arm 'GET' $sourceApiId
    $sourceJob = Invoke-Arm 'GET' $sourceJobId
    $targetApi = Invoke-Arm 'GET' $apiId
    if ($targetApi.properties.configuration.ingress.customDomains.Count -gt 0) {
        throw 'The replacement API has a custom domain. Staging must not overwrite a live cutover.'
    }
    $apiConfiguration = $sourceApi.properties.configuration
    if ($apiConfiguration.runtime.dotnet.autoConfigureDataProtection -ne $true) {
        throw 'The source API must have Azure-managed Data Protection enabled before cloning authentication configuration.'
    }
    $apiConfiguration.secrets = @(Copy-Secrets $sourceApiId)
    $apiConfiguration.ingress.customDomains = @()
    $apiConfiguration.ingress.PSObject.Properties.Remove('fqdn')
    $apiConfiguration.ingress.traffic = @(@{ latestRevision = $true; weight = 100 })
    $apiTemplate = $sourceApi.properties.template
    $apiTemplate.PSObject.Properties.Remove('revisionSuffix')
    $apiTemplate.containers[0].name = 'api'
    Set-EnvironmentValue $apiTemplate.containers[0] 'AllowedHosts' "api.askarabbi.ai;$($targetApi.properties.configuration.ingress.fqdn)"

    $jobConfiguration = $sourceJob.properties.configuration
    $jobConfiguration.secrets = @(Copy-Secrets $sourceJobId)
    $jobConfiguration.triggerType = 'Manual'
    $jobConfiguration.PSObject.Properties.Remove('scheduleTriggerConfig')
    $jobConfiguration | Add-Member -Force NoteProperty manualTriggerConfig @{ parallelism = 1; replicaCompletionCount = 1 }
    $jobConfiguration.replicaTimeout = 3600
    $jobTemplate = $sourceJob.properties.template
    $jobTemplate.containers[0].name = 'dvar-torah-generator'

    foreach ($container in @($apiTemplate.containers[0], $jobTemplate.containers[0])) {
        Set-EnvironmentValue $container 'DvarTorahAudio__Enabled' 'true'
        Set-EnvironmentValue $container 'DvarTorahAudio__StorageServiceUri' 'https://askarabbiaudioprod.blob.core.windows.net/'
        Set-EnvironmentValue $container 'DvarTorahAudio__ContainerName' 'dvar-torah-audio'
        Set-EnvironmentValue $container 'DvarTorahAudio__SpeechRegion' 'eastus2'
        Set-EnvironmentValue $container 'DvarTorahAudio__SpeechResourceId' "$resourcePrefix/providers/Microsoft.CognitiveServices/accounts/askarabbi-speech-prod"
        Set-EnvironmentValue $container 'DvarTorahAudio__Voice' 'en-US-AndrewMultilingualNeural'
    }
    $null = Invoke-Arm 'PUT' $apiId @{
        location = 'centralus'; identity = @{ type = 'SystemAssigned' }
        properties = @{ managedEnvironmentId = $environmentId; workloadProfileName = 'Consumption'; configuration = $apiConfiguration; template = $apiTemplate }
    }
    $configuredApi = Invoke-Arm 'GET' $apiId
    if ($configuredApi.properties.configuration.runtime.dotnet.autoConfigureDataProtection -ne $true) {
        throw 'The replacement API did not retain managed Data Protection. Do not cut over DNS or attempt sign-in.'
    }
    $null = Invoke-Arm 'PUT' $jobId @{
        location = 'centralus'; identity = @{ type = 'SystemAssigned' }
        properties = @{ environmentId = $environmentId; workloadProfileName = 'Consumption'; configuration = $jobConfiguration; template = $jobTemplate }
    }
    Write-Output 'Copied production configuration in memory, enabled private audio, and left the new job manual. No DNS or old-runtime changes were made.'
}
finally {
    $armHeaders.Clear()
    $apiConfiguration = $null
    $jobConfiguration = $null
}
