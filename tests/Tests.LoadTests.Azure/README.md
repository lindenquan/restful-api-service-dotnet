# Azure Load Testing

This folder contains configuration for **Azure Load Testing** - a cloud-based service for simulating massive user loads.

## When to Use

| Scenario | Use NBomber (Local) | Use Azure Load Testing |
|----------|---------------------|------------------------|
| Daily CI/CD | ✅ | ❌ |
| Development | ✅ | ❌ |
| Pre-release validation | ❌ | ✅ |
| Production stress testing | ❌ | ✅ |
| Capacity planning | ❌ | ✅ |
| Multi-region testing | ❌ | ✅ |

## Scale Comparison

| Metric | NBomber (Local) | Azure Load Testing |
|--------|-----------------|-------------------|
| **Max users** | ~1,000 | Millions |
| **Cost** | Free | Pay-per-use (~$0.15/VU-hour) |
| **Infrastructure** | Your machine | Azure-managed |
| **Regions** | Single | Multiple |

## Prerequisites

### 1. Create Azure Load Testing Resource

```bash
# Login to Azure
az login

# Create resource group (if needed)
az group create --name rg-loadtesting --location eastus

# Create Azure Load Testing resource
az load create --name prescription-api-loadtest \
               --resource-group rg-loadtesting \
               --location eastus
```

### 2. Configure GitHub Secrets

| Secret | Description | Example |
|--------|-------------|---------|
| `AZURE_CREDENTIALS` | Service principal JSON | `{"clientId":"...","clientSecret":"...","subscriptionId":"...","tenantId":"..."}` |
| `AZURE_LOAD_TEST_RESOURCE` | Load Testing resource name | `prescription-api-loadtest` |
| `AZURE_RESOURCE_GROUP` | Resource group name | `rg-loadtesting` |
| `API_BASE_URL` | API hostname (no protocol) | `my-api.azurewebsites.net` |
| `API_KEY` | API authentication key | `your-api-key` |

### 3. Create Service Principal

```bash
# Create service principal with contributor access
az ad sp create-for-rbac --name "github-loadtest" \
                         --role contributor \
                         --scopes /subscriptions/{subscription-id}/resourceGroups/rg-loadtesting \
                         --sdk-auth

# Copy the JSON output to AZURE_CREDENTIALS secret
```

## Running Tests

### Via GitHub Actions (Recommended)

1. Go to **Actions** → **Azure Load Test**
2. Click **Run workflow**
3. Select environment and parameters
4. Click **Run workflow**

### Via Azure CLI

```bash
# Create test run
az load test-run create --load-test-resource prescription-api-loadtest \
                        --resource-group rg-loadtesting \
                        --test-id prescription-api-load-test \
                        --test-run-id "run-$(date +%Y%m%d-%H%M%S)"
```

### Via Azure Portal

1. Go to Azure Portal → Azure Load Testing resource
2. Click **Tests** → **Create**
3. Upload `load-test.jmx`
4. Configure parameters
5. Click **Run**

## Files

| File | Description |
|------|-------------|
| `load-test.jmx` | JMeter test plan (80% reads, 20% writes) |
| `load-test-config.yaml` | Azure Load Testing configuration |

## Test Scenarios

The JMeter test plan includes:

1. **GET Orders** (80% of traffic)
   - Endpoint: `GET /api/v1/orders?$top=10`
   - Validates 2xx response

2. **POST Create Order** (20% of traffic)
   - Endpoint: `POST /api/v1/orders`
   - Creates order with random UUIDs
   - Validates 201 response

## Pass/Fail Criteria

| Metric | Threshold | Action |
|--------|-----------|--------|
| Average response time | > 500ms | ❌ Fail |
| P90 response time | > 1000ms | ❌ Fail |
| P95 response time | > 2000ms | ❌ Fail |
| Error rate | > 5% | ❌ Fail |
| Error rate (auto-stop) | > 10% for 60s | ⏹️ Stop test |

## Cost Estimation

| Virtual Users | Duration | Estimated Cost |
|---------------|----------|----------------|
| 100 | 5 min | ~$1.25 |
| 1,000 | 5 min | ~$12.50 |
| 1,000 | 1 hour | ~$150 |
| 10,000 | 5 min | ~$125 |

## Customizing Tests

### Adjust Load Profile

Edit `load-test-config.yaml`:

```yaml
env:
  - name: threads
    value: "500"      # Users per engine
  - name: duration
    value: "600"      # 10 minutes
```

### Add New Endpoints

Edit `load-test.jmx` in JMeter GUI or modify XML directly.

## Troubleshooting

### "Authentication failed"
- Verify `API_KEY` secret is set correctly
- Check API allows the key

### "Connection refused"
- Verify `API_BASE_URL` is accessible from Azure
- Check firewall/NSG rules

### "Test failed but API is fine"
- Review fail criteria thresholds
- Check if they're too strict for your SLAs

