# Copilot Instructions

## Project Guidelines
- Avoid Task.WhenAll for database operations that share the scoped GameStatsStore or DbContext in this Blazor app; execute database-backed loading sequentially to prevent Stats page concurrency issues.