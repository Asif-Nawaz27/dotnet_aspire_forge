# Testing rules

## TEST001 - No test project detected

**Default severity:** `warning`

Skipped for projects whose own name contains "Test". Otherwise, AspireForge walks upward from the
project directory looking for a `tests/` folder (matching the layout `aspireforge new` generates),
and fires if no directory under it both contains the project's name and contains "Test". If no
`tests/` folder is found at all, the rule can't determine the repository layout and stays silent
rather than report a false positive.

**Fix:** Add a unit test project matching the naming convention, for example
`tests/{ProjectName}.Tests`.

## TEST002 - No integration tests detected

**Default severity:** `info`

Same discovery as TEST001, but looks for any directory under `tests/` containing "Integration"
(regardless of which project it matches) rather than a project-specific match.

**Fix:** Add an integration test project, for example `tests/{SolutionName}.IntegrationTests` -
exactly what `aspireforge new` already generates for you.
