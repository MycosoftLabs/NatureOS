#!/bin/bash

# NatureOS Infrastructure Deployment Script
# Usage: ./deploy-infrastructure.sh [environment] [location]

set -euo pipefail

# Default values
ENVIRONMENT="${1:-dev}"
LOCATION="${2:-eastus}"
RESOURCE_GROUP="natureos-${ENVIRONMENT}-rg"
SUBSCRIPTION_ID=""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Helper functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."
    
    # Check if Azure CLI is installed
    if ! command -v az &> /dev/null; then
        log_error "Azure CLI is not installed. Please install it first."
        exit 1
    fi
    
    # Check if logged in to Azure
    if ! az account show &> /dev/null; then
        log_error "Not logged in to Azure. Please run 'az login' first."
        exit 1
    fi
    
    # Check if Bicep is installed
    if ! az bicep version &> /dev/null; then
        log_warning "Bicep is not installed. Installing..."
        az bicep install
    fi
    
    log_success "Prerequisites check completed"
}

# Get current subscription
get_subscription() {
    SUBSCRIPTION_ID=$(az account show --query id -o tsv)
    SUBSCRIPTION_NAME=$(az account show --query name -o tsv)
    log_info "Using subscription: ${SUBSCRIPTION_NAME} (${SUBSCRIPTION_ID})"
}

# Create resource group
create_resource_group() {
    log_info "Creating resource group: ${RESOURCE_GROUP}"
    
    if az group show --name "${RESOURCE_GROUP}" &> /dev/null; then
        log_warning "Resource group ${RESOURCE_GROUP} already exists"
    else
        az group create \
            --name "${RESOURCE_GROUP}" \
            --location "${LOCATION}" \
            --tags project=NatureOS environment="${ENVIRONMENT}" deployedBy=script
        log_success "Resource group created: ${RESOURCE_GROUP}"
    fi
}

# Deploy infrastructure
deploy_infrastructure() {
    log_info "Deploying NatureOS infrastructure..."
    
    local deployment_name="natureos-${ENVIRONMENT}-$(date +%Y%m%d-%H%M%S)"
    
    az deployment group create \
        --resource-group "${RESOURCE_GROUP}" \
        --template-file "infrastructure/main.bicep" \
        --parameters environment="${ENVIRONMENT}" location="${LOCATION}" \
        --name "${deployment_name}" \
        --verbose
    
    log_success "Infrastructure deployment completed: ${deployment_name}"
}

# Get deployment outputs
get_outputs() {
    log_info "Retrieving deployment outputs..."
    
    # Get the latest deployment
    local latest_deployment=$(az deployment group list \
        --resource-group "${RESOURCE_GROUP}" \
        --query "max_by([?contains(name, 'natureos-${ENVIRONMENT}')], &properties.timestamp).name" \
        -o tsv)
    
    if [ -z "${latest_deployment}" ]; then
        log_error "No deployment found for environment: ${ENVIRONMENT}"
        return 1
    fi
    
    # Create outputs file
    local outputs_file="outputs/deployment-outputs-${ENVIRONMENT}.json"
    mkdir -p outputs
    
    az deployment group show \
        --resource-group "${RESOURCE_GROUP}" \
        --name "${latest_deployment}" \
        --query properties.outputs \
        -o json > "${outputs_file}"
    
    log_success "Deployment outputs saved to: ${outputs_file}"
    
    # Display key outputs
    echo ""
    log_info "Key deployment outputs:"
    echo "Resource Group: ${RESOURCE_GROUP}"
    echo "Cosmos DB Account: $(jq -r '.cosmosDbAccountName.value' "${outputs_file}")"
    echo "IoT Hub: $(jq -r '.iotHubName.value' "${outputs_file}")"
    echo "API Management: $(jq -r '.apiManagementGatewayUrl.value' "${outputs_file}")"
    echo "Key Vault: $(jq -r '.keyVaultName.value' "${outputs_file}")"
}

# Store secrets in Key Vault
store_secrets() {
    log_info "Storing connection strings in Key Vault..."
    
    local outputs_file="outputs/deployment-outputs-${ENVIRONMENT}.json"
    local key_vault_name=$(jq -r '.keyVaultName.value' "${outputs_file}")
    
    # Store connection strings as secrets
    az keyvault secret set \
        --vault-name "${key_vault_name}" \
        --name "CosmosDbConnectionString" \
        --value "$(jq -r '.cosmosDbConnectionString.value' "${outputs_file}")" \
        --description "Cosmos DB connection string for MINDEX"
    
    az keyvault secret set \
        --vault-name "${key_vault_name}" \
        --name "IoTHubConnectionString" \
        --value "$(jq -r '.iotHubConnectionString.value' "${outputs_file}")" \
        --description "IoT Hub connection string"
    
    az keyvault secret set \
        --vault-name "${key_vault_name}" \
        --name "ServiceBusConnectionString" \
        --value "$(jq -r '.serviceBusConnectionString.value' "${outputs_file}")" \
        --description "Service Bus connection string"
    
    az keyvault secret set \
        --vault-name "${key_vault_name}" \
        --name "StorageConnectionString" \
        --value "$(jq -r '.storageConnectionString.value' "${outputs_file}")" \
        --description "Storage account connection string"
    
    az keyvault secret set \
        --vault-name "${key_vault_name}" \
        --name "ApplicationInsightsConnectionString" \
        --value "$(jq -r '.applicationInsightsConnectionString.value' "${outputs_file}")" \
        --description "Application Insights connection string"
    
    log_success "Secrets stored in Key Vault: ${key_vault_name}"
}

# Configure APIM
configure_apim() {
    log_info "Configuring API Management..."
    
    local outputs_file="outputs/deployment-outputs-${ENVIRONMENT}.json"
    local apim_name=$(jq -r '.apiManagementGatewayUrl.value' "${outputs_file}" | sed 's|https://||' | sed 's|\.azure-api\.net||')
    
    # Import Core API
    # This would typically be done after the API is deployed
    log_warning "APIM configuration requires Core API to be deployed first"
    log_info "You can configure APIM later using: az apim api import"
}

# Main execution
main() {
    echo "=========================================="
    echo "    NatureOS Infrastructure Deployment    "
    echo "=========================================="
    echo ""
    
    log_info "Environment: ${ENVIRONMENT}"
    log_info "Location: ${LOCATION}"
    log_info "Resource Group: ${RESOURCE_GROUP}"
    echo ""
    
    check_prerequisites
    get_subscription
    create_resource_group
    deploy_infrastructure
    get_outputs
    store_secrets
    configure_apim
    
    echo ""
    log_success "NatureOS infrastructure deployment completed successfully!"
    echo ""
    echo "Next steps:"
    echo "1. Deploy the Core API: ./scripts/deploy-core-api.sh ${ENVIRONMENT}"
    echo "2. Deploy the Ingestion Functions: ./scripts/deploy-ingestion.sh ${ENVIRONMENT}"
    echo "3. Deploy the Dashboard: ./scripts/deploy-dashboard.sh ${ENVIRONMENT}"
    echo ""
    echo "To clean up resources: ./scripts/cleanup.sh ${ENVIRONMENT}"
}

# Check if script is being sourced or executed
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi 