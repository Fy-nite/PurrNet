# FUR API Documentation

Base URL: `https://purr.finite.ovh/api/v1`

## Health & Information

### `GET /health`
Returns API health status and basic information.

**Response:**
```json
{
    "status": "healthy",
    "timestamp": "2024-01-01T00:00:00Z",
    "version": "1.0.0",
    "packageCount": 42,
    "testingMode": false,
    "database": "sqlite"
}
```

### `GET /info`
Returns API information and available endpoints.

## Package Management

### `GET /packages`
Gets all packages with optional filtering and pagination.

**Parameters:**
- `sort` (optional): Sort order
  - `name` - Alphabetical (default)
  - `mostDownloads` - Most Downloaded
  - `leastDownloads` - Least Downloaded
  - `recentlyUpdated` - Most Recently Updated
  - `recentlyUploaded` - Most Recently Uploaded
  - `oldestUpdated` - Longest time since updated
  - `oldestUploaded` - First Uploaded
- `search` (optional): Search query
- `details` (optional): Include full package details (default: false)
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Items per page (default: 50)

**Response:**
```json
{
    "package_count": 100,
    "packages": ["package1", "package2", "..."],
    "package_details": [...] // Only if details=true
}
```

### `GET /packages/{packageName}`
### `GET /packages/{packageName}/{version}`
Gets detailed information about a specific package.

**Response:** Returns the FurConfig JSON for the package.

### `POST /packages`
Uploads a new package (requires authentication).

**Request Body:** FurConfig JSON
**Response:** Created package information

### `POST /packages/{packageName}/download`
Increments the download counter for a package.

## Discovery & Search

### `GET /packages/statistics`
Returns repository statistics.

**Response:**
```json
{
    "totalPackages": 100,
    "activePackages": 95,
    "totalDownloads": 50000,
    "totalViews": 75000,
    "popularAuthors": ["author1", "author2"],
    "mostDownloaded": [...],
    "recentlyAdded": [...],
    "lastUpdated": "2024-01-01T00:00:00Z"
}
```

### `GET /packages/tags`
Gets popular tags/keywords.

**Parameters:**
- `limit` (optional): Number of tags to return (default: 10)

### `GET /packages/authors`
Gets popular authors.

**Parameters:**
- `limit` (optional): Number of authors to return (default: 10)

### `GET /packages/tags/{tag}`
Gets packages by tag/keyword.

### `GET /packages/authors/{author}`
Gets packages by author.

### `GET /packages/categories`
Gets the list of available categories (ordered by popularity).

**Parameters:**
- `limit` (optional): Number of categories to return (default: 10)

**Response:**
```json
["utility", "development", "cli", "tools"]
```

### `GET /packages/categories/{category}`
Gets packages that belong to the given category.

**Parameters:**
- `category` (path): Category name (URL-encoded if it contains spaces or special characters)

**Response:**
```json
[
    {
        "name": "mypackage",
        "version": "1.2.3",
        "description": "A useful tool",
        "authors": ["alice"],
        "dependencies": [],
        "homepage": "https://example.com"
    }
]
```

## Authentication

Some endpoints require authentication via GitHub OAuth. Include the authorization header:
```
Authorization: Bearer <token>
```

## Rate Limiting

The API currently has no rate limiting but may be added in the future.

## Error Responses

All endpoints return appropriate HTTP status codes:
- `200` - Success
- `201` - Created
- `400` - Bad Request
- `401` - Unauthorized
- `404` - Not Found
- `409` - Conflict (package already exists)
- `500` - Internal Server Error

Error responses include a message:
```json
{
    "error": "Package not found"
}
```

## Package Manager Integration

For package managers, the typical workflow is:

1. Search packages: `GET /packages?search=query`
2. Get package details: `GET /packages/{name}`
3. Download package: Use the `git` URL from package details
4. Report download: `POST /packages/{name}/download`

The API is fully self-contained and doesn't require external services.


