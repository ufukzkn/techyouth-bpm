# Documentation Guide

This index defines where each kind of project information belongs. The goal is
to keep one canonical source per subject while preserving review notes and
implementation history without letting them become competing specifications.

## Start Here

1. [PDF requirement matrix](00-requirements-from-pdf.md)
2. [Architecture](02-architecture.md)
3. [API and service contracts](04-api-and-services.md)
4. [Test strategy and quality gates](24-testing-and-quality-gates.md)
5. [Presentation study guide](23-presentation-study-guide.md)
6. [Turkish user interface guide](25-user-interface-guide.md)

For setup commands, use the repository-level [Quick Start](../QUICKSTART.md).

## Canonical Sources

| Subject | Canonical document | Owns |
| --- | --- | --- |
| PDF scope and completion | [00](00-requirements-from-pdf.md) | Required/bonus mapping and accepted gaps |
| Agent rules and stable decisions | [01](01-agent-notes.md) | Working rules, ownership and documentation policy |
| Architecture | [02](02-architecture.md) | Layer boundaries, dependency direction and frontend composition |
| State machine | [03](03-bpm-and-state-machine.md) | Lifecycle concepts and runtime safety |
| HTTP/API and services | [04](04-api-and-services.md) | Endpoints, DTO behavior and service responsibilities |
| Database | [08](08-local-database.md) | Providers, migrations, seed and local reset |
| Form flow | [09](09-ozgun-form-flow.md) | Designer, runner, validation and file metadata boundary |
| Access and shell | [10](10-ufuk-access-shell-flow.md) | Identity, session, dashboard and workspace behavior |
| i18n | [11](11-i18n-language-support.md) | Language rules and extension policy |
| Process/task flow | [12](12-cagdas-process-flow.md) | Runtime-facing process and task behavior |
| Permission model | [16](16-community-permission-model.md) | Platform/community roles and authorization scope |
| Deployment | [17](17-docker-and-deployment.md) | Local/cloud Compose behavior and secrets |
| Workflow architecture | [18](18-dynamic-workflow-and-team-architecture.md) | Product model, teams, runtime policy and ownership |
| UI/UX system | [19](19-ui-ux-system.md) | Tokens, loading, feedback, motion and responsive rules |
| User interface guide | [25](25-user-interface-guide.md) | Turkish menu, screen and action usage guide |
| Workflow graph contract | [20](20-dynamic-workflow-contract.md) | Versioned graph, form binding, task and HTTP contracts |
| Manual workflow acceptance | [22](22-workflow-end-to-end-test-scenarios.md) | Cross-role demo and negative scenarios |
| Automated quality | [24](24-testing-and-quality-gates.md) | Test layers, commands, evidence and remaining gaps |

## Reviews And Presentation

- [Frontend review](13-frontend-ui-review.md), [backend review](14-backend-api-review.md)
  and [product-readiness review](15-product-readiness-and-defense.md) are
  assessments. They may summarize findings, but they do not redefine contracts.
- [Code review guide](05-code-review-guide.md) is the review route through the
  codebase; detailed test evidence belongs to document 24.
- [Team presentation split](06-team-presentation-split.md) assigns speaking and
  feature ownership.
- [Presentation study guide](23-presentation-study-guide.md) is the single
  canonical Q&A source for technology and architecture defense.
- [Product TODO](07-product-todo.md) contains planned work, not implemented
  behavior.

## Historical Records

- [Implementation history](history/implementation-log.md) preserves the full
  chronological development journal.
- [Form-flow implementation history](history/09-ozgun-form-flow-implementation-log.md)
  preserves detailed designer/runner iteration notes that were consolidated into
  the current form contract.
- [Dynamic workflow rebase notes](21-dynamic-workflow-rebase-notes.md) preserves
  one integration decision record.

Historical files explain how the product arrived here. When they disagree with
a canonical document, the canonical document and current code win.

## Maintenance Rules

1. Keep each fact in its canonical document; other files link to it.
2. Put endpoint signatures only in document 04 and graph schema only in 20.
3. Put exact test commands and the latest verified counts only in document 24.
4. Put presentation answers only in document 23; reviews should record findings.
5. Append implementation chronology only to `history/implementation-log.md`.
6. Update the PDF matrix when scope changes and the architecture document when a
   dependency or ownership boundary changes.
7. Never place credentials, local absolute paths or private connection strings
   in tracked documentation.
8. Update document 25 whenever a menu label, route, permission boundary or
   user-facing action changes.
