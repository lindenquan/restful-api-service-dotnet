# HTTP Request Files

This folder contains `.rest` files for manually testing the API endpoints using VS Code.

## Setup

### 1. Install Extension (VS Code)

Install the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension.

- Extension: `humao.rest-client`
- Documentation: https://marketplace.visualstudio.com/items?itemName=humao.rest-client

### 2. Configure Environment

Copy the `.env.example` file to `.env` in the project root:

```powershell
Copy-Item .\.env.example .\.env
```

> **Note:** `.env` is gitignored to keep secrets secure.

### 3. Start the API

```powershell
.\build.ps1 docker-up-api
```

## Usage

1. Open any `.rest` file
2. Click "Send Request" above any request
3. View response in the side panel

## How REST Client Loads Environment Variables

REST Client uses the `$dotenv` syntax to load variables from the `.env` file:

```http
@apiKey = {{$dotenv RootAdmin__InitialApiKey}}
@baseUrl = {{$dotenv API_BASE_URL}}
```

Variables from `.env` are accessed using `{{$dotenv VARIABLE_NAME}}` syntax.

## Files

```
project-root/
├── .env                             # Private secrets (gitignored)
├── .env.example                     # Template for .env
└── http/
    ├── README.md                    # This file
    └── api-workflow.rest            # Main REST file with all endpoints
```

## Variables

Variables can be used in requests with `{{variableName}}` syntax:

- `{{$dotenv API_BASE_URL}}` - API base URL from `.env` file
- `{{$dotenv RootAdmin__InitialApiKey}}` - API key from `.env` file
- `@variableName = value` - Define request-scoped variables
- `{{requestName.response.body.id}}` - Reference response from a named request

## Chaining Requests

To use data from one request in another:

1. Name the first request with `# @name requestName`
2. Reference response data with `{{requestName.response.body.propertyName}}`

Example:
```http
# @name createPatient
POST {{baseUrl}}/api/v1/patients
...

### Get the created patient
GET {{baseUrl}}/api/v1/patients/{{createPatient.response.body.id}}
```

## Authentication

All API endpoints (except `/health`) require API key authentication:

```http
X-API-Key: {{apiKey}}
```

| Endpoint | Authentication |
|----------|----------------|
| `GET /health` | None (public) |
| `/api/v1/patients/*` | Required |
| `/api/v1/prescriptions/*` | Required |
| `/api/v1/orders/*` | Required |
| `/api/v2/orders/*` | Required |
