#!/bin/bash
set -e

# ================================
# Expense Management System - Basic Deployment Script
# ================================
# This script deploys the core infrastructure and application
# without Gen AI features. For Gen AI chat functionality,
# use deploy-with-chat.sh instead.

echo "🚀 Starting Expense Management System Deployment..."
echo ""

# Check if user is logged in to Azure CLI
echo "✓ Checking Azure CLI authentication..."
if ! az account show &>/dev/null; then
    echo "❌ Not logged in to Azure CLI. Please run 'az login' first."
    exit 1
fi

# Get current user info for SQL admin
ADMIN_USER_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)
ADMIN_USER_LOGIN=$(az ad signed-in-user show --query userPrincipalName -o tsv)

echo "✓ Authenticated as: $ADMIN_USER_LOGIN"
echo "✓ Object ID: $ADMIN_USER_OBJECT_ID"
echo ""

# Deployment parameters
RESOURCE_GROUP="rg-expensemgmt-demo"
LOCATION="uksouth"

# Create resource group
echo "📦 Creating resource group: $RESOURCE_GROUP..."
az group create \
    --name $RESOURCE_GROUP \
    --location $LOCATION \
    --output none

echo "✓ Resource group created"
echo ""

# Deploy infrastructure (App Service, SQL Database) WITHOUT GenAI
echo "🏗️  Deploying core infrastructure (App Service + SQL Database)..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group $RESOURCE_GROUP \
    --template-file bicep/main.bicep \
    --parameters deployGenAI=false \
                 adminLogin="$ADMIN_USER_LOGIN" \
                 adminObjectId="$ADMIN_USER_OBJECT_ID" \
    --query properties.outputs \
    --output json)

echo "✓ Infrastructure deployed"
echo ""

# Extract deployment outputs
APP_SERVICE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceName.value')
SQL_SERVER_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerName.value')
SQL_SERVER_FQDN=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerFqdn.value')
DATABASE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.databaseName.value')
MANAGED_IDENTITY_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityName.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityClientId.value')
APP_URL=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceUrl.value')

echo "📋 Deployment Details:"
echo "   - App Service: $APP_SERVICE_NAME"
echo "   - SQL Server: $SQL_SERVER_NAME"
echo "   - Database: $DATABASE_NAME"
echo "   - Managed Identity: $MANAGED_IDENTITY_NAME"
echo "   - Client ID: $MANAGED_IDENTITY_CLIENT_ID"
echo "   - App URL: $APP_URL"
echo ""

# Configure App Service settings
echo "⚙️  Configuring App Service settings..."
az webapp config appsettings set \
    --name $APP_SERVICE_NAME \
    --resource-group $RESOURCE_GROUP \
    --settings \
        "SqlServer=$SQL_SERVER_FQDN" \
        "Database=$DATABASE_NAME" \
        "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" \
        "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
        "AuthenticationMode=Active Directory Managed Identity" \
    --output none

echo "✓ App Service configured"
echo ""

# Wait for SQL Server to be fully ready
echo "⏳ Waiting 30 seconds for SQL Server to be fully ready..."
sleep 30

# Add current user's IP to SQL firewall for schema import
echo "🔥 Adding your IP to SQL Server firewall..."
MY_IP=$(curl -s https://api.ipify.org)
az sql server firewall-rule create \
    --resource-group $RESOURCE_GROUP \
    --server $SQL_SERVER_NAME \
    --name "AllowDeploymentIP" \
    --start-ip-address $MY_IP \
    --end-ip-address $MY_IP \
    --output none

echo "✓ Firewall rule added for IP: $MY_IP"
echo ""

# Install Python dependencies for SQL scripts
echo "🐍 Installing Python dependencies..."
pip3 install --quiet pyodbc azure-identity

echo "✓ Python dependencies installed"
echo ""

# Update Python scripts with actual server names
echo "📝 Updating SQL script configuration..."
sed -i.bak "s/sql-expensemgmt-REPLACE.database.windows.net/$SQL_SERVER_FQDN/g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s/sql-expensemgmt-REPLACE.database.windows.net/$SQL_SERVER_FQDN/g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s/sql-expensemgmt-REPLACE.database.windows.net/$SQL_SERVER_FQDN/g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak
sed -i.bak "s/MANAGED-IDENTITY-NAME/$MANAGED_IDENTITY_NAME/g" script.sql && rm -f script.sql.bak

echo "✓ Scripts configured"
echo ""

# Import database schema
echo "📊 Importing database schema..."
python3 run-sql.py

echo "✓ Database schema imported"
echo ""

# Configure managed identity database permissions
echo "🔑 Configuring managed identity database permissions..."
python3 run-sql-dbrole.py

echo "✓ Managed identity configured in database"
echo ""

# Deploy stored procedures
echo "📦 Deploying stored procedures..."
python3 run-sql-stored-procs.py

echo "✓ Stored procedures deployed"
echo ""

# Deploy application code
echo "🚀 Deploying application code..."
az webapp deploy \
    --resource-group $RESOURCE_GROUP \
    --name $APP_SERVICE_NAME \
    --src-path ./app.zip \
    --type zip \
    --async true

echo "✓ Application deployment initiated (running asynchronously)"
echo ""

# Final summary
echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║                   🎉 DEPLOYMENT COMPLETE! 🎉                  ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo ""
echo "📍 Application URL: $APP_URL/Index"
echo ""
echo "⚠️  IMPORTANT: The app is currently deploying in the background."
echo "   Please wait 2-3 minutes before accessing the URL."
echo ""
echo "📚 API Documentation: $APP_URL/swagger"
echo ""
echo "ℹ️  Note: This deployment does NOT include Gen AI chat features."
echo "   To enable AI chat, run: ./deploy-with-chat.sh"
echo ""
echo "🔧 To run the app locally:"
echo "   1. Run 'az login' to authenticate"
echo "   2. Update appsettings.Development.json with:"
echo "      - SqlServer: $SQL_SERVER_FQDN"
echo "      - Database: $DATABASE_NAME"
echo "      - AuthenticationMode: Active Directory Default"
echo "   3. Run 'dotnet run' from the ExpenseManagementApp directory"
echo ""
