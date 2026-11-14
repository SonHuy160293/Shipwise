# Module extension point

No business modules are shipped. A future module may have Domain, Application,
Infrastructure, API, Worker and MigrationRunner projects. Domain depends only on
BuildingBlocks; Application depends inward on Domain and neutral contracts;
Infrastructure implements Application abstractions. Executable hosts compose them.
Do not add references from BuildingBlocks to modules.
