# GitHub Secrets Setup for NatureOS CI/CD

To complete the CI/CD setup with OIDC authentication, you need to add the following secrets to your GitHub repository:

## Required GitHub Secrets

Navigate to your GitHub repository → Settings → Secrets and variables → Actions → New repository secret

### 1. AZURE_CLIENT_ID
- **Value:** `66caadf9-27e3-440e-8d88-73d856897115`
- **Description:** Azure AD Application (Client) ID

### 2. AZURE_TENANT_ID  
- **Value:** `ebd8f1d8-3aee-45ae-a1e6-2c4980a690d6`
- **Description:** Azure AD Tenant ID

### 3. AZURE_SUBSCRIPTION_ID
- **Value:** `e5f17591-e2b7-4e5b-8579-dd9bda332b9d`
- **Description:** Azure Subscription ID

## How to Add Secrets

1. Go to https://github.com/MycosoftLabs/NatureOS/settings/secrets/actions
2. Click "New repository secret"
3. Enter the secret name and value
4. Click "Add secret"
5. Repeat for all three secrets

## Verification

Once all secrets are added, the GitHub Actions workflow will automatically deploy to Azure when you push to the main branch.

The workflow includes:
- ✅ Build and test the .NET application
- ✅ Publish the application 
- ✅ Deploy to Azure App Service using OIDC (no passwords!)
- ✅ Automatic logout and cleanup

## Security Benefits

- 🔒 **No passwords** - Uses OIDC token exchange
- 🔒 **Short-lived tokens** - Automatically expire after deployment
- 🔒 **Scope-limited** - Only has access to the `rg-natureos-prod` resource group
- 🔒 **Branch-specific** - Only works for the `main` branch

## Next Steps

After adding the secrets, test the workflow by making a commit to the main branch. The deployment will run automatically. 