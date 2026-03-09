# Clean Architecture Rules

- Domain layer must have zero external dependencies.
- Application layer depends only on Domain.
- Infrastructure implements interfaces defined in Application.
- WebAPI/Presentation layer must not contain business logic.
- No direct database or HTTP calls from Application layer.
- Use Dependency Injection for all cross-layer communication.
- DTOs must be used for data crossing layer boundaries.
