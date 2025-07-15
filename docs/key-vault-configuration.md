# Key Vault Configuration for NatureOS

## ✅ **Completed Steps**

### 1. Secrets Stored in Key Vault
All connection strings are securely stored in `natureos-kv-prod001`:

- ✅ `CosmosDb-ConnectionString` - Cosmos DB connection
- ✅ `IoTHub-ConnectionString` - IoT Hub service connection  
- ✅ `ServiceBus-ConnectionString` - Service Bus messaging
- ✅ `ApplicationInsights-ConnectionString` - Telemetry monitoring
- ✅ `Storage-ConnectionString` - Blob storage access

### 2. Managed Identity Configured
- ✅ **System-assigned managed identity** enabled for `natureos-api-prod001`
- ✅ **Key Vault Secrets User** role granted to managed identity

## 🔧 **Manual Configuration Required**

Due to Azure CLI issues, please manually update the App Service configuration:

### Step 1: Open Azure Portal
1. Navigate to: https://portal.azure.com
2. Go to **App Services** → `natureos-api-prod001`
3. Select **Configuration** → **Application settings**

### Step 2: Update Connection String Settings

Replace the current values with Key Vault references:

| Setting Name | New Value |
|-------------|-----------|
| `CosmosDb__ConnectionString` | `@Microsoft.KeyVault(SecretUri=https://natureos-kv-prod001.vault.azure.net/secrets/CosmosDb-ConnectionString/)` |
| `IoTHub__ConnectionString` | `@Microsoft.KeyVault(SecretUri=https://natureos-kv-prod001.vault.azure.net/secrets/IoTHub-ConnectionString/)` |
| `ServiceBus__ConnectionString` | `@Microsoft.KeyVault(SecretUri=https://natureos-kv-prod001.vault.azure.net/secrets/ServiceBus-ConnectionString/)` |
| `ApplicationInsights__ConnectionString` | `@Microsoft.KeyVault(SecretUri=https://natureos-kv-prod001.vault.azure.net/secrets/ApplicationInsights-ConnectionString/)` |
| `Storage__ConnectionString` | `@Microsoft.KeyVault(SecretUri=https://natureos-kv-prod001.vault.azure.net/secrets/Storage-ConnectionString/)` |

### Step 3: Keep Static Settings
Leave these settings as they are:
- `CosmosDb__DatabaseName` = `MINDEX`
- `CosmosDb__ContainerName` = `events`
- `ASPNETCORE_ENVIRONMENT` = `Production`

### Step 4: Save and Restart
1. Click **Save** at the bottom
2. Click **Continue** when prompted
3. The app will automatically restart

## 🔐 **Security Benefits**

Once configured:
- 🔒 **No secrets in code** - All connection strings are in Key Vault
- 🔒 **No secrets in configuration** - Only Key Vault references
- 🔒 **Automatic rotation** - Update secrets in Key Vault without redeployment  
- 🔒 **Audit trail** - Key Vault logs all secret access
- 🔒 **Managed identity** - No stored credentials needed

## 📋 **Alternative: Azure CLI Commands**

If CLI is working, you can use these commands instead:

```bash
# Update all app settings with Key Vault references
az webapp config appsettings set --name natureos-api-prod001 --resource-group rg-natureos-prod --settings \
  "CosmosDb__ConnectionString=@Microsoft.KeyVault(SecretUri=https://natureos-kv-prod001.vault.azure.net/secrets/CosmosDb-ConnectionString/)" \
  "IoTHub__ConnectionString=@Microsoft.KeyVault(SecretUri=https://natureos-kv-prod001.vault.azure.net/secrets/IoTHub-ConnectionString/)" \
  "ServiceBus__ConnectionString=@Microsoft.KeyVault(SecretUri=https://natureos-kv-prod001.vault.azure.net/secrets/ServiceBus-ConnectionString/)" \
  "ApplicationInsights__ConnectionString=@Microsoft.KeyVault(SecretUri=https://natureos-kv-prod001.vault.azure.net/secrets/ApplicationInsights-ConnectionString/)" \
  "Storage__ConnectionString=@Microsoft.KeyVault(SecretUri=https://natureos-kv-prod001.vault.azure.net/secrets/Storage-ConnectionString/)"
```

## ✅ **Verification**

After configuration, verify:
1. App Service starts successfully
2. Configuration shows Key Vault references (green checkmarks)
3. Application logs show successful Key Vault access
4. No connection string values visible in configuration 